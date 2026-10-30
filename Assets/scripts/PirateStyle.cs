using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Estilo comun de toda la interfaz (paneles de madera y oro o banda oscura): fuentes, sprites y colores.
/// El asset vive en Assets/Resources/EstiloPirata.asset y lo crea "Isla Pirata > Estilo pirata".
/// Los scripts que crean interfaz en tiempo real (historia, pistas, objetivos, titulo) lo leen con PirateStyle.Get().
/// Si el asset no existe se usan los valores por defecto (fuente de TMP y paneles de color liso).
/// Fuentes: titulos = Pirata One, encabezados = IM Fell English SC, texto = Libre Baskerville (las tres OFL, en Assets/UI/Fuentes).
[CreateAssetMenu(menuName = "Isla Pirata/Estilo pirata", fileName = "EstiloPirata")]
public class PirateStyle : ScriptableObject
{
    public const string ResourceName = "EstiloPirata";

    public enum PanelLook { Madera, Banda }
    public enum FontRole { Body, Heading, Title }

    [Header("Paneles")]
    [Tooltip("Aspecto de los avisos, pistas, subtitulos y paneles de objetivos/fin.\n" +
             "Madera = placa de madera oscura con marco dorado y rombos en las esquinas.\n" +
             "Banda = franja oscura que se difumina a los lados, con filetes dorados.\n" +
             "Se nota al darle a Play (el menu de la muneca siempre es de madera).")]
    public PanelLook panelLook = PanelLook.Madera;

    [Header("Fuentes")]
    [Tooltip("Titulos (Pirata One). Vacio = la de texto")]
    public TMP_FontAsset titleFont;
    [Tooltip("Encabezados pequenos: nombre del que habla, pestanas, botones del mando (IM Fell English SC). Vacio = la de texto")]
    public TMP_FontAsset headingFont;
    [Tooltip("Texto normal (Libre Baskerville). Vacio = la de TMP por defecto")]
    public TMP_FontAsset bodyFont;

    [Header("Sprites (9-slice)")]
    [Tooltip("Ya no lo usa ningun panel (ahora son la placa y la banda)")]
    public Sprite parchment;
    [Tooltip("Madera lisa (cartel del titulo de la intro)")]
    public Sprite darkPanel;
    [Tooltip("Placa de madera con marco dorado y rombos en las esquinas")]
    public Sprite plaque;
    [Tooltip("Banda oscura difuminada a los lados con filetes dorados")]
    public Sprite band;
    [Tooltip("Filete dorado con un rombo en el centro (separador)")]
    public Sprite rule;
    [Tooltip("Botones del menu de la muneca (madera; solo si no estan los de la carta nautica)")]
    public Sprite button;
    [Tooltip("Circulo para dibujar los botones del mando (B, X, grip...)")]
    public Sprite circle;

    [Header("Menu de la muneca (carta nautica)")]
    [Tooltip("Fondo azul noche con marco dorado doble (9-slice)")]
    public Sprite chartFrame;
    [Tooltip("Cuadricula, rumbos, rosa de los vientos y escala de grados (se estira sobre todo el panel)")]
    public Sprite chartLines;
    [Tooltip("Pildora con borde dorado: pestanas y botones pequenos (blanca: se tine)")]
    public Sprite pill;
    [Tooltip("Fondo de cada fila de opciones (blanco: el boton le da la transparencia)")]
    public Sprite row;
    [Tooltip("Circulo de trazos detras de cada icono (blanco: se tine)")]
    public Sprite medallion;
    [Tooltip("Selector de valores, solo el borde: 0 = izquierda, 1 = medio, 2 = derecha, 3 = solo (redondeado por los dos lados)")]
    public Sprite[] segment = new Sprite[4];
    [Tooltip("Lo mismo, relleno (el valor elegido)")]
    public Sprite[] segmentFill = new Sprite[4];
    [Tooltip("Texto azulado de la carta")]
    public Color chartText = new Color(0.80f, 0.84f, 0.89f);
    [Tooltip("Azul noche: letra del valor elegido (sobre oro)")]
    public Color chartNavy = new Color(0.04f, 0.08f, 0.14f);

    [Header("Iconos del menu")]
    public Sprite iconMove, iconTurn, iconSpeed, iconVignette, iconTime, iconVolume, iconRestart, iconClose;

    [Header("Colores")]
    public Color ink = new Color(0.24f, 0.13f, 0.05f);
    public Color inkSoft = new Color(0.42f, 0.30f, 0.18f);
    public Color paperText = new Color(1f, 0.92f, 0.74f);
    public Color gold = new Color(1f, 0.78f, 0.32f);
    public Color done = new Color(0.25f, 0.5f, 0.15f);
    [Tooltip("Texto secundario sobre los paneles oscuros")]
    public Color panelSoft = new Color(0.78f, 0.68f, 0.52f);
    [Tooltip("Objetivos cumplidos sobre los paneles oscuros")]
    public Color panelDone = new Color(0.62f, 0.86f, 0.45f);
    public Color danger = new Color(0.7f, 0.15f, 0.1f);
    [Tooltip("Fondo de los subtitulos de la historia")]
    public Color subtitleBand = new Color(0.04f, 0.02f, 0.01f, 0.72f);

    static PirateStyle cached;

    public static PirateStyle Get()
    {
        if (cached != null) return cached;
        cached = Resources.Load<PirateStyle>(ResourceName);
        if (cached == null)
        {
            cached = CreateInstance<PirateStyle>();
            Debug.LogWarning("[ESTILO] No existe Assets/Resources/EstiloPirata.asset: interfaz con el estilo por defecto (pulsa Isla Pirata > Estilo pirata).");
        }
        return cached;
    }

    /// Sprite del panel segun 'panelLook' (null si aun no se han generado las texturas)
    public Sprite PanelSprite => panelLook == PanelLook.Banda ? band : plaque;

    /// Margen extra a los lados del contenido: la banda se difumina en los bordes
    public float PanelSidePadding => panelLook == PanelLook.Banda ? 70f : 0f;

    /// Pieza del selector de valores para la posicion i de n (la primera y la ultima redondeadas)
    public Sprite Segment(int i, int n, bool filled)
    {
        var set = filled ? segmentFill : segment;
        if (set == null || set.Length < 4) return null;
        int k = n <= 1 ? 3 : i == 0 ? 0 : i == n - 1 ? 2 : 1;
        return set[k];
    }

    public TMP_FontAsset Font(FontRole role)
    {
        if (role == FontRole.Title && titleFont != null) return titleFont;
        if (role == FontRole.Heading && headingFont != null) return headingFont;
        return bodyFont;
    }

    // ------------------------------------------------------------------ constructores de interfaz (en tiempo real)

    /// Panel con el aspecto elegido (madera o banda). 'content' es el hueco para los textos (sin los bordes difuminados)
    public static RectTransform MakePanel(string name, Transform parent, Vector2 sizePx, float pixelsPerMeter, out RectTransform content)
    {
        var s = Get();
        var sprite = s.PanelSprite;
        WarnIfMissing(s);
        var rt = MakeCanvas(name, parent, sizePx, pixelsPerMeter, sprite, sprite != null ? Color.white : new Color(0.08f, 0.05f, 0.02f, 0.85f));
        var go = new GameObject("Contenido", typeof(RectTransform));
        content = (RectTransform)go.transform;
        content.SetParent(rt, false);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(s.PanelSidePadding, 0f);
        content.offsetMax = new Vector2(-s.PanelSidePadding, 0f);
        return rt;
    }

    /// Pone a una Image el fondo de panel elegido (madera o banda)
    public static void SkinImage(Image img)
    {
        if (img == null) return;
        WarnIfMissing(Get());
        var sprite = Get().PanelSprite;
        img.sprite = sprite;
        img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = sprite != null ? Color.white : new Color(0.08f, 0.05f, 0.02f, 0.85f);
    }

    static bool warnedMissing;

    // Sin texturas generadas los paneles salen como un rectangulo oscuro liso: avisarlo una vez
    static void WarnIfMissing(PirateStyle s)
    {
        if (warnedMissing || s.PanelSprite != null) return;
        warnedMissing = true;
        Debug.LogWarning($"[ESTILO] No estan las texturas de los paneles ({s.panelLook}): salen lisos. " +
                         "Pulsa Isla Pirata > Estilo pirata (texturas, fuente y estilo) fuera de Play.");
    }

    /// Filete dorado con rombo, horizontal, a la altura 'anchorY' del padre (0 = abajo, 1 = arriba)
    public static Image MakeRule(RectTransform parent, float anchorY, float widthFraction = 0.7f)
    {
        var s = Get();
        var go = new GameObject("Filete", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f - widthFraction / 2f, anchorY);
        rt.anchorMax = new Vector2(0.5f + widthFraction / 2f, anchorY);
        rt.sizeDelta = new Vector2(0f, s.rule != null ? 16f : 2f);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.sprite = s.rule;
        img.color = s.rule != null ? Color.white : new Color(s.gold.r, s.gold.g, s.gold.b, 0.7f);
        return img;
    }

    /// Canvas World Space con fondo. pixelsPerMeter: 1000 = 1 pixel por milimetro.
    public static RectTransform MakeCanvas(string name, Transform parent, Vector2 sizePx, float pixelsPerMeter, Sprite background, Color tint)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        if (parent != null) rt.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;
        rt.sizeDelta = sizePx;
        rt.localScale = Vector3.one / pixelsPerMeter;
        var group = go.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = tint;
        if (background != null) { img.sprite = background; img.type = Image.Type.Sliced; }
        return rt;
    }

    /// Texto que ocupa todo el padre menos un margen
    public static TextMeshProUGUI MakeText(RectTransform parent, string name, float size, bool title, Color color, float margin = 16f)
    {
        var s = Get();
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin * 0.75f);
        rt.offsetMax = new Vector2(-margin, -margin * 0.75f);
        var t = go.AddComponent<TextMeshProUGUI>();
        var font = s.Font(title ? FontRole.Title : FontRole.Body);
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    /// Circulo con una letra o palabra corta (B, X, Y, GRIP...) para las pistas de controles
    public static RectTransform MakeButtonIcon(RectTransform parent, string label, float sizePx)
    {
        var s = Get();
        var go = new GameObject("Boton " + label, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        float w = label.Length > 1 ? sizePx * (0.55f + 0.32f * label.Length) : sizePx;
        rt.sizeDelta = new Vector2(w, sizePx);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = s.gold;   // circulo dorado y letra oscura: se ve sobre la madera y sobre la banda
        if (s.circle != null) { img.sprite = s.circle; img.type = Image.Type.Sliced; }
        var t = MakeText(rt, "Letra", sizePx * (label.Length > 1 ? 0.42f : 0.6f), false, s.ink, 2f);
        ApplyFont(t, FontRole.Heading);
        t.fontStyle = FontStyles.Bold;
        t.text = label;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return rt;
    }

    /// Viste un panel ya creado (objetivos, pantalla de fin): fondo de madera o banda y textos claros
    public static void SkinPanel(Canvas canvas, bool titleFont)
    {
        if (canvas == null) return;
        var s = Get();
        SkinImage(canvas.GetComponentInChildren<Image>(true));
        foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            ApplyFont(t, titleFont);
            t.color = titleFont ? s.gold : s.paperText;
            t.outlineWidth = 0f;
        }
    }

    /// Pone la fuente del estilo a un texto ya existente
    public static void ApplyFont(TMP_Text t, bool title) => ApplyFont(t, title ? FontRole.Title : FontRole.Body);

    public static void ApplyFont(TMP_Text t, FontRole role)
    {
        if (t == null) return;
        var f = Get().Font(role);
        if (f != null) t.font = f;
    }

    /// Pone la fuente de texto a todos los textos de la escena que no usan una fuente del estilo
    /// (los que se crearon con la de TMP por defecto). Devuelve cuantos ha cambiado.
    public static int ReplaceDefaultFonts()
    {
        var s = Get();
        if (s.bodyFont == null) return 0;
        int n = 0;
        foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            if (t.font != null && (t.font == s.titleFont || t.font == s.headingFont || t.font == s.bodyFont)) continue;
            t.font = s.bodyFont;
            n++;
        }
        return n;
    }
}
