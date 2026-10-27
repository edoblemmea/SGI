using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Herramienta de editor: menu "Isla Pirata > Palmeras sacudibles".
/// Crea Assets/Prefabs/PalmeraSacudible.prefab con el modelo de palmera del asset Island (Tree_a / Tree_b)
/// + XR Simple Interactable + ShakeablePalm, y coloca palmeras nuevas sobre el terreno.
/// Necesita antes el prefab Coco (menu Isla Pirata > Cocos, paso 1).
public class PalmSetupTool : EditorWindow
{
    const string PrefabPath = SetupToolUtils.PrefabFolder + "/PalmeraSacudible.prefab";
    const string CocoPath = SetupToolUtils.PrefabFolder + "/Coco.prefab";
    const string DefaultModel = "Assets/Island/Prefabs/Tree_a.prefab";

    GameObject model;
    int coconuts = 4;
    float crownHeight = 0.85f;
    int count = 4;
    float radius = 10f;
    Vector2 scaleRange = new Vector2(0.9f, 1.15f);

    [MenuItem("Isla Pirata/Palmeras sacudibles")]
    static void Open() => GetWindow<PalmSetupTool>("Palmeras");

    void OnEnable()
    {
        if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModel);
    }

    void OnGUI()
    {
        bool hasCoco = AssetDatabase.LoadAssetAtPath<Coconut>(CocoPath) != null;
        if (!hasCoco) EditorGUILayout.HelpBox("Primero crea el prefab Coco en Isla Pirata > Cocos (paso 1).", MessageType.Warning);

        EditorGUILayout.LabelField("1. Prefab de palmera sacudible", EditorStyles.boldLabel);
        model = (GameObject)EditorGUILayout.ObjectField("Modelo (Tree_a / Tree_b)", model, typeof(GameObject), false);
        coconuts = EditorGUILayout.IntSlider("Cocos en la copa", coconuts, 1, 8);
        crownHeight = EditorGUILayout.Slider(new GUIContent("Altura de la copa", "Fraccion de la altura del arbol donde cuelgan los cocos"), crownHeight, 0.5f, 1f);
        using (new EditorGUI.DisabledScope(!hasCoco))
            if (GUILayout.Button("Crear / actualizar prefab PalmeraSacudible")) CreatePrefab();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Colocar en la escena abierta", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Alrededor del objeto seleccionado o del centro de la Scene view, sobre el terreno. " +
                                "Despues puedes moverlas una a una con la herramienta Move.", MessageType.None);
        count = EditorGUILayout.IntSlider("Cantidad", count, 1, 20);
        radius = EditorGUILayout.FloatField("Radio (m)", radius);
        scaleRange = EditorGUILayout.Vector2Field("Escala min / max", scaleRange);
        using (new EditorGUI.DisabledScope(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null))
            if (GUILayout.Button("Colocar palmeras")) Place();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Arreglar las palmeras ya colocadas", EditorStyles.boldLabel);
        if (GUILayout.Button("Quitar 'Static' de las palmeras de la escena")) FixStatic();
    }

    void FixStatic()
    {
        int n = 0, palms = 0;
        foreach (var p in FindObjectsByType<ShakeablePalm>(FindObjectsInactive.Include)) { n += SetupToolUtils.ClearStatic(p.gameObject); palms++; }
        if (n > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[PALMERAS] {palms} palmeras revisadas: quitado 'Static' de {n} objetos. Ahora el tronco se balancea al sacudirlo. Guarda (Ctrl+S).");
    }

    void CreatePrefab()
    {
        if (model == null) { Debug.LogError("[PALMERAS] Elige un modelo."); return; }
        SetupToolUtils.EnsurePrefabFolder();

        var root = new GameObject("PalmeraSacudible");
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Modelo";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            SetupToolUtils.ClearStatic(visual);   // DEMO: Tree_a/Tree_b vienen Static y el tronco no se balancearia

            Bounds b = SetupToolUtils.RenderBounds(visual);

            // Tronco agarrable: usa el collider del modelo o crea uno
            if (visual.GetComponentInChildren<Collider>() == null)
            {
                var cap = visual.AddComponent<CapsuleCollider>();
                cap.direction = 1;
                cap.radius = 0.3f;
                cap.height = b.size.y * 0.75f;
                cap.center = new Vector3(0f, cap.height * 0.5f, 0f);
            }

            var crown = new GameObject("Copa").transform;
            crown.SetParent(visual.transform, false);
            crown.position = new Vector3(b.center.x, b.min.y + b.size.y * crownHeight, b.center.z);

            var simple = root.AddComponent<XRSimpleInteractable>();
            var palm = root.AddComponent<ShakeablePalm>();
            palm.swayPivot = visual.transform;
            palm.crown = crown;
            palm.coconutPrefab = AssetDatabase.LoadAssetAtPath<Coconut>(CocoPath);
            palm.coconutsInCrown = coconuts;
            palm.crownSpread = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.15f, 0.2f, 0.6f);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[PALMERAS] Prefab guardado en {PrefabPath}. Altura del arbol {b.size.y:F1} m, copa a {b.size.y * crownHeight:F1} m. " +
                      "Abre el prefab y mueve el objeto 'Copa' si los cocos no quedan bajo las hojas.");
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    void Place()
    {
        if (!SetupToolUtils.TryGetPlayer(out Transform head, out Health playerHealth)) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Vector3 center = SetupToolUtils.PlacementCenter(head.position);
        var terrain = Terrain.activeTerrain;   // DEMO: terreno de la Island demo

        var group = new GameObject("Palmeras sacudibles");
        Undo.RegisterCreatedObjectUndo(group, "Colocar palmeras");
        for (int i = 0; i < count; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 p = center + new Vector3(r.x, 0f, r.y);
            if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            go.name = $"Palmera {i + 1}";
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.transform.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);
            var palm = go.GetComponent<ShakeablePalm>();
            palm.playerHead = head;
            palm.playerHealth = playerHealth;
            Undo.RegisterCreatedObjectUndo(go, "Colocar palmeras");
        }
        Selection.activeObject = group;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(group.scene);
        Debug.Log($"[PALMERAS] Colocadas {count} palmeras alrededor de {center}. Comprueba que ninguna ha caido en el agua y guarda (Ctrl+S).");
    }
}
