using UnityEditor;
using UnityEngine;

/// Herramienta de editor: menu "Isla Pirata > Intro".
/// Crea el objeto "Intro" con GameIntro: la partida empieza de noche con el totem contando la historia
/// (sin musica y sin poder moverse), luego sale el titulo mientras amanece y empieza el juego.
public static class IntroSetupTool
{
    [MenuItem("Isla Pirata/Intro (historia y titulo)")]
    static void Create()
    {
        StyleSetupTool.Ensure();   // el cartel del titulo y los subtitulos usan el estilo pirata
        var intro = Object.FindAnyObjectByType<GameIntro>(FindObjectsInactive.Include);
        if (intro == null)
        {
            var go = new GameObject("Intro");
            Undo.RegisterCreatedObjectUndo(go, "Intro");
            intro = go.AddComponent<GameIntro>();
        }
        Undo.RecordObject(intro, "Intro");

        if (intro.storyTotem == null) intro.storyTotem = Object.FindAnyObjectByType<StoryTotem>(FindObjectsInactive.Include);
        if (intro.dayNight == null) intro.dayNight = Object.FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);

        // Musica: el objeto "Musica" que crea "Isla Pirata > Juego" dentro del GameManager
        if (intro.music == null)
        {
            var gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            var m = gm != null ? gm.transform.Find("Musica") : null;
            if (m != null) intro.music = m.GetComponent<AudioSource>();
        }
        if (intro.music != null && intro.music.playOnAwake)
        {
            Undo.RecordObject(intro.music, "Intro");
            intro.music.playOnAwake = false;   // la enciende GameIntro al acabar el titulo
        }

        AssignTitleImages(intro);

        // Niebla de la historia: URP quita del build los shaders con niebla si la escena no la tiene activada
        // (Graphics > Fog Modes = Automatic). Se activa en la escena con densidad casi nula (no se nota).
        if (intro.storyFog && !RenderSettings.fog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0001f;
            Debug.Log("[INTRO] Activada la niebla de la escena (Window > Rendering > Lighting > Environment > Fog) con densidad 0.0001, " +
                      "para que la niebla de la historia funcione tambien en el build.");
        }

        // El jugador tiene que empezar cerca del totem (no se puede mover durante la historia)
        string dist = "";
        var ps = Object.FindAnyObjectByType<PlayerStart>(FindObjectsInactive.Include);
        if (ps != null && intro.storyTotem != null)
        {
            float d = Vector3.Distance(ps.transform.position, intro.storyTotem.transform.position);
            dist = $" Distancia inicio-totem: {d:F1} m" + (d > 5f ? " (LEJOS: acerca 'Inicio jugador' al totem o vuelve a pulsar Isla Pirata > Totem narrador)" : "") + ".";
        }

        Selection.activeObject = intro.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(intro.gameObject.scene);
        string Ok(Object o) => o != null ? "ok" : "FALTA";
        Debug.Log($"[INTRO] Intro lista. Totem: {Ok(intro.storyTotem)}, dia/noche: {Ok(intro.dayNight)}, musica: {Ok(intro.music)}.{dist} " +
                  "El titulo se cambia en Game Intro > Game Title. Guarda (Ctrl+S).");
        if (intro.storyTotem == null) Debug.LogWarning("[INTRO] No hay totem narrador: pulsa antes Isla Pirata > Totem narrador.");
        if (intro.music == null) Debug.LogWarning("[INTRO] No encuentro la musica (GameManager > Musica). Si hay musica, arrastra su AudioSource a 'Music'.");
    }

    // Imagenes del titulo hechas por el equipo (tablero + texto que aparece animado encima)
    const string BoardPath = "Assets/UI/Titulo/tablero-fondo-titulo1.png";
    const string TextPath = "Assets/UI/Titulo/texto-titulo1.png";

    static void AssignTitleImages(GameIntro intro)
    {
        var board = ImportAsSprite(BoardPath);
        var text = ImportAsSprite(TextPath);
        if (board != null && intro.titleImage == null) intro.titleImage = board;
        if (text != null && intro.titleTextImage == null)
        {
            intro.titleTextImage = text;
            intro.textOverImage = false;
            intro.textReveal = GameIntro.Reveal.Pisoton;
            intro.stompCenter = new Vector2(0.5f, 0.3f);   // 'Calavera' esta en la parte de abajo de la imagen
            intro.revealGlowColor = new Color(1f, 0.45f, 0.15f);   // el texto es rojo: destello de fuego
        }
        if (board != null || text != null)
            Debug.Log($"[INTRO] Imagenes del titulo: tablero {(intro.titleImage != null ? intro.titleImage.name : "FALTA")}, " +
                      $"texto {(intro.titleTextImage != null ? intro.titleTextImage.name : "FALTA")} ({intro.textReveal}).");
    }

    // Sprite con transparencia y mipmaps (sin mipmaps parpadea a distancia en VR)
    static Sprite ImportAsSprite(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return null;
        if (ti.textureType != TextureImporterType.Sprite || !ti.alphaIsTransparency || !ti.mipmapEnabled || ti.maxTextureSize != 2048)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
            Debug.Log($"[INTRO] {path} importada como Sprite (transparencia y mipmaps)");
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
