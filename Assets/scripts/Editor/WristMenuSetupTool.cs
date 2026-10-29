using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// Herramienta de editor: menu "Isla Pirata > Menu muneca".
///  1) Vineta de confort: pone el prefab TunnelingVignette de los Starter Assets bajo Main Camera,
///     conectado a Move, giro suave y teletransporte.
///  2) Menu de la muneca: Canvas World Space en el Left Controller con WristMenu y sus botones.
///  3) EventSystem con XRUIInputModule (para pulsar con el rayo), si no lo hay.
public class WristMenuSetupTool : EditorWindow
{
    Vector3 localPos = new Vector3(0f, 0.09f, 0.06f);
    Vector3 localEuler = new Vector3(35f, 0f, 0f);
    float scale = 0.0007f;
    bool allowTimeChange = true;

    [MenuItem("Isla Pirata/Menu muneca")]
    static void Open() => GetWindow<WristMenuSetupTool>("Menu muneca");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Crea el menu de opciones en el mando izquierdo (boton Y = abrir/cerrar): movimiento teletransporte/continuo, " +
                                "giro por saltos/suave, grados, velocidad, vineta, hora, volumen y reiniciar. Se pulsa con el rayo del mando derecho.", MessageType.None);
        localPos = EditorGUILayout.Vector3Field("Posicion sobre el mando", localPos);
        localEuler = EditorGUILayout.Vector3Field("Inclinacion", localEuler);
        scale = EditorGUILayout.FloatField(new GUIContent("Escala", "0.0007 = el panel mide unos 22 x 33 cm"), scale);
        allowTimeChange = EditorGUILayout.Toggle(new GUIContent("Permitir cambiar la hora", "Si no, la hora solo cambia con el reloj de arena"), allowTimeChange);
        EditorGUILayout.Space();
        if (GUILayout.Button("Crear / actualizar menu de la muneca")) Create();
    }

    void Create()
    {
        var origin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (origin == null || origin.Camera == null) { Debug.LogError("[MENU] No hay XR Origin con camara en la escena."); return; }

        EnsureEventSystem();

        var managers = origin.GetComponentsInChildren<ControllerInputActionManager>(true);
        var left = managers.FirstOrDefault(m => m.name.ToLower().Contains("left"));
        var right = managers.FirstOrDefault(m => m.name.ToLower().Contains("right"));
        if (left == null) Debug.LogWarning($"[MENU] No encuentro el Controller Input Action Manager del mando izquierdo (hay {managers.Length}).");

        var move = origin.GetComponentInChildren<ContinuousMoveProvider>(true);
        var snap = origin.GetComponentInChildren<SnapTurnProvider>(true);
        var smooth = origin.GetComponentInChildren<ContinuousTurnProvider>(true);
        var teleport = origin.GetComponentInChildren<TeleportationProvider>(true);
        var vignette = EnsureVignette(origin, move, smooth, teleport);

        // Menu anterior fuera
        foreach (var old in FindObjectsByType<WristMenu>(FindObjectsInactive.Include)) Undo.DestroyObjectImmediate(old.gameObject);

        Transform parent = left != null ? left.transform : origin.Camera.transform.parent;
        var root = new GameObject("Menu Muneca");
        Undo.RegisterCreatedObjectUndo(root, "Menu muneca");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = left != null ? localPos : new Vector3(-0.25f, 1.1f, 0.4f);
        root.transform.localRotation = Quaternion.Euler(localEuler);

        // Canvas 320 x 480
        var cgo = new GameObject("Panel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster), typeof(Image));
        cgo.transform.SetParent(root.transform, false);
        cgo.transform.localScale = Vector3.one * scale;
        cgo.layer = LayerMask.NameToLayer("UI");
        cgo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)cgo.transform).sizeDelta = new Vector2(320, 524);
        var bg = cgo.GetComponent<Image>();
        bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.18f, 0.11f, 0.06f, 0.92f);

        Text(cgo.transform, "Titulo", "Opciones", 26, new Vector2(0, 232), new Vector2(300, 36), FontStyles.Bold);
        Text(cgo.transform, "Ayuda", "Y = abrir / cerrar · rayo derecho + gatillo", 13, new Vector2(0, 204), new Vector2(300, 22), FontStyles.Italic);

        var menu = root.AddComponent<WristMenu>();
        menu.panel = cgo;
        menu.leftHandManager = left;
        menu.rightHandManager = right;
        menu.moveProvider = move;
        menu.snapTurn = snap;
        menu.smoothTurn = smooth;
        menu.vignette = vignette;
        menu.dayNight = FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
        menu.allowTimeChange = allowTimeChange;

        float y = 162f;
        menu.moveButton = Btn(cgo.transform, "Movimiento", new Vector2(0, y), 290); y -= 44;
        menu.turnButton = Btn(cgo.transform, "Giro", new Vector2(0, y), 290); y -= 44;
        menu.snapButton = Btn(cgo.transform, "Salto de giro", new Vector2(0, y), 290); y -= 44;
        menu.speedButton = Btn(cgo.transform, "Velocidad", new Vector2(0, y), 290); y -= 44;
        menu.vignetteButton = Btn(cgo.transform, "Vineta", new Vector2(0, y), 290); y -= 44;
        menu.timeButton = Btn(cgo.transform, "Hora", new Vector2(0, y), 290); y -= 44;
        menu.volumeDownButton = Btn(cgo.transform, "Volumen -", new Vector2(-115, y), 60, "-");
        menu.volumeUpButton = Btn(cgo.transform, "Volumen +", new Vector2(115, y), 60, "+");
        menu.volumeText = Text(cgo.transform, "Volumen", "Volumen 100 %", 18, new Vector2(0, y), new Vector2(160, 36), FontStyles.Bold);
        y -= 52;
        menu.restartButton = Btn(cgo.transform, "Reiniciar", new Vector2(0, y), 290);
        menu.restartButton.GetComponent<Image>().color = new Color(0.75f, 0.35f, 0.3f);
        y -= 44;
        menu.closeButton = Btn(cgo.transform, "Cerrar", new Vector2(0, y), 290, "Cerrar");

        cgo.SetActive(false);   // empieza cerrado (se abre con Y)

        Selection.activeObject = root;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[MENU] Menu creado en '{parent.name}'. Managers izq {(left != null ? left.name : "FALTA")} der {(right != null ? right.name : "FALTA")}, " +
                  $"move {(move != null ? "ok" : "FALTA")}, snap {(snap != null ? "ok" : "FALTA")}, giro suave {(smooth != null ? "ok" : "FALTA")}, " +
                  $"vineta {(vignette != null ? "ok" : "FALTA")}, dia/noche {(menu.dayNight != null ? "ok" : "FALTA (Isla Pirata > Maldicion)")}. Guarda (Ctrl+S).");
    }

    // ------------------------------------------------------------------ vineta
    static TunnelingVignetteController EnsureVignette(XROrigin origin, LocomotionProvider move, LocomotionProvider smooth, LocomotionProvider teleport)
    {
        var v = origin.GetComponentInChildren<TunnelingVignetteController>(true);
        if (v == null)
        {
            var path = AssetDatabase.FindAssets("TunnelingVignette t:Prefab").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            var prefab = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            if (prefab == null) { Debug.LogWarning("[MENU] No encuentro el prefab TunnelingVignette de los Starter Assets: el menu no tendra vineta."); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, origin.Camera.transform);
            Undo.RegisterCreatedObjectUndo(go, "Vineta");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            v = go.GetComponent<TunnelingVignetteController>();
            Debug.Log("[MENU] Anadida la vineta de confort (" + path + ") bajo Main Camera.");
        }
        Undo.RecordObject(v, "Vineta");
        var list = new List<LocomotionVignetteProvider>();
        foreach (var p in new[] { move, smooth, teleport })
            if (p != null) list.Add(new LocomotionVignetteProvider { locomotionProvider = p, enabled = true });
        v.locomotionVignetteProviders = list;
        EditorUtility.SetDirty(v);
        return v;
    }

    // ------------------------------------------------------------------ UI
    static Button Btn(Transform parent, string name, Vector2 pos, float width, string label = "...")
    {
        var go = new GameObject("Boton " + name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, 38);
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = new Color(0.86f, 0.74f, 0.52f);
        var b = go.GetComponent<Button>();
        var c = b.colors;
        c.highlightedColor = new Color(1f, 1f, 0.7f);
        c.pressedColor = new Color(1f, 0.8f, 0.3f);
        c.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        b.colors = c;
        var t = Text(go.transform, "Texto", label, 17, Vector2.zero, new Vector2(width - 10, 36), FontStyles.Bold);
        t.color = new Color(0.2f, 0.1f, 0.05f);
        t.raycastTarget = false;
        return b;
    }

    static TMP_Text Text(Transform parent, string name, string text, float size, Vector2 pos, Vector2 box, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = style;
        t.color = new Color(0.95f, 0.88f, 0.7f);
        t.raycastTarget = false;
        return t;
    }

    static void EnsureEventSystem()
    {
        var es = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
            Debug.Log("[MENU] Creado EventSystem con XR UI Input Module.");
            return;
        }
        if (es.GetComponent<XRUIInputModule>() == null)
        {
            foreach (var m in es.GetComponents<BaseInputModule>()) Undo.DestroyObjectImmediate(m);
            Undo.AddComponent<XRUIInputModule>(es.gameObject);
            Debug.Log("[MENU] El EventSystem ahora usa XR UI Input Module.");
        }
    }
}
