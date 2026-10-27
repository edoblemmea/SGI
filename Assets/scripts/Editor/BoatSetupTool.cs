using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// Herramienta de editor: menu "Isla Pirata > Barca".
/// 1) Diagnostico del teletransporte muelle <-> barca y del remo (escribe un informe [BARCA] en la Console).
/// 2) Prepara la barca: BoatRowing con sus referencias, BoatHandRowing con "Require Oar Grab" y un Oar en cada remo.
/// 3) Arregla el teletransporte: capa TeleportAnchor sin colisiones, anclas en esa capa y en la Raycast Mask del
///    Teleport Interactor, eventos Teleporting -> Board / Disembark, ancla de la barca en el asiento del remero y
///    ancla del muelle a la altura de las tablas.
public class BoatSetupTool : EditorWindow
{
    const string AnchorLayerName = "TeleportAnchor";

    BoatRowing boat;
    Transform leftPaddle, rightPaddle;
    bool faceBow = true;
    float seatOffset = 0.45f;

    [MenuItem("Isla Pirata/Barca")]
    static void Open() => GetWindow<BoatSetupTool>("Barca");

    void OnGUI()
    {
        if (boat == null) boat = FindAnyObjectByType<BoatRowing>(FindObjectsInactive.Include);
        boat = (BoatRowing)EditorGUILayout.ObjectField("Barca (BoatRowing)", boat, typeof(BoatRowing), true);
        if (boat == null)
        {
            EditorGUILayout.HelpBox("No hay ningun BoatRowing en la escena abierta.", MessageType.Warning);
            return;
        }
        if (leftPaddle == null || rightPaddle == null) GuessPaddles();
        leftPaddle = (Transform)EditorGUILayout.ObjectField("Remo izquierdo", leftPaddle, typeof(Transform), true);
        rightPaddle = (Transform)EditorGUILayout.ObjectField("Remo derecho", rightPaddle, typeof(Transform), true);

        EditorGUILayout.Space();
        if (GUILayout.Button("1. Diagnostico (ver Console, filtro [BARCA])")) Diagnose();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(leftPaddle == null || rightPaddle == null))
            if (GUILayout.Button("2. Preparar barca y remos")) PrepareBoat();

        EditorGUILayout.Space();
        faceBow = EditorGUILayout.Toggle(new GUIContent("Remero mirando a proa", "Desmarcado = mirando a popa, como un remero real"), faceBow);
        seatOffset = EditorGUILayout.Slider(new GUIContent("Distancia asiento - empunaduras (m)"), seatOffset, 0.2f, 0.8f);
        if (GUILayout.Button("3. Arreglar teletransporte")) FixTeleport();
    }

    // ------------------------------------------------------------------ utilidades
    Transform Bow => boat.bowReference != null ? boat.bowReference : boat.transform;

    void GuessPaddles()
    {
        // Solo en el editor: propone los hijos con collider cuyo nombre parece un remo (se puede cambiar a mano)
        var cands = boat.GetComponentsInChildren<Collider>(true)
            .Select(c => c.transform).Distinct()
            .Where(t => new[] { "paddle", "remo", "oar" }.Any(k => t.name.ToLower().Contains(k)))
            .ToList();
        if (cands.Count < 2) return;
        Vector3 right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(Bow.forward, Vector3.up)).normalized;
        cands = cands.OrderBy(t => Vector3.Dot(t.position - boat.transform.position, right)).ToList();
        if (leftPaddle == null) leftPaddle = cands.First();
        if (rightPaddle == null) rightPaddle = cands.Last();
    }

    static XROrigin Origin() => FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);

    static IEnumerable<XRRayInteractor> TeleportRays()
    {
        int teleportIL = InteractionLayerMask.GetMask("Teleport");
        return FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include)
            .Where(r => (teleportIL != 0 && (r.interactionLayers.value & teleportIL) != 0) || r.name.ToLower().Contains("teleport"));
    }

    static string Persistent(UnityEventBase e)
    {
        if (e == null) return "-";
        var s = new List<string>();
        for (int i = 0; i < e.GetPersistentEventCount(); i++)
        {
            var t = e.GetPersistentTarget(i);
            s.Add($"{(t != null ? t.name : "MISSING")}.{e.GetPersistentMethodName(i)}");
        }
        return s.Count == 0 ? "(nada)" : string.Join(", ", s);
    }

    static bool HasCall(UnityEventBase e, Object target, string method)
    {
        for (int i = 0; i < e.GetPersistentEventCount(); i++)
            if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == method) return true;
        return false;
    }

    static void RemoveCalls(UnityEventBase e, string method)
    {
        for (int i = e.GetPersistentEventCount() - 1; i >= 0; i--)
            if (e.GetPersistentMethodName(i) == method) UnityEventTools.RemovePersistentListener(e, i);
    }

    // ------------------------------------------------------------------ 1. Diagnostico
    void Diagnose()
    {
        var sb = new StringBuilder("[BARCA] ===== DIAGNOSTICO =====\n");
        int problems = 0;
        void Bad(string s) { sb.AppendLine("  [X] " + s); problems++; }
        void Ok(string s) => sb.AppendLine("  [ok] " + s);

        var origin = Origin();
        var rb = boat.GetComponent<Rigidbody>();
        sb.AppendLine("BARCA");
        if (rb == null) Bad("La barca no tiene Rigidbody en el mismo objeto que BoatRowing");
        else Ok($"Rigidbody kinematic={rb.isKinematic} gravedad={rb.useGravity} masa={rb.mass} constraints={rb.constraints}");
        if (boat.xrOrigin == null) Bad("BoatRowing: 'Xr Origin' vacio -> Board() no hara nada");
        else Ok($"BoatRowing.xrOrigin = {boat.xrOrigin.name}");
        if (boat.bowReference == null) Bad("BoatRowing: 'Bow Reference' vacio");
        if (boat.disableWhileOnBoard == null || boat.disableWhileOnBoard.Length == 0) Bad("BoatRowing: 'Disable While On Board' vacio -> te moverias dentro de la barca");
        else Ok($"Desactiva a bordo: {string.Join(", ", boat.disableWhileOnBoard.Where(b => b != null).Select(b => b.GetType().Name))}");
        if (boat.startOnBoard) sb.AppendLine("  [!] Start On Board marcado: empiezas dentro de la barca (solo para pruebas)");

        sb.AppendLine("REMO");
        var rowers = boat.GetComponents<BoatHandRowing>();
        if (rowers.Length == 0) Bad("No hay BoatHandRowing en la barca -> no se puede remar");
        else if (rowers.Length > 1) Bad($"Hay {rowers.Length} BoatHandRowing -> doble fuerza");
        foreach (var r in rowers)
        {
            if (r.xrOrigin == null) Bad("BoatHandRowing: 'Xr Origin' vacio");
            sb.AppendLine($"  BoatHandRowing pushForce={r.pushForce} requireOarGrab={r.requireOarGrab}" + (r.requireOarGrab ? "" : "  <- se rema con el grip en el aire, SIN coger los remos"));
        }
        foreach (var p in new[] { leftPaddle, rightPaddle })
        {
            if (p == null) { Bad("Falta asignar un remo en esta ventana"); continue; }
            var oar = p.GetComponent<Oar>();
            if (oar == null) Bad($"'{p.name}' no tiene componente Oar -> no se puede agarrar");
            else if (oar.handle == null) Bad($"'{p.name}': Oar sin empunadura (Handle)");
            else Ok($"'{p.name}' agarrable, empunadura a {Vector3.Distance(oar.handle.position, p.position):F2} m del pivote");
        }

        sb.AppendLine("TELETRANSPORTE");
        int anchorLayer = LayerMask.NameToLayer(AnchorLayerName);
        if (anchorLayer < 0) Bad($"No existe la capa '{AnchorLayerName}': los colliders de las anclas son solidos y chocan (pared invisible en el muelle, la barca roza)");
        var rays = TeleportRays().ToList();
        if (rays.Count == 0) Bad("No encuentro ningun Teleport Interactor (XRRayInteractor con la interaction layer Teleport)");
        foreach (var ray in rays)
            sb.AppendLine($"  Teleport Interactor '{ray.name}' activo={ray.isActiveAndEnabled} raycastMask=[{MaskNames(ray.raycastMask)}] interactionLayers={ray.interactionLayers.value} triggers={ray.raycastTriggerInteraction}");

        foreach (var a in FindObjectsByType<TeleportationAnchor>(FindObjectsInactive.Include))
        {
            bool onBoat = a.transform.IsChildOf(boat.transform);
            sb.AppendLine($"  ANCLA '{a.name}' ({(onBoat ? "en la barca" : "fuera de la barca")}) capa={LayerMask.LayerToName(a.gameObject.layer)} orientacion={a.matchOrientation}");
            var cols = a.colliders.Count > 0 ? a.colliders.Where(c => c != null).ToList() : a.GetComponentsInChildren<Collider>().ToList();
            if (cols.Count == 0) Bad($"'{a.name}' no tiene collider -> el rayo no puede apuntarla");
            foreach (var c in cols)
            {
                bool inMask = rays.Any(r => (r.raycastMask.value & (1 << c.gameObject.layer)) != 0);
                bool collides = Enumerable.Range(0, 32).Any(i => !Physics.GetIgnoreLayerCollision(c.gameObject.layer, i) && !string.IsNullOrEmpty(LayerMask.LayerToName(i)));
                sb.AppendLine($"     collider '{c.name}' trigger={c.isTrigger} capa={LayerMask.LayerToName(c.gameObject.layer)} tamano={c.bounds.size}");
                if (c.isTrigger && rays.All(r => r.raycastTriggerInteraction != QueryTriggerInteraction.Collide)) Bad($"collider de '{a.name}' es trigger y el rayo ignora triggers");
                if (!inMask) Bad($"la capa del collider de '{a.name}' no esta en la Raycast Mask del Teleport Interactor");
                if (collides && !c.isTrigger) sb.AppendLine($"     [!] collider solido que choca con otras capas" + (onBoat ? " (forma parte del casco de la barca)" : " (pared invisible donde esta el ancla)"));
            }
            if (rays.Count > 0 && rays.All(r => (r.interactionLayers.value & a.interactionLayers.value) == 0)) Bad($"'{a.name}': sus Interaction Layers no coinciden con las del Teleport Interactor");
            string tp = Persistent(a.teleporting), se = Persistent(a.selectExited);
            sb.AppendLine($"     Teleporting -> {tp} | Select Exited -> {se}");
            if (tp.Contains("MISSING") || se.Contains("MISSING")) Bad($"'{a.name}' tiene eventos que apuntan a un objeto/funcion que ya no existe");
            if (onBoat && !HasCall(a.teleporting, boat, "Board") && !HasCall(a.selectExited, boat, "Board")) Bad($"'{a.name}' no llama a BoatRowing.Board");
            if (!onBoat && !HasCall(a.teleporting, boat, "Disembark")) Bad($"'{a.name}' no llama a BoatRowing.Disembark en Teleporting");

            if (!onBoat)
            {
                Vector3 p = a.teleportAnchorTransform.position;
                var hit = Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.transform.IsChildOf(a.transform) && h.normal.y > 0.7f)
                    .OrderBy(h => Mathf.Abs(h.point.y - p.y)).FirstOrDefault();
                if (hit.collider != null)
                {
                    float d = p.y - hit.point.y;
                    sb.AppendLine($"     suelo bajo el ancla: '{hit.collider.name}' a {d:F2} m" + (Mathf.Abs(d) > 0.1f ? "  <- los pies no quedan a ras del suelo" : ""));
                }
            }
        }
        if (origin != null)
            sb.AppendLine($"JUGADOR: XR Origin '{origin.name}' cuelga de '{(origin.transform.parent != null ? origin.transform.parent.name : "la raiz")}' " +
                          $"CharacterController={(origin.GetComponent<CharacterController>() != null)}");

        sb.AppendLine(problems == 0 ? "===== TODO CORRECTO =====" : $"===== {problems} PROBLEMA(S): usa los botones 2 y 3 =====");
        Debug.Log(sb.ToString());
    }

    static string MaskNames(LayerMask m)
    {
        var n = new List<string>();
        for (int i = 0; i < 32; i++)
            if ((m.value & (1 << i)) != 0) n.Add(string.IsNullOrEmpty(LayerMask.LayerToName(i)) ? i.ToString() : LayerMask.LayerToName(i));
        return string.Join(", ", n);
    }

    // ------------------------------------------------------------------ 2. Barca y remos
    void PrepareBoat()
    {
        var origin = Origin();
        if (origin == null) { Debug.LogError("[BARCA] No hay XR Origin en la escena."); return; }

        Undo.RecordObject(boat, "Preparar barca");
        boat.xrOrigin = origin.transform;
        boat.disableWhileOnBoard = origin.GetComponentsInChildren<LocomotionProvider>(true)
            .Where(p => !(p is TeleportationProvider) && !(p is SnapTurnProvider) && !(p is ContinuousTurnProvider))
            .Cast<Behaviour>().ToArray();

        var rowers = boat.GetComponents<BoatHandRowing>();
        BoatHandRowing rower;
        if (rowers.Length == 0)
        {
            rower = Undo.AddComponent<BoatHandRowing>(boat.gameObject);
            // Valores que teniais antes de separar los scripts
            rower.pushForce = 30f;
            rower.reverseMultiplier = 0.6f;
            rower.turnForce = 3f;
            rower.maxAngularSpeed = 0.6f;
        }
        else rower = rowers[0];
        if (rowers.Length > 1) Debug.LogWarning($"[BARCA] Hay {rowers.Length} BoatHandRowing: borra los que sobran (solo debe haber uno).");
        Undo.RecordObject(rower, "Preparar barca");
        rower.xrOrigin = origin.transform;
        rower.bowReference = boat.bowReference;
        rower.head = origin.Camera != null ? origin.Camera.transform : null;
        rower.requireOarGrab = true;

        SetupOar(leftPaddle, XRNode.LeftHand);
        SetupOar(rightPaddle, XRNode.RightHand);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(boat.gameObject.scene);
        Debug.Log($"[BARCA] Barca preparada. BoatRowing desactiva a bordo: {string.Join(", ", boat.disableWhileOnBoard.Select(b => b.GetType().Name))}. " +
                  "BoatHandRowing con 'Require Oar Grab'. Remos agarrables. Guarda (Ctrl+S).");
    }

    void SetupOar(Transform paddle, XRNode hand)
    {
        var oar = paddle.GetComponent<Oar>();
        if (oar == null) oar = Undo.AddComponent<Oar>(paddle.gameObject);
        Undo.RecordObject(oar, "Remo");
        oar.preferredHand = hand;

        if (oar.handle == null)
        {
            // Extremo del remo mas cercano al centro de la barca = empunadura
            var box = paddle.GetComponent<BoxCollider>();
            Vector3 axis = Vector3.right, center = Vector3.zero;
            float half = 0.5f;
            if (box != null)
            {
                Vector3 s = box.size;
                int i = s.x >= s.y && s.x >= s.z ? 0 : (s.y >= s.z ? 1 : 2);
                axis = i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
                half = s[i] * 0.5f;
                center = box.center;
            }
            Vector3 a = paddle.TransformPoint(center + axis * half), b = paddle.TransformPoint(center - axis * half);
            Vector3 mid = SetupToolUtils.RenderBounds(boat.gameObject).center;
            Vector3 end = Vector3.Distance(a, mid) < Vector3.Distance(b, mid) ? a : b;
            Vector3 inward = (paddle.position - end).normalized;

            var h = new GameObject("Empunadura").transform;
            Undo.RegisterCreatedObjectUndo(h.gameObject, "Remo");
            h.SetParent(paddle, false);
            h.position = end + inward * 0.1f;
            oar.handle = h;
            Debug.Log($"[BARCA] '{paddle.name}': empunadura creada a {Vector3.Distance(h.position, paddle.position):F2} m del pivote (selecciona 'Empunadura' para verla).");
        }
    }

    // ------------------------------------------------------------------ 3. Teletransporte
    void FixTeleport()
    {
        int layer = EnsureAnchorLayer();
        if (layer < 0) return;

        var anchors = FindObjectsByType<TeleportationAnchor>(FindObjectsInactive.Include);
        foreach (var a in anchors)
        {
            var cols = a.colliders.Count > 0 ? a.colliders.Where(c => c != null).ToList() : a.GetComponentsInChildren<Collider>().ToList();
            foreach (var c in cols)
            {
                Undo.RecordObject(c.gameObject, "Ancla");
                c.gameObject.layer = layer;
                Undo.RecordObject(c, "Ancla");
                c.isTrigger = false;   // el rayo de teletransporte ignora triggers
            }
            Undo.RecordObject(a, "Ancla");
            a.matchOrientation = MatchOrientation.TargetUpAndForward;

            if (a.transform.IsChildOf(boat.transform)) FixBoatAnchor(a, cols);
            else FixDockAnchor(a, layer);
        }

        foreach (var ray in TeleportRays())
        {
            Undo.RecordObject(ray, "Teleport");
            ray.raycastMask |= 1 << layer;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(boat.gameObject.scene);
        Debug.Log($"[BARCA] Teletransporte arreglado: {anchors.Length} anclas en la capa '{AnchorLayerName}' (no choca con nada), " +
                  "anadida a la Raycast Mask de los Teleport Interactors. Pulsa 'Diagnostico' para comprobarlo y guarda (Ctrl+S).");
    }

    void FixBoatAnchor(TeleportationAnchor a, List<Collider> cols)
    {
        // Eventos: Board en Teleporting (no en Select Exited)
        RemoveCalls(a.selectExited, "Board");
        RemoveCalls(a.teleporting, "Board");
        UnityEventTools.AddVoidPersistentListener(a.teleporting, boat.Board);

        // Asiento: detras (o delante) de las empunaduras, mirando a proa (o a popa)
        var l = leftPaddle != null ? leftPaddle.GetComponent<Oar>() : null;
        var r = rightPaddle != null ? rightPaddle.GetComponent<Oar>() : null;
        Transform t = a.teleportAnchorTransform != null ? a.teleportAnchorTransform : a.transform;
        Vector3 bow = Vector3.ProjectOnPlane(Bow.forward, Vector3.up).normalized;
        if (l != null && r != null && l.handle != null && r.handle != null)
        {
            Vector3 mid = (l.handle.position + r.handle.position) * 0.5f;
            Vector3 seat = mid + (faceBow ? -bow : bow) * seatOffset;
            Undo.RecordObject(t, "Ancla");
            t.position = new Vector3(seat.x, t.position.y, seat.z);
            t.rotation = Quaternion.LookRotation(faceBow ? bow : -bow, Vector3.up);
        }
        else Debug.LogWarning("[BARCA] Prepara antes los remos (boton 2) para colocar el ancla en el asiento.");

        // Collider para apuntar: por encima del casco para que el rayo lo toque antes que la barca
        foreach (var c in cols.OfType<BoxCollider>())
        {
            Undo.RecordObject(c, "Ancla");
            c.center = c.transform.InverseTransformPoint(t.position + Vector3.up * 0.5f);
            c.size = new Vector3(1.2f, 1f, 1.2f);
        }
        Debug.Log($"[BARCA] Ancla '{a.name}': Teleporting -> Board, asiento {(faceBow ? "mirando a proa" : "mirando a popa")}.");
    }

    void FixDockAnchor(TeleportationAnchor a, int anchorLayer)
    {
        RemoveCalls(a.selectExited, "Board");
        RemoveCalls(a.teleporting, "Board");
        if (!HasCall(a.teleporting, boat, "Disembark"))
        {
            RemoveCalls(a.teleporting, "Disembark");
            UnityEventTools.AddVoidPersistentListener(a.teleporting, boat.Disembark);
        }

        // Pies a ras de las tablas del muelle
        Transform t = a.teleportAnchorTransform != null ? a.teleportAnchorTransform : a.transform;
        int mask = ~((1 << anchorLayer) | (1 << Mathf.Max(0, LayerMask.NameToLayer("Player"))) | (1 << Mathf.Max(0, LayerMask.NameToLayer("Boat"))));
        Physics.SyncTransforms();
        // El suelo mas cercano a la altura actual (no una vela, cuerda o tejado que quede encima)
        var hit = Physics.RaycastAll(t.position + Vector3.up * 3f, Vector3.down, 6f, mask, QueryTriggerInteraction.Ignore)
            .Where(h => !h.transform.IsChildOf(a.transform) && h.normal.y > 0.7f)
            .OrderBy(h => Mathf.Abs(h.point.y - t.position.y)).FirstOrDefault();
        if (hit.collider != null && Mathf.Abs(hit.point.y - t.position.y) > 0.02f)
        {
            Vector3 delta = Vector3.up * (hit.point.y - t.position.y);
            Undo.RecordObject(t, "Ancla");
            t.position += delta;
            // el collider se queda donde estaba en el mundo
            foreach (var c in a.GetComponentsInChildren<BoxCollider>())
                if (c.transform == t) { Undo.RecordObject(c, "Ancla"); c.center -= t.InverseTransformVector(delta); }
            Debug.Log($"[BARCA] Ancla '{a.name}' subida/bajada {delta.y:F2} m hasta '{hit.collider.name}'.");
        }
        Debug.Log($"[BARCA] Ancla '{a.name}': Teleporting -> Disembark.");
    }

    static int EnsureAnchorLayer()
    {
        int layer = LayerMask.NameToLayer(AnchorLayerName);
        if (layer < 0)
        {
            if (!EditorUtility.DisplayDialog("Capa TeleportAnchor",
                    $"Se creara la capa '{AnchorLayerName}' (Project Settings > Tags and Layers) y se quitara de la matriz de colisiones " +
                    "(Project Settings > Physics): los colliders de las anclas solo los vera el rayo de teletransporte.", "Crear", "Cancelar"))
                return -1;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < 31 && layer < 0; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = AnchorLayerName;
                    layer = i;
                }
            if (layer < 0) { Debug.LogError("[BARCA] No quedan capas libres."); return -1; }
            tagManager.ApplyModifiedProperties();
        }

        // Matriz de colisiones: esta capa no choca con ninguna
        var dyn = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
        var matrix = dyn.FindProperty("m_LayerCollisionMatrix");
        if (matrix != null && matrix.isArray && matrix.arraySize == 32)
        {
            for (int i = 0; i < 32; i++)
            {
                var row = matrix.GetArrayElementAtIndex(i);
                row.longValue = i == layer ? 0 : (row.longValue & ~(1L << layer)) & 0xFFFFFFFFL;
            }
            dyn.ApplyModifiedProperties();
        }
        for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(layer, i, true);   // y en la sesion actual
        AssetDatabase.SaveAssets();
        Debug.Log($"[BARCA] Capa '{AnchorLayerName}' = {layer}, sin colisiones.");
        return layer;
    }
}
