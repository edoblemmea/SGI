using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Cofre que se abre al encajar la llave en su XR Socket Interactor (la cerradura).
/// Dos formas de abrirlo (usa una):
///  A) 'Lid': la tapa es un objeto aparte -> se gira hasta 'Open Lid Euler'.
///  B) 'Closed Model' / 'Open Model': el asset ChestFree trae chest_close y chest_open -> se cambia uno por otro.
public class ChestLock : MonoBehaviour
{
    [Header("Cerradura")]
    [Tooltip("XR Socket Interactor colocado en la cerradura del cofre")]
    public XRSocketInteractor socket;
    [Tooltip("Debe coincidir con 'Key Id' del KeyItem. Vacio = vale cualquier llave")]
    public string requiredKeyId = "cofre";
    [Tooltip("Al abrir, la llave desaparece (se queda 'gastada' en la cerradura)")]
    public bool consumeKey = true;

    [Header("A) Tapa que gira")]
    public Transform lid;
    public Vector3 openLidEuler = new Vector3(-110f, 0f, 0f);
    public float openDuration = 1.2f;

    [Header("B) Cambiar modelo cerrado por abierto")]
    public GameObject closedModel;
    public GameObject openModel;

    [Header("Recompensa (opcional)")]
    [Tooltip("Se activa al abrir (p. ej. las monedas 'coins' del asset ChestFree, desactivadas al empezar)")]
    public GameObject reward;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip openClip;
    public AudioClip wrongKeyClip;

    [Header("Maldicion: solo se abre bajo la luna (opcional)")]
    [Tooltip("Si se marca, el cofre solo se abre de noche")]
    public bool requireNight = false;
    [Tooltip("Desmarcado: hay que hacer que anochezca ANTES de meter la llave; de dia la llave se rechaza y vuelve al bolsillo. " +
             "Marcado: de dia la llave se queda puesta y el cofre se abre solo al anochecer")]
    public bool openWhenNightFallsWithKeyIn = false;
    [Tooltip("Bolsillo de la cadera: si se rechaza la llave vuelve ahi. Vacio = se busca solo")]
    public KeyPocket keyPocket;
    public DayNightCycle dayNight;
    [Tooltip("Texto que aparece sobre el cofre si intentas abrirlo de dia")]
    public TMPro.TMP_Text hintText;
    public string dayHint = "La llave no gira...\n\"Solo bajo la luna se rompe la maldicion\"";
    public float hintSeconds = 6f;

    [Header("Mensaje al llegar al cofre")]
    [Tooltip("Al acercarte a menos de esta distancia sale un mensaje (0 = nunca)")]
    public float approachRadius = 4f;
    [TextArea] public string approachDayHint = "El cofre maldito...\n\"Solo se abre bajo la luna\"\nDale la vuelta al reloj de arena para que anochezca y luego usa la llave";
    [TextArea] public string approachNightHint = "Ha salido la luna.\nMete la llave en la cerradura";
    [TextArea] public string approachNoCurseHint = "El cofre del capitan.\nMete la llave en la cerradura";
    [Tooltip("Luz o efecto en la cerradura que se enciende de noche")]
    public GameObject nightGlow;
    [Tooltip("Al abrirse rompe la maldicion (Curse.Instance)")]
    public bool breaksCurse = false;

    [Header("Juego")]
    public string objectiveId = "cofre";
    public UnityEvent onOpened;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool IsOpen { get; private set; }

    KeyItem waitingKey;   // llave puesta de dia, esperando a la noche
    float hintTimer;
    bool playerNear;

    bool NightOk => !requireNight || (dayNight != null && dayNight.IsNight);

    void Awake()
    {
        if (reward != null) reward.SetActive(false);
        if (openModel != null) openModel.SetActive(false);
        if (hintText != null) hintText.gameObject.SetActive(false);
        if (nightGlow != null) nightGlow.SetActive(false);
    }

    void Update()
    {
        CheckApproach();
        if (nightGlow != null) nightGlow.SetActive(!IsOpen && requireNight && NightOk);
        if (hintText != null && hintTimer > 0f)
        {
            // El cartel mira siempre al jugador
            var canvas = hintText.canvas != null ? hintText.canvas.transform : hintText.transform;
            if (Camera.main != null)
                canvas.rotation = Quaternion.LookRotation(canvas.position - Camera.main.transform.position);
            hintTimer -= Time.deltaTime;
            if (hintTimer <= 0f) hintText.gameObject.SetActive(false);
        }
        // La llave estaba puesta de dia y ya es de noche: gira sola
        if (!IsOpen && waitingKey != null && NightOk)
        {
            if (debugLogs) Debug.Log("[COFRE] Ha salido la luna: la llave gira");
            Open(waitingKey);
        }
    }

    void ShowHint()
    {
        if (debugLogs) Debug.Log("[COFRE] Es de dia: la llave no gira. Hay que esperar a la noche (reloj de arena)");
        if (audioSource != null && wrongKeyClip != null) audioSource.PlayOneShot(wrongKeyClip);
        XRHandInput.HapticBoth(0.4f, 0.15f);
        Say(dayHint);
    }

    void Say(string msg)
    {
        if (hintText == null) return;
        hintText.text = msg;
        hintText.gameObject.SetActive(true);
        hintTimer = hintSeconds;
    }

    // Mensaje al llegar junto al cofre (una vez cada vez que te acercas)
    void CheckApproach()
    {
        if (IsOpen || approachRadius <= 0f || Camera.main == null) return;
        float d = Vector3.Distance(Camera.main.transform.position, transform.position);
        if (!playerNear && d < approachRadius)
        {
            playerNear = true;
            string msg = !requireNight ? approachNoCurseHint : NightOk ? approachNightHint : approachDayHint;
            if (debugLogs) Debug.Log("[COFRE] Has llegado al cofre: " + msg.Replace("\n", " "));
            XRHandInput.HapticBoth(0.2f, 0.1f);
            Say(msg);
        }
        else if (playerNear && d > approachRadius + 1.5f) playerNear = false;
    }

    void OnEnable()
    {
        if (socket == null) return;
        socket.selectEntered.AddListener(OnSocketed);
        socket.selectExited.AddListener(OnUnsocketed);
    }

    void OnDisable()
    {
        if (socket == null) return;
        socket.selectEntered.RemoveListener(OnSocketed);
        socket.selectExited.RemoveListener(OnUnsocketed);
    }

    // Si sacas la llave de dia, ya no se abrira sola al anochecer
    void OnUnsocketed(SelectExitEventArgs args)
    {
        if (!IsOpen && waitingKey != null && args.interactableObject.transform.GetComponentInParent<KeyItem>() == waitingKey)
            waitingKey = null;
    }

    void Start()
    {
        if (socket == null) Debug.LogWarning($"[COFRE] '{name}': el campo 'Socket' esta VACIO, no se podra abrir.");
        if (lid == null && (closedModel == null || openModel == null))
            Debug.LogWarning($"[COFRE] '{name}': no hay 'Lid' ni pareja 'Closed/Open Model'. Se completara el objetivo pero no veras abrirse nada.");
        if (hintText == null) CreateHintText();
    }

    // Cartel flotante sobre el cofre para los mensajes (si no lo creo la herramienta de la maldicion)
    void CreateHintText()
    {
        Bounds b = new Bounds(transform.position, Vector3.one * 0.5f);
        foreach (var r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        var go = new GameObject("Mensaje cofre");
        go.transform.SetParent(transform, true);
        go.transform.position = new Vector3(b.center.x, b.max.y + 0.6f, b.center.z);
        var t = go.AddComponent<TMPro.TextMeshPro>();
        t.fontSize = 1.2f;
        t.alignment = TMPro.TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(2.5f, 1f);
        t.color = new Color(1f, 0.92f, 0.7f);
        t.outlineWidth = 0.2f;
        t.outlineColor = Color.black;
        hintText = t;
        go.SetActive(false);
        if (debugLogs) Debug.Log("[COFRE] No habia 'Hint Text': creo un cartel sobre el cofre para los mensajes");
    }

    void OnSocketed(SelectEnterEventArgs args)
    {
        if (IsOpen) return;
        var keyTransform = args.interactableObject.transform;
        var key = keyTransform.GetComponentInParent<KeyItem>();
        if (key == null || (!string.IsNullOrEmpty(requiredKeyId) && key.keyId != requiredKeyId))
        {
            if (debugLogs) Debug.Log($"[COFRE] '{keyTransform.name}' no es la llave correcta");
            if (audioSource != null && wrongKeyClip != null) audioSource.PlayOneShot(wrongKeyClip);
            return;
        }
        if (!NightOk)
        {
            ShowHint();
            if (openWhenNightFallsWithKeyIn) waitingKey = key;   // se queda puesta; se abrira al anochecer
            else StartCoroutine(RejectKey(args.interactableObject));   // hay que hacer de noche antes
            return;
        }
        Open(key);
    }

    /// Publico para poder probarlo sin llave: clic derecho en el componente > "Abrir ahora"
    [ContextMenu("Abrir ahora")]
    public void OpenNow() => Open(null);

    void Open(KeyItem key)
    {
        if (IsOpen) return;
        IsOpen = true;
        if (debugLogs) Debug.Log($"[COFRE] '{name}' abierto{(key != null ? " con la llave " + key.keyId : "")}");

        if (audioSource != null && openClip != null) audioSource.PlayOneShot(openClip);
        if (lid != null) StartCoroutine(RotateLid());
        if (closedModel != null && openModel != null)
        {
            closedModel.SetActive(false);
            openModel.SetActive(true);
        }
        if (reward != null) reward.SetActive(true);

        if (consumeKey && key != null) StartCoroutine(ConsumeKey(key));

        waitingKey = null;
        if (hintText != null) hintText.gameObject.SetActive(false);
        if (breaksCurse && Curse.Instance != null) Curse.Instance.Break();
        if (!string.IsNullOrEmpty(objectiveId) && GameManager.Instance != null)
            GameManager.Instance.CompleteObjective(objectiveId);
        onOpened?.Invoke();
    }

    // De dia: la cerradura suelta la llave y la devuelve al bolsillo (o la aparta), y no la acepta durante un momento
    IEnumerator RejectKey(IXRSelectInteractable keyInteractable)
    {
        yield return null;   // no tocar la seleccion dentro de su propio evento
        if (socket == null) yield break;
        socket.socketActive = false;
        if (socket.interactionManager != null && socket.IsSelecting(keyInteractable))
            socket.interactionManager.SelectExit((IXRSelectInteractor)socket, keyInteractable);
        yield return null;
        var pocket = keyPocket != null ? keyPocket : FindAnyObjectByType<KeyPocket>();
        if (pocket != null) pocket.StoreNow("la llave no gira de dia");
        else
        {
            var rb = keyInteractable.transform.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) rb.linearVelocity = Vector3.up * 1.5f + transform.forward;   // salta de la cerradura
        }
        if (debugLogs) Debug.Log("[COFRE] Llave rechazada: hay que hacer que anochezca ANTES de meterla" + (pocket != null ? " (vuelve al bolsillo)" : ""));
        yield return new WaitForSeconds(2f);
        socket.socketActive = true;
    }

    IEnumerator RotateLid()
    {
        Quaternion from = lid.localRotation;
        Quaternion to = Quaternion.Euler(openLidEuler);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(openDuration, 0.01f))
        {
            lid.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        lid.localRotation = to;
    }

    IEnumerator ConsumeKey(KeyItem key)
    {
        // Esperar un frame para no desactivar el socket dentro de su propio evento
        yield return null;
        if (socket != null) socket.socketActive = false;
        var grab = key.GetComponent<XRGrabInteractable>();
        if (grab != null) grab.enabled = false;
        key.gameObject.SetActive(false);
    }
}
