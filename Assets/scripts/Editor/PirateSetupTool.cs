using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// Herramienta de editor: menu "Isla Pirata > Esqueletos piratas".
/// Crea Assets/Prefabs/Esqueleto.prefab con el asset DungeonCharacters (la tripulacion maldita del barco fantasma):
/// NavMeshAgent + Health + EnemyAI + SkeletonCrumble (se desmorona al morir o al romperse la maldicion) y un
/// Animator propio con idle / walk / attack. Los coloca en la cubierta y quita los antiguos piratas lenadores.
public class PirateSetupTool : EditorWindow
{
    const string PrefabPath = SetupToolUtils.PrefabFolder + "/Esqueleto.prefab";
    const string ControllerPath = SetupToolUtils.PrefabFolder + "/EsqueletoAnimator.controller";
    const string DefaultModel = "Assets/DungeonCharacters/Skeletons_demo/DungeonSkeleton_demoPrefab.prefab";
    const string AnimFolder = "Assets/DungeonCharacters/Skeletons_demo/animation";
    const string OldPiratePrefab = SetupToolUtils.PrefabFolder + "/Pirata.prefab";
    const string OldPirateController = SetupToolUtils.PrefabFolder + "/PirataAnimator.controller";

    // Huesos del bipedo (Bip001) para el muneco de trapo, en orden: la pelvis primero
    static readonly string[] RagdollBones =
        { "Pelvis", "Spine", "Spine1", "Head", "L UpperArm", "L Forearm", "R UpperArm", "R Forearm", "L Thigh", "L Calf", "R Thigh", "R Calf" };

    GameObject model;
    float maxHealth = 50f;
    float damage = 15f;
    float windup = 0.4f;
    float chaseSpeed = 2.5f;
    string objectiveOnDeath = "";
    int count = 3;
    float radius = 3f;
    Transform ship;

    [MenuItem("Isla Pirata/Esqueletos piratas")]
    static void Open() => GetWindow<PirateSetupTool>("Esqueletos");

    void OnEnable()
    {
        if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModel);
    }

    void OnGUI()
    {
        if (!SetupToolUtils.HasNavMesh())
            EditorGUILayout.HelpBox("No hay NavMesh horneado: pulsa Bake en el objeto 'NavMesh' (la cubierta del barco debe salir azul).", MessageType.Warning);

        EditorGUILayout.LabelField("1. Prefab de esqueleto", EditorStyles.boldLabel);
        model = (GameObject)EditorGUILayout.ObjectField("Modelo", model, typeof(GameObject), false);
        maxHealth = EditorGUILayout.FloatField("Vida", maxHealth);
        damage = EditorGUILayout.FloatField("Dano por espadazo", damage);
        windup = EditorGUILayout.FloatField("Tiempo levantando la espada (s)", windup);
        chaseSpeed = EditorGUILayout.FloatField("Velocidad persiguiendo", chaseSpeed);
        objectiveOnDeath = EditorGUILayout.TextField(new GUIContent("Objetivo al morir", "p. ej. 'esqueletos' (anadelo al GameManager). Vacio = ninguno"), objectiveOnDeath);
        if (GUILayout.Button("Crear / actualizar prefab Esqueleto")) CreatePrefab();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Colocar en el barco fantasma", EditorStyles.boldLabel);
        if (ship == null) ship = GuessShip();
        ship = (Transform)EditorGUILayout.ObjectField("Barco fantasma", ship, typeof(Transform), true);
        EditorGUILayout.HelpBox("Se colocan sobre el NavMesh alrededor del cofre (o del centro del barco). Si seleccionas un objeto en la Hierarchy, alrededor de el.", MessageType.None);
        count = EditorGUILayout.IntSlider("Cantidad", count, 1, 8);
        radius = EditorGUILayout.FloatField("Radio (m)", radius);
        using (new EditorGUI.DisabledScope(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null))
            if (GUILayout.Button("Colocar esqueletos")) Place();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Limpieza", EditorStyles.boldLabel);
        if (GUILayout.Button("Quitar los piratas lenadores antiguos")) RemoveOldPirates();
    }

    static Transform GuessShip()
    {
        // Solo en el editor: propone la raiz que contiene StylShip_Body (se puede cambiar a mano)
        var part = FindObjectsByType<Transform>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name.StartsWith("StylShip_Body"));
        return part != null ? part.root : null;
    }

    // ------------------------------------------------------------------ 1
    void CreatePrefab()
    {
        if (model == null) { Debug.LogError("[ESQUELETOS] Elige el modelo (DungeonSkeleton_demoPrefab)."); return; }
        SetupToolUtils.EnsurePrefabFolder();

        // Animaciones: estan en FBX separados (idle, walk, attack)
        var fbxs = Directory.Exists(AnimFolder)
            ? AssetDatabase.FindAssets("t:Model", new[] { AnimFolder }).Select(AssetDatabase.GUIDToAssetPath).ToArray()
            : new string[0];
        foreach (var f in fbxs) SetupToolUtils.SetLoopingClips(f, "idle", "walk");
        var clips = fbxs.SelectMany(SetupToolUtils.Clips).ToArray();
        Debug.Log($"[ESQUELETOS] Animaciones encontradas: {string.Join(", ", clips.Select(c => c.name))}");
        var controller = clips.Length == 0 ? null : SetupToolUtils.BuildEnemyController(ControllerPath,
            SetupToolUtils.FindClip(clips, "idle") ?? clips[0],
            SetupToolUtils.FindClip(clips, "walk", "run"),
            SetupToolUtils.FindClip(clips, "attack"),
            SetupToolUtils.FindClip(clips, "death", "die", "dead"));

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Esqueleto";
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var legacy in inst.GetComponentsInChildren<Animation>(true)) DestroyImmediate(legacy);

            var animator = inst.GetComponentInChildren<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.applyRootMotion = false;   // lo mueve el NavMeshAgent
            if (controller != null) animator.runtimeAnimatorController = controller;

            Bounds b = SetupToolUtils.RenderBounds(inst);
            float s = Mathf.Max(Mathf.Abs(inst.transform.lossyScale.y), 0.0001f);

            var col = SetupToolUtils.GetOrAdd<CapsuleCollider>(inst);
            col.direction = 1;
            col.height = b.size.y / s;
            col.radius = 0.3f / s;
            col.center = new Vector3(0f, col.height * 0.5f, 0f);

            var rb = SetupToolUtils.GetOrAdd<Rigidbody>(inst);
            rb.isKinematic = true;
            rb.useGravity = false;

            var agent = SetupToolUtils.GetOrAdd<NavMeshAgent>(inst);
            agent.radius = 0.3f / s;
            agent.height = b.size.y / s;
            agent.baseOffset = 0f;
            agent.angularSpeed = 360f;
            agent.acceleration = 10f;

            var health = SetupToolUtils.GetOrAdd<Health>(inst);
            health.maxHealth = maxHealth;
            health.destroyOnDeath = false;   // lo destruye SkeletonCrumble tras desmoronarse

            var audio = SetupToolUtils.GetOrAdd<AudioSource>(inst);
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;

            var ai = SetupToolUtils.GetOrAdd<EnemyAI>(inst);
            ai.animator = animator;
            ai.attackRange = 1.6f;
            ai.attackDamage = damage;
            ai.attackWindup = windup;
            ai.attackCooldown = 1.8f;
            ai.detectRange = 8f;
            ai.loseRange = 14f;
            ai.patrolSpeed = 1f;
            ai.chaseSpeed = chaseSpeed;
            ai.wanderRadius = 3f;     // en cubierta no hay mucho sitio
            ai.objectiveOnDeath = objectiveOnDeath;
            ai.fallOverIfNoDieAnim = false;   // se desmorona con SkeletonCrumble
            ai.audioSource = audio;
            agent.stoppingDistance = ai.attackRange * 0.8f;

            var crumble = SetupToolUtils.GetOrAdd<SkeletonCrumble>(inst);
            var all = inst.GetComponentsInChildren<Transform>(true);
            crumble.bones = RagdollBones
                .Select(n => all.FirstOrDefault(t => t.name == "Bip001 " + n || t.name.EndsWith(" " + n)))
                .Where(t => t != null).Distinct().ToArray();
            crumble.audioSource = audio;

            PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
            Debug.Log($"[ESQUELETOS] Prefab guardado en {PrefabPath}. Altura {b.size.y:F2} m, {crumble.bones.Length}/{RagdollBones.Length} huesos para desmoronarse. " +
                      "Prueba: arrastralo a la escena, Play, y clic derecho en SkeletonCrumble > 'Desmoronar ahora'.");
            if (b.size.y < 1f || b.size.y > 3f) Debug.LogWarning("[ESQUELETOS] La altura es rara: revisa la escala del modelo.");
        }
        finally
        {
            DestroyImmediate(inst);
        }
    }

    // ------------------------------------------------------------------ 2
    void Place()
    {
        if (!SetupToolUtils.TryGetPlayer(out Transform head, out Health playerHealth)) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Vector3 center;
        if (Selection.activeTransform != null) center = Selection.activeTransform.position;
        else
        {
            var chest = FindAnyObjectByType<ChestLock>(FindObjectsInactive.Include);
            center = chest != null ? chest.transform.position
                   : ship != null ? SetupToolUtils.RenderBounds(ship.gameObject).center
                   : SetupToolUtils.PlacementCenter(head.position);
        }
        var group = SetupToolUtils.PlaceAround(prefab, "Esqueletos", center, count, radius, go =>
        {
            var ai = go.GetComponent<EnemyAI>();
            ai.target = head;
            ai.targetHealth = playerHealth;
        });
        if (ship != null) Undo.SetTransformParent(group.transform, ship, "Esqueletos");
    }

    // ------------------------------------------------------------------ 3
    static void RemoveOldPirates()
    {
        var old = AssetDatabase.LoadAssetAtPath<GameObject>(OldPiratePrefab);
        var instances = old == null ? new GameObject[0] :
            FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Select(t => t.gameObject)
                .Where(g => PrefabUtility.GetNearestPrefabInstanceRoot(g) == g &&
                            PrefabUtility.GetCorrespondingObjectFromSource(g) == old)
                .ToArray();
        if (!EditorUtility.DisplayDialog("Quitar piratas lenadores",
                $"Se borraran {instances.Length} lenadores de la escena abierta" +
                (old != null ? $" y los ficheros {OldPiratePrefab} y {OldPirateController}" : "") + ".\n\nLa escena se puede deshacer con Ctrl+Z; los ficheros no.",
                "Quitar", "Cancelar"))
            return;

        foreach (var g in instances) Undo.DestroyObjectImmediate(g);
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t != null && t.name == "Piratas" && t.childCount == 0))
            Undo.DestroyObjectImmediate(t.gameObject);
        if (instances.Length > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        AssetDatabase.DeleteAsset(OldPiratePrefab);
        AssetDatabase.DeleteAsset(OldPirateController);
        Debug.Log($"[ESQUELETOS] Quitados {instances.Length} lenadores de la escena y sus ficheros. Guarda la escena (Ctrl+S). " +
                  "La carpeta Assets/FreeCharacter ya no se usa: puedes borrarla si nadie la necesita.");
    }
}
