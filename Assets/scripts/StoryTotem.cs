using TMPro;
using UnityEngine;
using UnityEngine.XR;

/// Totem que cuenta la historia (la voz del capitan Calavera, atrapada en el). Al acercarte empieza a hablar:
/// el texto aparece flotando sobre el totem, pagina a pagina. Pasa solo cada 'secondsPerPage' o con X.
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
        "Primero sacude las palmeras hasta encontrar la llave.\nCuidado: las arañas de las rocas despertarán.",
        "Rema hasta el barco, sube por la escala y abre el cofre...\npero solo bajo la luna.\nEl reloj de arena te ayudará.",
        "Libéranos, grumete.\nSolo tú puedes romper la maldición.",
    };
    [Tooltip("Segundos que se queda cada pagina (luego pasa sola)")]
    public float secondsPerPage = 7f;
    [Tooltip("Pasar de pagina con X (mando izquierdo)")]
    public bool advanceWithButton = true;

    [Header("Cuando habla")]
    [Tooltip("Empieza a hablar al acercarte a menos de esta distancia (m)")]
    public float talkRadius = 4f;

    [Header("Ambiente")]
    [Tooltip("Mientras habla el capitan se hace de noche (mas misterio); al terminar o al alejarte vuelve la hora que habia")]
    public bool nightDuringStory = true;
    [Range(0f, 24f)] public float storyHour = 23f;
    [Tooltip("Segundos que tarda en anochecer / amanecer")]
    public float moodTransition = 3f;
    [Tooltip("Ciclo dia/noche. Vacio = se busca solo")]
    public DayNightCycle dayNight;

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
    [Tooltip("Voz de TODA la historia en un solo audio. Si se pone, el texto va sincronizado con ella: cada pagina dura " +
             "una parte del audio proporcional a lo larga que es. Tiene prioridad sobre 'Voice Clips'")]
    public AudioClip fullStoryClip;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool Finished { get; private set; }
    /// Esta contando la historia ahora mismo (las pistas de controles esperan a que termine)
    public bool IsTalking => talking;

    Transform head;
    int page = -1;
    float timer;
    bool talking, prevButton, moodOn;
    float hourBefore;
    float[] pageStart;   // con fullStoryClip: segundo del audio en que empieza cada pagina

    bool FullVoice => fullStoryClip != null && audioSource != null;

    void Start()
    {
        if (Camera.main != null) head = Camera.main.transform;
        if (text == null) CreateText();
        if (dayNight == null) dayNight = FindAnyObjectByType<DayNightCycle>();
        text.gameObject.SetActive(false);
        if (FullVoice)
        {
            // Reparto del audio entre las paginas segun su longitud de texto
            pageStart = new float[pages.Length];
            float total = 0f;
            foreach (var pg in pages) total += Mathf.Max(1, pg.Length);
            float acc = 0f;
            for (int i = 0; i < pages.Length; i++) { pageStart[i] = fullStoryClip.length * acc / total; acc += Mathf.Max(1, pages[i].Length); }
            audioSource.clip = fullStoryClip;
            audioSource.loop = false;
        }
        if (debugLogs) Debug.Log($"[TOTEM] Listo: {pages.Length} paginas" + (FullVoice ? $", voz de {fullStoryClip.length:F0} s" : "") +
                                 $". Acercate a menos de {talkRadius:F0} m para escuchar la historia.");
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
            if (FullVoice)
            {
                if (page < 0) { page = 0; audioSource.time = 0f; audioSource.Play(); Show(); }
                else { audioSource.UnPause(); Show(); }
            }
            else if (page < 0) NextPage(); else Show();
            BeginMood();
            if (debugLogs) Debug.Log("[TOTEM] El capitan empieza a hablar");
        }
        else if (talking && dist > talkRadius + 3f)
        {
            talking = false;
            text.gameObject.SetActive(false);
            if (audioSource != null) { if (FullVoice) audioSource.Pause(); else audioSource.Stop(); }
            EndMood();
            if (debugLogs) Debug.Log("[TOTEM] Te alejas: el capitan se calla");
        }
        if (!talking) return;

        // Que el texto mire siempre al jugador
        var tt = text.transform;
        tt.rotation = Quaternion.LookRotation(tt.position - head.position);

        bool button = advanceWithButton && XRHandInput.PrimaryButton(XRNode.LeftHand);   // solo X (mando izquierdo)
        bool pressed = button && !prevButton;
        prevButton = button;

        if (FullVoice) { UpdateFullVoice(pressed); return; }
        timer -= Time.deltaTime;
        if (timer <= 0f || pressed) NextPage();
    }

    // Con la voz de toda la historia: la pagina la marca el punto del audio; X salta a la siguiente
    void UpdateFullVoice(bool pressed)
    {
        if (pressed)
        {
            if (page + 1 >= pages.Length) { EndStory(); return; }
            audioSource.time = Mathf.Min(pageStart[page + 1], fullStoryClip.length - 0.05f);
        }
        if (!audioSource.isPlaying) { EndStory(); return; }   // el audio ha terminado
        int p = 0;
        for (int i = 0; i < pageStart.Length; i++) if (audioSource.time >= pageStart[i]) p = i;
        if (p != page) { page = p; Show(); XRHandInput.HapticBoth(0.1f, 0.04f); }
    }

    void EndStory()
    {
        Finished = true;
        talking = false;
        text.gameObject.SetActive(false);
        if (audioSource != null) audioSource.Stop();
        EndMood();
        if (debugLogs) Debug.Log("[TOTEM] Fin de la historia");
    }

    // Anochecer mientras habla y volver a la hora de antes al acabar
    void BeginMood()
    {
        if (!nightDuringStory || dayNight == null || moodOn) return;
        moodOn = true;
        hourBefore = dayNight.hour;
        dayNight.TransitionTo(storyHour, moodTransition);
    }

    void EndMood()
    {
        if (!moodOn || dayNight == null) return;
        moodOn = false;
        dayNight.TransitionTo(hourBefore, moodTransition);
    }

    void NextPage()
    {
        page++;
        if (page >= pages.Length)
        {
            Finished = true;
            talking = false;
            text.gameObject.SetActive(false);
            EndMood();
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
        text.text = pages[page] + (advanceWithButton ? $"\n<size=55%><i>({page + 1}/{pages.Length})   X = siguiente</i></size>" : "");
        timer = secondsPerPage;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, talkRadius);
    }
}
