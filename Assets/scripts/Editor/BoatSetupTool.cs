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

        EditorGUILayout.Space();
        if (GUILayout.Button("4. Sacar la barca si esta varada (terreno o colliders debajo)")) FreeBoat();
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
        var statics = boat.GetComponentsInChildren<Transform>(true).Where(t => GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0).Select(t => t.name).ToArray();
        if (statics.Length > 0) Bad($"Objetos marcados Static en la barca: {string.Join(", ", statics)} -> la malla no se mueve aunque el Rigidbody si. Pulsa '2. Preparar barca y remos'");
        else Ok("Ningun objeto de la barca es Static");

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

        sb.AppendLine("FISICA (¿algo frena la barca?)");
        var blockers = PhysicsBlockers(out Bounds hull, out float terrainTop);
        sb.AppendLine($"  Casco: fondo Y={hull.min.y:F2}, tamano {hull.size}");
        if (!float.IsNegativeInfinity(terrainTop))
        {
            if (terrainTop > hull.min.y - 0.05f) Bad($"El TERRENO bajo el casco llega a Y={terrainTop:F2} (por encima del fondo del casco): la barca esta VARADA. Pulsa el boton 4.");
            else Ok($"El terreno bajo el casco queda {hull.min.y - terrainTop:F2} m por debajo (bien)");
        }
        foreach (var c in blockers)
            Bad($"El casco toca '{c.name}' (capa {LayerMask.LayerToName(c.gameObject.layer)}): la frena o la bloquea. Muevela (boton 4) o quita/ajusta ese collider.");
        if (blockers.Count == 0) Ok("Ningun collider ajeno toca el casco");
        if (rb != null && rb.isKinematic) Bad("El Rigidbody de la barca es kinematic: las fuerzas no la mueven");
        foreach (var r in rowers)
            if (r.pushForce < 5f) Bad($"BoatHandRowing.pushForce = {r.pushForce}: muy bajo (prueba 30)");

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

    // Colliders solidos del casco (sin triggers ni la capa TeleportAnchor)
    List<Collider> HullColliders()
    {
        int anchorLayer = LayerMask.NameToLayer(AnchorLayerName);
        return boat.GetComponentsInChildren<Collider>(true)
            .Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy && c.gameObject.layer != anchorLayer && !(c is CharacterController))
            .ToList();
    }

    // Terreno mas alto bajo el casco y colliders ajenos (que chocan con la barca) que lo tocan
    List<Collider> PhysicsBlockers(out Bounds hull, out float terrainTop)
    {
        var cols = HullColliders();
        hull = cols.Count > 0 ? cols[0].bounds : new Bounds(boat.transform.position, Vector3.one);
        foreach (var c in cols) hull.Encapsulate(c.bounds);

        terrainTop = float.NegativeInfinity;
        var t = Terrain.activeTerrain;   // DEMO: terreno de la isla
        if (t != null)
            for (int ix = 0; ix <= 4; ix++)
                for (int iz = 0; iz <= 4; iz++)
                {
                    var p = new Vector3(Mathf.Lerp(hull.min.x, hull.max.x, ix / 4f), 0f, Mathf.Lerp(hull.min.z, hull.max.z, iz / 4f));
                    terrainTop = Mathf.Max(terrainTop, t.SampleHeight(p) + t.transform.position.y);
                }

        Physics.SyncTransforms();
        var result = new List<Collider>();
        foreach (var hc in cols)
        {
            var hits = Physics.OverlapBox(hc.bounds.center, hc.bounds.extents * 0.98f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.transform.IsChildOf(boat.transform) || result.Contains(h)) continue;
                if (Physics.GetIgnoreLayerCollision(hc.gameObject.layer, h.gameObject.layer)) continue;
                if (h is TerrainCollider && hull.min.y > terrainTop) continue;   // el AABB toca el terreno pero el casco no
                result.Add(h);
            }
        }
        return result;
    }

    void FreeBoat()
    {
        var blockers = PhysicsBlockers(out Bounds hull, out float terrainTop);
        bool stuck = blockers.Count > 0 || (!float.IsNegativeInfinity(terrainTop) && terrainTop > hull.min.y - 0.05f);
        if (!stuck) { Debug.Log("[BARCA] La barca no esta varada ni toca nada: no la muevo."); return; }

        var t = Terrain.activeTerrain;
        Vector3 start = boat.transform.position;
        // Busca en anillos cada vez mas lejos un sitio con agua suficiente bajo todo el casco y sin colliders
        for (float r = 1f; r <= 40f; r += 1f)
            for (int a = 0; a < 16; a++)
            {
                float ang = a * Mathf.PI * 2f / 16f;
                Vector3 offset = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                Bounds b = hull;
                b.center += offset;
                bool deep = true;
                if (t != null)
                    for (int ix = 0; ix <= 4 && deep; ix++)
                        for (int iz = 0; iz <= 4 && deep; iz++)
                        {
                            var p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, ix / 4f), 0f, Mathf.Lerp(b.min.z, b.max.z, iz / 4f));
                            if (t.SampleHeight(p) + t.transform.position.y > b.min.y - 0.4f) deep = false;
                        }
                if (!deep) continue;
                bool clear = Physics.OverlapBox(b.center, b.extents * 1.05f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .All(h => h.transform.IsChildOf(boat.transform) || h is TerrainCollider ||
                              Physics.GetIgnoreLayerCollision(boat.gameObject.layer, h.gameObject.layer));
                if (!clear) continue;

                Undo.RecordObject(boat.transform, "Sacar barca");
                boat.transform.position = start + offset;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(boat.gameObject.scene);
                Selection.activeObject = boat.gameObject;
                Debug.Log($"[BARCA] Barca movida {r:F0} m (de {start} a {boat.transform.position}) a una zona con agua suficiente y sin colliders. " +
                          "Comprueba que se puede llegar desde el muelle con el teletransporte (o mueve el ancla del muelle) y guarda (Ctrl+S).");
                return;
            }
        Debug.LogWarning("[BARCA] No encuentro un sitio libre a menos de 40 m: mueve la barca a mano mar adentro.");
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

        // Manos y grip: los objetos Left/Right Controller (lo que se ve en el casco) y las acciones de XRI
        var all = origin.GetComponentsInChildren<Transform>(true);
        Transform leftCtrl = all.FirstOrDefault(t => t.name.ToLower().Contains("left") && t.name.ToLower().Contains("controller"));
        Transform rightCtrl = all.FirstOrDefault(t => t.name.ToLower().Contains("right") && t.name.ToLower().Contains("controller"));
        var leftGrip = rower.leftGrip != null ? rower.leftGrip : FindAction("XRI Left Interaction/Select Value");
        var rightGrip = rower.rightGrip != null ? rower.rightGrip : FindAction("XRI Right Interaction/Select Value");
        rower.leftHandFallback = leftCtrl;
        rower.rightHandFallback = rightCtrl;
        rower.leftGrip = leftGrip;
        rower.rightGrip = rightGrip;
        Debug.Log($"[BARCA] Manos: {(leftCtrl != null ? leftCtrl.name : "NO ENCONTRADA")} / {(rightCtrl != null ? rightCtrl.name : "NO ENCONTRADA")}. " +
                  $"Grip: {(leftGrip != null ? leftGrip.name : "-")} / {(rightGrip != null ? rightGrip.name : "-")}");

        SetupOar(leftPaddle, XRNode.LeftHand, leftCtrl, rightCtrl, leftGrip, rightGrip);
        SetupOar(rightPaddle, XRNode.RightHand, leftCtrl, rightCtrl, leftGrip, rightGrip);
        ClearStatic();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(boat.gameObject.scene);
        Debug.Log($"[BARCA] Barca preparada. BoatRowing desactiva a bordo: {string.Join(", ", boat.disableWhileOnBoard.Select(b => b.GetType().Name))}. " +
                  "BoatHandRowing con 'Require Oar Grab'. Remos agarrables. Guarda (Ctrl+S).");
    }

    // DEMO: el prefab del asset Boats marca la barca y los remos como Static (todo, incluido Batching Static).
    // Con Static Batching la malla se "congela" en el mundo: el Rigidbody avanza pero la barca que ves no, y un remo no gira.
    void ClearStatic()
    {
        int n = 0;
        foreach (var t in boat.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0) continue;
            Undo.RecordObject(t.gameObject, "Quitar Static");
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            n++;
        }
        Debug.Log(n > 0 ? $"[BARCA] Quitado 'Static' de {n} objetos de la barca (la malla y los remos ya pueden moverse)."
                        : "[BARCA] La barca no tenia objetos Static (bien).");
    }

    static UnityEngine.InputSystem.InputActionReference FindAction(string name)
    {
        // Acciones de los Starter Assets de XRI (solo en el editor)
        foreach (var guid in AssetDatabase.FindAssets("XRI Default Input Actions"))
        {
            var r = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid))
                .OfType<UnityEngine.InputSystem.InputActionReference>()
                .FirstOrDefault(a => a.name == name);
            if (r != null) return r;
        }
        Debug.LogWarning($"[BARCA] No encuentro la accion '{name}' en los Starter Assets: el grip se leera solo del casco.");
        return null;
    }

    void SetupOar(Transform paddle, XRNode hand, Transform leftCtrl, Transform rightCtrl,
                  UnityEngine.InputSystem.InputActionReference leftGrip, UnityEngine.InputSystem.InputActionReference rightGrip)
    {
        var oar = paddle.GetComponent<Oar>();
        if (oar == null) oar = Undo.AddComponent<Oar>(paddle.gameObject);
        Undo.RecordObject(oar, "Remo");
        oar.preferredHand = hand;
        oar.leftHand = leftCtrl;
        oar.rightHand = rightCtrl;
        oar.leftGrip = leftGrip;
        oar.rightGrip = rightGrip;
        if (oar.grabRadius < 0.3f) oar.grabRadius = 0.3f;

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
