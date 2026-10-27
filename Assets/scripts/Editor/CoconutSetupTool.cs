using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Herramienta de editor: menu "Isla Pirata > Cocos".
/// 1) Crea Assets/Prefabs/Coco.prefab a partir del Coconut del asset Island (Rigidbody + collider + XR Grab + Coconut).
/// 2) Busca las palmeras PINTADAS en el Terrain mas cercanas al centro de la Scene view y pone un CoconutTree en su copa,
///    ademas de unos cocos en el suelo para cogerlos y lanzarlos.
public class CoconutSetupTool : EditorWindow
{
    const string PrefabPath = SetupToolUtils.PrefabFolder + "/Coco.prefab";
    const string DefaultModel = "Assets/Island/Prefabs/Misc/Coconut.prefab";

    GameObject model;
    float diameter = 0.18f;
    int prototype;
    int palms = 5;
    float searchRadius = 40f;
    int groundCoconuts = 2;
    float damagePerSpeed = 5f;

    [MenuItem("Isla Pirata/Cocos")]
    static void Open() => GetWindow<CoconutSetupTool>("Cocos");

    void OnEnable()
    {
        if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModel);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("1. Prefab de coco", EditorStyles.boldLabel);
        model = (GameObject)EditorGUILayout.ObjectField("Modelo", model, typeof(GameObject), false);
        diameter = EditorGUILayout.FloatField("Diametro (m)", diameter);
        damagePerSpeed = EditorGUILayout.FloatField(new GUIContent("Dano por m/s", "Un coco que cae 6 m llega a ~11 m/s"), damagePerSpeed);
        if (GUILayout.Button("Crear / actualizar prefab Coco")) CreatePrefab();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Palmeras del Terrain", EditorStyles.boldLabel);
        var terrain = Terrain.activeTerrain;   // DEMO: las palmeras estan pintadas en el terreno de la demo
        if (terrain == null) { EditorGUILayout.HelpBox("No hay Terrain en la escena abierta.", MessageType.Warning); return; }
        var protos = terrain.terrainData.treePrototypes;
        if (protos.Length == 0) { EditorGUILayout.HelpBox("El Terrain no tiene arboles pintados.", MessageType.Warning); return; }
        string[] names = protos.Select((p, i) =>
            $"{i}: {(p.prefab != null ? p.prefab.name : "?")} ({terrain.terrainData.treeInstances.Count(t => t.prototypeIndex == i)} en el mapa)").ToArray();
        prototype = EditorGUILayout.Popup("Tipo de arbol (palmera)", Mathf.Clamp(prototype, 0, protos.Length - 1), names);
        palms = EditorGUILayout.IntSlider("Palmeras con cocos", palms, 1, 30);
        searchRadius = EditorGUILayout.FloatField("Buscar a menos de (m)", searchRadius);
        groundCoconuts = EditorGUILayout.IntSlider("Cocos en el suelo por palmera", groundCoconuts, 0, 5);
        EditorGUILayout.HelpBox("Se eligen las palmeras mas cercanas al objeto seleccionado o al centro de la Scene view.", MessageType.None);
        using (new EditorGUI.DisabledScope(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null))
            if (GUILayout.Button("Poner cocos en las palmeras")) Place(terrain);
    }

    void CreatePrefab()
    {
        if (model == null) { Debug.LogError("[COCOS] Elige un modelo."); return; }
        SetupToolUtils.EnsurePrefabFolder();

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            inst.name = "Coco";
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var c in inst.GetComponentsInChildren<Collider>()) DestroyImmediate(c);

            // Escalar para que mida 'diameter'
            Bounds b = SetupToolUtils.RenderBounds(inst);
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (size > 0.0001f) inst.transform.localScale *= diameter / size;
            b = SetupToolUtils.RenderBounds(inst);

            var col = inst.AddComponent<SphereCollider>();
            float s = Mathf.Max(inst.transform.lossyScale.x, 0.0001f);
            col.center = inst.transform.InverseTransformPoint(b.center);
            col.radius = diameter * 0.5f / s;

            var rb = SetupToolUtils.GetOrAdd<Rigidbody>(inst);
            rb.mass = 1f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // no atraviesa el suelo al lanzarlo
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var grab = SetupToolUtils.GetOrAdd<XRGrabInteractable>(inst);
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;

            var audio = SetupToolUtils.GetOrAdd<AudioSource>(inst);
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;

            var coco = SetupToolUtils.GetOrAdd<Coconut>(inst);
            coco.damagePerMeterPerSecond = damagePerSpeed;
            coco.audioSource = audio;

            PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
            Debug.Log($"[COCOS] Prefab guardado en {PrefabPath} (diametro {diameter} m). Si quieres sonido de golpe, ponle un clip en 'Hit Clip'.");
        }
        finally
        {
            DestroyImmediate(inst);
        }
    }

    void Place(Terrain terrain)
    {
        if (!SetupToolUtils.TryGetPlayer(out Transform head, out Health playerHealth)) return;
        var prefab = AssetDatabase.LoadAssetAtPath<Coconut>(PrefabPath);
        var data = terrain.terrainData;
        var proto = data.treePrototypes[prototype];
        Vector3 center = SetupToolUtils.PlacementCenter(head.position);

        // Altura y desplazamiento de la copa (las palmeras pueden estar inclinadas)
        Vector3 top = new Vector3(0f, 6f, 0f);
        if (proto.prefab != null)
        {
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(proto.prefab);
            tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Bounds b = SetupToolUtils.RenderBounds(tmp);
            DestroyImmediate(tmp);
            top = new Vector3(b.center.x, b.max.y * 0.85f, b.center.z);
        }

        var trees = data.treeInstances
            .Where(t => t.prototypeIndex == prototype)
            .Select(t => (inst: t, pos: Vector3.Scale(t.position, data.size) + terrain.transform.position))
            .Where(t => Vector3.Distance(new Vector3(t.pos.x, center.y, t.pos.z), center) < searchRadius)
            .OrderBy(t => (t.pos - center).sqrMagnitude)
            .Take(palms).ToList();

        if (trees.Count == 0)
        {
            Debug.LogWarning($"[COCOS] No hay palmeras de tipo '{proto.prefab?.name}' a menos de {searchRadius} m de {center}. Mueve la Scene view o sube el radio.");
            return;
        }

        var group = new GameObject("Palmeras con cocos");
        Undo.RegisterCreatedObjectUndo(group, "Cocos");

        int n = 0;
        foreach (var (inst, pos) in trees)
        {
            Quaternion rot = Quaternion.Euler(0f, inst.rotation * Mathf.Rad2Deg, 0f);
            Vector3 scale = new Vector3(inst.widthScale, inst.heightScale, inst.widthScale);
            Vector3 crown = pos + rot * Vector3.Scale(top, scale);

            var spawner = new GameObject($"Cocos {++n}");
            spawner.transform.SetParent(group.transform);
            spawner.transform.position = crown;
            var tree = spawner.AddComponent<CoconutTree>();
            tree.coconutPrefab = prefab;
            tree.playerHead = head;
            tree.playerHealth = playerHealth;
            tree.spread = 1f;
            tree.triggerRadius = 3f;

            // Cocos en el suelo para coger y lanzar (no caducan)
            for (int i = 0; i < groundCoconuts; i++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(0.8f, 1.8f);
                Vector3 p = pos + new Vector3(r.x, 0f, r.y);
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.15f;
                var c = (Coconut)PrefabUtility.InstantiatePrefab(prefab, spawner.transform);
                c.transform.SetPositionAndRotation(p, Random.rotation);
                c.lifetime = 0f;
                c.playerHead = head;
                c.playerHealth = playerHealth;
            }
        }

        Selection.activeObject = group;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(group.scene);
        Debug.Log($"[COCOS] Cocos puestos en {trees.Count} palmeras ('{proto.prefab?.name}'), copa a ~{top.y:F1} m. Guarda la escena (Ctrl+S).");
    }
}
