using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// Herramienta de editor: menu "Isla Pirata > Estilo pirata".
/// Crea todo lo que necesita el estilo comun de la interfaz (pergamino, madera y tinta), sin assets externos:
///  - texturas generadas por codigo en Assets/UI: Pergamino, Madera, Boton, Circulo e iconos del menu (sprites 9-slice);
///  - fuentes TMP a partir de Assets/UI/Fuentes: titulos PirataOne-Regular.ttf, encabezados IMFellEnglishSC-Regular.ttf
///    y texto LibreBaskerville-Regular/Italic.ttf (licencias OFL junto a los .ttf); la de texto pasa a ser la de TMP por defecto (pregunta antes);
///  - texturas de los paneles: placa de madera y oro, banda difuminada y filete (se elige con PirateStyle.panelLook);
///  - el asset Assets/Resources/EstiloPirata.asset que leen los scripts (PirateStyle.Get()).
public static class StyleSetupTool
{
    const string UIFolder = "Assets/UI";
    const string FontFolder = UIFolder + "/Fuentes";
    const string TitleTtf = FontFolder + "/PirataOne-Regular.ttf";
    const string TitleFontAsset = FontFolder + "/PirataOne SDF.asset";
    const string HeadingTtf = FontFolder + "/IMFellEnglishSC-Regular.ttf";
    const string HeadingFontAsset = FontFolder + "/IMFellEnglishSC SDF.asset";
    // Texto: Libre Baskerville (30/10; antes Alegreya, cuyos .ttf y SDF siguen en la carpeta pero no se usan)
    const string BodyName = "LibreBaskerville";
    const string BodyTtf = FontFolder + "/LibreBaskerville-Regular.ttf";
    const string BodyFontAsset = FontFolder + "/LibreBaskerville SDF.asset";
    const string BodyItalicTtf = FontFolder + "/LibreBaskerville-Italic.ttf";
    const string BodyItalicFontAsset = FontFolder + "/LibreBaskerville Italic SDF.asset";
    // Respaldo de la de texto para caracteres raros: la que trae TMP
    const string TmpFallbackFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string StylePath = "Assets/Resources/" + PirateStyle.ResourceName + ".asset";

    // Caracteres de la fuente de titulos (atlas estatico: no depende del .ttf en el build)
    const string Chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                         "áéíóúÁÉÍÓÚñÑüÜ¿¡·°ºª«»—–…’‘“”";

    /// El estilo, creandolo si aun no existe (lo usan las herramientas del menu de la muneca y de la intro)
    public static PirateStyle Ensure()
    {
        var s = AssetDatabase.LoadAssetAtPath<PirateStyle>(StylePath);
        if (s != null) return s;
        Debug.Log("[ESTILO] No habia estilo pirata: lo creo ahora.");
        Create();
        return AssetDatabase.LoadAssetAtPath<PirateStyle>(StylePath);
    }

    [MenuItem("Isla Pirata/Estilo pirata (texturas, fuente y estilo)")]
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder(UIFolder)) AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        var style = AssetDatabase.LoadAssetAtPath<PirateStyle>(StylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<PirateStyle>();
            AssetDatabase.CreateAsset(style, StylePath);
        }
        Undo.RecordObject(style, "Estilo pirata");

        style.parchment = SaveSprite("Pergamino", 256, 256, 56, Parchment);
        style.darkPanel = SaveSprite("Madera", 256, 256, 40, Wood);
        style.plaque = SaveSprite("Placa", 256, 256, 56, Plaque);
        style.band = SaveSprite("Banda", 256, 128, new Vector4(96, 10, 96, 10), Band);
        style.rule = SaveSprite("Filete", 256, 16, 0, Rule);
        style.button = SaveSprite("Boton", 128, 128, 30, ButtonWood);
        style.chartFrame = SaveSprite("Carta_Marco", 128, 128, 24, ChartFrame);
        style.chartLines = SaveSprite("Carta_Lineas", 320, 400, 0, ChartLines);
        style.pill = SaveSprite("Pildora", 64, 32, 15, Pill);
        // Fila: relleno muy suave y raya a la izquierda (el boton la deja invisible y la enciende en oro al pasar el rayo)
        style.row = SaveSprite("Fila", 32, 32, new Vector4(4, 1, 1, 1), (x, y, w, h) => new Color(1f, 1f, 1f, x < 3 ? 1f : 0.1f));
        style.medallion = SaveSprite("Medallon", 64, 64, 0, Medallion);
        // Selector de valores (pildoras unidas): izquierda, medio, derecha y suelta; con borde y rellenas
        string[] segNames = { "Izq", "Medio", "Der", "Solo" };
        Vector4[] segBorders = { new Vector4(14, 13, 2, 13), new Vector4(2, 13, 2, 13), new Vector4(2, 13, 14, 13), new Vector4(14, 13, 14, 13) };
        bool[] roundL = { true, false, false, true }, roundR = { false, false, true, true }, lineL = { true, false, false, true };
        style.segment = new Sprite[4];
        style.segmentFill = new Sprite[4];
        for (int k = 0; k < 4; k++)
        {
            int kk = k;
            style.segment[k] = SaveSprite("Segmento_" + segNames[k], 64, 28, segBorders[k], (x, y, w, h) => Segment(x, y, w, h, roundL[kk], roundR[kk], lineL[kk], false));
            style.segmentFill[k] = SaveSprite("Segmento_" + segNames[k] + "_Lleno", 64, 28, segBorders[k], (x, y, w, h) => Segment(x, y, w, h, roundL[kk], roundR[kk], lineL[kk], true));
        }
        style.circle = SaveSprite("Circulo", 128, 128, 63, (x, y, w, h) => new Color(1f, 1f, 1f, Cover(x, y, (px, py) => Circle(px, py, 64, 64, 62))));
        style.iconMove = SaveIcon("Icono_Mover", IconMove);
        style.iconTurn = SaveIcon("Icono_Girar", (x, y) => IconArc(x, y, true));
        style.iconSpeed = SaveIcon("Icono_Velocidad", IconSpeed);
        style.iconVignette = SaveIcon("Icono_Vineta", IconVignette);
        style.iconTime = SaveIcon("Icono_Hora", IconMoon);
        style.iconVolume = SaveIcon("Icono_Volumen", IconVolume);
        style.iconRestart = SaveIcon("Icono_Reiniciar", (x, y) => IconArc(x, y, false));
        style.iconClose = SaveIcon("Icono_Cerrar", IconClose);

        // Texto primero: es el respaldo de las otras dos para los caracteres que les falten
        var bodyFallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFallbackFont);
        style.bodyFont = MakeFont(BodyTtf, BodyFontAsset, BodyName, bodyFallback);
        var italic = MakeFont(BodyItalicTtf, BodyItalicFontAsset, BodyName + " Italic", bodyFallback);
        if (style.bodyFont != null && italic != null && style.bodyFont.fontWeightTable[4].italicTypeface != italic)
        {
            style.bodyFont.fontWeightTable[4].italicTypeface = italic;   // la cursiva de verdad, no inclinada a la fuerza
            EditorUtility.SetDirty(style.bodyFont);
        }
        style.titleFont = MakeFont(TitleTtf, TitleFontAsset, "PirataOne", style.bodyFont);
        style.headingFont = MakeFont(HeadingTtf, HeadingFontAsset, "IMFellEnglishSC", style.bodyFont);
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();
        SetDefaultFont(style.bodyFont);
        Selection.activeObject = style;
        Debug.Log($"[ESTILO] Estilo pirata listo en {StylePath}. Paneles: {style.panelLook}. Fuentes: titulos {Name(style.titleFont, TitleTtf)}, " +
                  $"encabezados {Name(style.headingFont, HeadingTtf)}, texto {Name(style.bodyFont, BodyTtf)}. " +
                  "Ahora vuelve a pulsar Isla Pirata > Menu muneca (para el menu con el estilo nuevo). Lo demas (historia, pistas, objetivos) lo usa solo al darle a Play.");
    }

    static string Name(TMP_FontAsset f, string ttf) => f != null ? f.name : "FALTA (" + ttf + ")";

    // ------------------------------------------------------------------ elegir el aspecto de los paneles
    [MenuItem("Isla Pirata/Estilo pirata - paneles de madera y oro")]
    static void LookWood() => SetLook(PirateStyle.PanelLook.Madera);

    [MenuItem("Isla Pirata/Estilo pirata - paneles en banda difuminada")]
    static void LookBand() => SetLook(PirateStyle.PanelLook.Banda);

    static void SetLook(PirateStyle.PanelLook look)
    {
        var style = Ensure();
        if (style == null) return;
        Undo.RecordObject(style, "Aspecto de los paneles");
        style.panelLook = look;
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();
        Selection.activeObject = style;
        Debug.Log($"[ESTILO] Paneles: {look}. Dale a Play para verlos (avisos, pistas, subtitulos del totem, objetivos y pantalla final).");
    }

    // ------------------------------------------------------------------ fuentes
    static TMP_FontAsset MakeFont(string ttf, string assetPath, string label, TMP_FontAsset fallback)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
        {
            // La de titulos se creo antes con la de TMP de respaldo: ahora el respaldo es la de texto
            if (fallback != null && (existing.fallbackFontAssetTable == null || existing.fallbackFontAssetTable.Count == 0 || existing.fallbackFontAssetTable[0] != fallback))
            {
                existing.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                EditorUtility.SetDirty(existing);
            }
            return existing;
        }
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        if (font == null) { Debug.LogWarning($"[ESTILO] No esta {ttf}: se usara otra fuente en su lugar."); return null; }

        var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
        if (fa == null) { Debug.LogWarning($"[ESTILO] TMP no ha podido crear la fuente {label}."); return null; }
        fa.name = label + " SDF";
        if (!fa.TryAddCharacters(Chars, out string missing) && !string.IsNullOrEmpty(missing))
            Debug.Log($"[ESTILO] La fuente {label} no tiene: '{missing}' (se usara la de respaldo para esos)");
        fa.atlasPopulationMode = AtlasPopulationMode.Static;   // atlas guardado en el asset
        if (fallback != null) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

        AssetDatabase.CreateAsset(fa, assetPath);
        fa.atlasTextures[0].name = label + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = label + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ESTILO] Creada la fuente {assetPath}");
        return fa;
    }

    // Que los textos nuevos (de las herramientas o creados al jugar) salgan con la de texto y no con la de TMP
    static void SetDefaultFont(TMP_FontAsset body)
    {
        if (body == null || TMP_Settings.instance == null || TMP_Settings.defaultFontAsset == body) return;
        string old = TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset.name : "ninguna";
        if (!EditorUtility.DisplayDialog("Estilo pirata",
                $"¿Poner {body.name} como fuente por defecto de TextMeshPro?\n\n" +
                $"Ahora es {old}. Asi ningun texto nuevo sale con la fuente por defecto. " +
                "Se cambia el asset 'TextMesh Pro/Resources/TMP Settings' (se puede deshacer con Ctrl+Z).",
                "Si, cambiarla", "No"))
        {
            Debug.Log("[ESTILO] No se ha cambiado la fuente por defecto de TMP. Los textos del juego usan igualmente la del estilo al darle a Play.");
            return;
        }
        Undo.RecordObject(TMP_Settings.instance, "Fuente por defecto de TMP");
        TMP_Settings.defaultFontAsset = body;
        EditorUtility.SetDirty(TMP_Settings.instance);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ESTILO] Fuente por defecto de TMP: {old} -> {body.name}");
    }

    // ------------------------------------------------------------------ guardar texturas como sprites
    static Sprite SaveSprite(string name, int w, int h, int border, System.Func<int, int, int, int, Color> pixel) =>
        SaveSprite(name, w, h, new Vector4(border, border, border, border), pixel);

    // border = izquierda, abajo, derecha, arriba (como Sprite Editor)
    static Sprite SaveSprite(string name, int w, int h, Vector4 border, System.Func<int, int, int, int, Color> pixel)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) px[y * w + x] = pixel(x, y, w, h);
        tex.SetPixels(px);
        tex.Apply();
        string path = $"{UIFolder}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spriteBorder = border;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = true;   // en VR se ve de lejos: sin mipmaps parpadea
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite SaveIcon(string name, System.Func<float, float, bool> inside) =>
        SaveSprite(name, 64, 64, 0, (x, y, w, h) => new Color(1f, 1f, 1f, Cover(x, y, inside)));

    // Cobertura del pixel con 4x4 muestras (antialiasing)
    static float Cover(int x, int y, System.Func<float, float, bool> inside)
    {
        int n = 0;
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                if (inside(x + (i + 0.5f) / 4f, y + (j + 0.5f) / 4f)) n++;
        return n / 16f;
    }

    static float Fbm(float x, float y)
    {
        float v = 0f, a = 0.5f, f = 1f;
        for (int o = 0; o < 4; o++) { v += a * Mathf.PerlinNoise(x * f + 17.3f * o, y * f + 5.1f * o); a *= 0.5f; f *= 2.1f; }
        return v;
    }

    // ------------------------------------------------------------------ pergamino, madera, boton
    static Color Parchment(int x, int y, int w, int h)
    {
        float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
        float n = Fbm(x * 0.03f, y * 0.03f);
        var paper = Color.Lerp(new Color(0.86f, 0.75f, 0.53f), new Color(0.97f, 0.91f, 0.74f), n);
        // Bordes tostados e irregulares
        float rough = Fbm(x * 0.09f + 40f, y * 0.09f) * 14f;
        float burn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f + rough * 0.4f, 40f + rough, edge));
        var c = Color.Lerp(new Color(0.45f, 0.27f, 0.12f), paper, burn);
        c.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rough * 0.25f, 3f + rough * 0.25f, edge));
        return c;
    }

    static Color Wood(int x, int y, int w, int h)
    {
        float edge = RoundedEdge(x, y, w, h, 22f);
        float grain = Mathf.Sin(y * 0.22f + Fbm(x * 0.01f, y * 0.06f) * 9f) * 0.5f + 0.5f;
        var c = Color.Lerp(new Color(0.17f, 0.10f, 0.05f), new Color(0.29f, 0.18f, 0.09f), grain * 0.7f + Fbm(x * 0.05f, y * 0.05f) * 0.3f);
        // Marco: filete dorado y sombra interior
        if (edge < 10f) c = Color.Lerp(new Color(0.55f, 0.38f, 0.18f), new Color(0.35f, 0.22f, 0.1f), edge / 10f);
        else if (edge < 16f) c *= 0.6f;
        c.a = Mathf.Clamp01(edge + 0.5f);
        return c;
    }

    static readonly Color GoldLine = new Color(0.94f, 0.76f, 0.35f);
    static readonly Color GoldDeep = new Color(0.72f, 0.54f, 0.18f);

    static Color WoodGrain(int x, int y, Color dark, Color light)
    {
        float grain = Mathf.Sin(y * 0.22f + Fbm(x * 0.01f, y * 0.06f) * 9f) * 0.5f + 0.5f;
        return Color.Lerp(dark, light, grain * 0.7f + Fbm(x * 0.05f, y * 0.05f) * 0.3f);
    }

    // Placa: madera oscura, filete dorado grueso por fuera, hueco oscuro, filete dorado fino y rombos en las esquinas
    static Color Plaque(int x, int y, int w, int h)
    {
        const float m = 10f;   // sitio para que los rombos sobresalgan del marco
        float edge = Mathf.Min(Mathf.Min(x - m, w - 1 - m - x), Mathf.Min(y - m, h - 1 - m - y));
        Color c;
        if (edge < 3f) c = Color.Lerp(GoldDeep, GoldLine, Mathf.Clamp01(edge / 3f));
        else if (edge < 7f) c = new Color(0.09f, 0.05f, 0.02f);
        else if (edge < 8.5f) c = GoldLine;
        else
        {
            c = WoodGrain(x, y, new Color(0.12f, 0.07f, 0.035f), new Color(0.24f, 0.15f, 0.08f));
            c *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((edge - 8.5f) / 16f));   // sombra junto al marco
        }
        c.a = Mathf.Clamp01(edge + 0.5f);

        // Rombos en las cuatro esquinas del marco
        float cx = x < w / 2 ? m + 1f : w - 2 - m, cy = y < h / 2 ? m + 1f : h - 2 - m;
        float d = Cover(x, y, (px, py) => Mathf.Abs(px - cx - 0.5f) + Mathf.Abs(py - cy - 0.5f) <= 9f);
        if (d > 0f)
        {
            var diamond = y > cy ? GoldLine : GoldDeep;   // mitad de arriba mas clara: parece que tiene relieve
            c = Color.Lerp(c, diamond, d);
            c.a = Mathf.Max(c.a, d);
        }
        return c;
    }

    // Banda: oscura en el centro, se difumina a los lados; filetes dorados arriba y abajo que se apagan antes
    static Color Band(int x, int y, int w, int h)
    {
        float t = Mathf.Min(x, w - 1 - x) / 96f;
        float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
        var c = new Color(0.05f, 0.03f, 0.015f) * (0.9f + 0.2f * Fbm(x * 0.04f, y * 0.04f));
        c.a = 0.82f * a;
        bool line = (y >= 1 && y <= 2) || (y >= h - 3 && y <= h - 2);
        if (line)
        {
            c = GoldLine;
            c.a = 0.95f * a * a;
        }
        return c;
    }

    // Filete: linea dorada que se apaga en las puntas con un rombo en el centro (256 x 16)
    static Color Rule(int x, int y, int w, int h)
    {
        float cx = w / 2f, cy = h / 2f;
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(x, w - 1 - x) / 70f));
        float line = Cover(x, y, (px, py) => Mathf.Abs(py - cy) <= 0.8f) * fade;
        float diamond = Cover(x, y, (px, py) => Mathf.Abs(px - cx) + Mathf.Abs(py - cy) <= 6.5f);
        var c = GoldLine;
        c.a = Mathf.Max(line, diamond);
        return c;
    }

    // Boton del menu: madera algo mas clara que el panel (para que se note el tinte de la pestana activa) y canto dorado
    // ------------------------------------------------------------------ carta nautica (menu de la muneca)
    static readonly Color Navy = new Color(0.04f, 0.08f, 0.14f);
    static readonly Color NavyLight = new Color(0.063f, 0.125f, 0.22f);

    // Marco: aro dorado tenue por fuera, franja azul, filete dorado y fondo azul noche (mas claro arriba)
    static Color ChartFrame(int x, int y, int w, int h)
    {
        float e = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
        Color c;
        if (e < 1.3f) { c = GoldLine; c.a = 0.35f; return c; }
        if (e < 5f) c = Navy;
        else if (e < 6.3f) c = Color.Lerp(Navy, GoldLine, 0.6f);
        else c = Color.Lerp(Navy, NavyLight, y / (float)h);
        c.a = 0.96f;
        return c;
    }

    // Lineas de la carta (se estiran sobre todo el panel): cuadricula, 16 rumbos desde una rosa de los vientos
    // arriba a la derecha y escala de grados arriba y abajo
    static Color ChartLines(int x, int y, int w, int h)
    {
        float a = 0f;
        if (x % 22 == 0 || y % 22 == 0) a = 0.06f;                              // cuadricula
        float cx = w * 0.86f, cy = h * 0.86f, dx = x - cx, dy = y - cy;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        float delta = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 22.5f) * 22.5f));
        if (r > 18f && r * Mathf.Sin(delta * Mathf.Deg2Rad) < 0.6f) a = Mathf.Max(a, 0.12f);   // rumbos
        bool star = Mathf.Abs(dx) / 16f + Mathf.Abs(dy) / 4f <= 1f || Mathf.Abs(dx) / 4f + Mathf.Abs(dy) / 16f <= 1f;
        if (star) a = Mathf.Max(a, 0.55f);                                        // rosa de los vientos
        if (Mathf.Abs(r - 10f) < 0.6f) a = Mathf.Max(a, 0.4f);
        bool tickRow = (y >= 9 && y <= 14) || (y >= h - 15 && y <= h - 10);
        if (tickRow && x >= 14 && x <= w - 15 && x % 8 == 0) a = Mathf.Max(a, 0.65f);   // escala de grados
        var c = GoldLine;
        c.a = a;
        return c;
    }

    // Pildora blanca: borde opaco y relleno muy suave (se tine de oro desde el script)
    static Color Pill(int x, int y, int w, int h)
    {
        float e = RoundedEdge(x, y, w, h, 15f);
        var c = Color.white;
        c.a = e < 1.4f ? Mathf.Clamp01(e + 0.5f) : 0.16f;
        return c;
    }

    // Pieza del selector: borde de 1 px (o relleno). Las del medio y la derecha no dibujan el borde izquierdo:
    // lo pone la pieza de al lado, como en la plantilla
    static Color Segment(int x, int y, int w, int h, bool roundL, bool roundR, bool lineL, bool filled)
    {
        float r = (h - 1) / 2f;
        float e = Mathf.Min(y, h - 1 - y);
        if (roundL && x < r) e = Mathf.Min(e, r - Vector2.Distance(new Vector2(x, y), new Vector2(r, r)));
        else if (lineL) e = Mathf.Min(e, x);
        if (roundR && x > w - 1 - r) e = Mathf.Min(e, r - Vector2.Distance(new Vector2(x, y), new Vector2(w - 1 - r, r)));
        else if (!roundR) e = Mathf.Min(e, w - 1 - x);
        float shape = Mathf.Clamp01(e + 0.5f);
        var c = Color.white;
        c.a = filled || e < 1.3f ? shape : 0f;
        return c;
    }

    // Medallon: circulo de trazos (24 trazos) con el interior casi transparente
    static Color Medallion(int x, int y, int w, int h)
    {
        float dx = x - (w - 1) / 2f, dy = y - (h - 1) / 2f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
        bool dash = ((int)(ang / 7.5f)) % 2 == 0;
        float ring = Mathf.Clamp01(1.2f - Mathf.Abs(d - 29f));
        var c = Color.white;
        c.a = Mathf.Max(dash ? ring : 0f, d < 28f ? 0.08f : 0f);
        return c;
    }

    static Color ButtonWood(int x, int y, int w, int h)
    {
        float edge = RoundedEdge(x, y, w, h, 16f);
        var c = WoodGrain(x, y, new Color(0.30f, 0.19f, 0.10f), new Color(0.46f, 0.31f, 0.17f));
        c *= Mathf.Lerp(0.85f, 1.05f, y / (float)h);                       // un poco mas oscuro abajo (relieve)
        if (edge < 2.5f) c = GoldDeep;                                       // canto dorado
        else if (edge < 4.5f) c = new Color(0.10f, 0.06f, 0.03f);
        c.a = Mathf.Clamp01(edge + 0.5f);
        return c;
    }

    // Distancia al borde de un rectangulo de esquinas redondeadas (negativa fuera)
    static float RoundedEdge(float x, float y, int w, int h, float r)
    {
        float dx = Mathf.Max(r - x, x - (w - 1 - r), 0f);
        float dy = Mathf.Max(r - y, y - (h - 1 - r), 0f);
        if (dx > 0f && dy > 0f) return r - Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
    }

    // ------------------------------------------------------------------ iconos (64 x 64, blanco: se tinen)
    static bool Circle(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;

    static bool Seg(float x, float y, float ax, float ay, float bx, float by, float width)
    {
        var p = new Vector2(x, y); var a = new Vector2(ax, ay); var b = new Vector2(bx, by);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
        return Vector2.Distance(p, a + t * (b - a)) <= width * 0.5f;
    }

    static bool Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
    {
        float S(Vector2 p1, Vector2 p2) => (x - p2.x) * (p1.y - p2.y) - (p1.x - p2.x) * (y - p2.y);
        float d1 = S(a, b), d2 = S(b, c), d3 = S(c, a);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }

    static bool IconMove(float x, float y) =>
        (x >= 8 && x <= 38 && y >= 27 && y <= 37) || Tri(x, y, new Vector2(36, 14), new Vector2(36, 50), new Vector2(58, 32));

    static bool IconSpeed(float x, float y) =>
        Seg(x, y, 10, 14, 28, 32, 8) || Seg(x, y, 28, 32, 10, 50, 8) || Seg(x, y, 32, 14, 50, 32, 8) || Seg(x, y, 50, 32, 32, 50, 8);

    static bool IconVignette(float x, float y)
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
        return (d >= 20 && d <= 28) || d <= 7;
    }

    static bool IconMoon(float x, float y) => Circle(x, y, 32, 32, 24) && !Circle(x, y, 44, 40, 20);

    static bool IconVolume(float x, float y)
    {
        bool box = x >= 8 && x <= 18 && y >= 24 && y <= 40;
        bool cone = Tri(x, y, new Vector2(16, 32), new Vector2(32, 12), new Vector2(32, 52)) || (x >= 16 && x <= 32 && y >= 24 && y <= 40);
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(30, 32));
        float ang = Mathf.Atan2(y - 32, x - 30) * Mathf.Rad2Deg;
        bool arcs = Mathf.Abs(ang) < 45f && ((d >= 13 && d <= 17) || (d >= 22 && d <= 26));
        return box || cone || arcs;
    }

    // Flecha en circulo: girar (sentido horario) o reiniciar (antihorario)
    static bool IconArc(float x, float y, bool clockwise)
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
        float ang = Mathf.Atan2(y - 32, x - 32) * Mathf.Rad2Deg;   // -180..180, 0 = derecha
        bool ring = d >= 15 && d <= 22 && !(ang > 20f && ang < 80f);   // hueco arriba a la derecha
        bool head = clockwise
            ? Tri(x, y, new Vector2(40, 36), new Vector2(58, 36), new Vector2(49, 50))     // punta al final del arco (abajo del hueco)
            : Tri(x, y, new Vector2(30, 44), new Vector2(30, 60), new Vector2(44, 52));    // punta al principio (encima del hueco)
        return ring || head;
    }

    static bool IconClose(float x, float y) => Seg(x, y, 14, 14, 50, 50, 9) || Seg(x, y, 14, 50, 50, 14, 9);
}
