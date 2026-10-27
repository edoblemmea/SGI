using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Herramienta de editor: menu "Isla Pirata > Maldicion".
/// Monta el final del juego en el barco fantasma:
///  - ciclo dia/noche (el tiempo solo avanza con el reloj de arena o el menu),
///  - objeto 'Maldicion' (Curse),
///  - cofre maldito: de dia la llave no gira (mensaje), de noche brilla la cerradura y se abre rompiendo la maldicion,
///  - reloj de arena junto al cofre (darle la vuelta = anochece / amanece),
///  - totems StaffOfPain alrededor del cofre que brillan en rojo hasta que se rompe la maldicion.
public class CurseSetupTool : EditorWindow
{
    const string HourglassPath = SetupToolUtils.PrefabFolder + "/RelojArena.prefab";
    const string TotemPath = SetupToolUtils.PrefabFolder + "/TotemMaldito.prefab";
    const string StaffModel = "Assets/StaffOfPain/Prefab/StaffOfPain.prefab";
    const string SoundFolder = "Assets/AS_Casual_Island_Game_Sounds_FREE";

    ChestLock chest;
    int totems = 4;
    float totemRadius = 2f;
    float totemHeight = 1.6f;

    [MenuItem("Isla Pirata/Maldicion")]
    static void Open() => GetWindow<CurseSetupTool>("Maldicion");

    void OnGUI()
    {
        if (chest == null) chest = FindAnyObjectByType<ChestLock>(FindObjectsInactive.Include);
        chest = (ChestLock)EditorGUILayout.ObjectField("Cofre (en el barco fantasma)", chest, typeof(ChestLock), true);
        if (chest == null) EditorGUILayout.HelpBox("Prepara antes el cofre: Isla Pirata > Llave y cofre > Preparar cofre.", MessageType.Warning);
        totems = EditorGUILayout.IntSlider("Totems alrededor del cofre", totems, 0, 8);
        totemRadius = EditorGUILayout.FloatField("Distancia al cofre (m)", totemRadius);
        totemHeight = EditorGUILayout.FloatField("Altura de cada totem (m)", totemHeight);
        using (new EditorGUI.DisabledScope(chest == null))
            if (GUILayout.Button("Preparar maldicion (dia/noche, cofre, reloj de arena y totems)")) Prepare();
    }

    void Prepare()
    {
        var dayNight = EnsureDayNight();
        var curse = EnsureCurse();
        SetupChest(dayNight);
        PlaceHourglass(dayNight);
        if (totems > 0) PlaceTotems(dayNight);

        var gm = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        var cofre = gm != null ? gm.objectives.Find(o => o.id == "cofre") : null;
        if (cofre != null) { Undo.RecordObject(gm, "Objetivo"); cofre.description = "Abre el cofre bajo la luna"; }

        Selection.activeObject = chest.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(chest.gameObject.scene);
        Debug.Log("[MALDICION] Listo: 'DiaNoche', 'Maldicion', cofre maldito, 'Reloj de arena' y totems. Guarda (Ctrl+S). " +
                  "Prueba sin casco en Play: en 'DiaNoche' clic derecho > 'Hacer de noche', y luego en ChestLock > 'Abrir ahora'.");
    }

    // ------------------------------------------------------------------ dia/noche y maldicion
    static DayNightCycle EnsureDayNight()
    {
        var dn = FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
        if (dn != null) return dn;
        var go = new GameObject("DiaNoche");
        Undo.RegisterCreatedObjectUndo(go, "DiaNoche");
        dn = go.AddComponent<DayNightCycle>();
        // DEMO: el sol es la Directional Light de la escena (la mas intensa)
        dn.sun = FindObjectsByType<Light>(FindObjectsInactive.Exclude)
            .Where(l => l.type == LightType.Directional).OrderByDescending(l => l.intensity).FirstOrDefault();
        dn.hour = 9f;
        dn.dayLengthMinutes = 0f;   // el tiempo solo pasa con el reloj de arena / el menu

        var moonGo = new GameObject("Luna");
        moonGo.transform.SetParent(go.transform, false);
        var moon = moonGo.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.65f, 1f);
        moon.intensity = 0f;
        moon.shadows = LightShadows.None;
        dn.moon = moon;
        dn.moonIntensity = 0.35f;
        Debug.Log($"[MALDICION] Creado 'DiaNoche' (sol = {(dn.sun != null ? dn.sun.name : "NINGUNO, asignalo a mano")}). Empieza a las 9 h y el tiempo no avanza solo.");
        return dn;
    }

    static Curse EnsureCurse()
    {
        var c = FindAnyObjectByType<Curse>(FindObjectsInactive.Include);
        if (c != null) return c;
        var go = new GameObject("Maldicion");
        Undo.RegisterCreatedObjectUndo(go, "Maldicion");
        c = go.AddComponent<Curse>();
        var a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 0f;
        c.audioSource = a;
        c.breakClip = Sound("Jingle_3");
        return c;
    }

    static AudioClip Sound(string name) =>
        AssetDatabase.FindAssets(name + " t:AudioClip", new[] { SoundFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(c => c != null && c.name == name);

    // ------------------------------------------------------------------ cofre
    void SetupChest(DayNightCycle dayNight)
    {
        Undo.RecordObject(chest, "Cofre maldito");
        chest.requireNight = true;
        chest.dayNight = dayNight;
        chest.breaksCurse = true;
        Bounds b = SetupToolUtils.RenderBounds(chest.gameObject);

        if (chest.hintText == null)
            chest.hintText = SetupToolUtils.MakeWorldText("Mensaje Maldicion", chest.transform,
                new Vector3(b.center.x, b.max.y + 0.6f, b.center.z), 1.2f, chest.dayHint, 60f);
        // que el texto mire hacia fuera del cofre (hacia donde estara el jugador se ajusta en Play con el casco)
        if (chest.nightGlow == null)
        {
            var glow = new GameObject("Brillo Cerradura");
            Undo.RegisterCreatedObjectUndo(glow, "Brillo");
            glow.transform.SetParent(chest.socket != null ? chest.socket.transform : chest.transform, false);
            glow.transform.position = chest.socket != null ? chest.socket.transform.position : b.center;
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.6f, 0.8f, 1f);
            l.range = 2f;
            l.intensity = 3f;
            l.shadows = LightShadows.None;
            chest.nightGlow = glow;
        }
        if (chest.wrongKeyClip == null) chest.wrongKeyClip = Sound("SFX_1");
        if (chest.openClip == null) chest.openClip = Sound("Jingle_4");
        Debug.Log("[MALDICION] Cofre maldito: de dia muestra el mensaje, de noche brilla la cerradura y al abrirse rompe la maldicion.");
    }

    // ------------------------------------------------------------------ reloj de arena
    void PlaceHourglass(DayNightCycle dayNight)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HourglassPath);
        if (prefab == null) prefab = BuildHourglassPrefab();

        var old = FindAnyObjectByType<Hourglass>(FindObjectsInactive.Include);
        GameObject go = old != null ? old.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (old == null) Undo.RegisterCreatedObjectUndo(go, "Reloj de arena");
        go.name = "Reloj de arena";

        Bounds b = SetupToolUtils.RenderBounds(chest.gameObject);
        Vector3 side = chest.transform.right * (b.extents.magnitude + 0.5f);
        Vector3 p = Ground(b.center + side, b.max.y + 1f);
        go.transform.SetParent(chest.transform.parent, true);
        go.transform.position = p + Vector3.up * 0.02f;
        go.transform.rotation = Quaternion.identity;

        var h = go.GetComponent<Hourglass>();
        Undo.RecordObject(h, "Reloj");
        h.dayNight = dayNight;
        if (h.flipClip == null) h.flipClip = Sound("SFX_2");
        Debug.Log($"[MALDICION] Reloj de arena junto al cofre, en {p}.");
    }

    static GameObject BuildHourglassPrefab()
    {
        SetupToolUtils.EnsurePrefabFolder();
        var root = new GameObject("RelojArena");
        try
        {
            var wood = SimpleMat("Madera", new Color(0.35f, 0.2f, 0.1f), false);
            var glass = SimpleMat("Cristal", new Color(0.8f, 0.9f, 1f, 0.25f), true);
            var sand = SimpleMat("Arena", new Color(0.95f, 0.8f, 0.45f), false);

            Part(root, "Base", PrimitiveType.Cylinder, new Vector3(0, 0.01f, 0), new Vector3(0.14f, 0.01f, 0.14f), wood);
            Part(root, "Tapa", PrimitiveType.Cylinder, new Vector3(0, 0.25f, 0), new Vector3(0.14f, 0.01f, 0.14f), wood);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Part(root, "Poste", PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * 0.055f, 0.13f, Mathf.Sin(a) * 0.055f), new Vector3(0.012f, 0.12f, 0.012f), wood);
            }
            Part(root, "Cristal Arriba", PrimitiveType.Sphere, new Vector3(0, 0.185f, 0), new Vector3(0.09f, 0.11f, 0.09f), glass);
            Part(root, "Cristal Abajo", PrimitiveType.Sphere, new Vector3(0, 0.075f, 0), new Vector3(0.09f, 0.11f, 0.09f), glass);
            var top = Part(root, "Arena Arriba", PrimitiveType.Cylinder, new Vector3(0, 0.17f, 0), new Vector3(0.06f, 0.025f, 0.06f), sand);
            var bottom = Part(root, "Arena Abajo", PrimitiveType.Cylinder, new Vector3(0, 0.045f, 0), new Vector3(0.06f, 0.025f, 0.06f), sand);

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.13f, 0);
            col.height = 0.26f;
            col.radius = 0.07f;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            var h = root.AddComponent<Hourglass>();
            h.sandTop = top;
            h.sandBottom = bottom;
            h.audioSource = audio;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HourglassPath);
            Debug.Log($"[MALDICION] Prefab {HourglassPath} creado con primitivas (26 cm). Si encontrais un modelo de reloj de arena, ponedlo dentro y quitad las piezas.");
            return prefab;
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    static Transform Part(GameObject root, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(root.transform, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g.transform;
    }

    static Material SimpleMat(string name, Color c, bool transparent)
    {
        string path = $"{SetupToolUtils.PrefabFolder}/Reloj_{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        if (transparent)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ------------------------------------------------------------------ totems
    void PlaceTotems(DayNightCycle dayNight)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TotemPath);
        if (prefab == null) prefab = BuildTotemPrefab();
        if (prefab == null) return;

        foreach (var oldT in FindObjectsByType<CurseTotem>(FindObjectsInactive.Include)) Undo.DestroyObjectImmediate(oldT.gameObject);
        var group = new GameObject("Totems Maldicion");
        Undo.RegisterCreatedObjectUndo(group, "Totems");
        group.transform.SetParent(chest.transform.parent, false);

        Bounds b = SetupToolUtils.RenderBounds(chest.gameObject);
        int placed = 0;
        for (int i = 0; i < totems; i++)
        {
            float a = i * Mathf.PI * 2f / totems + Mathf.PI / totems;
            Vector3 p = b.center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * totemRadius;
            p = Ground(p, b.max.y + 1f);
            var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            t.name = $"Totem {i + 1}";
            t.transform.position = p;
            Vector3 look = Vector3.ProjectOnPlane(b.center - p, Vector3.up);
            if (look.sqrMagnitude > 0.01f) t.transform.rotation = Quaternion.LookRotation(look);
            t.GetComponent<CurseTotem>().dayNight = dayNight;
            placed++;
        }
        Debug.Log($"[MALDICION] {placed} totems alrededor del cofre a {totemRadius} m. Muevelos si alguno queda fuera de la cubierta.");
    }

    GameObject BuildTotemPrefab()
    {
        var staff = AssetDatabase.LoadAssetAtPath<GameObject>(StaffModel);
        if (staff == null) { Debug.LogWarning($"[MALDICION] No encuentro {StaffModel}: no pongo totems."); return null; }
        SetupToolUtils.EnsurePrefabFolder();

        // El material del asset usa el shader antiguo (se veria rosa)
        foreach (var r in staff.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (SetupToolUtils.UpgradeToURP(m)) Debug.Log($"[MALDICION] Material '{m.name}' convertido a URP/Lit.");

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(staff);
        try
        {
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "TotemMaldito";
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // La Reflection Probe del asset gasta mucho en el Quest
            foreach (var probe in inst.GetComponentsInChildren<ReflectionProbe>(true)) DestroyImmediate(probe.gameObject == inst ? (Object)probe : probe.gameObject);

            Bounds b = SetupToolUtils.RenderBounds(inst);
            if (b.size.y > 0.0001f) inst.transform.localScale *= totemHeight / b.size.y;
            b = SetupToolUtils.RenderBounds(inst);
            // Que la base quede en el suelo (pivote abajo)
            var root = new GameObject("TotemMaldito");
            root.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
            inst.transform.SetParent(root.transform, true);
            root.transform.position = Vector3.zero;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, b.size.y * 0.5f, 0);
            col.height = b.size.y;
            col.radius = Mathf.Max(0.05f, Mathf.Min(b.extents.x, b.extents.z) * 0.5f);

            var lightGo = new GameObject("Luz Maldicion");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0, b.size.y * 0.85f, 0);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 4f;
            l.shadows = LightShadows.None;
            var totem = root.AddComponent<CurseTotem>();
            totem.glow = l;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TotemPath);
            DestroyImmediate(root);
            inst = null;
            Debug.Log($"[MALDICION] Prefab {TotemPath} creado (StaffOfPain, {totemHeight} m, sin Reflection Probe, con luz roja).");
            return prefab;
        }
        finally
        {
            if (inst != null) DestroyImmediate(inst);
        }
    }

    // Suelo bajo el punto (cubierta del barco o terreno)
    static Vector3 Ground(Vector3 p, float fromY)
    {
        Physics.SyncTransforms();
        var hit = Physics.RaycastAll(new Vector3(p.x, fromY, p.z), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.normal.y > 0.7f && h.collider.GetComponentInParent<ChestLock>() == null)
            .OrderBy(h => h.distance).FirstOrDefault();
        return hit.collider != null ? hit.point : p;
    }
}
