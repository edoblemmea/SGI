using TMPro;
using UnityEngine;
using UnityEngine.XR;

/// Totem que cuenta la historia (la voz del capitan Calavera, atrapada en el). Al acercarte empieza a hablar:
/// el texto aparece flotando sobre el totem, pagina a pagina. Pasa solo cada 'secondsPerPage' o con A / X.
/// Si te alejas se calla y, al volver, sigue donde lo dejo. Al terminar se puede volver a escuchar acercandose otra vez.
/// Lo monta "Isla Pirata > Totem narrador".
public class StoryTotem : MonoBehaviour
{
    [Header("Historia")]
    [TextArea(2, 5)]
    public string[] pages =
    {
        "Grumete... ¿me oyes?\nSoy yo, el capitán Calavera.\nMi alma ha quedado atrapada en este tótem.",
        "Encontramos un cofre maldito.\nLa leyenda decía que solo podía abrirse bajo la luna...\npero yo lo abrí a mediodía.",
        "Toda la tripulación se convirtió en esqueletos,\ncondenados a vigilar el cofre para siempre.\nTú te salvaste porque estabas en tierra.",
        "En mi último momento humano te lancé la llave.\nLa escondiste de noche en una palmera...\npero no recuerdas en cuál.",
        "El barco fantasma sigue anclado frente a la isla,\nesperando en la niebla.",
        "Después sacude las palmeras hasta encontrar la llave.\nCuidado: las arañas de las rocas despertarán.",
        "Rema hasta el barco, sube por la escala y abre el cofre...\npero solo bajo la luna.\nEl reloj de arena te ayudará.",
        "Libéranos, grumete.\nSolo tú puedes romper la maldición.",
    };
    [Tooltip("Segundos que se queda cada pagina (luego pasa sola)")]
    public float secondsPerPage = 7f;
    [Tooltip("Pasar de pagina con A (mando derecho) o X (izquierdo)")]
    public bool advanceWithButton = true;

    [Header("Cuando habla")]
    [Tooltip("Empieza a hablar al acercarte a menos de esta distancia (m)")]
    public float talkRadius = 4f;

    [Header("Texto")]
    [Tooltip("Texto donde se escribe. Vacio = se crea uno flotando sobre el totem")]
    public TMP_Text text;
    public float textHeight = 0.6f;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    [Tooltip("Suena al pasar cada pagina")]
    public AudioClip pageClip;
    [Tooltip("Voz grabada para cada pagina (mismo orden que 'Pages'); si la hay, la pagina dura lo que dure el audio")]
    public AudioClip[] voiceClips;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool Finished { get; private set; }
    /// Esta contando la historia ahora mismo (las pistas de controles esperan a que termine)
    public bool IsTalking => talking;

    Transform head;
    int page = -1;
    float timer;
    bool talking, prevButton;

    void Start()
    {
        if (Camera.main != null) head = Camera.main.transform;
        if (text == null) CreateText();
        text.gameObject.SetActive(false);
        if (debugLogs) Debug.Log($"[TOTEM] Listo: {pages.Length} paginas. Acercate a menos de {talkRadius:F0} m para escuchar la historia.");
    }

    void CreateText()
    {
        Bounds b = new Bounds(transform.position, Vector3.one * 0.5f);
        foreach (var r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        var go = new GameObject("Historia (texto)");
        go.transform.SetParent(transform, true);
        go.transform.position = new Vector3(b.center.x, b.max.y + textHeight, b.center.z);
        var t = go.AddComponent<TextMeshPro>();
        t.fontSize = 1.1f;
        t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(3f, 1.4f);
        t.color = new Color(1f, 0.85f, 0.6f);
        t.outlineWidth = 0.25f;
        t.outlineColor = Color.black;
        text = t;
    }

    void Update()
    {
        if (head == null || pages == null || pages.Length == 0) return;
        Vector3 d = head.position - transform.position;
        d.y = 0f;
        float dist = d.magnitude;

        if (!talking && dist < talkRadius)
        {
            talking = true;
            if (Finished) { Finished = false; page = -1; }   // volver a escucharla
            if (page < 0) NextPage(); else Show();
            if (debugLogs) Debug.Log("[TOTEM] El capitan empieza a hablar");
        }
        else if (talking && dist > talkRadius + 3f)
        {
            talking = false;
            text.gameObject.SetActive(false);
            if (audioSource != null) audioSource.Stop();
            if (debugLogs) Debug.Log("[TOTEM] Te alejas: el capitan se calla");
        }
        if (!talking) return;

        // Que el texto mire siempre al jugador
        var tt = text.transform;
        tt.rotation = Quaternion.LookRotation(tt.position - head.position);

        timer -= Time.deltaTime;
        bool button = advanceWithButton && (XRHandInput.PrimaryButton(XRNode.RightHand) || XRHandInput.PrimaryButton(XRNode.LeftHand));
        bool pressed = button && !prevButton;
        prevButton = button;
        if (timer <= 0f || pressed) NextPage();
    }

    void NextPage()
    {
        page++;
        if (page >= pages.Length)
        {
            Finished = true;
            talking = false;
            text.gameObject.SetActive(false);
            if (debugLogs) Debug.Log("[TOTEM] Fin de la historia");
            return;
        }
        Show();
        if (audioSource != null)
        {
            var voice = voiceClips != null && page < voiceClips.Length ? voiceClips[page] : null;
            if (voice != null) { audioSource.Stop(); audioSource.PlayOneShot(voice); timer = Mathf.Max(voice.length + 0.5f, 2f); }
            else if (pageClip != null) audioSource.PlayOneShot(pageClip, 0.6f);
        }
        XRHandInput.HapticBoth(0.15f, 0.05f);
    }

    void Show()
    {
        text.gameObject.SetActive(true);
        text.text = pages[page] + (advanceWithButton ? $"\n<size=55%><i>({page + 1}/{pages.Length})   A / X = siguiente</i></size>" : "");
        timer = secondsPerPage;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, talkRadius);
    }
}
