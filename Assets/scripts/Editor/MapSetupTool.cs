using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// Herramienta de editor: menu "Isla Pirata > Carta".
///  1) Foto cenital de la isla -> Assets/Prefabs/MapaIsla.png (tono sepia, de pergamino).
///  2) Crea la carta en la escena (y Assets/Prefabs/Carta.prefab): pergamino + 2 varillas para cogerla,
///     Canvas con la imagen, "estas aqui", lugares, objetivos y avisos. Escalado a dos manos.
///  3) Lugares (MapMarker): Muelle (descubierto), Mirador (telescopio), Palmeral, Barco fantasma (sin viaje rapido).
///  4) EventSystem + XRUIInputModule para poder pulsar la carta con el rayo.
public class MapSetupTool : EditorWindow
{
    const string ImagePath = SetupToolUtils.PrefabFolder + "/MapaIsla.png";
    const string PrefabPath = SetupToolUtils.PrefabFolder + "/Carta.prefab";
    const string SoundFolder = "Assets/AS_Casual_Island_Game_Sounds_FREE";

    int resolution = 1024;
    float sepia = 0.55f;
    float margin = 0f;

    [MenuItem("Isla Pirata/Carta")]
    static void Open() => GetWindow<MapSetupTool>("Carta");

    void OnGUI()
    {
        EditorGUILayout.LabelField("1. Imagen de la isla", EditorStyles.boldLabel);
        resolution = EditorGUILayout.IntPopup("Resolucion", resolution, new[] { "512", "1024", "2048" }, new[] { 512, 1024, 2048 });
        sepia = EditorGUILayout.Slider("Tono pergamino", sepia, 0f, 1f);
        margin = EditorGUILayout.FloatField(new GUIContent("Recorte del borde (m)", "Quita mar de los bordes del terreno"), margin);
        if (GUILayout.Button("Hacer foto de la isla")) RenderMap();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Carta y lugares", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Crea la carta flotando delante del XR Origin, los lugares (Muelle, Mirador, Palmeral, Barco fantasma) y el EventSystem para pulsarla con el rayo. " +
                                "Haz antes la foto, el telescopio y las palmeras.", MessageType.None);
        using (new EditorGUI.DisabledScope(AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath) == null))
            if (GUILayout.Button("Crear / actualizar carta")) CreateMap();
    }

    // ------------------------------------------------------------------ 1. foto
    bool Area(out Vector2 min, out Vector2 max, out float topY)
    {
        min = max = Vector2.zero;
        topY = 50f;
        var t = Terrain.activeTerrain;   // DEMO: terreno de la isla
        if (t == null) { Debug.LogError("[CARTA] No hay Terrain en la escena."); return false; }
        Vector3 p = t.transform.position, s = t.terrainData.size;
        min = new Vector2(p.x + margin, p.z + margin);
        max = new Vector2(p.x + s.x - margin, p.z + s.z - margin);
        topY = p.y + s.y + 50f;
        return true;
    }

    void RenderMap()
    {
        if (!Area(out Vector2 min, out Vector2 max, out float topY)) return;
        SetupToolUtils.EnsurePrefabFolder();
        Vector2 size = max - min;
        int w = resolution, h = Mathf.Max(64, Mathf.RoundToInt(resolution * size.y / Mathf.Max(size.x, 0.01f)));

        var camGo = new GameObject("CamaraCarta (temporal)");
        var rt = new RenderTexture(w, h, 24);
        try
        {
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size.y * 0.5f;
            cam.aspect = size.x / size.y;
            camGo.transform.position = new Vector3((min.x + max.x) * 0.5f, topY, (min.y + max.y) * 0.5f);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // mira hacia abajo; arriba de la imagen = +Z
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = topY + 500f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.7f, 0.75f);
            int hide = 0;
            foreach (var n in new[] { "Player", "UI", "TeleportAnchor" }) { int l = LayerMask.NameToLayer(n); if (l >= 0) hide |= 1 << l; }
            cam.cullingMask = ~hide;
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;

            // Tono de pergamino viejo
            var px = tex.GetPixels();
            Color paper = new Color(0.86f, 0.74f, 0.52f);
            float avg = 0f;
            for (int i = 0; i < px.Length; i++)
            {
                float g = px[i].grayscale;
                avg += g;
                Color sep = Color.Lerp(new Color(0.35f, 0.22f, 0.1f), paper, g);
                px[i] = Color.Lerp(px[i], sep, sepia);
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(ImagePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ImagePath);
            Debug.Log($"[CARTA] Foto guardada en {ImagePath} ({w}x{h}), zona X {min.x:F0}..{max.x:F0}, Z {min.y:F0}..{max.y:F0}." +
                      (avg / px.Length < 0.02f ? "  <-- PARECE NEGRA: revisa que haya luz en la escena o repite la foto." : ""));
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath));
        }
        finally
        {
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

    // ------------------------------------------------------------------ 2. carta
    void CreateMap()
    {
        if (!Area(out Vector2 min, out Vector2 max, out _)) return;
        var origin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (origin == null || origin.Camera == null) { Debug.LogError("[CARTA] No hay XR Origin con camara."); return; }
        var image = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);

        EnsureEventSystem();

        var old = FindAnyObjectByType<TreasureMap>(FindObjectsInactive.Include);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject("Carta");
        Undo.RegisterCreatedObjectUndo(root, "Carta");

        // Pergamino y varillas (las varillas son lo que se agarra)
        var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
        paper.name = "Pergamino";
        Object.DestroyImmediate(paper.GetComponent<Collider>());
        paper.transform.SetParent(root.transform, false);
        paper.transform.localScale = new Vector3(0.5f, 0.64f, 1f);
        paper.GetComponent<Renderer>().sharedMaterial = Mat("Carta_Pergamino", new Color(0.9f, 0.8f, 0.6f));
        foreach (float x in new[] { -0.27f, 0.27f })
        {
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);   // trae CapsuleCollider: es el asa
            rod.name = x < 0 ? "Varilla Izquierda" : "Varilla Derecha";
            rod.transform.SetParent(root.transform, false);
            rod.transform.localPosition = new Vector3(x, 0f, 0f);
            rod.transform.localScale = new Vector3(0.035f, 0.35f, 0.035f);
            rod.GetComponent<Renderer>().sharedMaterial = Mat("Carta_Madera", new Color(0.35f, 0.2f, 0.1f));
        }

        // Fisica y agarre (dos manos = escalar)
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        var grab = root.AddComponent<XRGrabInteractable>();
        grab.selectMode = InteractableSelectMode.Multiple;
        grab.useDynamicAttach = true;
        grab.throwOnDetach = false;
        var tr = root.AddComponent<XRGeneralGrabTransformer>();
        tr.allowTwoHandedScaling = true;
        tr.minimumScaleRatio = 0.6f;
        tr.maximumScaleRatio = 2.5f;
        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        // Canvas (480 x 620 mm) delante del pergamino
        var cgo = new GameObject("Interfaz", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
        cgo.transform.SetParent(root.transform, false);
        cgo.transform.localPosition = new Vector3(0f, 0f, -0.003f);
        cgo.transform.localScale = Vector3.one * 0.001f;
        cgo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var crt = (RectTransform)cgo.transform;
        crt.sizeDelta = new Vector2(480, 620);

        Text(cgo.transform, "Titulo", "Carta del capitan Calavera", 24, new Vector2(0, 285), new Vector2(460, 40), TextAlignmentOptions.Center, FontStyles.Bold);

        var mapGo = new GameObject("Mapa", typeof(RectTransform), typeof(RawImage));
        mapGo.transform.SetParent(cgo.transform, false);
        var mapRt = (RectTransform)mapGo.transform;
        float aspect = (max.x - min.x) / Mathf.Max(max.y - min.y, 0.01f);
        mapRt.sizeDelta = aspect >= 1f ? new Vector2(440, 440 / aspect) : new Vector2(440 * aspect, 440);
        mapRt.anchoredPosition = new Vector2(0, 45);
        mapGo.GetComponent<RawImage>().texture = image;
        mapGo.GetComponent<RawImage>().raycastTarget = false;

        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // "Estas aqui"
        var player = new GameObject("Estas aqui", typeof(RectTransform), typeof(Image));
        player.transform.SetParent(mapRt, false);
        var prt = (RectTransform)player.transform;
        prt.sizeDelta = new Vector2(16, 16);
        var pimg = player.GetComponent<Image>();
        pimg.sprite = knob; pimg.color = new Color(0.8f, 0.05f, 0.05f); pimg.raycastTarget = false;
        var arrow = new GameObject("Flecha", typeof(RectTransform), typeof(Image));
        arrow.transform.SetParent(prt, false);
        ((RectTransform)arrow.transform).sizeDelta = new Vector2(5, 14);
        ((RectTransform)arrow.transform).anchoredPosition = new Vector2(0, 12);
        arrow.GetComponent<Image>().color = new Color(0.8f, 0.05f, 0.05f);
        arrow.GetComponent<Image>().raycastTarget = false;

        // Plantilla de lugar: boton redondo + nombre
        var tmpl = new GameObject("Plantilla Lugar", typeof(RectTransform), typeof(Image), typeof(Button));
        tmpl.transform.SetParent(mapRt, false);
        ((RectTransform)tmpl.transform).sizeDelta = new Vector2(26, 26);
        var timg = tmpl.GetComponent<Image>();
        timg.sprite = knob;
        var btn = tmpl.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 1f, 0.6f);
        colors.pressedColor = new Color(1f, 0.85f, 0.3f);
        colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        btn.colors = colors;
        var label = Text(tmpl.transform, "Nombre", "Lugar", 14, new Vector2(92, 0), new Vector2(160, 50), TextAlignmentOptions.Left, FontStyles.Bold);
        label.color = new Color(0.25f, 0.12f, 0.05f);
        label.raycastTarget = false;
        tmpl.SetActive(false);

        var status = Text(cgo.transform, "Avisos", "", 15, new Vector2(0, -190), new Vector2(460, 26), TextAlignmentOptions.Center, FontStyles.Italic);
        var objectives = Text(cgo.transform, "Objetivos", "Objetivos", 15, new Vector2(0, -255), new Vector2(460, 100), TextAlignmentOptions.TopLeft, FontStyles.Normal);
        foreach (var t in new[] { status, objectives }) { t.color = new Color(0.25f, 0.12f, 0.05f); t.raycastTarget = false; }

        // Componente
        var map = root.AddComponent<TreasureMap>();
        map.worldMin = min;
        map.worldMax = max;
        map.mapArea = mapRt;
        map.playerIcon = prt;
        map.markerTemplate = btn;
        map.statusText = status;
        map.head = origin.Camera.transform;
        map.xrOrigin = origin.transform;
        map.teleportation = FindAnyObjectByType<TeleportationProvider>(FindObjectsInactive.Include);
        map.boat = FindAnyObjectByType<BoatRowing>(FindObjectsInactive.Include);
        map.audioSource = audio;
        map.discoverClip = Sound("Jingle_5");
        map.travelClip = Sound("SFX_3");

        // Delante del jugador al empezar (en el muelle)
        Vector3 f = Vector3.ProjectOnPlane(origin.transform.forward, Vector3.up).normalized;
        if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
        root.transform.position = origin.transform.position + f * 0.6f + Vector3.up * 1.1f;
        root.transform.rotation = Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(30f, 0f, 0f);

        // El GameManager escribe los objetivos en la carta (en vez de en el panel de la muneca)
        var gm = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm != null)
        {
            if (gm.objectivesText != null && gm.objectivesText.canvas != null && gm.objectivesText.canvas.name == "Panel Objetivos")
            {
                Undo.RecordObject(gm.objectivesText.canvas.gameObject, "Panel");
                gm.objectivesText.canvas.gameObject.SetActive(false);
                Debug.Log("[CARTA] Desactivado el 'Panel Objetivos' de la muneca: ahora los objetivos salen en la carta.");
            }
            Undo.RecordObject(gm, "Carta");
            gm.objectivesText = objectives;
        }
        else Debug.LogWarning("[CARTA] No hay GameManager: los objetivos no se veran en la carta (Isla Pirata > Juego).");

        CreateMarkers(origin);

        SetupToolUtils.EnsurePrefabFolder();
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        Selection.activeObject = root;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[CARTA] Carta creada delante del XR Origin y guardada en " + PrefabPath + ". Revisa los lugares en 'Lugares (carta)'. Guarda (Ctrl+S).");
    }

    static TMP_Text Text(Transform parent, string name, string text, float size, Vector2 pos, Vector2 box, TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.fontStyle = style;
        t.color = new Color(0.2f, 0.1f, 0.05f);
        return t;
    }

    static Material Mat(string name, Color c)
    {
        string path = $"{SetupToolUtils.PrefabFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static AudioClip Sound(string name) =>
        AssetDatabase.FindAssets(name + " t:AudioClip", new[] { SoundFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(c => c != null && c.name == name);

    static void EnsureEventSystem()
    {
        var es = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
            Debug.Log("[CARTA] Creado EventSystem con XR UI Input Module (para pulsar botones 3D con el rayo).");
            return;
        }
        if (es.GetComponent<XRUIInputModule>() == null)
        {
            foreach (var m in es.GetComponents<BaseInputModule>()) Undo.DestroyObjectImmediate(m);
            Undo.AddComponent<XRUIInputModule>(es.gameObject);
            Debug.Log("[CARTA] El EventSystem ahora usa XR UI Input Module.");
        }
    }

    // ------------------------------------------------------------------ 3. lugares
    static void CreateMarkers(XROrigin origin)
    {
        foreach (var oldGroup in FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t != null && t.name == "Lugares (carta)").ToArray())
            Undo.DestroyObjectImmediate(oldGroup.gameObject);
        var group = new GameObject("Lugares (carta)");
        Undo.RegisterCreatedObjectUndo(group, "Lugares");

        var boat = FindAnyObjectByType<BoatRowing>(FindObjectsInactive.Include);
        var ship = FindObjectsByType<Transform>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name.StartsWith("StylShip_Body"));
        Transform shipRoot = ship != null ? ship.root : null;

        // Muelle: el ancla de teletransporte que no esta ni en la barca ni en el barco fantasma
        var dock = FindObjectsByType<TeleportationAnchor>(FindObjectsInactive.Include)
            .FirstOrDefault(a => (boat == null || !a.transform.IsChildOf(boat.transform)) && (shipRoot == null || !a.transform.IsChildOf(shipRoot)));
        if (dock != null)
        {
            var t = dock.teleportAnchorTransform != null ? dock.teleportAnchorTransform : dock.transform;
            var m = Marker(group, "Muelle", t.position, new Color(0.45f, 0.25f, 0.1f), true, true, "", "");
            m.travelPoint = t;
        }
        else Debug.LogWarning("[CARTA] No encuentro el ancla del muelle.");

        var tele = FindAnyObjectByType<Telescope>(FindObjectsInactive.Include);
        if (tele != null)
        {
            var m = Marker(group, "Mirador", tele.transform.position, new Color(0.2f, 0.35f, 0.6f), false, true, "Aqui esta el catalejo", "");
            m.travelPoint = TravelPoint(m.transform, tele.transform.position - tele.transform.forward * 1f);
        }

        var palms = FindObjectsByType<ShakeablePalm>(FindObjectsInactive.Include);
        if (palms.Length > 0)
        {
            Vector3 c = palms.Aggregate(Vector3.zero, (s, p) => s + p.transform.position) / palms.Length;
            var m = Marker(group, "Palmeral", c, new Color(0.15f, 0.45f, 0.15f), false, true, "Escondi la llave en una de estas palmeras", "");
            m.travelPoint = TravelPoint(m.transform, c + Vector3.right * 2f);
        }

        if (shipRoot != null)
        {
            var gm = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            string reveal = gm != null && gm.objectives.Any(o => o.id == "otear") ? "otear" : "";
            var m = Marker(group, "Barco fantasma", SetupToolUtils.RenderBounds(shipRoot.gameObject).center, new Color(0.5f, 0.05f, 0.05f), false, false,
                           "Solo se llega remando", reveal);
            m.discoverRadius = 15f;
        }
        Debug.Log($"[CARTA] Lugares creados: {string.Join(", ", group.GetComponentsInChildren<MapMarker>().Select(x => x.label))}. " +
                  "Puedes anadir mas: objeto vacio + MapMarker.");
    }

    static MapMarker Marker(GameObject group, string label, Vector3 pos, Color color, bool discovered, bool travel, string hint, string hiddenUntil)
    {
        var go = new GameObject("Lugar " + label);
        go.transform.SetParent(group.transform, false);
        go.transform.position = pos;
        var m = go.AddComponent<MapMarker>();
        m.label = label;
        m.color = color;
        m.discoveredAtStart = discovered;
        m.allowFastTravel = travel;
        m.hint = hint;
        m.hiddenUntilObjective = hiddenUntil;
        return m;
    }

    // Punto de llegada del viaje rapido, sobre el suelo
    static Transform TravelPoint(Transform parent, Vector3 near)
    {
        int mask = ~0;
        foreach (var n in new[] { "Player", "TeleportAnchor", "Boat" }) { int l = LayerMask.NameToLayer(n); if (l >= 0) mask &= ~(1 << l); }
        Physics.SyncTransforms();
        if (Physics.Raycast(near + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 60f, mask, QueryTriggerInteraction.Ignore))
            near = hit.point;
        else Debug.LogWarning($"[CARTA] No hay suelo bajo el punto de llegada de '{parent.name}': ajustalo a mano.");
        var t = new GameObject("Punto de llegada").transform;
        t.SetParent(parent, false);
        t.position = near;
        return t;
    }
}
