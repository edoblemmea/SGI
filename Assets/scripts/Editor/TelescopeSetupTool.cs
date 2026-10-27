using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// Herramienta de editor: menu "Isla Pirata > Telescopio".
/// Crea Assets/Prefabs/Telescopio.prefab a partir de Maritime Telescope (URP):
///  - raiz con Rigidbody + XR Grab Interactable (dos manos) + Telescope;
///  - tramos first/second/third_segment que se extienden (calculado a partir de sus mallas);
///  - camara en la punta, lente en el ocular con material Unlit.
/// Y lo coloca en la escena apuntando al barco pirata.
public class TelescopeSetupTool : EditorWindow
{
    const string PrefabPath = SetupToolUtils.PrefabFolder + "/Telescopio.prefab";
    const string LensMatPath = SetupToolUtils.PrefabFolder + "/LenteTelescopio.mat";
    const string DefaultModel = "Assets/Maritime Telescope/URP & Built-In/URP/Prefabs/maritime_telescope.prefab";
    static readonly string[] SegmentNames = { "first_segment", "second_segment", "third_segment" };

    GameObject model;
    float closedLength = 0.45f;
    float slideFraction = 0.8f;
    Transform target;
    bool addObjective = true;

    [MenuItem("Isla Pirata/Telescopio")]
    static void Open() => GetWindow<TelescopeSetupTool>("Telescopio");

    void OnEnable()
    {
        if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModel);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("1. Prefab", EditorStyles.boldLabel);
        model = (GameObject)EditorGUILayout.ObjectField("Modelo", model, typeof(GameObject), false);
        closedLength = EditorGUILayout.FloatField(new GUIContent("Largo recogido (m)", "Tamano del catalejo recogido"), closedLength);
        slideFraction = EditorGUILayout.Slider(new GUIContent("Cuanto sale cada tramo", "Fraccion de su largo"), slideFraction, 0.3f, 0.95f);
        if (GUILayout.Button("Crear / actualizar prefab Telescopio")) CreatePrefab();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Colocar en la escena", EditorStyles.boldLabel);
        if (target == null)
        {
            var part = FindObjectsByType<Transform>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name.StartsWith("StylShip_Body"));
            if (part != null) target = part.root;   // solo en el editor: propone el barco pirata
        }
        target = (Transform)EditorGUILayout.ObjectField("Que hay que encontrar", target, typeof(Transform), true);
        addObjective = EditorGUILayout.Toggle(new GUIContent("Objetivo 'otear'", "Primer objetivo: 'Busca el barco pirata con el catalejo'"), addObjective);
        EditorGUILayout.HelpBox("Se coloca en el objeto seleccionado o en el centro de la Scene view (p. ej. encima de un barril en la playa o en lo alto de la isla).", MessageType.None);
        using (new EditorGUI.DisabledScope(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null))
            if (GUILayout.Button("Colocar telescopio")) Place();
    }

    // ------------------------------------------------------------------ 1
    void CreatePrefab()
    {
        if (model == null) { Debug.LogError("[TELESCOPIO] Elige el modelo (maritime_telescope de la carpeta URP)."); return; }
        SetupToolUtils.EnsurePrefabFolder();

        var root = new GameObject("Telescopio");
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Modelo";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            // --- Geometria: eje largo, cuerpo y tramos (en espacio de la raiz)
            var all = visual.GetComponentsInChildren<Transform>(true);
            var lodGroup = visual.GetComponentInChildren<LODGroup>();
            var lod0Renderers = lodGroup != null ? lodGroup.GetLODs()[0].renderers.Where(r => r != null).ToList()
                                                 : visual.GetComponentsInChildren<Renderer>().ToList();
            Bounds whole = Encapsulate(lod0Renderers);
            Vector3 size = whole.size;
            int ai = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            Vector3 axis = ai == 0 ? Vector3.right : ai == 1 ? Vector3.up : Vector3.forward;

            Renderer bodyR = lod0Renderers.FirstOrDefault(r => !SegmentNames.Contains(r.name));
            var segR = SegmentNames.Select(n => lod0Renderers.FirstOrDefault(r => r.name == n)).ToArray();
            if (bodyR == null || segR.Any(r => r == null))
            {
                Debug.LogError("[TELESCOPIO] No encuentro el cuerpo y los tramos first/second/third_segment en el LOD0. ¿Es el maritime_telescope?");
                return;
            }
            Bounds body = bodyR.bounds;
            float bodyC = Vector3.Dot(body.center, axis), bodyL = body.size[ai];
            float[] segC = segR.Select(r => Vector3.Dot(r.bounds.center, axis)).ToArray();
            float[] segL = segR.Select(r => r.bounds.size[ai]).ToArray();

            // Hacia donde salen: lado en el que estan los tramos respecto al cuerpo
            float side = segC.Average() - bodyC;
            if (Mathf.Abs(side) < 0.001f) side = segC[2] - bodyC;
            float dir = side >= 0f ? 1f : -1f;

            // ¿El modelo viene ya extendido? (el tramo mas fino sobresale mucho del cuerpo)
            bool comesExtended = (segC[2] - bodyC) * dir > bodyL * 0.5f;

            // --- Tramos: desplazamiento acumulado (cada tramo arrastra a los mas finos)
            var tel = root.AddComponent<Telescope>();
            tel.segments.Clear();
            float cumulative = 0f;
            for (int k = 0; k < SegmentNames.Length; k++)
            {
                cumulative += segL[k] * slideFraction;
                var parts = all.Where(t => t.name == SegmentNames[k]).ToArray();
                Vector3 offsetRoot = axis * dir * cumulative;
                var seg = new Telescope.Segment { parts = parts, openOffset = parts[0].parent.InverseTransformVector(offsetRoot) };
                if (comesExtended)
                    foreach (var p in parts) p.localPosition -= seg.openOffset;   // dejarlo recogido como reposo
                tel.segments.Add(seg);
            }

            // --- Ocular (en la punta del tramo mas fino) y objetivo (al otro extremo del cuerpo)
            Vector3 viewDir = -axis * dir;   // desde el ocular hacia la punta grande
            var thirdLod0 = segR[2].transform;
            Bounds thirdB = segR[2].bounds;   // ya en posicion recogida
            var eyepiece = new GameObject("Ocular").transform;
            eyepiece.SetParent(thirdLod0, false);
            eyepiece.position = thirdB.center + axis * dir * (thirdB.extents[ai] + 0.003f);
            eyepiece.rotation = Quaternion.LookRotation(viewDir, ai == 1 ? Vector3.forward : Vector3.up);

            var camGo = new GameObject("CamaraTelescopio");
            camGo.transform.SetParent(visual.transform, false);
            camGo.transform.position = body.center - axis * dir * (body.extents[ai] + 0.01f);
            camGo.transform.rotation = Quaternion.LookRotation(viewDir, ai == 1 ? Vector3.forward : Vector3.up);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.enabled = false;

            var lens = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lens.name = "Lente";
            DestroyImmediate(lens.GetComponent<Collider>());
            lens.transform.SetParent(root.transform, false);
            float lensSize = Mathf.Min(thirdB.size[(ai + 1) % 3], thirdB.size[(ai + 2) % 3]) * 0.9f;
            lens.transform.localScale = Vector3.one * Mathf.Max(lensSize, 0.01f);
            lens.transform.SetPositionAndRotation(eyepiece.position, eyepiece.rotation);
            var lr = lens.GetComponent<MeshRenderer>();
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = LensMaterial();

            tel.lensCamera = cam;
            tel.lensRenderer = lr;
            tel.eyepiece = eyepiece;

            // --- Tamano: que recogido mida 'closedLength'
            float currentLen = Encapsulate(lod0Renderers).size[ai];
            float scale = currentLen > 0.0001f ? closedLength / currentLen : 1f;
            visual.transform.localScale = Vector3.one * scale;
            lens.transform.localScale *= scale;
            lens.transform.SetPositionAndRotation(eyepiece.position, eyepiece.rotation);

            // --- Fisica y agarre a dos manos
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (visual.GetComponentInChildren<Collider>() == null)
            {
                var cap = root.AddComponent<CapsuleCollider>();
                cap.direction = ai;
                cap.height = closedLength;
                cap.radius = lensSize * scale;
            }
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.selectMode = InteractableSelectMode.Multiple;   // permite cogerlo con las dos manos
            grab.useDynamicAttach = true;                         // se queda donde lo agarras
            grab.throwOnDetach = true;
            var transformer = root.AddComponent<XRGeneralGrabTransformer>();
            transformer.allowTwoHandedScaling = false;            // el "alargar" lo hace Telescope, no un escalado uniforme
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            tel.audioSource = audio;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[TELESCOPIO] Prefab guardado en {PrefabPath}. Eje {"XYZ"[ai]}, los tramos salen hacia {(dir > 0 ? "+" : "-")}{"XYZ"[ai]}, " +
                      $"modelo {(comesExtended ? "venia EXTENDIDO (lo he recogido)" : "venia recogido")}, largo recogido {closedLength} m, " +
                      $"recorrido total de los tramos {cumulative * scale:F2} m. Abre el prefab y mueve el slider 'Extension' del componente Telescope para verlo.");
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    static Bounds Encapsulate(List<Renderer> rs)
    {
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Material LensMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(LensMatPath);
        if (mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        mat = new Material(shader != null ? shader : Shader.Find("Unlit/Texture"));
        mat.SetColor("_BaseColor", new Color(0.05f, 0.08f, 0.1f));
        AssetDatabase.CreateAsset(mat, LensMatPath);
        return mat;
    }

    // ------------------------------------------------------------------ 2
    void Place()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        SetupToolUtils.TryGetPlayer(out Transform head, out _);
        Vector3 p = SetupToolUtils.PlacementCenter(head != null ? head.position : Vector3.zero);
        Physics.SyncTransforms();
        if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Telescopio");
        go.transform.position = p + Vector3.up * 0.15f;
        if (target != null)
        {
            Vector3 look = Vector3.ProjectOnPlane(target.position - p, Vector3.up);
            if (look.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(look);
        }

        var tel = go.GetComponent<Telescope>();
        tel.target = target;
        tel.objectiveOnSpot = addObjective ? "otear" : "";

        if (addObjective)
        {
            var gm = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null) Debug.LogWarning("[TELESCOPIO] No hay GameManager: crea uno con Isla Pirata > Juego y vuelve a colocar el telescopio.");
            else if (gm.objectives.All(o => o.id != "otear"))
            {
                Undo.RecordObject(gm, "Objetivo otear");
                gm.objectives.Insert(0, new GameManager.Objective { id = "otear", description = "Busca el barco pirata con el catalejo", required = 1 });
            }
        }

        Selection.activeObject = go;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log($"[TELESCOPIO] Colocado en {p}" + (target != null ? $", buscando '{target.name}'" : ", SIN objetivo (asigna 'Target')") +
                  (addObjective ? ". Objetivo 'otear' anadido al principio." : ".") + " Guarda (Ctrl+S).");
    }
}
