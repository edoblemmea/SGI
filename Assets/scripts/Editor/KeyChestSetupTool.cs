using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Herramienta de editor: menu "Isla Pirata > Llave y cofre".
/// 1) Prepara la llave (Rigidbody + XR Grab Interactable + KeyItem).
/// 2) La esconde en la copa de una palmera sacudible (ShakeablePalm.hiddenKey).
/// 3) Prepara el cofre: lo mete en un objeto 'Cofre' con ChestLock, una cerradura (XR Socket Interactor) encima,
///    el modelo abierto (chest_open) y las monedas (coins) de recompensa.
public class KeyChestSetupTool : EditorWindow
{
    const string ChestOpenPath = "Assets/ChestFree/chest_open.prefab";
    const string CoinsPath = "Assets/ChestFree/coins.prefab";
    const string KeyId = "cofre";

    GameObject key;
    ShakeablePalm palm;
    int afterCoconuts = 2;
    GameObject chest;

    [MenuItem("Isla Pirata/Llave y cofre")]
    static void Open() => GetWindow<KeyChestSetupTool>("Llave y cofre");

    void OnGUI()
    {
        if (key == null)
        {
            var k = FindAnyObjectByType<KeyItem>(FindObjectsInactive.Include);
            if (k != null) key = k.gameObject;
        }

        EditorGUILayout.LabelField("1. Llave", EditorStyles.boldLabel);
        key = (GameObject)EditorGUILayout.ObjectField("Llave (rust_key de la escena)", key, typeof(GameObject), true);
        using (new EditorGUI.DisabledScope(key == null))
            if (GUILayout.Button("Preparar llave")) PrepareKey();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Esconderla en una palmera", EditorStyles.boldLabel);
        var palms = FindObjectsByType<ShakeablePalm>(FindObjectsInactive.Exclude);
        if (palms.Length == 0)
            EditorGUILayout.HelpBox("No hay palmeras sacudibles en la escena: colocalas con Isla Pirata > Palmeras sacudibles.", MessageType.Warning);
        palm = (ShakeablePalm)EditorGUILayout.ObjectField("Palmera", palm, typeof(ShakeablePalm), true);
        afterCoconuts = EditorGUILayout.IntSlider("Cae despues de N cocos", afterCoconuts, 0, 8);
        using (new EditorGUI.DisabledScope(key == null || palms.Length == 0))
        {
            if (GUILayout.Button("Esconder en la palmera elegida") && palm != null) Hide(palm, palms);
            if (GUILayout.Button("Esconder en una palmera al azar")) Hide(palms[Random.Range(0, palms.Length)], palms);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Cofre", EditorStyles.boldLabel);
        chest = (GameObject)EditorGUILayout.ObjectField("Cofre cerrado (chest_close)", chest, typeof(GameObject), true);
        using (new EditorGUI.DisabledScope(chest == null))
            if (GUILayout.Button("Preparar cofre")) PrepareChest();
    }

    // ------------------------------------------------------------------ 1
    void PrepareKey()
    {
        if (EditorUtility.IsPersistent(key)) { Debug.LogError("[LLAVE] Arrastra la llave de la ESCENA (Hierarchy), no la del Project."); return; }
        var rb = GetOrAddUndo<Rigidbody>(key);
        Undo.RecordObject(rb, "Llave");
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // que no atraviese el suelo al caer
        if (key.GetComponentInChildren<Collider>() == null) Undo.AddComponent<BoxCollider>(key);
        var grab = GetOrAddUndo<XRGrabInteractable>(key);
        Undo.RecordObject(grab, "Llave");
        grab.throwOnDetach = true;
        var item = GetOrAddUndo<KeyItem>(key);
        Undo.RecordObject(item, "Llave");
        item.keyId = KeyId;
        item.objectiveOnGrab = "llave";
        Debug.Log($"[LLAVE] '{key.name}' lista: Rigidbody + XR Grab Interactable + KeyItem (id '{KeyId}').");
    }

    // No usar '??' con componentes de Unity: un componente que falta no siempre es null de verdad
    static T GetOrAddUndo<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    // ------------------------------------------------------------------ 2
    void Hide(ShakeablePalm target, ShakeablePalm[] all)
    {
        if (key.GetComponent<KeyItem>() == null) PrepareKey();
        foreach (var p in all.Where(p => p.hiddenKey == key.transform && p != target))
        {
            Undo.RecordObject(p, "Llave");
            p.hiddenKey = null;
        }
        Undo.RecordObject(target, "Llave");
        target.hiddenKey = key.transform;
        target.dropKeyAfterCoconuts = afterCoconuts;
        palm = target;

        var gm = FindAnyObjectByType<GameManager>();
        var obj = gm != null ? gm.objectives.Find(o => o.id == "llave") : null;
        if (obj != null)
        {
            Undo.RecordObject(gm, "Objetivo llave");
            obj.description = "Sacude las palmeras y encuentra la llave";
        }

        Selection.activeObject = target.gameObject;
        EditorGUIUtility.PingObject(target.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        Debug.Log($"[LLAVE] Escondida en '{target.name}'. Al dar a Play subira sola a su copa y caera tras {afterCoconuts} cocos. Guarda (Ctrl+S).");
    }

    // ------------------------------------------------------------------ 3
    void PrepareChest()
    {
        if (EditorUtility.IsPersistent(chest)) { Debug.LogError("[COFRE] Arrastra el cofre de la ESCENA (Hierarchy), no el del Project."); return; }
        if (chest.GetComponentInParent<ChestLock>() != null) { Debug.Log("[COFRE] Este cofre ya tiene ChestLock."); Selection.activeObject = chest.GetComponentInParent<ChestLock>().gameObject; return; }

        Transform t = chest.transform;
        Bounds b = SetupToolUtils.RenderBounds(chest);

        // Raiz nueva 'Cofre' en la posicion del cofre; el cerrado y el abierto cuelgan de ella
        var root = new GameObject("Cofre");
        Undo.RegisterCreatedObjectUndo(root, "Cofre");
        root.transform.SetPositionAndRotation(t.position, t.rotation);
        Undo.SetTransformParent(t, root.transform, "Cofre");

        GameObject open = null;
        var openPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestOpenPath);
        if (openPrefab != null)
        {
            open = (GameObject)PrefabUtility.InstantiatePrefab(openPrefab, root.transform);
            open.transform.SetLocalPositionAndRotation(t.localPosition, t.localRotation);
            open.transform.localScale = t.localScale;
            open.AddComponent<BoxCollider>();   // que siga siendo solido abierto
            Undo.RegisterCreatedObjectUndo(open, "Cofre");
        }
        else Debug.LogWarning($"[COFRE] No encuentro {ChestOpenPath}: el cofre no cambiara de aspecto.");

        GameObject coins = null;
        var coinsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinsPath);
        if (coinsPrefab != null)
        {
            coins = (GameObject)PrefabUtility.InstantiatePrefab(coinsPrefab, root.transform);
            coins.transform.SetLocalPositionAndRotation(t.localPosition, t.localRotation);
            coins.transform.localScale = t.localScale;
            Undo.RegisterCreatedObjectUndo(coins, "Cofre");
        }

        // Cerradura: encima de la tapa (dejar la llave sobre el cofre la encaja)
        var lockGo = new GameObject("Cerradura");
        Undo.RegisterCreatedObjectUndo(lockGo, "Cofre");
        lockGo.transform.SetParent(root.transform, false);
        lockGo.transform.position = new Vector3(b.center.x, b.max.y + 0.08f, b.center.z);
        var sphere = lockGo.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.6f, 0.15f, 0.35f);
        var socket = lockGo.AddComponent<XRSocketInteractor>();

        var chestLock = root.AddComponent<ChestLock>();
        chestLock.socket = socket;
        chestLock.requiredKeyId = KeyId;
        chestLock.closedModel = chest;
        chestLock.openModel = open;
        chestLock.reward = coins;
        chestLock.objectiveId = "cofre";
        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        chestLock.audioSource = audio;

        Selection.activeObject = root;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[COFRE] Cofre listo: 'Cofre' > {chest.name} (cerrado), {(open != null ? open.name : "-")} (abierto), " +
                  $"{(coins != null ? coins.name : "-")} (monedas), Cerradura (socket de radio {sphere.radius:F2} m encima de la tapa). Guarda (Ctrl+S).");
    }
}
