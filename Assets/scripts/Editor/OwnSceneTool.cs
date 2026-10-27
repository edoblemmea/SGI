using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Herramienta de editor: menu "Isla Pirata > Crear escena propia".
/// Copia la escena abierta (la Demo de Island) a una escena nuestra, duplica el TerrainData para no modificar el
/// del asset, y pone la escena nueva la primera en File > Build Profiles > Scene List.
/// La escena original (Demo.unity) no se modifica.
public static class OwnSceneTool
{
    const string Folder = "Assets/Scenes";
    const string SceneName = "IslaPirata";

    [MenuItem("Isla Pirata/Crear escena propia")]
    static void Create()
    {
        string scenePath = $"{Folder}/{SceneName}.unity";
        var src = EditorSceneManager.GetActiveScene();

        if (string.IsNullOrEmpty(src.path))
        {
            EditorUtility.DisplayDialog("Crear escena propia", "Abre primero la escena del juego (Assets/Island/Scenes/Demo.unity).", "Vale");
            return;
        }
        if (src.path == scenePath)
        {
            EditorUtility.DisplayDialog("Crear escena propia", $"Ya estas en {scenePath}.", "Vale");
            return;
        }
        if (File.Exists(scenePath))
        {
            EditorUtility.DisplayDialog("Crear escena propia", $"Ya existe {scenePath}. Abrela con doble clic en el Project, " +
                                        "o borrala/renombrala si quieres crearla otra vez.", "Vale");
            return;
        }
        if (!EditorUtility.DisplayDialog("Crear escena propia",
                $"Se copiara '{src.path}' a '{scenePath}' (la original no se toca) y se abrira la copia.\n\n" +
                "- Se duplica el TerrainData para no modificar el del asset.\n" +
                "- La escena nueva pasa a ser la primera de Build Profiles > Scene List (las demas se desmarcan).\n\n" +
                "Si la escena actual tiene cambios sin guardar, primero se te preguntara si guardarlos.",
                "Crear", "Cancelar"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Scenes");

        // 1. Copia de la escena y abrirla
        if (!EditorSceneManager.SaveScene(src, scenePath, true))
        {
            Debug.LogError($"[ESCENA] No se pudo guardar la copia en {scenePath}.");
            return;
        }
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Debug.Log($"[ESCENA] Copiada '{src.path}' -> '{scenePath}'. Ahora estas trabajando en la escena nueva.");

        // 2. TerrainData propio (DEMO: el terreno de la Island demo)
        int t = 0;
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
        {
            var data = terrain.terrainData;
            if (data == null) continue;
            string dataPath = AssetDatabase.GetAssetPath(data);
            if (dataPath.StartsWith(Folder + "/")) continue;   // ya es nuestro

            string copyPath = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{SceneName}_Terrain{(t > 0 ? "_" + t : "")}.asset");
            if (!AssetDatabase.CopyAsset(dataPath, copyPath))
            {
                Debug.LogError($"[ESCENA] No se pudo copiar el TerrainData '{dataPath}'.");
                continue;
            }
            var copy = AssetDatabase.LoadAssetAtPath<TerrainData>(copyPath);
            Undo.RecordObject(terrain, "Terreno propio");
            terrain.terrainData = copy;
            var col = terrain.GetComponent<TerrainCollider>();
            if (col != null) { Undo.RecordObject(col, "Terreno propio"); col.terrainData = copy; }
            Debug.Log($"[ESCENA] Terreno '{terrain.name}': TerrainData copiado '{dataPath}' -> '{copyPath}' (Terrain y Terrain Collider).");
            t++;
        }
        if (t == 0) Debug.Log("[ESCENA] No habia TerrainData del asset que copiar.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // 3. Build Profiles > Scene List: la nueva la primera; el resto se conserva desmarcado
        var list = EditorBuildSettings.scenes.Where(s => s.path != scenePath)
            .Select(s => new EditorBuildSettingsScene(s.path, false)).ToList();
        list.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[ESCENA] Scene List: '{scenePath}' es la escena 0 (las demas desmarcadas).");

        EditorUtility.DisplayDialog("Escena propia creada",
            $"Ya estas en {scenePath}.\n\nQue falta:\n" +
            "- NavMesh: selecciona el objeto 'NavMesh' y pulsa Bake (cada escena guarda el suyo).\n" +
            "- Usa a partir de ahora el menu Isla Pirata en ESTA escena.\n" +
            "- Comprueba en Play que todo sigue igual que en la demo.",
            "Vale");
    }
}
