using System;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// Funciones comunes de las herramientas del menu "Isla Pirata" (solo editor).
public static class SetupToolUtils
{
    public const string PrefabFolder = "Assets/Prefabs";

    public static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets", "Prefabs");
    }

    /// Busca el XR Origin, su camara y su Health (lo anade si falta, con PlayerDamageFeedback).
    public static bool TryGetPlayer(out Transform head, out Health health)
    {
        head = null;
        health = null;
        var origin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
        if (origin == null) { Debug.LogError("[HERRAMIENTA] No hay XR Origin en la escena abierta."); return false; }
        head = origin.Camera != null ? origin.Camera.transform : origin.transform;

        health = origin.GetComponent<Health>();
        if (health == null)
        {
            health = Undo.AddComponent<Health>(origin.gameObject);
            health.flashChildrenIfEmpty = false;
            Debug.Log("[HERRAMIENTA] Anadido Health al XR Origin (100 de vida).");
        }
        if (origin.GetComponent<PlayerDamageFeedback>() == null)
            Undo.AddComponent<PlayerDamageFeedback>(origin.gameObject).health = health;

        var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (gm != null && gm.playerHealth == null) { Undo.RecordObject(gm, "Asignar vida"); gm.playerHealth = health; }
        return true;
    }

    /// Centro para colocar cosas: el objeto seleccionado en la Hierarchy o el centro de la Scene view.
    public static Vector3 PlacementCenter(Vector3 fallback)
    {
        if (Selection.activeTransform != null) return Selection.activeTransform.position;
        return SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : fallback;
    }

    public static bool HasNavMesh() => NavMesh.CalculateTriangulation().vertices.Length > 0;

    /// Coloca el punto sobre el NavMesh (o sobre el terreno si no hay NavMesh). False si no hay suelo.
    public static bool Ground(ref Vector3 p, bool useNav)
    {
        if (useNav)
        {
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 5f, NavMesh.AllAreas)) return false;
            p = hit.position;
            return true;
        }
        var terrain = Terrain.activeTerrain;   // DEMO: terreno de la Island demo
        if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        return true;
    }

    /// Bounds en mundo de todas las mallas del objeto.
    public static Bounds RenderBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        Bounds b = rends.Length > 0 ? rends[0].bounds : new Bounds(go.transform.position, Vector3.one * 0.5f);
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }

    public static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    /// Marca como Loop Time los clips cuyo nombre contiene alguna de las palabras.
    public static void SetLoopingClips(string modelPath, params string[] loopKeywords)
    {
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) return;
        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        bool changed = importer.clipAnimations.Length == 0;
        foreach (var c in clips)
        {
            bool loop = loopKeywords.Any(k => c.name.ToLower().Contains(k));
            if (c.loopTime != loop) { c.loopTime = loop; changed = true; }
        }
        if (!changed || clips.Length == 0) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Debug.Log($"[HERRAMIENTA] '{modelPath}': marcados en bucle los clips con {string.Join("/", loopKeywords)}.");
    }

    public static AnimationClip[] Clips(string modelPath) =>
        AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();

    public static AnimationClip FindClip(AnimationClip[] clips, params string[] keys) =>
        keys.Select(k => clips.FirstOrDefault(c => c.name.ToLower() == k)).FirstOrDefault(c => c != null)
        ?? keys.Select(k => clips.FirstOrDefault(c => c.name.ToLower().Contains(k))).FirstOrDefault(c => c != null);

    /// Animator Controller con float Speed (Idle<->Walk), trigger Attack y, si hay clip, trigger Die.
    public static AnimatorController BuildEnemyController(string path, AnimationClip idle, AnimationClip walk,
                                                         AnimationClip attack, AnimationClip die)
    {
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        if (die != null) ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
        var sWalk = sm.AddState("Walk"); sWalk.motion = walk != null ? walk : idle;
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
            back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.1f;
        }
        if (die != null)
        {
            var sDie = sm.AddState("Die"); sDie.motion = die;
            var d = sm.AddAnyStateTransition(sDie);
            d.hasExitTime = false; d.duration = 0.1f; d.canTransitionToSelf = false;
            d.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[HERRAMIENTA] Animator '{path}': Idle={Name(idle)} Walk={Name(walk)} Attack={Name(attack)} Die={Name(die)}");
        return ctrl;
    }

    static string Name(AnimationClip c) => c != null ? c.name : "-";

    /// Texto flotante (Canvas World Space + TextMeshPro) de 'size' metros de ancho, hijo de 'parent'
    public static TMPro.TMP_Text MakeWorldText(string name, Transform parent, Vector3 worldPos, float widthMeters, string text, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, true);
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.one * 0.001f / Mathf.Max(parent != null ? parent.lossyScale.x : 1f, 0.0001f);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = new Vector2(widthMeters * 1000f, widthMeters * 400f);

        var t = new GameObject("Texto", typeof(RectTransform));
        t.transform.SetParent(go.transform, false);
        var rt = (RectTransform)t.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = t.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.85f, 0.6f);
        return tmp;
    }

    /// Material del shader antiguo (Standard) -> URP/Lit copiando sus texturas. Devuelve true si lo cambio.
    public static bool UpgradeToURP(Material m)
    {
        if (m == null || m.shader == null || m.shader.name.StartsWith("Universal Render Pipeline")) return false;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return false;
        Texture main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
        Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
        Texture normal = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
        Texture metal = m.HasProperty("_MetallicGlossMap") ? m.GetTexture("_MetallicGlossMap") : null;
        Texture occ = m.HasProperty("_OcclusionMap") ? m.GetTexture("_OcclusionMap") : null;
        Undo.RecordObject(m, "URP");
        m.shader = lit;
        if (main != null) m.SetTexture("_BaseMap", main);
        m.SetColor("_BaseColor", color);
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
        if (metal != null) { m.SetTexture("_MetallicGlossMap", metal); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
        if (occ != null) { m.SetTexture("_OcclusionMap", occ); m.EnableKeyword("_OCCLUSIONMAP"); }
        EditorUtility.SetDirty(m);
        return true;
    }

    /// Coloca 'count' copias del prefab en circulo alrededor de 'center' dentro de un grupo nuevo.
    public static GameObject PlaceAround(GameObject prefab, string groupName, Vector3 center, int count, float radius,
                                         Action<GameObject> configure)
    {
        bool nav = HasNavMesh();
        if (!nav) Debug.LogWarning("[HERRAMIENTA] El NavMesh NO esta horneado: coloco sobre el terreno.");
        var group = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(group, "Colocar " + groupName);
        group.transform.position = center;

        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            float ang = i * Mathf.PI * 2f / count;
            Vector3 p = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius * UnityEngine.Random.Range(0.4f, 1f);
            if (!Ground(ref p, nav)) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            go.name = $"{prefab.name} {i + 1}";
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            configure?.Invoke(go);
            Undo.RegisterCreatedObjectUndo(go, "Colocar " + groupName);
            placed++;
        }
        Selection.activeObject = group;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(group.scene);
        Debug.Log($"[HERRAMIENTA] Colocados {placed}/{count} en '{groupName}' alrededor de {center}. Guarda la escena (Ctrl+S).");
        if (placed < count) Debug.LogWarning("[HERRAMIENTA] Algunos no tenian suelo debajo (agua o fuera del NavMesh).");
        return group;
    }
}
