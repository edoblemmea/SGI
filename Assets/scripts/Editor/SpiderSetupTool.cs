using System;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// Herramienta de editor: menu "Isla Pirata > Aranas".
/// Crea el NavMesh, un prefab de arana propio (Assets/Prefabs/Arana.prefab) con NavMeshAgent, Health y EnemyAI,
/// su Animator Controller, y coloca aranas en la escena ya conectadas al jugador.
/// No modifica objetos de la demo (solo anade Health/PlayerDamageFeedback al XR Origin, que es nuestro).
public class SpiderSetupTool : EditorWindow
{
    const string PrefabFolder = "Assets/Prefabs";
    const string PrefabPath = PrefabFolder + "/Arana.prefab";
    const string ControllerPath = PrefabFolder + "/AranaAnimator.controller";
    const string DefaultModel = "Assets/Spiders - characters with animations/Spiders - characters with animations/Models/Prefabs/tarantula.prefab";
    const string NavSurfaceType = "Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation";

    GameObject model;
    int count = 4;
    float radius = 6f;
    float maxHealth = 30f;
    float damage = 10f;
    float chaseSpeed = 4f;

    [MenuItem("Isla Pirata/Aranas")]
    static void Open() => GetWindow<SpiderSetupTool>("Aranas");

    void OnEnable()
    {
        if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModel);
    }

    void OnGUI()
    {
        Type surfaceType = Type.GetType(NavSurfaceType);

        EditorGUILayout.LabelField("1. NavMesh (suelo por el que andan)", EditorStyles.boldLabel);
        if (surfaceType == null)
        {
            EditorGUILayout.HelpBox("Falta el paquete AI Navigation.", MessageType.Warning);
            if (GUILayout.Button("Instalar AI Navigation")) InstallNavigation();
        }
        else
        {
            bool baked = NavMesh.CalculateTriangulation().vertices.Length > 0;
            EditorGUILayout.HelpBox(baked ? "NavMesh horneado." : "NavMesh SIN hornear.", baked ? MessageType.Info : MessageType.Warning);
            if (GUILayout.Button("Crear / seleccionar NavMesh Surface")) CreateSurface(surfaceType);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Prefab de arana", EditorStyles.boldLabel);
        model = (GameObject)EditorGUILayout.ObjectField("Modelo", model, typeof(GameObject), false);
        maxHealth = EditorGUILayout.FloatField("Vida", maxHealth);
        damage = EditorGUILayout.FloatField("Dano por mordisco", damage);
        chaseSpeed = EditorGUILayout.FloatField("Velocidad persiguiendo", chaseSpeed);
        if (GUILayout.Button("Crear / actualizar prefab Arana")) CreatePrefab();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Colocar en la escena abierta", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Se colocan alrededor del objeto seleccionado en la Hierarchy o, si no hay ninguno, " +
                                "alrededor del centro de la Scene view.", MessageType.None);
        count = EditorGUILayout.IntSlider("Cantidad", count, 1, 20);
        radius = EditorGUILayout.FloatField("Radio (m)", radius);
        using (new EditorGUI.DisabledScope(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null))
            if (GUILayout.Button("Colocar aranas")) PlaceSpiders();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("4. Nidos en rocas (las aranas salen de las rocas)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("A) Selecciona una o varias rocas en la Hierarchy y pulsa 'Convertir seleccionadas'.\n" +
                                "B) O pulsa 'Colocar rocas con nido' para poner rocas de RockFREE alrededor de la Scene view.\n" +
                                "Despues vuelve a pulsar Bake en el NavMesh (las rocas nuevas lo cambian).", MessageType.None);
        rocksToPlace = EditorGUILayout.IntSlider("Rocas a colocar", rocksToPlace, 1, 10);
        nestSpiders = EditorGUILayout.IntSlider("Aranas por nido (total)", nestSpiders, 1, 10);
        using (new EditorGUI.DisabledScope(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null))
        {
            if (GUILayout.Button("Convertir rocas seleccionadas en nidos")) MakeNests(Selection.gameObjects.Where(g => !EditorUtility.IsPersistent(g)).ToArray());
            if (GUILayout.Button("Colocar rocas con nido")) PlaceRocks();
            if (GUILayout.Button("Quitar las aranas sueltas (grupo 'Aranas')")) RemoveLooseSpiders();
        }
    }

    int rocksToPlace = 4;
    int nestSpiders = 3;
    const string RockFolder = "Assets/RockFREE/LODGroups";

    // ------------------------------------------------------------------ 4. Nidos
    void PlaceRocks()
    {
        var rocks = AssetDatabase.FindAssets("t:Prefab", new[] { RockFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != null).ToArray();
        if (rocks.Length == 0) { Debug.LogError($"[ARANAS] No encuentro rocas en {RockFolder}."); return; }
        var terrain = Terrain.activeTerrain;   // DEMO: terreno de la isla
        Vector3 center = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        if (Selection.activeTransform != null) center = Selection.activeTransform.position;

        var group = new GameObject("Rocas con nido");
        Undo.RegisterCreatedObjectUndo(group, "Rocas");
        var placed = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < rocksToPlace; i++)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * radius * 2f;
            Vector3 p = center + new Vector3(r.x, 0f, r.y);
            if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y - 0.2f;   // un poco enterrada
            var rock = (GameObject)PrefabUtility.InstantiatePrefab(rocks[UnityEngine.Random.Range(0, rocks.Length)], group.transform);
            rock.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            rock.transform.localScale *= UnityEngine.Random.Range(1.2f, 2.2f);
            Undo.RegisterCreatedObjectUndo(rock, "Rocas");
            placed.Add(rock);
        }
        MakeNests(placed.ToArray());
        Selection.activeObject = group;
        Debug.Log($"[ARANAS] Colocadas {placed.Count} rocas con nido alrededor de {center}. Si alguna sale rosa, convierte sus materiales a URP. Pulsa Bake en el NavMesh.");
    }

    void MakeNests(GameObject[] rocks)
    {
        if (rocks.Length == 0) { Debug.LogWarning("[ARANAS] Selecciona primero una o varias rocas en la Hierarchy."); return; }
        if (!SetupToolUtils.TryGetPlayer(out Transform head, out Health playerHealth)) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var dayNight = FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
        var origin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);

        var groupT = FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == "Nidos de aranas" && t.parent == null);
        GameObject group = groupT != null ? groupT.gameObject : new GameObject("Nidos de aranas");
        if (groupT == null) Undo.RegisterCreatedObjectUndo(group, "Nidos");

        int n = 0;
        foreach (var rock in rocks)
        {
            Bounds b = SetupToolUtils.RenderBounds(rock);
            Vector3 c = b.center;
            // La salida mira hacia donde empieza el jugador (las aranas salen "a por ti")
            Vector3 dir = origin != null ? Vector3.ProjectOnPlane(origin.transform.position - c, Vector3.up) : Vector3.forward;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            float rad = Mathf.Max(b.extents.x, b.extents.z);
            Vector3 exit = c + dir * (rad + 0.6f);
            if (NavMesh.SamplePosition(exit, out NavMeshHit hit, 3f, NavMesh.AllAreas)) exit = hit.position;
            else
            {
                var t = Terrain.activeTerrain;
                if (t != null) exit.y = t.SampleHeight(exit) + t.transform.position.y;
            }

            var nestGo = new GameObject($"Nido {rock.name}");
            Undo.RegisterCreatedObjectUndo(nestGo, "Nido");
            nestGo.transform.SetParent(group.transform, false);
            nestGo.transform.position = new Vector3(c.x, exit.y, c.z);

            var hidden = new GameObject("Dentro de la roca").transform;
            hidden.SetParent(nestGo.transform, false);
            hidden.position = new Vector3(c.x, exit.y, c.z) + dir * (rad * 0.3f);
            var exitT = new GameObject("Salida").transform;
            exitT.SetParent(nestGo.transform, false);
            exitT.position = exit;
            exitT.rotation = Quaternion.LookRotation(dir, Vector3.up);

            var nest = nestGo.AddComponent<SpiderNest>();
            nest.spiderPrefab = prefab;
            nest.playerHead = head;
            nest.playerHealth = playerHealth;
            nest.hiddenPoint = hidden;
            nest.exitPoint = exitT;
            nest.totalSpiders = nestSpiders;
            nest.dayNight = dayNight;
            var audio = nestGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            nest.audioSource = audio;
            n++;
        }
        Selection.activeObject = group;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(group.scene);
        Debug.Log($"[ARANAS] {n} nidos creados en 'Nidos de aranas'. Selecciona uno para ver su radio (morado) y la linea por donde salen. " +
                  "Si la 'Salida' queda dentro de otra roca o en el agua, muevela. Guarda (Ctrl+S).");
    }

    static void RemoveLooseSpiders()
    {
        var groups = FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t != null && t.parent == null && t.name == "Aranas").ToArray();
        if (groups.Length == 0) { Debug.Log("[ARANAS] No hay grupo 'Aranas' en la escena."); return; }
        foreach (var g in groups) Undo.DestroyObjectImmediate(g.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[ARANAS] Quitado el grupo 'Aranas' ({groups.Length}). Ahora solo salen de los nidos. Ctrl+Z para deshacer.");
    }

    // ------------------------------------------------------------------ 1. NavMesh
    static void InstallNavigation()
    {
        if (!EditorUtility.DisplayDialog("Instalar AI Navigation",
                "Se instalara el paquete com.unity.ai.navigation desde el Package Manager. Unity recompilara. Despues vuelve a abrir esta ventana.",
                "Instalar", "Cancelar")) return;
        UnityEditor.PackageManager.Client.Add("com.unity.ai.navigation");
        Debug.Log("[ARANAS] Instalando AI Navigation... espera a que termine de compilar.");
    }

    static void CreateSurface(Type surfaceType)
    {
        var existing = FindObjectsByType(surfaceType, FindObjectsInactive.Include);
        Component surface = existing.Length > 0 ? (Component)existing[0] : null;
        if (surface == null)
        {
            var go = new GameObject("NavMesh");
            Undo.RegisterCreatedObjectUndo(go, "Crear NavMesh");
            surface = Undo.AddComponent(go, surfaceType);

            // Hornear desde COLLIDERS: el agua de la demo no tiene collider, asi las aranas no andan por el mar.
            // DEMO: el terreno usa Terrain Collider; el barco (capa Boat) y el jugador se excluyen.
            var so = new SerializedObject(surface);
            var geom = so.FindProperty("m_UseGeometry");
            if (geom != null) geom.intValue = (int)NavMeshCollectGeometry.PhysicsColliders;
            var mask = so.FindProperty("m_LayerMask");
            if (mask != null) mask.intValue = ~LayersMask("Player", "Boat", "UI", "Water", "Ignore Raycast", "TeleportAnchor");
            so.ApplyModifiedProperties();
            Debug.Log("[ARANAS] Creado 'NavMesh' (desde colliders, sin capas Player/Boat/Water/UI). Pulsa BAKE en su Inspector.");
        }
        Selection.activeObject = surface.gameObject;
        EditorGUIUtility.PingObject(surface.gameObject);
        EditorUtility.DisplayDialog("NavMesh", "Seleccionado 'NavMesh'. En su Inspector (NavMesh Surface) pulsa el boton BAKE.\n\n" +
                                               "Comprueba en la Scene view que la isla sale en azul y el mar NO.", "Vale");
    }

    static int LayersMask(params string[] names)
    {
        int m = 0;
        foreach (var n in names)
        {
            int l = LayerMask.NameToLayer(n);
            if (l >= 0) m |= 1 << l;
        }
        return m;
    }

    // ------------------------------------------------------------------ 2. Prefab
    void CreatePrefab()
    {
        if (model == null) { Debug.LogError("[ARANAS] Elige un modelo de arana."); return; }
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets", "Prefabs");

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            inst.name = "Arana";
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var animator = inst.GetComponentInChildren<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.applyRootMotion = false;
            var controller = BuildController(animator);
            if (controller != null) animator.runtimeAnimatorController = controller;

            // Tamano real de la malla, en espacio local del objeto raiz
            var rends = inst.GetComponentsInChildren<Renderer>();
            Bounds b = rends.Length > 0 ? rends[0].bounds : new Bounds(Vector3.zero, Vector3.one * 0.5f);
            foreach (var r in rends) b.Encapsulate(r.bounds);
            Vector3 s = inst.transform.lossyScale;
            float scaleXZ = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z), 0.0001f);
            float scaleY = Mathf.Max(Mathf.Abs(s.y), 0.0001f);
            float worldRadius = Mathf.Max(b.extents.x, b.extents.z);

            var col = GetOrAdd<CapsuleCollider>(inst);
            col.center = inst.transform.InverseTransformPoint(b.center);
            col.radius = worldRadius / scaleXZ;
            col.height = Mathf.Max(b.size.y / scaleY, col.radius * 2f);
            col.direction = 1;

            var rb = GetOrAdd<Rigidbody>(inst);
            rb.isKinematic = true;
            rb.useGravity = false;

            var agent = GetOrAdd<NavMeshAgent>(inst);
            agent.radius = Mathf.Max(0.1f, worldRadius * 0.8f) / scaleXZ;
            agent.height = Mathf.Max(0.2f, b.size.y) / scaleY;
            agent.baseOffset = 0f;
            agent.speed = 1.5f;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;

            var health = GetOrAdd<Health>(inst);
            health.maxHealth = maxHealth;
            health.destroyOnDeath = true;
            health.destroyDelay = 4f;

            var ai = GetOrAdd<EnemyAI>(inst);
            ai.animator = animator;
            ai.attackRange = Mathf.Max(1.2f, worldRadius + 0.8f);
            ai.attackDamage = damage;
            ai.attackWindup = 0f;
            ai.attackCooldown = 1.5f;
            ai.chaseSpeed = chaseSpeed;
            ai.patrolSpeed = 1.5f;
            ai.speedParam = "Speed";
            ai.attackTrigger = "Attack";
            ai.dieTrigger = "Die";
            agent.stoppingDistance = ai.attackRange * 0.8f;

            PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
            Debug.Log($"[ARANAS] Prefab guardado en {PrefabPath}. Tamano {b.size}, radio colision {worldRadius:F2} m, " +
                      $"alcance de ataque {ai.attackRange:F2} m. Animator: {(controller != null ? ControllerPath : "SIN controller")}");
        }
        finally
        {
            DestroyImmediate(inst);
        }
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static AnimatorController BuildController(Animator animator)
    {
        string fbx = animator.avatar != null ? AssetDatabase.GetAssetPath(animator.avatar) : null;
        if (string.IsNullOrEmpty(fbx))
        {
            var smr = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null) fbx = AssetDatabase.GetAssetPath(smr.sharedMesh);
        }
        if (string.IsNullOrEmpty(fbx)) { Debug.LogWarning("[ARANAS] No encuentro el FBX del modelo: la arana ira sin animaciones."); return null; }

        // idle y walk deben repetirse en bucle
        var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (importer != null)
        {
            var clipsDef = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            bool changed = importer.clipAnimations.Length == 0;
            foreach (var c in clipsDef)
            {
                bool loop = c.name.ToLower().Contains("idle") || c.name.ToLower().Contains("walk");
                if (c.loopTime != loop) { c.loopTime = loop; changed = true; }
            }
            if (changed && clipsDef.Length > 0)
            {
                importer.clipAnimations = clipsDef;
                importer.SaveAndReimport();
                Debug.Log($"[ARANAS] '{fbx}': idle/walk marcados como Loop Time.");
            }
        }

        var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        Debug.Log($"[ARANAS] Animaciones en '{fbx}': {string.Join(", ", clips.Select(c => c.name))}");
        if (clips.Length == 0) return null;

        AnimationClip Find(params string[] keys) =>
            keys.Select(k => clips.FirstOrDefault(c => c.name.ToLower() == k)).FirstOrDefault(c => c != null)
            ?? keys.Select(k => clips.FirstOrDefault(c => c.name.ToLower().Contains(k))).FirstOrDefault(c => c != null);

        var idle = Find("idle") ?? clips[0];
        var walk = Find("walk", "run") ?? idle;
        var attack = Find("attack_1", "attack");
        var die = Find("dead", "death", "die");

        AssetDatabase.DeleteAsset(ControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
        var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
        sm.defaultState = sIdle;

        var t = sIdle.AddTransition(sWalk);
        t.hasExitTime = false; t.duration = 0.15f; t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        t = sWalk.AddTransition(sIdle);
        t.hasExitTime = false; t.duration = 0.15f; t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        if (attack != null)
        {
            var sAttack = sm.AddState("Attack"); sAttack.motion = attack;
            var a = sm.AddAnyStateTransition(sAttack);
            a.hasExitTime = false; a.duration = 0.1f; a.canTransitionToSelf = false;
            a.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            var back = sAttack.AddTransition(sIdle);
            back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.1f;
        }
        if (die != null)
        {
            var sDie = sm.AddState("Die"); sDie.motion = die;
            var d = sm.AddAnyStateTransition(sDie);
            d.hasExitTime = false; d.duration = 0.1f; d.canTransitionToSelf = false;
            d.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[ARANAS] Animator creado: Idle={idle.name}, Walk={walk.name}, Attack={(attack != null ? attack.name : "-")}, Die={(die != null ? die.name : "-")}");
        return ctrl;
    }

    // ------------------------------------------------------------------ 3. Colocar
    void PlaceSpiders()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var origin = FindAnyObjectByType<XROrigin>();
        if (origin == null) { Debug.LogError("[ARANAS] No hay XR Origin en la escena abierta."); return; }
        Transform head = origin.Camera != null ? origin.Camera.transform : null;

        // Vida del jugador (en el XR Origin, que es nuestro)
        var playerHealth = origin.GetComponent<Health>();
        if (playerHealth == null)
        {
            playerHealth = Undo.AddComponent<Health>(origin.gameObject);
            playerHealth.flashChildrenIfEmpty = false;
            Debug.Log("[ARANAS] Anadido Health al XR Origin (100 de vida).");
        }
        if (origin.GetComponent<PlayerDamageFeedback>() == null)
        {
            var fb = Undo.AddComponent<PlayerDamageFeedback>(origin.gameObject);
            fb.health = playerHealth;
        }

        bool hasNav = NavMesh.CalculateTriangulation().vertices.Length > 0;
        if (!hasNav) Debug.LogWarning("[ARANAS] El NavMesh NO esta horneado: las coloco sobre el terreno, pero no se moveran hasta que hornees.");

        Vector3 center = Selection.activeTransform != null ? Selection.activeTransform.position
                       : SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : head.position;

        var group = new GameObject("Aranas");
        Undo.RegisterCreatedObjectUndo(group, "Colocar aranas");
        group.transform.position = center;

        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            float ang = i * Mathf.PI * 2f / count;
            Vector3 p = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius * UnityEngine.Random.Range(0.4f, 1f);
            if (!Ground(ref p, hasNav)) continue;

            var s = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            s.name = $"Arana {i + 1}";
            s.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            var ai = s.GetComponent<EnemyAI>();
            ai.target = head;
            ai.targetHealth = playerHealth;
            Undo.RegisterCreatedObjectUndo(s, "Colocar aranas");
            placed++;
        }

        // El GameManager pierde la partida si muere el jugador
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null && gm.playerHealth == null) { Undo.RecordObject(gm, "Asignar vida"); gm.playerHealth = playerHealth; }

        Selection.activeObject = group;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(group.scene);
        Debug.Log($"[ARANAS] Colocadas {placed}/{count} aranas en el grupo 'Aranas' alrededor de {center}. Guarda la escena (Ctrl+S).");
        if (placed < count) Debug.LogWarning("[ARANAS] Algunas no tenian suelo debajo (agua o fuera del NavMesh). Prueba en otro sitio o con menos radio.");
    }

    static bool Ground(ref Vector3 p, bool hasNav)
    {
        if (hasNav)
        {
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 5f, NavMesh.AllAreas)) return false;
            p = hit.position;
            return true;
        }
        var terrain = Terrain.activeTerrain;   // DEMO: terreno de la Island demo
        if (terrain == null) return true;
        p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        return true;
    }
}
