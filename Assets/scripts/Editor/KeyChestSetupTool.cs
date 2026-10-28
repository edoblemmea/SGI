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
        if (GUILayout.Button(new GUIContent("Poner la cerradura en el candado", "Mueve 'Cerradura' (donde encaja la llave) al candado del frente del cofre, en vez de encima de la tapa")))
            MoveLockToPadlock();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("4. Bolsillo para la llave", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Para llevar la llave mientras remas o trepas: se suelta junto a la cadera derecha y se queda ahi. " +
                                "Si cae al agua vuelve sola al bolsillo.", MessageType.None);
        using (new EditorGUI.DisabledScope(key == null))
            if (GUILayout.Button("Crear / actualizar bolsillo")) CreatePocket();
    }

    void CreatePocket()
    {
        var grab = key.GetComponent<XRGrabInteractable>();
        if (grab == null) { PrepareKey(); grab = key.GetComponent<XRGrabInteractable>(); }
        var pocket = FindAnyObjectByType<KeyPocket>(FindObjectsInactive.Include);
        if (pocket == null)
        {
            var go = new GameObject("Bolsillo llave");
            Undo.RegisterCreatedObjectUndo(go, "Bolsillo");
            pocket = go.AddComponent<KeyPocket>();
        }
        Undo.RecordObject(pocket, "Bolsillo");
        pocket.key = grab;
        var cam = Camera.main;
        if (cam != null) pocket.head = cam.transform;
        // DEMO: el agua no tiene collider; su altura ~ el fondo del casco de la barca de remos
        var boat = FindAnyObjectByType<BoatRowing>(FindObjectsInactive.Include);
        if (boat != null) pocket.lostBelowY = SetupToolUtils.RenderBounds(boat.gameObject).min.y - 0.3f;
        else Debug.LogWarning("[BOLSILLO] No hay barca: pon a mano 'Lost Below Y' (un poco por debajo del agua).");
        Selection.activeObject = pocket.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pocket.gameObject.scene);
        Debug.Log($"[BOLSILLO] Bolsillo listo para '{key.name}'. La llave vuelve sola si baja de y={pocket.lostBelowY:F2}. Guarda (Ctrl+S).");
    }

    // Busca el candado en la malla del cofre cerrado: en el lado donde lo que mas sobresale esta centrado (el candado),
    // a diferencia de los lados planos o con esquinas. Devuelve el punto en el espacio de 'frame' y la direccion hacia fuera.
    static bool FindPadlock(GameObject model, Transform frame, out Vector3 localPoint, out Vector3 localOut)
    {
        localPoint = Vector3.zero; localOut = Vector3.forward;
        var pts = new System.Collections.Generic.List<Vector3>();
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            foreach (var v in mf.sharedMesh.vertices) pts.Add(frame.InverseTransformPoint(mf.transform.TransformPoint(v)));
        }
        if (pts.Count == 0) return false;
        Vector3 min = pts[0], max = pts[0];
        foreach (var q in pts) { min = Vector3.Min(min, q); max = Vector3.Max(max, q); }
        Vector3 size = max - min, mid = (min + max) * 0.5f;
        float best = float.MaxValue;
        foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
        {
            Vector3 side = Vector3.Cross(Vector3.up, d);
            float half = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(side.x), 0f, Mathf.Abs(side.z)))) * 0.5f;
            float top = pts.Max(q => Vector3.Dot(q, d));
            var front = pts.Where(q => Vector3.Dot(q, d) > top - Mathf.Max(size.x, size.z) * 0.02f).ToList();
            float spread = front.Average(q => Mathf.Abs(Vector3.Dot(q - mid, side))) / Mathf.Max(half, 0.001f);
            if (spread < best)
            {
                best = spread;
                localOut = d;
                Vector3 c = Vector3.zero;
                foreach (var q in front) c += q;
                localPoint = c / front.Count;
            }
        }
        return true;
    }

    void MoveLockToPadlock()
    {
        var cl = FindAnyObjectByType<ChestLock>(FindObjectsInactive.Include);
        if (cl == null || cl.socket == null || cl.closedModel == null) { Debug.LogError("[COFRE] No hay cofre preparado (ChestLock con Socket y Closed Model). Pulsa antes 'Preparar cofre'."); return; }
        if (!FindPadlock(cl.closedModel, cl.transform, out Vector3 lp, out Vector3 lo)) { Debug.LogError("[COFRE] El cofre cerrado no tiene mallas."); return; }
        var lockT = cl.socket.transform;
        Undo.RecordObject(lockT, "Cerradura");
        lockT.position = cl.transform.TransformPoint(lp + lo * 0.04f);   // un poco por delante del candado
        lockT.rotation = Quaternion.LookRotation(cl.transform.TransformDirection(-lo), cl.transform.up);   // la llave entra hacia el cofre
        var sphere = lockT.GetComponent<SphereCollider>();
        if (sphere != null) { Undo.RecordObject(sphere, "Cerradura"); sphere.radius = 0.12f / Mathf.Max(lockT.lossyScale.x, 0.0001f); sphere.center = Vector3.zero; }
        Selection.activeObject = lockT.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lockT.gameObject.scene);
        Debug.Log($"[COFRE] Cerradura movida al candado (lado {lo} del cofre, radio 0,12 m). Mira en la Scene que la esfera quede sobre el candado; si no, muevela a mano. Guarda (Ctrl+S).");
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
        // Un Mesh Collider concavo no funciona con un Rigidbody dinamico: Unity lo ignora y la llave atraviesa el suelo
        foreach (var mc in key.GetComponentsInChildren<MeshCollider>(true).Where(m => !m.convex))
        {
            Undo.RecordObject(mc, "Llave");
            mc.convex = true;
            Debug.Log($"[LLAVE] Mesh Collider de '{mc.name}' puesto en Convex (si no, Unity lo ignora con un Rigidbody dinamico).");
        }
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
        // Solo en el editor: avisar si hay mas de una llave (p. ej. una copia de rust_key tirada en el suelo)
        var others = FindObjectsByType<Transform>(FindObjectsInactive.Include)
            .Where(t => t.gameObject != key && (t.GetComponent<KeyItem>() != null || t.name.StartsWith("rust_key")) && !t.IsChildOf(key.transform))
            .Select(t => t.name).ToArray();
        if (others.Length > 0)
            Debug.LogWarning($"[LLAVE] Hay otras llaves en la escena ademas de '{key.name}': {string.Join(", ", others)}. Si sobran, borralas.");
        foreach (var p in all.Where(p => p.hiddenKey == key.transform && p != target))
        {
            Undo.RecordObject(p, "Llave");
            p.hiddenKey = null;
        }
        Undo.RecordObject(target, "Llave");
        target.hiddenKey = key.transform;
        target.dropKeyAfterCoconuts = afterCoconuts;
        // En Play la llave sube sola a la copa; la muevo ya en el editor para que no se quede tirada donde estaba
        if (target.crown != null)
        {
            Undo.RecordObject(key.transform, "Llave");
            key.transform.position = target.crown.position + Vector3.down * 0.2f;
        }
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
