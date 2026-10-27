using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// Herramienta de editor: menu "Isla Pirata > Juego".
/// 1) GameManager con los objetivos (otear, llave, barco, cofre y opcionalmente aranas/esqueletos), vida del jugador,
///    musica y jingles del asset AS_Casual_Island_Game_Sounds_FREE.
/// 2) Barco fantasma: ancla de desembarco en cubierta y zona de llegada en barca (objetivo 'barco').
/// 3) Interfaz: panel de objetivos en la muneca izquierda, pantalla de fin delante de la camara y velo rojo de dano.
public class GameSetupTool : EditorWindow
{
    const string SoundFolder = "Assets/AS_Casual_Island_Game_Sounds_FREE";
    const string OverlayMatPath = SetupToolUtils.PrefabFolder + "/VeloDano.mat";

    bool killSpiders, killPirates, music = true;
    Transform leftHand;
    Transform pirateShip;

    [MenuItem("Isla Pirata/Juego")]
    static void Open() => GetWindow<GameSetupTool>("Juego");

    void OnGUI()
    {
        EditorGUILayout.LabelField("1. GameManager", EditorStyles.boldLabel);
        killSpiders = EditorGUILayout.Toggle("Objetivo: matar las aranas", killSpiders);
        killPirates = EditorGUILayout.Toggle("Objetivo: acabar con los esqueletos", killPirates);
        music = EditorGUILayout.Toggle("Musica de fondo", music);
        if (GUILayout.Button("Crear / actualizar GameManager")) CreateGameManager();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Barco fantasma (el cofre esta a bordo)", EditorStyles.boldLabel);
        if (pirateShip == null) pirateShip = GuessPirateShip();
        pirateShip = (Transform)EditorGUILayout.ObjectField("Barco fantasma (raiz)", pirateShip, typeof(Transform), true);
        using (new EditorGUI.DisabledScope(pirateShip == null))
            if (GUILayout.Button("Preparar barco fantasma")) PreparePirateShip();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Interfaz", EditorStyles.boldLabel);
        if (leftHand == null) leftHand = GuessLeftHand();
        leftHand = (Transform)EditorGUILayout.ObjectField("Mano izquierda (Left Controller)", leftHand, typeof(Transform), true);
        if (GUILayout.Button("Crear panel de objetivos, pantalla de fin y velo de dano")) CreateUI();
    }

    static Transform GuessLeftHand()
    {
        // Solo en el editor: propone el hijo del XR Origin que se llama "Left Controller" (se puede cambiar a mano)
        var origin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (origin == null) return null;
        return origin.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name.ToLower().Contains("left") && t.name.ToLower().Contains("controller"));
    }

    static GameManager FindOrCreateGM()
    {
        var gm = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm != null) return gm;
        var go = new GameObject("GameManager");
        Undo.RegisterCreatedObjectUndo(go, "GameManager");
        gm = go.AddComponent<GameManager>();
        gm.objectives.Clear();
        return gm;
    }

    static void EnsureObjective(GameManager gm, string id, string description, int required)
    {
        var o = gm.objectives.Find(x => x.id == id);
        if (o == null) gm.objectives.Add(new GameManager.Objective { id = id, description = description, required = Mathf.Max(1, required) });
        else { o.description = description; o.required = Mathf.Max(1, required); }
    }

    static AudioClip Sound(string name) =>
        AssetDatabase.FindAssets(name + " t:AudioClip", new[] { SoundFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(c => c != null && c.name == name);

    // ------------------------------------------------------------------ 1
    void CreateGameManager()
    {
        var gm = FindOrCreateGM();
        Undo.RecordObject(gm, "GameManager");

        bool keyInPalm = FindObjectsByType<ShakeablePalm>(FindObjectsInactive.Include).Any(p => p.hiddenKey != null);
        gm.objectives.RemoveAll(o => o.id == "isla" || o.id == "piratas");   // de versiones anteriores de la historia
        EnsureObjective(gm, "llave", keyInPalm ? "Sacude las palmeras y encuentra la llave" : "Encuentra la llave", 1);
        EnsureObjective(gm, "barco", "Rema hasta el barco fantasma", 1);
        EnsureObjective(gm, "cofre", "Abre el cofre bajo la luna", 1);

        // Aranas: sin windup; esqueletos: levantan la espada antes de golpear
        var enemies = FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude);
        SetKillObjective(gm, enemies, "aranas", "Acaba con las aranas", killSpiders, e => e.attackWindup <= 0f);
        SetKillObjective(gm, enemies, "esqueletos", "Libera a los esqueletos de la tripulacion", killPirates, e => e.attackWindup > 0f);

        if (SetupToolUtils.TryGetPlayer(out _, out Health playerHealth)) gm.playerHealth = playerHealth;

        // Sonido: jingles para objetivo/victoria y musica de fondo
        var src = gm.GetComponent<AudioSource>();
        if (src == null) src = Undo.AddComponent<AudioSource>(gm.gameObject);
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        gm.audioSource = src;
        if (gm.objectiveClip == null) gm.objectiveClip = Sound("Jingle_1");
        if (gm.winClip == null) gm.winClip = Sound("Jingle_2");

        var musicT = gm.transform.Find("Musica");
        if (music && musicT == null)
        {
            var m = new GameObject("Musica");
            Undo.RegisterCreatedObjectUndo(m, "Musica");
            m.transform.SetParent(gm.transform, false);
            var a = m.AddComponent<AudioSource>();
            a.clip = Sound("Main_Soundtrack");
            a.loop = true;
            a.playOnAwake = true;
            a.volume = 0.25f;
            a.spatialBlend = 0f;
        }
        else if (!music && musicT != null) Undo.DestroyObjectImmediate(musicT.gameObject);

        Selection.activeObject = gm.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
        Debug.Log($"[JUEGO] GameManager listo con objetivos: {string.Join(", ", gm.objectives.Select(o => o.id + (o.required > 1 ? $" x{o.required}" : "")))}. " +
                  $"Vida del jugador: {(gm.playerHealth != null ? gm.playerHealth.name : "NULL")}. Musica: {(music ? "si" : "no")}. Guarda (Ctrl+S).");
    }

    static void SetKillObjective(GameManager gm, EnemyAI[] all, string id, string desc, bool on, System.Func<EnemyAI, bool> filter)
    {
        var group = all.Where(filter).ToArray();
        if (on && group.Length > 0)
        {
            EnsureObjective(gm, id, desc, group.Length);
            foreach (var e in group) { Undo.RecordObject(e, "Objetivo"); e.objectiveOnDeath = id; }
        }
        else
        {
            gm.objectives.RemoveAll(o => o.id == id);
            foreach (var e in group.Where(e => e.objectiveOnDeath == id)) { Undo.RecordObject(e, "Objetivo"); e.objectiveOnDeath = ""; }
            if (on) Debug.LogWarning($"[JUEGO] No hay enemigos para el objetivo '{id}' en la escena.");
        }
    }

    // ------------------------------------------------------------------ 2
    static Transform GuessPirateShip()
    {
        // Solo en el editor: propone la raiz cuyo hijo se llama StylShip_* (se puede cambiar a mano)
        var part = FindObjectsByType<Transform>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name.StartsWith("StylShip_Body"));
        return part != null ? part.root : null;
    }

    void PreparePirateShip()
    {
        var boat = FindAnyObjectByType<BoatRowing>(FindObjectsInactive.Include);
        var origin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        var chest = FindAnyObjectByType<ChestLock>(FindObjectsInactive.Include);
        if (boat == null || origin == null) { Debug.LogError("[BARCO PIRATA] Falta la barca (BoatRowing) o el XR Origin en la escena."); return; }
        if (boat.transform.IsChildOf(pirateShip)) { Debug.LogError("[BARCO PIRATA] Has elegido la barca de remos, no el barco pirata."); return; }
        if (chest == null) Debug.LogWarning("[BARCO PIRATA] No hay cofre preparado (Isla Pirata > Llave y cofre > Preparar cofre).");
        else if (!chest.transform.IsChildOf(pirateShip) && Vector3.Distance(chest.transform.position, pirateShip.position) > 30f)
            Debug.LogWarning($"[BARCO PIRATA] El cofre '{chest.name}' parece estar lejos del barco pirata.");

        Bounds ship = SetupToolUtils.RenderBounds(pirateShip.gameObject);

        // 1) Ancla de desembarco en la cubierta, junto al cofre (o en el centro del barco)
        Vector3 target = chest != null ? chest.transform.position : ship.center;
        Vector3 toCenter = Vector3.ProjectOnPlane(ship.center - target, Vector3.up);
        Vector3 spot = target + (toCenter.sqrMagnitude > 0.01f ? toCenter.normalized : -pirateShip.forward) * 1.5f;
        Physics.SyncTransforms();
        var hit = Physics.RaycastAll(new Vector3(spot.x, ship.max.y + 1f, spot.z), Vector3.down, ship.size.y + 5f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.transform.IsChildOf(pirateShip) && h.normal.y > 0.7f)
            .OrderBy(h => Mathf.Abs(h.point.y - target.y)).FirstOrDefault();
        if (hit.collider != null) spot = hit.point;
        else Debug.LogWarning("[BARCO PIRATA] No encontre la cubierta debajo del ancla: revisa su altura a mano.");

        var oldAnchor = pirateShip.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>(true).FirstOrDefault();
        GameObject anchorGo = oldAnchor != null ? oldAnchor.gameObject : new GameObject("anclaBarcoPirata");
        if (oldAnchor == null) Undo.RegisterCreatedObjectUndo(anchorGo, "Ancla barco pirata");
        Undo.SetTransformParent(anchorGo.transform, pirateShip, "Ancla barco pirata");
        Undo.RecordObject(anchorGo.transform, "Ancla barco pirata");
        anchorGo.transform.position = spot;
        Vector3 look = Vector3.ProjectOnPlane(target - spot, Vector3.up);
        anchorGo.transform.rotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : pirateShip.rotation;

        var box = anchorGo.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(anchorGo);
        box.isTrigger = false;
        box.center = new Vector3(0f, 0.5f, 0f);
        box.size = new Vector3(1.5f, 1f, 1.5f);
        int anchorLayer = LayerMask.NameToLayer("TeleportAnchor");
        if (anchorLayer >= 0) anchorGo.layer = anchorLayer;

        var anchor = anchorGo.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>();
        if (anchor == null) anchor = Undo.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>(anchorGo);
        Undo.RecordObject(anchor, "Ancla barco pirata");
        anchor.teleportAnchorTransform = anchorGo.transform;
        anchor.matchOrientation = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation.TargetUpAndForward;
        anchor.teleportationProvider = FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>(FindObjectsInactive.Include);
        // Mismas Interaction Layers que el ancla del muelle
        var dock = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>(FindObjectsInactive.Include)
            .FirstOrDefault(a => a != anchor && !a.transform.IsChildOf(boat.transform));
        if (dock != null) anchor.interactionLayers = dock.interactionLayers;
        bool hasDisembark = false;
        for (int i = 0; i < anchor.teleporting.GetPersistentEventCount(); i++)
            if (anchor.teleporting.GetPersistentMethodName(i) == "Disembark") hasDisembark = true;
        if (!hasDisembark) UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(anchor.teleporting, boat.Disembark);

        // 2) Zona alrededor del barco pirata: se completa al llegar con la barca
        foreach (var z in FindObjectsByType<ObjectiveZone>(FindObjectsInactive.Include).Where(z => z.objectiveId == "isla"))
        {
            Debug.Log($"[BARCO PIRATA] Borro '{z.name}' (objetivo 'isla'), ya no hace falta.");
            Undo.DestroyObjectImmediate(z.gameObject);
        }
        var zoneT = pirateShip.Find("ZonaBarcoPirata");
        GameObject zoneGo = zoneT != null ? zoneT.gameObject : new GameObject("ZonaBarcoPirata");
        if (zoneT == null) { Undo.RegisterCreatedObjectUndo(zoneGo, "Zona"); Undo.SetTransformParent(zoneGo.transform, pirateShip, "Zona"); }
        zoneGo.transform.SetPositionAndRotation(ship.center, Quaternion.identity);
        var zbox = zoneGo.GetComponent<BoxCollider>();
        if (zbox == null) zbox = Undo.AddComponent<BoxCollider>(zoneGo);
        zbox.isTrigger = true;
        Vector3 ls = zoneGo.transform.lossyScale;
        Vector3 size = ship.size + new Vector3(12f, 4f, 12f);   // 6 m alrededor del barco
        zbox.size = new Vector3(size.x / Mathf.Max(ls.x, 0.0001f), size.y / Mathf.Max(ls.y, 0.0001f), size.z / Mathf.Max(ls.z, 0.0001f));
        zbox.center = Vector3.zero;
        var zone = zoneGo.GetComponent<ObjectiveZone>();
        if (zone == null) zone = Undo.AddComponent<ObjectiveZone>(zoneGo);
        zone.watch = boat.transform;
        zone.objectiveId = "barco";
        zone.requirePlayerOnBoard = true;
        zone.xrOrigin = origin.transform;

        // 3) Objetivos: llave -> barco -> cofre
        var gm = FindOrCreateGM();
        Undo.RecordObject(gm, "Objetivos");
        gm.objectives.RemoveAll(o => o.id == "isla");
        var llave = gm.objectives.Find(o => o.id == "llave");
        var cofre = gm.objectives.Find(o => o.id == "cofre");
        var barco = gm.objectives.Find(o => o.id == "barco") ?? new GameManager.Objective { id = "barco" };
        barco.description = "Rema hasta el barco fantasma";
        barco.required = 1;
        if (cofre != null) cofre.description = "Abre el cofre bajo la luna";
        var otear = gm.objectives.Find(o => o.id == "otear");
        var rest = gm.objectives.Where(o => o != otear && o != llave && o != barco && o != cofre).ToList();
        gm.objectives.Clear();
        if (otear != null) gm.objectives.Add(otear);
        if (llave != null) gm.objectives.Add(llave);
        gm.objectives.Add(barco);
        if (cofre != null) gm.objectives.Add(cofre);
        gm.objectives.AddRange(rest);

        Selection.activeObject = anchorGo;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pirateShip.gameObject.scene);
        Debug.Log($"[BARCO PIRATA] Listo: ancla '{anchorGo.name}' en la cubierta ({(hit.collider != null ? "sobre '" + hit.collider.name + "'" : "altura sin comprobar")}), " +
                  $"zona 'ZonaBarcoPirata' ({size.x:F0} x {size.z:F0} m) y objetivos: {string.Join(" -> ", gm.objectives.Select(o => o.id))}. " +
                  "Ahora: Isla Pirata > Barca > 3. Arreglar teletransporte + Diagnostico, Bake del NavMesh y guarda (Ctrl+S).");
    }

    // ------------------------------------------------------------------ 3
    void CreateUI()
    {
        var origin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (origin == null || origin.Camera == null) { Debug.LogError("[JUEGO] No hay XR Origin con camara."); return; }
        var cam = origin.Camera.transform;
        var gm = FindOrCreateGM();
        Undo.RecordObject(gm, "Interfaz");

        // Panel de objetivos en la muneca (o abajo a la izquierda de la vista si no hay mano)
        if (gm.objectivesText == null)
        {
            Transform parent = leftHand != null ? leftHand : cam;
            var canvas = MakeCanvas("Panel Objetivos", parent, new Vector2(320, 200));
            if (leftHand != null)
            {
                canvas.transform.localPosition = new Vector3(0f, 0.06f, -0.06f);
                canvas.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
                canvas.transform.localScale = Vector3.one * 0.0006f;   // ~19 x 12 cm en la muneca
            }
            else
            {
                canvas.transform.localPosition = new Vector3(-0.35f, -0.25f, 0.8f);
                canvas.transform.localRotation = Quaternion.identity;
            }
            gm.objectivesText = MakeText(canvas.transform, "Objetivos", 22, TextAlignmentOptions.TopLeft);
            if (leftHand == null) Debug.LogWarning("[JUEGO] No encontre la mano izquierda: el panel va pegado a la vista. Mejor arrastra 'Left Controller' y repite.");
        }

        // Pantalla de fin delante de los ojos (desactivada al empezar)
        if (gm.endPanel == null)
        {
            var end = MakeCanvas("Pantalla Fin", cam, new Vector2(800, 400));
            end.transform.localPosition = new Vector3(0f, 0f, 1.5f);
            end.transform.localRotation = Quaternion.identity;
            gm.endText = MakeText(end.transform, "¡Fin!", 56, TextAlignmentOptions.Center);
            gm.endPanel = end.gameObject;
            end.gameObject.SetActive(false);
        }

        // Velo rojo de dano
        var feedback = origin.GetComponent<PlayerDamageFeedback>();
        if (feedback == null)
        {
            SetupToolUtils.TryGetPlayer(out _, out _);
            feedback = origin.GetComponent<PlayerDamageFeedback>();
        }
        if (feedback != null && feedback.overlay == null)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Undo.RegisterCreatedObjectUndo(quad, "Velo");
            quad.name = "Velo Dano";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(cam, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            quad.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            var r = quad.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = OverlayMaterial();
            Undo.RecordObject(feedback, "Velo");
            feedback.overlay = r;
        }

        Selection.activeObject = gm.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
        Debug.Log("[JUEGO] Interfaz creada: 'Panel Objetivos' (muneca), 'Pantalla Fin' (delante de la camara, oculta) y 'Velo Dano'. Guarda (Ctrl+S).");
    }

    static Canvas MakeCanvas(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * 0.001f;   // 1 pixel = 1 mm
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = size;

        var bg = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var rt = (RectTransform)bg.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        return canvas;
    }

    static TMP_Text MakeText(Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(16, 12); rt.offsetMax = new Vector2(-16, -12);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        return t;
    }

    static Material OverlayMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(OverlayMatPath);
        if (mat != null) return mat;
        SetupToolUtils.EnsurePrefabFolder();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        // URP Unlit transparente
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent + 100;
        mat.SetColor("_BaseColor", new Color(0.8f, 0f, 0f, 0f));
        AssetDatabase.CreateAsset(mat, OverlayMatPath);
        return mat;
    }
}
