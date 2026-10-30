using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Pistas de controles en contexto: cada una sale UNA vez, cuando hace falta (al acercarte a una palmera, ver la llave,
/// subir a la barca, llegar a la escala, al cofre...). Se ven en un pergamino con el dibujo de los botones (B, X, GRIP...).
/// Si la pista es de un mando concreto, sale encima de ese mando; si no, un poco por debajo de la vista, siguiendola suavemente.
/// Espera a que el totem narrador termine de hablar. Busca los objetos por tipo: no hay que asignar nada.
/// Lo crea solo el GameManager al empezar (casilla 'Tutorial Hints').
public class TutorialHints : MonoBehaviour
{
    [Tooltip("Segundos que se ve cada pista")]
    public float secondsPerHint = 8f;
    [Tooltip("Distancia del texto delante de los ojos (m)")]
    public float distance = 1.1f;
    [Tooltip("Cuanto por debajo de la mirada (m)")]
    public float below = 0.25f;
    [Tooltip("Cuanto por encima del mando cuando la pista es de un mando (m)")]
    public float aboveHand = 0.14f;
    [Tooltip("Tamano de la pista junto al mando respecto a la que va delante de la vista")]
    [Range(0.3f, 1f)] public float handScale = 0.55f;
    public bool debugLogs = true;

    enum Side { View, Left, Right }

    class Hint
    {
        public string id, text;
        public Side side;
        public string[] buttons;
        public System.Func<bool> when;
        public bool done;
    }

    readonly List<Hint> hints = new List<Hint>();
    readonly Queue<Hint> queue = new Queue<Hint>();
    Transform head;
    RectTransform board, iconRow;
    CanvasGroup group;
    TextMeshProUGUI label;
    Hint current;
    Vector3 baseScale;
    float showTimer, checkTimer, gap, startTime;
    bool damaged;

    ShakeablePalm[] palms;
    KeyItem[] keys;
    Sword[] swords;
    BoatRowing boat;
    RopeLadder[] ladders;
    ChestLock[] chests;
    StoryTotem[] totems;
    KeyPocket pocket;

    void Start()
    {
        if (Camera.main != null) head = Camera.main.transform;
        if (head == null) { enabled = false; return; }
        startTime = Time.time;
        palms = FindObjectsByType<ShakeablePalm>(FindObjectsInactive.Exclude);
        keys = FindObjectsByType<KeyItem>(FindObjectsInactive.Exclude);
        swords = FindObjectsByType<Sword>(FindObjectsInactive.Exclude);
        boat = FindAnyObjectByType<BoatRowing>();
        ladders = FindObjectsByType<RopeLadder>(FindObjectsInactive.Exclude);
        chests = FindObjectsByType<ChestLock>(FindObjectsInactive.Exclude);
        totems = FindObjectsByType<StoryTotem>(FindObjectsInactive.Exclude);
        pocket = FindAnyObjectByType<KeyPocket>();
        var playerHealth = head.GetComponentInParent<Health>();
        if (playerHealth != null) playerHealth.onDamaged.AddListener(_ => damaged = true);
        CreateLabel();
        Define();
        if (debugLogs) Debug.Log($"[PISTAS] Listo: {hints.Count} pistas de controles");
    }

    // ------------------------------------------------------------------ las pistas
    void Define()
    {
        Add("andar", Side.View, new[] { "Stick izq.", "Stick der." },
            "<b>Stick izquierdo:</b> andar.\n<b>Stick derecho</b> adelante: teletransporte (apunta y suelta). A los lados: girar.",
            () => Time.time - startTime > 3f);
        Add("menu", Side.Left, new[] { "Y" }, "Menú de opciones\n(movimiento, giro, volumen...)",
            () => Time.time - startTime > 20f);
        Add("palmera", Side.View, new[] { "GRIP" }, "Acerca la mano al tronco, aprieta el <b>GRIP</b>\ny sacude la mano a los lados con fuerza.",
            () => Near(palms, 3f));
        Add("llave", Side.View, new[] { "GRIP" }, "¡La llave! Cógela con el <b>GRIP</b>\n(o apúntala con el rayo y aprieta el GRIP).",
            () => KeyOnGround(3f));
        Add("bolsillo", Side.Right, new[] { "GRIP" }, "Guárdala soltando el <b>GRIP</b> junto a tu\ncadera DERECHA (bolita verde).",
            () => KeyHeld());
        Add("espada", Side.View, new[] { "GRIP" }, "Cógela con el <b>GRIP</b> y da tajos RÁPIDOS.\nEnfúndala soltándola junto a tu cadera IZQUIERDA.",
            () => Near(swords, 2.5f));
        Add("aranas", Side.View, new[] { "GRIP" }, "¡Arañas! Lánzales cocos (coge, lanza y suelta el <b>GRIP</b>)\no dales tajos con la espada.",
            () => SpiderChasing());
        Add("barca", Side.View, new[] { "GRIP" }, "Acerca una mano a un remo y aprieta el <b>GRIP</b>:\ncogerás los dos remos y subirás a la barca.",
            () => boat != null && !boat.IsBoarded && Dist(boat.transform) < 3.5f);
        Add("remar", Side.View, new[] { "GRIP", "B", "A", "X" }, "<b>GRIP</b> apretado y tira de las manos hacia ti; suéltalo para volver.\nUn solo remo = girar.   <b>B</b>: atrás.   <b>A / X</b>: girar 90°.",
            () => boat != null && boat.IsBoarded);
        Add("bajar", Side.Right, new[] { "Stick der." }, "Para bajar: <b>stick derecho</b> adelante,\napunta al muelle o a la cubierta y suelta.",
            () => boat != null && boat.IsBoarded && Done("remar") && Time.time - DoneTime("remar") > 25f);
        Add("escala", Side.View, new[] { "GRIP" }, "Apunta a la escala con el rayo y aprieta el <b>GRIP</b>.\nTrepa agarrando y tirando hacia ABAJO, mano a mano.",
            () => Near(ladders, 6f));
        Add("cofre", Side.Right, new[] { "GRIP" }, "Saca la llave de tu cadera derecha y toca el candado...\npero solo bajo la luna.",
            () => Near(chests, 4f));
        Add("reloj", Side.View, new[] { "GRIP" }, "Para que anochezca: coge el <b>reloj de arena</b>\ncon el GRIP y dale la vuelta.",
            () => Done("cofre") && Time.time - DoneTime("cofre") > 10f && !NightNow());
        Add("dano", Side.View, new string[0], "¡Te atacan! La barra de abajo es tu vida.\nAléjate o defiéndete.",
            () => damaged);
    }

    // ------------------------------------------------------------------ condiciones
    readonly Dictionary<string, float> doneAt = new Dictionary<string, float>();
    bool Done(string id) => doneAt.ContainsKey(id);
    float DoneTime(string id) => doneAt.TryGetValue(id, out float t) ? t : 0f;

    float Dist(Transform t)
    {
        Vector3 d = t.position - head.position;
        d.y = 0f;
        return d.magnitude;
    }

    bool Near<T>(T[] list, float r) where T : Component
    {
        if (list == null) return false;
        foreach (var c in list) if (c != null && c.gameObject.activeInHierarchy && Dist(c.transform) < r) return true;
        return false;
    }

    bool KeyOnGround(float r)
    {
        if (keys == null) return false;
        foreach (var k in keys)
        {
            if (k == null || !k.gameObject.activeInHierarchy || k.GetComponentInParent<ShakeablePalm>() != null) continue;   // aun en la palmera
            if (pocket != null && pocket.HasKey) continue;
            var g = k.GetComponent<XRGrabInteractable>();
            if (g != null && g.isSelected) continue;
            if (Vector3.Distance(k.transform.position, head.position) < r) return true;
        }
        return false;
    }

    bool KeyHeld()
    {
        if (keys == null) return false;
        foreach (var k in keys)
        {
            var g = k != null ? k.GetComponent<XRGrabInteractable>() : null;
            if (g != null && g.isSelected) return true;
        }
        return false;
    }

    bool SpiderChasing()
    {
        foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude))
        {
            if (e.Current != EnemyAI.State.Chase && e.Current != EnemyAI.State.Attack) continue;
            var c = e.GetComponent<Collider>();
            if (c != null && c.bounds.size.y < 1f) return true;
        }
        return false;
    }

    bool NightNow()
    {
        foreach (var c in chests) if (c != null && c.dayNight != null) return c.dayNight.IsNight;
        return false;
    }

    bool TotemTalking()
    {
        if (GameIntro.Running) return true;   // historia y titulo del principio
        if (totems == null) return false;
        foreach (var t in totems) if (t != null && t.IsTalking) return true;
        return false;
    }

    void Add(string id, Side side, string[] buttons, string text, System.Func<bool> when) =>
        hints.Add(new Hint { id = id, side = side, buttons = buttons, text = text, when = when });

    // ------------------------------------------------------------------ mostrar
    void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = 0.3f;
            foreach (var h in hints)
            {
                if (h.done) continue;
                bool ok;
                try { ok = h.when(); } catch { ok = false; }
                if (ok) { h.done = true; doneAt[h.id] = Time.time; queue.Enqueue(h); }
            }
        }

        if (showTimer > 0f)
        {
            showTimer -= Time.deltaTime;
            group.alpha = Mathf.Clamp01(Mathf.Min(showTimer / 0.4f, (secondsPerHint - showTimer) / 0.3f));
            if (showTimer <= 0f) { board.gameObject.SetActive(false); gap = 1f; current = null; }
        }
        else if ((gap -= Time.deltaTime) <= 0f && queue.Count > 0 && !TotemTalking())
        {
            current = queue.Dequeue();
            label.text = current.text;
            SetIcons(current.buttons);
            board.localScale = current.side == Side.View ? baseScale : baseScale * handScale;
            board.gameObject.SetActive(true);
            group.alpha = 0f;
            Place(true);
            showTimer = secondsPerHint;
            if (current.side == Side.Left) XRHandInput.Haptic(XRNode.LeftHand, 0.25f, 0.08f);
            else if (current.side == Side.Right) XRHandInput.Haptic(XRNode.RightHand, 0.25f, 0.08f);
            else XRHandInput.HapticBoth(0.1f, 0.05f);
            if (debugLogs) Debug.Log($"[PISTAS] ({current.side}) " + current.text.Replace("\n", " "));
        }
    }

    void LateUpdate()
    {
        if (board == null || !board.gameObject.activeSelf || current == null) return;
        Place(false);
    }

    // Encima del mando de la pista; si no, por debajo de la vista siguiendo la cabeza con suavidad (pegado marea)
    void Place(bool snap)
    {
        Vector3 target;
        float follow = 4f;
        if (current.side != Side.View &&
            XRHandInput.TryGetWorldPosition(current.side == Side.Left ? XRNode.LeftHand : XRNode.RightHand, head.parent, null, out Vector3 hand))
        {
            Vector3 toHead = Vector3.ProjectOnPlane(head.position - hand, Vector3.up).normalized;
            target = hand + Vector3.up * aboveHand + toHead * 0.04f;
            follow = 12f;
        }
        else
        {
            Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (f.sqrMagnitude < 0.001f) f = head.up;
            target = head.position + f.normalized * distance + Vector3.down * below;
        }
        board.position = snap ? target : Vector3.Lerp(board.position, target, 1f - Mathf.Exp(-follow * Time.deltaTime));
        board.rotation = Quaternion.LookRotation(board.position - head.position);
    }

    void SetIcons(string[] buttons)
    {
        for (int i = iconRow.childCount - 1; i >= 0; i--) Destroy(iconRow.GetChild(i).gameObject);
        if (buttons != null) foreach (var b in buttons) PirateStyle.MakeButtonIcon(iconRow, b, 46f);
        iconRow.gameObject.SetActive(buttons != null && buttons.Length > 0);
    }

    void CreateLabel()
    {
        var s = PirateStyle.Get();
        board = PirateStyle.MakePanel("Pista", transform, new Vector2(620, 220), 560f, out RectTransform content);
        baseScale = board.localScale;
        group = board.GetComponent<CanvasGroup>();

        // Fila de botones arriba
        var row = new GameObject("Botones", typeof(RectTransform));
        iconRow = (RectTransform)row.transform;
        iconRow.SetParent(content, false);
        iconRow.anchorMin = new Vector2(0f, 1f);
        iconRow.anchorMax = new Vector2(1f, 1f);
        iconRow.pivot = new Vector2(0.5f, 1f);
        iconRow.sizeDelta = new Vector2(0f, 50f);
        iconRow.anchoredPosition = new Vector2(0f, -18f);
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 12f;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        label = PirateStyle.MakeText(content, "Texto", 28, false, s.paperText, 30f);
        label.rectTransform.offsetMax = new Vector2(-30f, -74f);   // deja sitio a los botones
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = 28f;
        board.gameObject.SetActive(false);
        board.position = head.position + head.forward * distance;
    }
}
