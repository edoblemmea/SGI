using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// Herramienta de editor: menu "Isla Pirata > Totem narrador".
/// Pone un TotemMaldito con StoryTotem delante del punto de inicio (PlayerStart) o, si no hay, del ancla del muelle,
/// mirando al jugador: al acercarte, el capitan Calavera (atrapado en el totem) cuenta la historia.
public static class StoryTotemSetupTool
{
    const string TotemPath = SetupToolUtils.PrefabFolder + "/TotemMaldito.prefab";
    const string SoundFolder = "Assets/AS_Casual_Island_Game_Sounds_FREE";

    [MenuItem("Isla Pirata/Totem narrador")]
    static void Create()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TotemPath);
        if (prefab == null) { Debug.LogError($"[TOTEM] No existe {TotemPath}: crealo antes con Isla Pirata > Maldicion."); return; }

        // Donde empieza el jugador: PlayerStart, o el ancla del muelle (Teleporting -> Disembark, fuera de la barca)
        var ps = Object.FindAnyObjectByType<PlayerStart>(FindObjectsInactive.Include);
        Transform start = ps != null ? ps.transform : null;
        if (start == null)
        {
            var boat = Object.FindAnyObjectByType<BoatRowing>(FindObjectsInactive.Include);
            var dock = Object.FindObjectsByType<TeleportationAnchor>(FindObjectsInactive.Include).FirstOrDefault(a =>
                boat != null && !a.transform.IsChildOf(boat.transform) &&
                Enumerable.Range(0, a.teleporting.GetPersistentEventCount()).Any(i => a.teleporting.GetPersistentMethodName(i) == "Disembark"));
            if (dock != null) start = dock.transform;
        }
        if (start == null) { Debug.LogError("[TOTEM] No encuentro el punto de inicio (PlayerStart) ni el ancla del muelle. Selecciona donde ponerlo y usa Add Component > Story Totem."); return; }

        var old = Object.FindAnyObjectByType<StoryTotem>(FindObjectsInactive.Include);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        Vector3 f = Vector3.ProjectOnPlane(start.forward, Vector3.up).normalized;
        if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
        Vector3 pos = start.position + f * 2.5f;
        Physics.SyncTransforms();
        var hit = Physics.RaycastAll(pos + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).FirstOrDefault();
        if (hit.collider != null) pos = hit.point;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Totem narrador");
        go.name = "Totem narrador";
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-f, Vector3.up));   // mirando al jugador

        var story = Undo.AddComponent<StoryTotem>(go);
        var audio = go.GetComponent<AudioSource>();
        if (audio == null) audio = Undo.AddComponent<AudioSource>(go);
        audio.playOnAwake = false;
        audio.spatialBlend = 0.8f;
        story.audioSource = audio;
        story.pageClip = AssetDatabase.FindAssets("Button_3 t:AudioClip", new[] { SoundFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(c => c != null && c.name == "Button_3");

        Selection.activeObject = go;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log($"[TOTEM] Totem narrador colocado a 2,5 m de '{start.name}', mirandote. Los textos se cambian en Story Totem > Pages. Guarda (Ctrl+S).");
    }
}
