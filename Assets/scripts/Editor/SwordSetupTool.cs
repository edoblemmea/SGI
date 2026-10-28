using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// Herramienta de editor: menu "Isla Pirata > Espada".
///  1) Crea Assets/Prefabs/Espada.prefab con el modelo Rusty Sword (Danvil): Rigidbody + XR Grab Interactable + Sword,
///     'Agarre' (la empunadura queda en la mano y la hoja hacia delante), 'Hoja' (Box Collider trigger) y 'Punta'.
///     La hoja y la empunadura se detectan solas en la malla: la punta es el extremo estrecho; la empunadura, el de la guarda.
///  2) La coloca en el suelo junto al ancla del muelle (o junto al objeto seleccionado).
public class SwordSetupTool : EditorWindow
{
    const string DefaultModel = "Assets/Danvil/Rusty Sword/Prefabs/RustySword.prefab";
    const string PrefabPath = SetupToolUtils.PrefabFolder + "/Espada.prefab";
    const string SoundFolder = "Assets/AS_Casual_Island_Game_Sounds_FREE";

    GameObject model;
    float length = 0.95f;
    float damage = 8f;
    float minSwing = 2f;

    [MenuItem("Isla Pirata/Espada")]
    static void Open() => GetWindow<SwordSetupTool>("Espada");

    void OnEnable()
    {
        if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModel);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("1. Prefab de espada", EditorStyles.boldLabel);
        model = (GameObject)EditorGUILayout.ObjectField("Modelo", model, typeof(GameObject), false);
        length = EditorGUILayout.Slider(new GUIContent("Largo total (m)", "Se escala el modelo a este largo"), length, 0.5f, 1.5f);
        damage = EditorGUILayout.FloatField(new GUIContent("Dano por m/s", "Dano = velocidad de la punta x esto (tope 50)"), damage);
        minSwing = EditorGUILayout.FloatField(new GUIContent("Velocidad minima del tajo (m/s)", "Mas despacio no hace dano"), minSwing);
        using (new EditorGUI.DisabledScope(model == null))
            if (GUILayout.Button("Crear / actualizar prefab Espada")) CreatePrefab();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Colocar en la escena", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Junto al ancla del muelle (donde empiezas). Si seleccionas un objeto en la Hierarchy, junto a el.", MessageType.None);
        using (new EditorGUI.DisabledScope(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null))
            if (GUILayout.Button("Colocar espada")) Place();
    }

    // ------------------------------------------------------------------ 1
    void CreatePrefab()
    {
        SetupToolUtils.EnsurePrefabFolder();
        var root = new GameObject("Espada");
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Modelo";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            SetupToolUtils.ClearStatic(visual);
            foreach (var c in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var mb in visual.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);   // DanvilRotate (gira sola)

            // Vertices en el espacio de la raiz
            var pts = new List<Vector3>();
            foreach (var mf in visual.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null)
                    foreach (var v in mf.sharedMesh.vertices) pts.Add(root.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
            if (pts.Count == 0) { Debug.LogError("[ESPADA] El modelo no tiene mallas."); return; }

            // Escala: el lado mas largo = 'length'
            Bounds b = new Bounds(pts[0], Vector3.zero);
            foreach (var p in pts) b.Encapsulate(p);
            int axisIndex = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : (b.size.y >= b.size.z ? 1 : 2);
            float scale = length / Mathf.Max(b.size[axisIndex], 0.0001f);
            visual.transform.localScale *= scale;
            for (int i = 0; i < pts.Count; i++) pts[i] *= scale;
            b = new Bounds(b.center * scale, b.size * scale);
            Vector3 axis = axisIndex == 0 ? Vector3.right : axisIndex == 1 ? Vector3.up : Vector3.forward;

            // Que extremo es la punta: el estrecho. La empunadura esta del lado de la guarda (lo mas ancho)
            float lo = Vector3.Dot(b.min, axis), hi = Vector3.Dot(b.max, axis), len = hi - lo;
            float Width(float from, float to) => pts.Where(p => { float t = (Vector3.Dot(p, axis) - lo) / len; return t >= from && t <= to; })
                                                     .Select(p => (p - axis * Vector3.Dot(p, axis) - (b.center - axis * Vector3.Dot(b.center, axis))).magnitude)
                                                     .DefaultIfEmpty(0f).Max();
            bool tipAtHigh = Width(0.6f, 1f) < Width(0f, 0.4f);
            Vector3 toTip = tipAtHigh ? axis : -axis;
            Vector3 mid = b.center;
            Vector3 tipPos = mid + toTip * len * 0.5f;
            Vector3 pommel = mid - toTip * len * 0.5f;

            // Guarda: donde mas ancha es la espada (separa empunadura y hoja)
            float guardT = 0.25f, widest = 0f;
            for (float t = 0.05f; t < 0.5f; t += 0.025f)
            {
                float w = tipAtHigh ? Width(t, t + 0.025f) : Width(1f - t - 0.025f, 1f - t);
                if (w > widest) { widest = w; guardT = t; }
            }
            Vector3 guard = pommel + toTip * len * guardT;
            Vector3 gripPos = Vector3.Lerp(pommel, guard, 0.55f);

            // Perpendicular de la hoja (su lado ancho) para orientar el agarre
            Vector3 side = Vector3.Cross(toTip, Vector3.up).sqrMagnitude > 0.01f ? Vector3.Cross(toTip, Vector3.up).normalized : Vector3.right;

            var grip = new GameObject("Agarre").transform;
            grip.SetParent(root.transform, false);
            grip.localPosition = gripPos;
            grip.localRotation = Quaternion.LookRotation(toTip, Vector3.Cross(side, toTip));   // Z = hacia la punta

            var tip = new GameObject("Punta").transform;
            tip.SetParent(root.transform, false);
            tip.localPosition = tipPos;

            // Hoja: trigger de la guarda a la punta (corta). Cuerpo: capsula solida (para cogerla y que repose en el suelo)
            var blade = new GameObject("Hoja");
            blade.transform.SetParent(root.transform, false);
            blade.transform.localPosition = (guard + tipPos) * 0.5f;
            blade.transform.localRotation = Quaternion.LookRotation(toTip, Vector3.Cross(side, toTip));
            var bc = blade.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            float bladeLen = Vector3.Distance(guard, tipPos);
            bc.size = new Vector3(Mathf.Max(widest * 0.8f, 0.06f), 0.04f, bladeLen);

            var cap = root.AddComponent<CapsuleCollider>();
            cap.direction = axisIndex;
            cap.center = mid;
            cap.height = len;
            cap.radius = Mathf.Clamp(widest * 0.5f, 0.03f, 0.1f);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = grip;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;   // sigue a la mano sin chocar con el mundo
            grab.throwOnDetach = false;
            grab.farAttachMode = UnityEngine.XR.Interaction.Toolkit.Attachment.InteractableFarAttachMode.Near;   // con el rayo, viene a la mano

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            var sword = root.AddComponent<Sword>();
            sword.tip = tip;
            sword.damagePerMeterPerSecond = damage;
            sword.minSwingSpeed = minSwing;
            sword.audioSource = audio;
            sword.hitClip = AssetDatabase.FindAssets("SFX_2 t:AudioClip", new[] { SoundFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[ESPADA] Prefab guardado en {PrefabPath}: {len:F2} m, hoja de {bladeLen:F2} m, punta hacia {(tipAtHigh ? "+" : "-")}{"XYZ"[axisIndex]} del modelo. " +
                      "Abre el prefab si quieres retocar 'Agarre' (donde va la mano) o 'Hoja'.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ------------------------------------------------------------------ 2
    void Place()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Vector3 pos; Vector3 right = Vector3.right;
        if (Selection.activeTransform != null && !EditorUtility.IsPersistent(Selection.activeTransform.gameObject))
        {
            pos = Selection.activeTransform.position + Selection.activeTransform.right * 0.8f;
            right = Selection.activeTransform.right;
        }
        else
        {
            // Junto al ancla del muelle: la que tiene Teleporting -> Disembark y no es de la barca
            var boat = FindAnyObjectByType<BoatRowing>(FindObjectsInactive.Include);
            var dock = FindObjectsByType<TeleportationAnchor>(FindObjectsInactive.Include).FirstOrDefault(a =>
                boat != null && !a.transform.IsChildOf(boat.transform) &&
                Enumerable.Range(0, a.teleporting.GetPersistentEventCount()).Any(i => a.teleporting.GetPersistentMethodName(i) == "Disembark"));
            if (dock == null) { Debug.LogError("[ESPADA] No encuentro el ancla del muelle. Selecciona un objeto en la Hierarchy y la pongo a su lado."); return; }
            pos = dock.transform.position + dock.transform.right * 0.8f;
            right = dock.transform.right;
        }
        // Sobre el suelo de debajo
        Physics.SyncTransforms();
        var hit = Physics.RaycastAll(pos + Vector3.up * 1.5f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).FirstOrDefault();
        if (hit.collider != null) pos = hit.point + Vector3.up * 0.08f;

        var old = GameObject.Find("Espada");   // solo en el editor: sustituir la anterior
        if (old != null && old.GetComponent<Sword>() != null) Undo.DestroyObjectImmediate(old);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Espada");
        go.name = "Espada";
        go.transform.position = pos;
        // Tumbada en el suelo
        go.transform.rotation = Quaternion.FromToRotation(go.GetComponent<Sword>().tip.position - go.transform.position, right) * go.transform.rotation;
        go.transform.position = pos;
        Selection.activeObject = go;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log($"[ESPADA] Espada colocada en {pos}. Guarda (Ctrl+S).");
    }
}
