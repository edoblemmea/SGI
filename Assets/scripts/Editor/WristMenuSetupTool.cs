using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// Herramienta de editor: menu "Isla Pirata > Menu muneca".
///  1) Vineta de confort: pone el prefab TunnelingVignette de los Starter Assets bajo Main Camera,
///     conectado a Move, giro suave y teletransporte.
///  2) Menu de la muneca: Canvas World Space en el Left Controller con WristMenu y sus botones.
///  3) EventSystem con XRUIInputModule (para pulsar con el rayo), si no lo hay.
public class WristMenuSetupTool : EditorWindow
{
    Vector3 localPos = new Vector3(0f, 0.09f, 0.06f);
    Vector3 localEuler = new Vector3(35f, 0f, 0f);
    float scale = 0.0007f;
    bool allowTimeChange = true;

    [MenuItem("Isla Pirata/Menu muneca")]
    static void Open() => GetWindow<WristMenuSetupTool>("Menu muneca");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Crea el menu de opciones en el mando izquierdo (boton Y = abrir/cerrar): movimiento teletransporte/continuo, " +
                                "giro por saltos/suave, grados, velocidad, vineta, hora, volumen y reiniciar. Se pulsa con el rayo del mando derecho.", MessageType.None);
        localPos = EditorGUILayout.Vector3Field("Posicion sobre el mando", localPos);
        localEuler = EditorGUILayout.Vector3Field("Inclinacion", localEuler);
        scale = EditorGUILayout.FloatField(new GUIContent("Escala", "0.0007 = el panel mide unos 22 x 33 cm"), scale);
        allowTimeChange = EditorGUILayout.Toggle(new GUIContent("Permitir cambiar la hora", "Si no, la hora solo cambia con el reloj de arena"), allowTimeChange);
        EditorGUILayout.Space();
        if (GUILayout.Button("Crear / actualizar menu de la muneca")) Create();
    }

    void Create()
    {
        var origin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (origin == null || origin.Camera == null) { Debug.LogError("[MENU] No hay XR Origin con camara en la escena."); return; }

        EnsureEventSystem();

        var managers = origin.GetComponentsInChildren<ControllerInputActionManager>(true);
        var left = managers.FirstOrDefault(m => m.name.ToLower().Contains("left"));
        var right = managers.FirstOrDefault(m => m.name.ToLower().Contains("right"));
        if (left == null) Debug.LogWarning($"[MENU] No encuentro el Controller Input Action Manager del mando izquierdo (hay {managers.Length}).");

        var move = origin.GetComponentInChildren<ContinuousMoveProvider>(true);
        var snap = origin.GetComponentInChildren<SnapTurnProvider>(true);
        var smooth = origin.GetComponentInChildren<ContinuousTurnProvider>(true);
        var teleport = origin.GetComponentInChildren<TeleportationProvider>(true);
        var vignette = EnsureVignette(origin, move, smooth, teleport);

        style = StyleSetupTool.Ensure();
        if (style == null || style.chartFrame == null || style.pill == null || style.row == null || style.medallion == null ||
            style.Segment(0, 2, false) == null || style.Segment(0, 2, true) == null)
        {
            Debug.LogError("[MENU] Faltan las texturas de la carta nautica: pulsa antes Isla Pirata > Estilo pirata. No he tocado el menu.");
            return;
        }

        // Menu anterior fuera
        foreach (var old in FindObjectsByType<WristMenu>(FindObjectsInactive.Include)) Undo.DestroyObjectImmediate(old.gameObject);

        Transform parent = left != null ? left.transform : origin.Camera.transform.parent;
        var root = new GameObject("Menu Muneca");
        Undo.RegisterCreatedObjectUndo(root, "Menu muneca");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = left != null ? localPos : new Vector3(-0.25f, 1.1f, 0.4f);
        root.transform.localRotation = Quaternion.Euler(localEuler);


        // Carta nautica, como el diseno E de la plantilla. Canvas 400 x 500 (a escala 0.0007: 28 x 35 cm); filas de 60 px = 4,2 cm
        var cgo = new GameObject("Panel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster), typeof(Image));
        cgo.transform.SetParent(root.transform, false);
        cgo.transform.localScale = Vector3.one * scale;
        cgo.layer = LayerMask.NameToLayer("UI");
        cgo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)cgo.transform).sizeDelta = new Vector2(400, 500);
        var bg = cgo.GetComponent<Image>();
        bg.sprite = style.chartFrame;
        bg.type = Image.Type.Sliced;
        var lines = Img(cgo.transform, "Carta (lineas)", Vector2.zero, Vector2.zero, style.chartLines, Color.white, false);
        Stretch(lines.rectTransform);

        var title = Text(cgo.transform, "Titulo", "Opciones", 40, new Vector2(0, 208), new Vector2(260, 52), FontStyles.Normal);
        title.color = Gold;
        PirateStyle.ApplyFont(title, PirateStyle.FontRole.Title);

        var menu = root.AddComponent<WristMenu>();
        menu.panel = cgo;
        menu.chartLayout = true;
        menu.leftHandManager = left;
        menu.rightHandManager = right;
        menu.moveProvider = move;
        menu.snapTurn = snap;
        menu.smoothTurn = smooth;
        menu.vignette = vignette;
        menu.dayNight = FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
        menu.allowTimeChange = allowTimeChange;

        // Cerrar: medallon de trazos arriba a la derecha
        var closeRt = Rect(cgo.transform, "Boton Cerrar", new Vector2(164, 208), new Vector2(34, 34), typeof(Image), typeof(Button));
        var closeImg = closeRt.GetComponent<Image>();
        closeImg.sprite = style.medallion;
        closeImg.color = Gold;
        menu.closeButton = closeRt.GetComponent<Button>();
        SetColors(menu.closeButton, new Color(1f, 1f, 1f, 0.8f), Color.white, new Color(1f, 0.85f, 0.5f));
        Img(closeRt, "Icono", Vector2.zero, new Vector2(13, 13), style.iconClose, Gold, false);

        // Pestanas: pildoras con icono y nombre
        string[] tabNames = { "Movimiento", "Confort", "Partida" };
        Sprite[] tabIcons = { style.iconMove, style.iconVignette, style.iconTime };
        menu.tabButtons = new Button[tabNames.Length];
        menu.tabPages = new GameObject[tabNames.Length];
        for (int i = 0; i < tabNames.Length; i++)
        {
            menu.tabButtons[i] = ChartTab(cgo.transform, tabNames[i], new Vector2(-120 + i * 120, 150), tabIcons[i]);
            var page = Rect(cgo.transform, "Pagina " + tabNames[i], Vector2.zero, new Vector2(400, 500));
            menu.tabPages[i] = page.gameObject;
        }
        Transform p0 = menu.tabPages[0].transform, p1 = menu.tabPages[1].transform, p2 = menu.tabPages[2].transform;
        float[] rowY = { 88f, 20f, -48f, -116f };

        // Movimiento
        menu.moveButton = ChartRow(p0, "Movimiento", rowY[0], style.iconMove, 2);
        menu.turnButton = ChartRow(p0, "Giro", rowY[1], style.iconTurn, 2);
        menu.snapButton = ChartRow(p0, "Salto de giro", rowY[2], style.iconTurn, Mathf.Max(1, menu.snapAngles.Length));
        menu.speedButton = ChartRow(p0, "Velocidad", rowY[3], style.iconSpeed, Mathf.Max(1, menu.moveSpeeds.Length));

        // Confort: vineta y volumen (-, valor, +)
        menu.vignetteButton = ChartRow(p1, "Viñeta de confort", rowY[0], style.iconVignette, 2);
        var volRow = Rect(p1, "Opcion Volumen", new Vector2(0, rowY[1]), new Vector2(352, 60));
        var volBox = RowContent(volRow, "Volumen", style.iconVolume, 0);
        menu.volumeDownButton = SmallSegment(volBox, 0, "–");
        var mid = Segment(volBox, 1, 3, "100 %");
        menu.volumeText = mid.GetComponentInChildren<TMP_Text>();
        menu.volumeText.color = ChartText;
        menu.volumeUpButton = SmallSegment(volBox, 2, "+");

        // Partida
        menu.timeButton = ChartRow(p2, "Hora", rowY[0], style.iconTime, 2);
        menu.restartButton = ChartRow(p2, "Partida", rowY[1], style.iconRestart, 1);

        var help = Text(cgo.transform, "Ayuda", "Y abre y cierra · apunta con el rayo derecho", 13, new Vector2(0, -222), new Vector2(360, 22), FontStyles.Italic);
        help.color = new Color(ChartText.r, ChartText.g, ChartText.b, 0.6f);

        menu.tabNormal = new Color(Gold.r, Gold.g, Gold.b, 0.3f);
        menu.tabActive = Gold;
        cgo.SetActive(false);   // empieza cerrado (se abre con Y)

        Selection.activeObject = root;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[MENU] Menu creado en '{parent.name}'. Managers izq {(left != null ? left.name : "FALTA")} der {(right != null ? right.name : "FALTA")}, " +
                  $"move {(move != null ? "ok" : "FALTA")}, snap {(snap != null ? "ok" : "FALTA")}, giro suave {(smooth != null ? "ok" : "FALTA")}, " +
                  $"vineta {(vignette != null ? "ok" : "FALTA")}, dia/noche {(menu.dayNight != null ? "ok" : "FALTA (Isla Pirata > Maldicion)")}. Guarda (Ctrl+S).");
    }

    // ------------------------------------------------------------------ vineta
    static TunnelingVignetteController EnsureVignette(XROrigin origin, LocomotionProvider move, LocomotionProvider smooth, LocomotionProvider teleport)
    {
        var v = origin.GetComponentInChildren<TunnelingVignetteController>(true);
        if (v == null)
        {
            var path = AssetDatabase.FindAssets("TunnelingVignette t:Prefab").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            var prefab = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            if (prefab == null) { Debug.LogWarning("[MENU] No encuentro el prefab TunnelingVignette de los Starter Assets: el menu no tendra vineta."); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, origin.Camera.transform);
            Undo.RegisterCreatedObjectUndo(go, "Vineta");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            v = go.GetComponent<TunnelingVignetteController>();
            Debug.Log("[MENU] Anadida la vineta de confort (" + path + ") bajo Main Camera.");
        }
        Undo.RecordObject(v, "Vineta");
        var list = new List<LocomotionVignetteProvider>();
        foreach (var p in new[] { move, smooth, teleport })
            if (p != null) list.Add(new LocomotionVignetteProvider { locomotionProvider = p, enabled = true });
        v.locomotionVignetteProviders = list;
        EditorUtility.SetDirty(v);
        return v;
    }

    // ------------------------------------------------------------------ UI
    static PirateStyle style;

    static Sprite Icon(System.Func<PirateStyle, Sprite> pick) => style != null ? pick(style) : null;

    static Image IconImage(Transform parent, Sprite sprite, Vector2 pos, float size, Color color)
    {
        var go = new GameObject("Icono", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Boton de 64 px de alto con icono a la izquierda (si lo hay) y texto de dos lineas (nombre / valor)
    static Button Btn(Transform parent, string name, Vector2 pos, float width, string label = "...", Sprite icon = null)
    {
        var go = new GameObject("Boton " + name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, 64);
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = style != null && style.button != null ? style.button : AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = style != null && style.button != null ? Color.white : new Color(0.86f, 0.74f, 0.52f);
        var b = go.GetComponent<Button>();
        var c = b.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(1f, 0.93f, 0.6f);
        c.pressedColor = new Color(1f, 0.75f, 0.35f);
        c.selectedColor = Color.white;
        c.disabledColor = new Color(0.55f, 0.5f, 0.45f, 0.6f);
        c.fadeDuration = 0.08f;
        b.colors = c;
        // Botones de madera: texto claro e iconos dorados
        var fg = style != null ? style.paperText : new Color(0.2f, 0.1f, 0.05f);
        float left = 0f;
        if (icon != null)
        {
            IconImage(go.transform, icon, new Vector2(-width / 2f + 36f, 0f), 40f, style != null ? style.gold : fg);
            left = 56f;
        }
        var t = Text(go.transform, "Texto", label, width < 100f ? 34 : 21, new Vector2(left / 2f, 0f), new Vector2(width - 16 - left, 60), FontStyles.Normal);
        t.color = fg;
        t.lineSpacing = -12f;
        t.enableAutoSizing = true;   // que "Movimiento" quepa en la pestana
        t.fontSizeMin = 13f;
        t.fontSizeMax = t.fontSize;
        t.raycastTarget = false;
        return b;
    }

    static TMP_Text Text(Transform parent, string name, string text, float size, Vector2 pos, Vector2 box, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        var t = go.AddComponent<TextMeshProUGUI>();
        if (style != null) PirateStyle.ApplyFont(t, PirateStyle.FontRole.Body);
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = style;
        t.color = new Color(0.95f, 0.88f, 0.7f);
        t.raycastTarget = false;
        return t;
    }

    // ------------------------------------------------------------------ carta nautica (diseno E de la plantilla)
    static readonly Color ChartText = new Color(0.80f, 0.84f, 0.89f);
    static Color Gold => style != null ? style.gold : new Color(1f, 0.78f, 0.32f);

    static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size, params System.Type[] components)
    {
        var go = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray());
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Image Img(Transform parent, string name, Vector2 pos, Vector2 size, Sprite sprite, Color color, bool sliced)
    {
        var img = Rect(parent, name, pos, size, typeof(Image)).GetComponent<Image>();
        img.sprite = sprite;
        img.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void SetColors(Button b, Color normal, Color hover, Color pressed)
    {
        var c = b.colors;
        c.normalColor = normal;
        c.highlightedColor = hover;
        c.pressedColor = pressed;
        c.selectedColor = normal;
        c.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.4f);
        c.fadeDuration = 0.08f;
        b.colors = c;
    }

    // Pestana: pildora con icono y nombre (WristMenu.ShowTab le da el oro a la activa)
    static Button ChartTab(Transform parent, string label, Vector2 pos, Sprite icon)
    {
        var rt = Rect(parent, "Pestana " + label, pos, new Vector2(114, 36), typeof(Image), typeof(Button));
        var img = rt.GetComponent<Image>();
        img.sprite = style.pill;
        img.type = Image.Type.Sliced;
        var b = rt.GetComponent<Button>();
        SetColors(b, Color.white, new Color(1f, 0.93f, 0.7f), new Color(1f, 0.85f, 0.5f));
        var content = Rect(rt, "Contenido", Vector2.zero, Vector2.zero, typeof(HorizontalLayoutGroup));
        Stretch(content);
        var hl = content.GetComponent<HorizontalLayoutGroup>();
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.spacing = 5f;
        hl.childControlWidth = true;
        hl.childControlHeight = false;
        hl.childForceExpandWidth = hl.childForceExpandHeight = false;
        if (icon != null)
        {
            var ic = Img(content, "Icono", Vector2.zero, new Vector2(15, 15), icon, Gold, false);
            var le = ic.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 15f;
        }
        var t = Text(content, "Texto", label, 15, Vector2.zero, new Vector2(80, 30), FontStyles.Normal);
        PirateStyle.ApplyFont(t, PirateStyle.FontRole.Heading);
        t.color = Gold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return b;
    }

    // Fila de opcion: el boton entero cambia el valor; invisible hasta que pasa el rayo (raya y brillo dorados)
    static Button ChartRow(Transform parent, string name, float y, Sprite icon, int values)
    {
        var rt = Rect(parent, "Opcion " + name, new Vector2(0, y), new Vector2(352, 60), typeof(Image), typeof(Button));
        var img = rt.GetComponent<Image>();
        img.sprite = style.row;
        img.type = Image.Type.Sliced;
        var b = rt.GetComponent<Button>();
        SetColors(b, new Color(1f, 1f, 1f, 0f), new Color(1f, 0.85f, 0.45f, 0.9f), new Color(1f, 0.8f, 0.4f, 1f));
        RowContent(rt, name, icon, values);
        return b;
    }

    // Medallon con el icono a la izquierda, nombre arriba y selector de valores abajo (hijo "Valores")
    static RectTransform RowContent(RectTransform row, string name, Sprite icon, int values)
    {
        Img(row, "Medallon", new Vector2(-146, 0), new Vector2(40, 40), style.medallion, new Color(Gold.r, Gold.g, Gold.b, 0.75f), false);
        if (icon != null) Img(row, "Icono", new Vector2(-146, 0), new Vector2(22, 22), icon, Gold, false);
        var n = Text(row, "Nombre", name, 15, new Vector2(23, 14), new Vector2(286, 20), FontStyles.Normal);
        PirateStyle.ApplyFont(n, PirateStyle.FontRole.Heading);
        n.alignment = TextAlignmentOptions.Left;
        n.color = ChartText;
        var box = Rect(row, "Valores", new Vector2(23, -11), new Vector2(286, 28), typeof(HorizontalLayoutGroup));
        var hl = box.GetComponent<HorizontalLayoutGroup>();
        hl.spacing = 0f;
        hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = true;
        for (int i = 0; i < values; i++) Segment(box, i, values, "");
        return box;
    }

    // Pieza del selector (WristMenu.SetValues le pone el texto y la rellena de oro si es el valor elegido)
    static Image Segment(Transform box, int i, int n, string label)
    {
        var img = Img(box, "Valor " + i, Vector2.zero, new Vector2(60, 28), style.Segment(i, n, false), new Color(Gold.r, Gold.g, Gold.b, 0.35f), true);
        img.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var t = Text(img.transform, "Texto", label, 15, Vector2.zero, Vector2.zero, FontStyles.Normal);
        Stretch(t.rectTransform);
        t.color = new Color(ChartText.r, ChartText.g, ChartText.b, 0.6f);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMin = 10f;
        t.fontSizeMax = 15f;
        return img;
    }

    // Boton pequeno dentro del selector (- y + del volumen)
    static Button SmallSegment(Transform box, int i, string label)
    {
        var img = Segment(box, i, 3, label);
        img.color = Gold;
        img.raycastTarget = true;
        var le = img.GetComponent<LayoutElement>();
        le.flexibleWidth = 0f;
        le.preferredWidth = 56f;
        var t = img.GetComponentInChildren<TMP_Text>();
        t.color = Gold;
        t.fontStyle = FontStyles.Bold;
        var b = img.gameObject.AddComponent<Button>();
        SetColors(b, new Color(1f, 1f, 1f, 0.6f), Color.white, new Color(1f, 0.85f, 0.5f));
        return b;
    }

    static void EnsureEventSystem()
    {
        var es = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
            Debug.Log("[MENU] Creado EventSystem con XR UI Input Module.");
            return;
        }
        if (es.GetComponent<XRUIInputModule>() == null)
        {
            foreach (var m in es.GetComponents<BaseInputModule>()) Undo.DestroyObjectImmediate(m);
            Undo.AddComponent<XRUIInputModule>(es.gameObject);
            Debug.Log("[MENU] El EventSystem ahora usa XR UI Input Module.");
        }
    }
}
