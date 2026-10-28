using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// Herramienta de editor: menu "Isla Pirata > Escala de cuerda".
/// Cuelga una escala de cuerda (primitivas) en el costado del barco pirata que da hacia la barca de remos,
/// con RopeLadder: se trepa desde la barca con el grip y se aparece en la cubierta.
public class LadderSetupTool : EditorWindow
{
    Transform ship;
    float rungSpacing = 0.35f;
    float overRail = 1.0f;

    [MenuItem("Isla Pirata/Escala de cuerda")]
    static void Open() => GetWindow<LadderSetupTool>("Escala de cuerda");

    void OnEnable()
    {
        // Solo en el editor: propone la raiz cuyo hijo se llama StylShip_* (se puede cambiar a mano)
        if (ship == null)
        {
            var part = FindObjectsByType<Transform>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name.StartsWith("StylShip_Body"));
            if (part != null) ship = part.root;
        }
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Cuelga una escala en el costado del barco pirata que mira hacia la barca de remos. " +
                                "Despues puedes mover el objeto 'Escala de cuerda' (y su 'Punto cubierta') con la herramienta Move.", MessageType.None);
        ship = (Transform)EditorGUILayout.ObjectField("Barco pirata", ship, typeof(Transform), true);
        rungSpacing = EditorGUILayout.Slider("Separacion de peldanos (m)", rungSpacing, 0.25f, 0.5f);
        overRail = EditorGUILayout.Slider(new GUIContent("Sobresale de la borda (m)", "Cuanto sube la escala por encima de la cubierta"), overRail, 0.5f, 1.5f);
        using (new EditorGUI.DisabledScope(ship == null))
            if (GUILayout.Button("Crear / actualizar escala")) Create();
    }

    void Create()
    {
        var boat = FindAnyObjectByType<BoatRowing>(FindObjectsInactive.Include);
        var origin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (boat == null || origin == null) { Debug.LogError("[ESCALA] Falta la barca (BoatRowing) o el XR Origin en la escena."); return; }
        if (boat.transform.IsChildOf(ship)) { Debug.LogError("[ESCALA] Has elegido la barca de remos, no el barco pirata."); return; }

        Physics.SyncTransforms();
        Bounds b = SetupToolUtils.RenderBounds(ship.gameObject);
        Vector3 center = b.center;

        // Costado: el que mira hacia la barca de remos
        Vector3 side = Vector3.ProjectOnPlane(ship.right, Vector3.up).normalized;
        if (Vector3.Dot(boat.transform.position - center, side) < 0f) side = -side;

        // Cubierta: raycast hacia abajo a medio camino entre el centro y el costado (cerca de la altura del cofre si lo hay)
        var chest = FindAnyObjectByType<ChestLock>(FindObjectsInactive.Include);
        float halfWidth = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(side.x), 0f, Mathf.Abs(side.z))));
        Vector3 deckProbe = center + side * halfWidth * 0.5f;
        var deckHits = Physics.RaycastAll(new Vector3(deckProbe.x, b.max.y + 1f, deckProbe.z), Vector3.down, b.size.y + 5f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.transform.IsChildOf(ship) && h.normal.y > 0.7f).ToArray();
        float refY = chest != null ? chest.transform.position.y : b.min.y + b.size.y * 0.35f;
        float deckY = deckHits.Length > 0 ? deckHits.OrderBy(h => Mathf.Abs(h.point.y - refY)).First().point.y : refY;
        if (deckHits.Length == 0) Debug.LogWarning("[ESCALA] No encontre la cubierta con un raycast: revisa la altura de 'Punto cubierta' a mano.");

        // Casco: raycast horizontal desde fuera hacia el centro, un poco por debajo de la cubierta
        float hullY = deckY - 0.5f;
        Vector3 from = new Vector3(center.x, hullY, center.z) + side * (halfWidth + 5f);
        var hull = Physics.RaycastAll(from, -side, halfWidth + 5f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.transform.IsChildOf(ship)).OrderBy(h => h.distance).FirstOrDefault();
        Vector3 hullPoint = hull.collider != null ? hull.point : new Vector3(center.x, hullY, center.z) + side * halfWidth;
        if (hull.collider == null) Debug.LogWarning("[ESCALA] No encontre el casco con un raycast: la escala queda en el borde de los bounds.");

        Vector3 hang = hullPoint + side * 0.25f;   // separada del casco
        float waterY = boat.transform.position.y + 0.3f;   // DEMO: altura del agua = la de la barca de remos
        Vector3 bottomPos = new Vector3(hang.x, waterY, hang.z);
        Vector3 topPos = new Vector3(hang.x, deckY + overRail, hang.z);
        Vector3 deckPos = new Vector3(hullPoint.x, deckY, hullPoint.z) - side * 1.2f;

        // Escala anterior fuera
        foreach (var old in ship.GetComponentsInChildren<RopeLadder>(true)) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject("Escala de cuerda");
        Undo.RegisterCreatedObjectUndo(root, "Escala");
        root.transform.SetParent(ship, true);
        root.transform.SetPositionAndRotation(bottomPos, Quaternion.LookRotation(-side, Vector3.up));   // Z mira al casco

        var bottom = Empty("Abajo", root.transform, bottomPos);
        var top = Empty("Arriba", root.transform, topPos);
        var deck = Empty("Punto cubierta", root.transform, deckPos);

        // Visual: dos cuerdas y peldanos, sin colliders (no deben chocar con la barca)
        var mat = Mat();
        float height = topPos.y - bottomPos.y;
        Vector3 right = Vector3.Cross(Vector3.up, -side).normalized;
        foreach (float s in new[] { -0.22f, 0.22f })
            Piece(root.transform, "Cuerda", (bottomPos + topPos) * 0.5f + right * s, Quaternion.identity, new Vector3(0.03f, height * 0.5f, 0.03f), mat);
        int n = 0;
        for (float y = 0.3f; y < height - 0.05f; y += rungSpacing, n++)
            Piece(root.transform, "Peldano", bottomPos + Vector3.up * y, Quaternion.identity, new Vector3(0.04f, 0.24f, 0.04f), mat, right);

        // Collider + XR Simple Interactable: para apuntar a la escala con el rayo desde lejos (subes solo)
        var box = root.AddComponent<BoxCollider>();
        Vector3 ls = root.transform.lossyScale;
        box.size = new Vector3(0.6f / Mathf.Max(ls.x, 0.0001f), height / Mathf.Max(ls.y, 0.0001f), 0.15f / Mathf.Max(ls.z, 0.0001f));
        box.center = new Vector3(0f, height * 0.5f / Mathf.Max(ls.y, 0.0001f), 0f);
        root.AddComponent<XRSimpleInteractable>();

        var ladder = root.AddComponent<RopeLadder>();
        ladder.bottom = bottom;
        ladder.top = top;
        ladder.deckPoint = deck;
        ladder.xrOrigin = origin.transform;
        ladder.head = origin.Camera != null ? origin.Camera.transform : null;
        ladder.boat = boat;
        ladder.disableWhileClimbing = origin.GetComponentsInChildren<LocomotionProvider>(true)
            .Where(p => !(p is TeleportationProvider) && !(p is SnapTurnProvider) && !(p is ContinuousTurnProvider))
            .Cast<Behaviour>().ToArray();

        Selection.activeObject = root;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[ESCALA] Escala creada en el costado del barco pirata: {height:F1} m, {n} peldanos, cubierta a y={deckY:F2}. " +
                  $"Apaga mientras trepas: {string.Join(", ", ladder.disableWhileClimbing.Select(p => p.GetType().Name))}. " +
                  "Rema hasta ella y agarra con el grip y tira hacia abajo, o apunta desde lejos con el rayo y aprieta el grip. Guarda (Ctrl+S).");
    }

    static Transform Empty(string name, Transform parent, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, true);
        t.position = pos;
        t.rotation = parent.rotation;
        return t;
    }

    static void Piece(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material mat, Vector3? axis = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        // Los cilindros de Unity van a lo largo de su Y: los peldanos se tumban en horizontal (eje 'axis')
        go.transform.rotation = axis.HasValue ? Quaternion.FromToRotation(Vector3.up, axis.Value) : rot;
        go.transform.localScale = new Vector3(scale.x / Mathf.Max(parent.lossyScale.x, 0.0001f),
                                              scale.y / Mathf.Max(parent.lossyScale.y, 0.0001f),
                                              scale.z / Mathf.Max(parent.lossyScale.z, 0.0001f));
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material Mat()
    {
        SetupToolUtils.EnsurePrefabFolder();
        string path = SetupToolUtils.PrefabFolder + "/Escala_Cuerda.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.55f, 0.42f, 0.25f));
        m.SetFloat("_Smoothness", 0.05f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
