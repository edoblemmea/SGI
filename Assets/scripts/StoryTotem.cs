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
    [Tooltip("Mantener B (mando derecho) para saltar toda la historia")]
    public bool allowSkip = true;
    public float skipHoldSeconds = 1f;

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

    [Header("Fuego (mientras habla)")]
    [Tooltip("Luces naranjas que parpadean como antorchas alrededor del totem mientras habla")]
    public bool fireLights = true;
    public Color fireColor = new Color(1f, 0.45f, 0.12f);
    public float fireIntensity = 3f;
    public float fireRange = 6f;
    [Tooltip("Efecto de fuego opcional (prefab con particulas): se pone en la base del totem mientras habla")]
    public GameObject fireEffectPrefab;

    [Header("Texto")]
    [Tooltip("Texto donde se escribe. Vacio = se crean subtitulos (banda oscura) flotando sobre el totem")]
    public TMP_Text text;
    public float textHeight = 0.6f;
    [Tooltip("Nombre que sale encima de los subtitulos")]
    public string speakerName = "Capitán Calavera";

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
    /// La ultima vez se salto con B
    public bool Skipped { get; private set; }
    /// Esta contando la historia ahora mismo (las pistas de controles esperan a que termine)
    public bool IsTalking => talking;

    Transform head;
    int page = -1;
    float timer;
    bool talking, prevButton, moodOn, voiceChecked;
    bool forced;        // la ha empezado GameIntro: habla aunque estes lejos
    bool waitForLeave;  // al acabar no vuelve a empezar hasta que te alejes
    float skipHeld;
    Light[] fires;
    GameObject fireFx;
    float fireLevel;
    RectTransform board;   // subtitulos creados por el script (null si 'text' se asigno a mano)
    int shownLine = -2;
    float voiceStartedAt;
    float hourBefore;
    float[] pageStart;   // con fullStoryClip: segundo del audio en que empieza cada pagina

    bool FullVoice => fullStoryClip != null && audioSource != null;

    void Start()
    {
        if (Camera.main != null) head = Camera.main.transform;
        if (text == null) CreateText();
        if (dayNight == null) dayNight = FindAnyObjectByType<DayNightCycle>();
        SetVisible(false);
        if (fireLights) CreateFire();
        if (fullStoryClip != null && audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f;
            Debug.LogWarning("[TOTEM] Hay 'Full Story Clip' pero 'Audio Source' estaba vacio: he creado un AudioSource en el totem.");
        }
        if (FullVoice) PrepareVoice();
        else if (debugLogs) Debug.Log("[TOTEM] Sin voz completa: 'Full Story Clip' esta vacio (solo texto" + (voiceClips != null && voiceClips.Length > 0 ? " y voz por pagina)" : ")"));
        if (debugLogs) Debug.Log($"[TOTEM] Listo: {pages.Length} paginas" + (FullVoice ? $", voz '{fullStoryClip.name}' de {fullStoryClip.length:F0} s" : "") +
                                 $". Acercate a menos de {talkRadius:F0} m para escuchar la historia.");
    }

    // Reparto del audio entre las paginas segun su longitud de texto, y carga del clip
    void PrepareVoice()
    {
        pageStart = new float[pages.Length];
        float total = 0f;
        foreach (var pg in pages) total += Mathf.Max(1, pg.Length);
        float acc = 0f;
        for (int i = 0; i < pages.Length; i++) { pageStart[i] = fullStoryClip.length * acc / total; acc += Mathf.Max(1, pages[i].Length); }
        audioSource.clip = fullStoryClip;
        audioSource.loop = false;
        if (fullStoryClip.loadState == AudioDataLoadState.Unloaded) fullStoryClip.LoadAudioData();
        string problems = VoiceProblems();
        if (problems.Length > 0) Debug.LogWarning("[TOTEM] La voz puede no oirse:" + problems);
    }

    // Motivos tipicos por los que no se oye la voz (se escriben en la Console)
    string VoiceProblems()
    {
        string p = "";
        if (!audioSource.enabled) p += "\n - el componente AudioSource esta desactivado";
        if (audioSource.mute) p += "\n - el AudioSource tiene 'Mute' marcado";
        if (audioSource.volume < 0.05f) p += $"\n - el volumen del AudioSource es {audioSource.volume:F2}";
        if (AudioListener.volume < 0.05f) p += $"\n - el volumen general es {AudioListener.volume:F2} (menu de la muneca > Volumen, se guarda entre partidas)";
        if (AudioListener.pause) p += "\n - el audio del juego esta en pausa (AudioListener.pause)";
        var listener = FindAnyObjectByType<AudioListener>();
        if (listener == null) p += "\n - no hay ningun AudioListener activo en la escena (debe estar en la Main Camera)";
        else if (audioSource.spatialBlend > 0.01f && Vector3.Distance(listener.transform.position, transform.position) > audioSource.maxDistance)
            p += $"\n - el oyente esta mas lejos que 'Max Distance' ({audioSource.maxDistance:F0} m)";
        if (fullStoryClip.loadState == AudioDataLoadState.Failed) p += $"\n - el audio '{fullStoryClip.name}' no se ha podido cargar (revisa su Import Settings)";
        return p;
    }

    // Subtitulos: panel del estilo (madera o banda) sobre el totem, con el nombre del que habla arriba
    void CreateText()
    {
        Bounds b = new Bounds(transform.position, Vector3.one * 0.5f);
        foreach (var r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        var s = PirateStyle.Get();
        board = PirateStyle.MakePanel("Historia (subtitulos)", transform, new Vector2(1000, 330), 330f, out RectTransform content);
        board.position = new Vector3(b.center.x, b.max.y + textHeight + 0.3f, b.center.z);

        var who = PirateStyle.MakeText(content, "Quien habla", 36, false, s.gold, 28f);
        PirateStyle.ApplyFont(who, PirateStyle.FontRole.Heading);
        who.rectTransform.anchorMin = new Vector2(0f, 0.8f);
        who.alignment = TextAlignmentOptions.Left;
        who.text = speakerName;

        text = PirateStyle.MakeText(content, "Texto", 46, false, s.paperText, 40f);
        text.rectTransform.offsetMax = new Vector2(-40f, -62f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 26f;
        text.fontSizeMax = 46f;
    }

    void SetVisible(bool v)
    {
        if (board != null) board.gameObject.SetActive(v);
        else text.gameObject.SetActive(v);
    }

    /// Empezar a contar la historia desde el principio aunque el jugador este lejos (lo usa GameIntro)
    public void Tell()
    {
        if (talking) return;
        forced = true;
        Finished = false;
        page = -1;
        StartTalking();
    }

    void StartTalking()
    {
        talking = true;
        Skipped = false;
        skipHeld = 0f;
        if (Finished) { Finished = false; page = -1; }   // volver a escucharla
        if (FullVoice)
        {
            if (pageStart == null || audioSource.clip != fullStoryClip) PrepareVoice();   // clip asignado durante el Play
            if (page < 0) { page = 0; audioSource.Stop(); audioSource.Play(); Show(); }
            else { audioSource.UnPause(); Show(); }
            voiceStartedAt = Time.time;
            voiceChecked = false;
            if (debugLogs) Debug.Log($"[TOTEM] Voz: reproduciendo '{fullStoryClip.name}' (carga: {fullStoryClip.loadState}, volumen {audioSource.volume:F2}, " +
                                     $"volumen general {AudioListener.volume:F2}, isPlaying {audioSource.isPlaying})");
        }
        else if (page < 0) NextPage(); else Show();
        BeginMood();
        if (debugLogs) Debug.Log("[TOTEM] El capitan empieza a hablar");
    }

    void Update()
    {
        UpdateFire();
        if (head == null || pages == null || pages.Length == 0) return;
        Vector3 d = head.position - transform.position;
        d.y = 0f;
        float dist = d.magnitude;

        if (waitForLeave && dist > talkRadius + 1f) waitForLeave = false;
        // Durante la intro solo habla cuando GameIntro lo pide (Tell), cuando el casco ya da imagen
        if (!talking && !waitForLeave && dist < talkRadius && !GameIntro.Running) StartTalking();
        else if (talking && !forced && dist > talkRadius + 3f)
        {
            talking = false;
            SetVisible(false);
            if (audioSource != null) { if (FullVoice) audioSource.Pause(); else audioSource.Stop(); }
            EndMood();
            if (debugLogs) Debug.Log("[TOTEM] Te alejas: el capitan se calla");
        }
        if (!talking) return;

        // Que el texto mire siempre al jugador
        var tt = board != null ? (Transform)board : text.transform;
        tt.rotation = Quaternion.LookRotation(tt.position - head.position);

        bool button = advanceWithButton && XRHandInput.PrimaryButton(XRNode.LeftHand);   // solo X (mando izquierdo)
        bool pressed = button && !prevButton;
        prevButton = button;

        // Mantener B = saltar la historia
        if (allowSkip && XRHandInput.SecondaryButton(XRNode.RightHand))
        {
            skipHeld += Time.deltaTime;
            if (skipHeld >= skipHoldSeconds) { Skipped = true; EndStory(); return; }
        }
        else skipHeld = 0f;

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
        if (!voiceChecked && Time.time - voiceStartedAt > 1.5f)
        {
            voiceChecked = true;
            if (audioSource.isPlaying && audioSource.time > 0.1f) { if (debugLogs) Debug.Log($"[TOTEM] Voz OK: va por el segundo {audioSource.time:F1}"); }
            else Debug.LogWarning($"[TOTEM] La voz NO avanza (isPlaying {audioSource.isPlaying}, segundo {audioSource.time:F1}, carga {fullStoryClip.loadState})." + VoiceProblems());
        }
        if (!audioSource.isPlaying)
        {
            // Recien lanzado o aun cargando el mp3: esperar, no dar la historia por acabada
            if (fullStoryClip.loadState != AudioDataLoadState.Loaded && fullStoryClip.loadState != AudioDataLoadState.Failed) return;
            if (Time.time - voiceStartedAt < 1f) return;
            EndStory();   // el audio ha terminado
            return;
        }
        int p = 0;
        for (int i = 0; i < pageStart.Length; i++) if (audioSource.time >= pageStart[i]) p = i;
        if (p != page) { page = p; Show(); XRHandInput.HapticBoth(0.1f, 0.04f); }

        // Linea que se esta diciendo: reparto del tramo de la pagina segun la longitud de cada linea
        float end = page + 1 < pageStart.Length ? pageStart[page + 1] : fullStoryClip.length;
        float frac = Mathf.InverseLerp(pageStart[page], end, audioSource.time);
        var lines = pages[page].Split('\n');
        float total = 0f, acc = 0f;
        foreach (var l in lines) total += Mathf.Max(1, l.Length);
        int line = lines.Length - 1;
        for (int i = 0; i < lines.Length; i++)
        {
            acc += Mathf.Max(1, lines[i].Length);
            if (frac * total < acc) { line = i; break; }
        }
        if (line != shownLine) Render(line);
    }

    void EndStory()
    {
        Finished = true;
        talking = false;
        forced = false;
        waitForLeave = true;
        SetVisible(false);
        if (audioSource != null) audioSource.Stop();
        EndMood();
        if (debugLogs) Debug.Log(Skipped ? "[TOTEM] Historia saltada (B)" : "[TOTEM] Fin de la historia");
    }

    // Luces de fuego: 3 alrededor de la base, parpadeo con ruido; se encienden poco a poco al hablar
    void CreateFire()
    {
        Bounds b = new Bounds(transform.position, Vector3.one * 0.5f);
        foreach (var r in GetComponentsInChildren<Renderer>()) if (r.gameObject != text.gameObject) b.Encapsulate(r.bounds);
        fires = new Light[3];
        for (int i = 0; i < fires.Length; i++)
        {
            float ang = i * Mathf.PI * 2f / fires.Length;
            var go = new GameObject("Fuego " + (i + 1));
            go.transform.SetParent(transform, true);
            go.transform.position = new Vector3(b.center.x + Mathf.Cos(ang) * 0.8f, b.min.y + 0.4f, b.center.z + Mathf.Sin(ang) * 0.8f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = fireColor;
            l.range = fireRange;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            l.enabled = false;
            fires[i] = l;
        }
        if (fireEffectPrefab != null)
        {
            fireFx = Instantiate(fireEffectPrefab, new Vector3(b.center.x, b.min.y, b.center.z), Quaternion.identity, transform);
            fireFx.SetActive(false);
        }
    }

    void UpdateFire()
    {
        if (fires == null) return;
        fireLevel = Mathf.MoveTowards(fireLevel, talking ? 1f : 0f, Time.deltaTime / 1.5f);
        for (int i = 0; i < fires.Length; i++)
        {
            float flicker = 0.65f + 0.35f * Mathf.PerlinNoise(Time.time * 6f, i * 7.3f);
            fires[i].intensity = fireIntensity * fireLevel * flicker;
            fires[i].enabled = fireLevel > 0.001f;
        }
        if (fireFx != null && fireFx.activeSelf != fireLevel > 0.001f) fireFx.SetActive(fireLevel > 0.001f);
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
        if (page >= pages.Length) { EndStory(); return; }
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
        SetVisible(true);
        Render(FullVoice ? 0 : -1);
        timer = secondsPerPage;
    }

    // highlightLine = linea que se esta diciendo (las dichas quedan normales, las que faltan mas apagadas); -1 = todas igual
    void Render(int highlightLine)
    {
        shownLine = highlightLine;
        var s = PirateStyle.Get();
        string bright = "#" + ColorUtility.ToHtmlStringRGB(s.paperText);
        string said = "#" + ColorUtility.ToHtmlStringRGBA(new Color(s.paperText.r, s.paperText.g, s.paperText.b, 0.75f));
        string next = "#" + ColorUtility.ToHtmlStringRGBA(new Color(s.paperText.r, s.paperText.g, s.paperText.b, 0.35f));
        string gold = "#" + ColorUtility.ToHtmlStringRGB(s.gold);

        var sb = new System.Text.StringBuilder();
        var lines = pages[page].Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            if (highlightLine < 0) sb.Append($"<color={bright}>{lines[i]}</color>");
            else if (i == highlightLine) sb.Append($"<color={gold}>{lines[i]}</color>");
            else sb.Append($"<color={(i < highlightLine ? said : next)}>{lines[i]}</color>");
        }
        string help = (advanceWithButton ? "X = siguiente" : "") + (advanceWithButton && allowSkip ? "   ·   " : "") + (allowSkip ? "mantén B = saltar" : "");
        sb.Append($"\n<size=55%><color={said}><i>({page + 1}/{pages.Length})   {help}</i></color></size>");
        text.text = sb.ToString();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, talkRadius);
    }
}
