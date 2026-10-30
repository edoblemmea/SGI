using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// Herramienta de editor: menu "Isla Pirata > Limpieza del proyecto".
///  1. Assets sin usar: mira de que dependen las escenas del build, las carpetas Resources, los ajustes de render
///     y las rutas "Assets/..." que usan nuestras herramientas, y lista por carpeta lo que nadie usa.
///     Se envia a la PAPELERA del sistema (recuperable) solo lo que marques, tras confirmar.
///  2. Escenas del build: dejar solo la del juego.
///  3. Logs: apagar / encender las casillas 'debugLogs' de la escena y los prefabs, y la traza de pila de los logs.
///  4. Texturas grandes: limitar a 2048 px en Android (Quest), que es lo que mas pesa y mas tarda en el build.
/// Hazlo en una rama de git: si algo sale rosa o falta, se recupera con git.
public class CleanupTool : EditorWindow
{
    // Nunca se proponen para borrar
    static readonly string[] Protected =
    {
        "Assets/scripts", "Assets/Prefabs", "Assets/Resources", "Assets/UI", "Assets/Settings", "Assets/XR", "Assets/XRI",
        "Assets/TextMesh Pro", "Assets/CompositionLayers", "Assets/Samples", "Assets/Scenes/IslaPirata", "Assets/Audio",
        "Assets/Island/Scenes",   // Demo.unity (original, no se toca) y el NavMesh horneado que usa la escena
    };
    // Tipos que no se borran sueltos (se pueden usar por nombre: Shader.Find, scripts, ensamblados)
    static readonly string[] KeepExtensions = { ".cs", ".asmdef", ".asmref", ".dll", ".shader", ".cginc", ".hlsl", ".shadergraph", ".compute" };

    class Folder
    {
        public string path;
        public int total, unused;
        public long totalBytes, unusedBytes;
        public List<string> unusedFiles = new List<string>();
        public bool selected;
        public bool Whole => unused == total && total > 0;
    }

    List<Folder> folders;
    List<(string path, int size, long bytes)> bigTextures;
    int logComponents, logOn;
    Vector2 scroll;
    string gameScene = "Assets/Scenes/IslaPirata.unity";

    [MenuItem("Isla Pirata/Limpieza del proyecto")]
    static void Open() => GetWindow<CleanupTool>("Limpieza");

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Haz esto en una rama de git y con la escena guardada. Lo borrado va a la papelera del sistema y se puede recuperar con git.\n" +
                                "Los assets sin usar NO entran en el build (Unity solo mete lo que usa la escena): borrarlos aligera el repositorio y la importacion. " +
                                "Lo que acelera el build es la seccion 4 (texturas) y no borrar la carpeta Library (es la cache).", MessageType.Info);

        // ---------------------------------------------------------------- 1
        EditorGUILayout.LabelField("1. Assets sin usar", EditorStyles.boldLabel);
        if (GUILayout.Button("Analizar el proyecto")) Analyze();
        if (folders != null)
        {
            if (folders.Count == 0) EditorGUILayout.LabelField("No hay nada sin usar fuera de las carpetas protegidas.");
            foreach (var f in folders)
            {
                EditorGUILayout.BeginHorizontal();
                f.selected = EditorGUILayout.ToggleLeft(
                    $"{f.path}   {(f.Whole ? "ENTERA sin usar" : $"{f.unused}/{f.total} sin usar")}   {Mb(f.unusedBytes)}", f.selected);
                if (GUILayout.Button("Lista", GUILayout.Width(50)))
                    Debug.Log($"[LIMPIEZA] {f.path}: {f.unused} sin usar\n" + string.Join("\n", f.unusedFiles));
                EditorGUILayout.EndHorizontal();
            }
            long sel = folders.Where(f => f.selected).Sum(f => f.unusedBytes);
            using (new EditorGUI.DisabledScope(sel == 0))
                if (GUILayout.Button($"Enviar a la papelera lo marcado ({Mb(sel)})")) TrashSelected();
        }

        // ---------------------------------------------------------------- 2
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Escenas del build", EditorStyles.boldLabel);
        foreach (var s in EditorBuildSettings.scenes)
            EditorGUILayout.LabelField($"   {(s.enabled ? "[x]" : "[ ]")} {s.path}");
        gameScene = EditorGUILayout.TextField("Escena del juego", gameScene);
        if (GUILayout.Button("Dejar solo la escena del juego en Build Profiles")) OnlyGameScene();

        // ---------------------------------------------------------------- 3
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Logs", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Los logs [SISTEMA] son vuestra herramienta de depuracion: mejor apagarlos que borrarlos del codigo. " +
                                "Ademas, GameManager > 'Only Warnings In Build' oculta los Debug.Log normales en el build (en el editor siguen).", MessageType.None);
        if (GUILayout.Button("Contar casillas 'debugLogs'")) CountLogs();
        if (logComponents > 0) EditorGUILayout.LabelField($"   {logComponents} componentes con 'debugLogs', {logOn} encendidos");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apagar todos")) SetLogs(false);
        if (GUILayout.Button("Encender todos")) SetLogs(true);
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Logs normales sin traza de pila (Project Settings)")) StackTraces();

        // ---------------------------------------------------------------- 4
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("4. Texturas grandes (Android / Quest)", EditorStyles.boldLabel);
        if (GUILayout.Button("Buscar texturas de mas de 2048 px que usa la escena")) FindBigTextures();
        if (bigTextures != null)
        {
            if (bigTextures.Count == 0) EditorGUILayout.LabelField("   Ninguna.");
            foreach (var t in bigTextures.Take(25)) EditorGUILayout.LabelField($"   {t.size} px  {Mb(t.bytes)}  {t.path}");
            if (bigTextures.Count > 25) EditorGUILayout.LabelField($"   ... y {bigTextures.Count - 25} mas (la lista completa sale en la Console)");
            using (new EditorGUI.DisabledScope(bigTextures.Count == 0))
                if (GUILayout.Button($"Limitar estas {bigTextures.Count} texturas a 2048 en Android (ASTC 6x6)")) LimitTextures();
        }

        // ---------------------------------------------------------------- 5
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("5. Carpetas generadas (no se entregan ni se suben a git)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Library (cache de importacion, 3 GB: si la borras, Unity tarda mucho en reimportar todo), Temp, Logs, obj, *.csproj y *.slnx " +
                                "se regeneran solas y ya estan en .gitignore. Para entregar: Assets, Packages, ProjectSettings y UserSettings. " +
                                "Fuera de Assets sobran cap.png e ignore.conf (borralos a mano si no los usais).", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    static string Mb(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / (1024f * 1024f):F1} MB" : $"{bytes / 1024f:F0} KB";

    // ------------------------------------------------------------------ 1. analisis
    void Analyze()
    {
        var roots = new HashSet<string>();
        foreach (var s in EditorBuildSettings.scenes) if (s.enabled && File.Exists(s.path)) roots.Add(s.path);
        if (File.Exists(gameScene)) roots.Add(gameScene);

        // Resources (entran siempre en el build)
        foreach (var dir in Directory.GetDirectories("Assets", "Resources", SearchOption.AllDirectories))
            foreach (var g in AssetDatabase.FindAssets("", new[] { dir.Replace('\\', '/') })) roots.Add(AssetDatabase.GUIDToAssetPath(g));

        // Ajustes de render (URP) y de calidad
        AddObject(roots, GraphicsSettings.defaultRenderPipeline);
        for (int i = 0; i < QualitySettings.count; i++) AddObject(roots, QualitySettings.GetRenderPipelineAssetAt(i));

        // Rutas que usan nuestras herramientas ("Assets/..." en el codigo del editor): asi siguen pudiendo crear los prefabs
        int toolPaths = 0;
        foreach (var file in Directory.GetFiles("Assets/scripts", "*.cs", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"(Assets/[^\"]+)\""))
            {
                string p = m.Groups[1].Value;
                if (AssetDatabase.IsValidFolder(p))
                {
                    foreach (var g in AssetDatabase.FindAssets("", new[] { p })) roots.Add(AssetDatabase.GUIDToAssetPath(g));
                    toolPaths++;
                }
                else if (File.Exists(p)) { roots.Add(p); toolPaths++; }
            }

        var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));

        folders = new List<Folder>();
        foreach (var top in AssetDatabase.GetSubFolders("Assets"))
        {
            if (Protected.Any(p => top == p)) continue;
            var f = new Folder { path = top };
            foreach (var g in AssetDatabase.FindAssets("", new[] { top }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (AssetDatabase.IsValidFolder(p) || !File.Exists(p)) continue;
                f.total++;
                long bytes = new FileInfo(p).Length;
                f.totalBytes += bytes;
                if (used.Contains(p) || Protected.Any(pr => p.StartsWith(pr)) || p.Contains("/Editor/")) continue;
                if (KeepExtensions.Contains(Path.GetExtension(p).ToLowerInvariant())) continue;
                f.unused++;
                f.unusedBytes += bytes;
                f.unusedFiles.Add(p);
            }
            // Carpeta entera: si no se usa nada, se puede borrar tambien con sus scripts
            if (f.total > 0 && f.unused > 0 && !AssetDatabase.FindAssets("", new[] { top }).Select(AssetDatabase.GUIDToAssetPath).Any(used.Contains))
            {
                f.unused = f.total;
                f.unusedBytes = f.totalBytes;
                f.unusedFiles = new List<string> { top + "  (carpeta entera)" };
            }
            if (f.unused > 0) folders.Add(f);
        }
        folders = folders.OrderByDescending(f => f.unusedBytes).ToList();
        Debug.Log($"[LIMPIEZA] Analisis: {used.Count} assets en uso (escenas, Resources, URP y {toolPaths} rutas de las herramientas). " +
                  $"{folders.Count} carpetas con cosas sin usar, {Mb(folders.Sum(f => f.unusedBytes))} en total:\n" +
                  string.Join("\n", folders.Select(f => $"  {f.path}: {(f.Whole ? "ENTERA" : $"{f.unused}/{f.total}")} {Mb(f.unusedBytes)}")));
    }

    static void AddObject(HashSet<string> roots, Object o)
    {
        if (o == null) return;
        string p = AssetDatabase.GetAssetPath(o);
        if (!string.IsNullOrEmpty(p)) roots.Add(p);
    }

    void TrashSelected()
    {
        var sel = folders.Where(f => f.selected).ToList();
        var paths = new List<string>();
        foreach (var f in sel)
        {
            if (f.Whole) paths.Add(f.path);
            else paths.AddRange(f.unusedFiles);
        }
        string summary = string.Join("\n", sel.Select(f => $"{f.path} ({(f.Whole ? "entera" : f.unused + " archivos")}, {Mb(f.unusedBytes)})"));
        if (!EditorUtility.DisplayDialog("Enviar a la papelera",
                $"Se enviaran a la papelera del sistema:\n\n{summary}\n\n¿Estas en una rama de git y con la escena guardada?", "Si, enviar", "Cancelar"))
            return;
        var failed = new List<string>();
        AssetDatabase.MoveAssetsToTrash(paths.ToArray(), failed);
        AssetDatabase.Refresh();
        Debug.Log($"[LIMPIEZA] Enviados a la papelera: {paths.Count - failed.Count} elementos." +
                  (failed.Count > 0 ? $" No se pudieron mover {failed.Count}:\n" + string.Join("\n", failed) : "") +
                  " Abre la escena, dale a Play y comprueba que no sale nada rosa ni 'Missing'. Luego haz commit.");
        folders = null;
    }

    // ------------------------------------------------------------------ 2. escenas
    void OnlyGameScene()
    {
        if (!File.Exists(gameScene)) { Debug.LogError($"[LIMPIEZA] No existe {gameScene}."); return; }
        if (!EditorUtility.DisplayDialog("Escenas del build", $"Build Profiles > Scene List quedara solo con:\n{gameScene}\n\n(Cambia Project Settings)", "Si", "Cancelar")) return;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(gameScene, true) };
        Debug.Log($"[LIMPIEZA] Escenas del build: solo {gameScene}");
    }

    // ------------------------------------------------------------------ 3. logs
    static IEnumerable<MonoBehaviour> LogBehaviours(out List<GameObject> prefabRoots)
    {
        prefabRoots = new List<GameObject>();
        var list = new List<MonoBehaviour>();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            foreach (var root in EditorSceneManager.GetSceneAt(i).GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<MonoBehaviour>(true));
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { SetupToolUtils.PrefabFolder }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
            if (go == null) continue;
            prefabRoots.Add(go);
            list.AddRange(go.GetComponentsInChildren<MonoBehaviour>(true));
        }
        return list.Where(b => b != null && new SerializedObject(b).FindProperty("debugLogs") != null);
    }

    void CountLogs()
    {
        var all = LogBehaviours(out _).ToList();
        logComponents = all.Count;
        logOn = all.Count(b => new SerializedObject(b).FindProperty("debugLogs").boolValue);
    }

    void SetLogs(bool on)
    {
        int n = 0;
        var behaviours = LogBehaviours(out var prefabs).ToList();
        foreach (var b in behaviours)
        {
            var so = new SerializedObject(b);
            var p = so.FindProperty("debugLogs");
            if (p.boolValue == on) continue;
            p.boolValue = on;
            so.ApplyModifiedProperties();   // con Undo
            EditorUtility.SetDirty(b);
            n++;
        }
        foreach (var go in prefabs) EditorUtility.SetDirty(go);
        AssetDatabase.SaveAssets();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetSceneAt(i));
        CountLogs();
        Debug.LogWarning($"[LIMPIEZA] 'debugLogs' {(on ? "encendido" : "apagado")} en {n} componentes (escena y Assets/Prefabs). Guarda la escena (Ctrl+S).");
    }

    void StackTraces()
    {
        if (!EditorUtility.DisplayDialog("Traza de pila", "Los Debug.Log normales no guardaran la traza de pila (mas rapido en el casco). " +
                                         "Warnings: solo scripts. Errores: completa.\n\n(Cambia Project Settings > Player)", "Si", "Cancelar")) return;
        PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
        Debug.Log("[LIMPIEZA] Traza de pila: Log = None, Warning = ScriptOnly, Error = sin cambios");
    }

    // ------------------------------------------------------------------ 4. texturas
    void FindBigTextures()
    {
        var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
        if (File.Exists(gameScene) && !roots.Contains(gameScene)) roots.Add(gameScene);
        bigTextures = new List<(string, int, long)>();
        foreach (var p in AssetDatabase.GetDependencies(roots.ToArray(), true))
        {
            if (!(AssetImporter.GetAtPath(p) is TextureImporter imp)) continue;
            var tex = AssetDatabase.LoadAssetAtPath<Texture>(p);
            if (tex == null) continue;
            var android = imp.GetPlatformTextureSettings("Android");
            int max = android.overridden ? android.maxTextureSize : imp.maxTextureSize;
            int size = Mathf.Min(Mathf.Max(tex.width, tex.height), max);
            if (size > 2048) bigTextures.Add((p, size, new FileInfo(p).Length));
        }
        bigTextures = bigTextures.OrderByDescending(t => t.bytes).ToList();
        Debug.Log($"[LIMPIEZA] {bigTextures.Count} texturas de mas de 2048 px en el build:\n" + string.Join("\n", bigTextures.Select(t => $"  {t.size} px {Mb(t.bytes)} {t.path}")));
    }

    void LimitTextures()
    {
        if (!EditorUtility.DisplayDialog("Limitar texturas", $"{bigTextures.Count} texturas pasaran a maximo 2048 px en Android (ASTC 6x6). " +
                                         "Solo cambia sus Import Settings (en el editor de PC se ven igual). Tarda un rato en reimportar.", "Si", "Cancelar")) return;
        int n = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var t in bigTextures)
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(t.path);
                var s = imp.GetPlatformTextureSettings("Android");
                s.overridden = true;
                s.maxTextureSize = 2048;
                s.format = TextureImporterFormat.ASTC_6x6;
                imp.SetPlatformTextureSettings(s);
                imp.SaveAndReimport();
                n++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        Debug.Log($"[LIMPIEZA] {n} texturas limitadas a 2048 px en Android.");
        bigTextures = null;
    }
}
