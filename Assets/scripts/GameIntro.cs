using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// Inicio de la partida:
///   1. Es de noche, sin musica y sin poder moverse: el totem (con luz de fuego) cuenta la historia.
///      X = pagina siguiente; mantener B = saltar la historia.
///   2. Al acabar (o saltarla) sale el titulo del juego mientras amanece.
///   3. Empieza la musica y el jugador ya se puede mover.
/// Va en un objeto vacio "Intro". Lo monta "Isla Pirata > Intro".
public class GameIntro : MonoBehaviour
{
    /// La intro esta en marcha (las pistas de controles esperan a que acabe)
    public static bool Running { get; private set; }

    [Header("Historia")]
    [Tooltip("Totem que cuenta la historia al empezar")]
    public StoryTotem storyTotem;
    [Tooltip("Desmarcado = se salta la historia y empieza directamente con el titulo (para probar)")]
    public bool playStory = true;
    [Tooltip("Al reiniciar la partida (GameManager.Restart) no se repite la historia, solo el titulo")]
    public bool storyOnlyFirstTime = true;

    static bool storyPlayed;   // sobrevive a la recarga de la escena

    // Cada vez que se pulsa Play en el editor se vuelve a contar (aunque no se recargue el dominio)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { storyPlayed = false; Running = false; }

    [Header("Ambiente")]
    [Tooltip("Ciclo dia/noche. Vacio = se busca solo")]
    public DayNightCycle dayNight;
    [Range(0f, 24f)] public float storyHour = 23f;
    [Tooltip("Hora a la que se juega (amanece al acabar la historia)")]
    [Range(0f, 24f)] public float gameHour = 9f;
    [Tooltip("Segundos que tarda en amanecer (mientras sale el titulo)")]
    public float dawnSeconds = 6f;
    [Tooltip("Musica de fondo (el objeto 'Musica' del GameManager): callada durante la historia")]
    public AudioSource music;
    public float musicFadeIn = 3f;
    [Tooltip("Niebla durante la historia (se disipa al amanecer)")]
    public bool storyFog = true;
    public Color fogColor = new Color(0.07f, 0.09f, 0.14f);
    [Range(0f, 0.2f)] public float fogDensity = 0.035f;

    [Header("Titulo")]
    public string gameTitle = "La Maldición del Capitán Calavera";
    public string subtitle = "Isla Pirata";
    [Tooltip("Vacio (recomendado) = cartel de madera con marco dorado. Si se asigna un texto, solo se escribe ahi")]
    public TMP_Text titleText;
    public float titleDistance = 3.5f;
    [Tooltip("Metros por encima de los ojos")]
    public float titleHeight = 0.25f;
    public float titleFade = 1.5f;
    public float titleHold = 4f;
    public Color titleColor = new Color(1f, 0.8f, 0.38f);
    [Tooltip("Sonido al aparecer el titulo. Vacio = un gong generado por codigo")]
    public AudioClip titleClip;
    [Tooltip("Imagen propia del titulo (PNG importado como Sprite). Si se pone, sustituye al cartel de madera")]
    public Sprite titleImage;
    [Tooltip("Ancho de la imagen en metros (el alto sale de sus proporciones)")]
    public float titleImageWidth = 3f;
    [Tooltip("Escribir tambien 'Game Title' y 'Subtitle' encima de la imagen (desmarcado si el nombre ya esta dibujado en ella)")]
    public bool textOverImage = false;

    public enum Reveal { IzquierdaADerecha, ArribaAAbajo, Circular, Fundido, Pisoton }
    [Tooltip("Imagen del texto (PNG transparente del MISMO tamano en pixeles que 'Title Image'), que aparece animada encima del panel")]
    public Sprite titleTextImage;
    [Tooltip("Como aparece el texto: Pisoton (cae desde grande y golpea el tablero, como el efecto de Canva), barrido de izquierda a derecha (como si se escribiera), de arriba abajo, en circulo o fundido")]
    public Reveal textReveal = Reveal.Pisoton;
    [Tooltip("Pisoton: tamano del texto al empezar a caer (2 = el doble)")]
    public float stompScale = 2.2f;
    [Tooltip("Pisoton: segundos que tarda en caer (corto = mas golpe)")]
    public float stompSeconds = 0.28f;
    [Tooltip("Pisoton: cuanto tiembla el cartel con el golpe (metros)")]
    public float stompShake = 0.025f;
    [Tooltip("Pisoton: centro del texto dentro de la imagen (0,0 = abajo a la izquierda; 1,1 = arriba a la derecha). El texto crece y cae hacia este punto")]
    public Vector2 stompCenter = new Vector2(0.5f, 0.3f);
    [Tooltip("Segundos desde que sale el panel hasta que empieza a aparecer el texto")]
    public float textRevealDelay = 0.8f;
    [Tooltip("Segundos que tarda en aparecer el texto entero")]
    public float textRevealSeconds = 2.5f;
    [Tooltip("Destello que va por el borde del texto mientras aparece (solo en los barridos)")]
    public bool revealGlow = false;
    public Color revealGlowColor = new Color(1f, 0.85f, 0.45f);

    [Header("Movimiento")]
    [Tooltip("No dejar andar ni teletransportarse hasta que acabe el titulo (el giro si se permite)")]
    public bool lockLocomotion = true;

    [Header("Arranque")]
    [Tooltip("No empezar la historia hasta que el casco muestre imagen. Con Quest Link la imagen tarda 1-3 s en salir " +
             "despues de pulsar Play y se oia la voz con la pantalla en negro")]
    public bool waitForHeadset = true;
    [Tooltip("Segundos de espera extra cuando el casco ya esta listo (primeros fotogramas a tirones)")]
    public float startDelay = 1f;
    [Tooltip("Espera maxima al casco (sin casco o en el simulador empieza al momento)")]
    public float headsetTimeout = 10f;
    [Tooltip("Silenciar todo el sonido (olas, musica, voz) hasta que el casco este listo")]
    public bool muteUntilReady = true;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    Transform head;
    RectTransform card;
    CanvasGroup cardGroup;
    TMP_Text cardTitle, cardSubtitle;
    UnityEngine.UI.Image textImage, glow;
    float musicVolume;
    readonly List<Behaviour> locked = new List<Behaviour>();
    bool totemNightBefore;
    bool fogBefore;
    Color fogColorBefore;
    float fogDensityBefore;
    FogMode fogModeBefore;

    void Awake()
    {
        Running = true;
        if (waitForHeadset && muteUntilReady) AudioListener.pause = true;   // las olas del GameManager tambien esperan
        if (dayNight == null) dayNight = FindAnyObjectByType<DayNightCycle>();
        // Antes del Start de DayNightCycle: la partida arranca ya de noche, sin transicion
        if (dayNight != null) dayNight.hour = storyHour;
        else Debug.LogWarning("[INTRO] No hay DayNightCycle en la escena: no se hara de noche.");
    }

    void OnDestroy()
    {
        Running = false;
        if (waitForHeadset && muteUntilReady) AudioListener.pause = false;
    }

    // Hay casco (subsistema de pantalla XR) y ya esta pintando con la cabeza localizada.
    // Sin subsistema XR (simulador, sin Link) se considera listo: se ve en la ventana Game.
    static bool HeadsetReady()
    {
        var displays = new List<UnityEngine.XR.XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        if (displays.Count == 0) return true;
        bool running = false;
        foreach (var d in displays) running |= d.running;
        if (!running) return false;
        var hmd = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
        return hmd.isValid && hmd.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked;
    }

    IEnumerator WaitForHeadset()
    {
        float t0 = Time.realtimeSinceStartup;
        while (!HeadsetReady() && Time.realtimeSinceStartup - t0 < headsetTimeout) yield return null;
        bool ok = HeadsetReady();
        yield return new WaitForSecondsRealtime(startDelay);
        if (muteUntilReady) AudioListener.pause = false;
        if (debugLogs)
        {
            float waited = Time.realtimeSinceStartup - t0;
            if (ok) Debug.Log($"[INTRO] Casco listo: empiezo tras {waited:F1} s");
            else Debug.LogWarning($"[INTRO] El casco no ha dado imagen en {headsetTimeout:F0} s: empiezo igualmente (¿Quest Link conectado? ¿OpenXR activo?)");
        }
    }

    void Start()
    {
        if (Camera.main != null) head = Camera.main.transform;
        if (music != null)
        {
            musicVolume = music.volume > 0.01f ? music.volume : 0.25f;
            music.Stop();
        }
        else if (debugLogs) Debug.Log("[INTRO] 'Music' vacio: no se calla ninguna musica.");
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return null;   // despues de los Start que encienden el movimiento (BoatRowing, PlayerRescue)
        if (lockLocomotion) LockLocomotion(true);
        if (storyFog) FogOn();
        if (waitForHeadset) yield return WaitForHeadset();

        bool story = playStory && storyTotem != null && !(storyOnlyFirstTime && storyPlayed);
        if (story)
        {
            storyPlayed = true;
            if (debugLogs) Debug.Log("[INTRO] Empieza la historia del totem (X = siguiente, mantener B = saltar)");
            totemNightBefore = storyTotem.nightDuringStory;
            storyTotem.nightDuringStory = false;   // la noche la pone la intro
            storyTotem.Tell();
            while (!storyTotem.Finished) yield return null;
            storyTotem.nightDuringStory = totemNightBefore;
            if (debugLogs) Debug.Log(storyTotem.Skipped ? "[INTRO] Historia saltada" : "[INTRO] Historia terminada");
        }
        else if (debugLogs) Debug.Log(storyTotem == null ? "[INTRO] 'Story Totem' vacio: sin historia." :
                                      !playStory ? "[INTRO] 'Play Story' desmarcado: sin historia." : "[INTRO] Partida reiniciada: la historia ya se conto, solo titulo.");

        // Titulo mientras amanece (y se va la niebla)
        if (dayNight != null) dayNight.TransitionTo(gameHour, dawnSeconds);
        if (storyFog) StartCoroutine(FogOff());
        yield return ShowTitle();

        // Empieza el juego
        if (lockLocomotion) LockLocomotion(false);
        if (music != null) StartCoroutine(FadeInMusic());
        XRHandInput.HapticBoth(0.3f, 0.1f);
        Running = false;
        if (debugLogs) Debug.Log("[INTRO] Fin de la intro: ya te puedes mover");
    }

    IEnumerator ShowTitle()
    {
        if (string.IsNullOrEmpty(gameTitle)) yield break;
        if (titleText != null) { yield return SimpleTitle(); yield break; }   // texto propio asignado a mano

        BuildCard();
        Vector3 f = Vector3.forward;
        Vector3 basePos = transform.position;
        if (head != null)
        {
            f = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            basePos = head.position + f * titleDistance + Vector3.up * titleHeight;
        }
        card.position = basePos;
        card.rotation = Quaternion.LookRotation(f, Vector3.up);   // fijo en el mundo (no pegado a la cara: marea menos)
        card.gameObject.SetActive(true);
        if (debugLogs) Debug.Log($"[INTRO] Titulo: '{gameTitle}'");

        var clip = titleClip != null ? titleClip : ProceduralSounds.Gong();
        ProceduralSounds.Play(clip, basePos, 0.9f, 0.3f, 0f);
        XRHandInput.HapticBoth(0.4f, 0.25f);

        Vector3 scale0 = card.localScale;
        float total = titleFade * 2f + titleHold;
        // Con texto animado, que le de tiempo a aparecer entero y a verse 1,5 s antes de irse
        bool stomp = textReveal == Reveal.Pisoton;
        float revealDur = Mathf.Max(0.05f, stomp ? stompSeconds : textRevealSeconds);
        if (textImage != null) total = Mathf.Max(total, textRevealDelay + revealDur + 1.5f + titleFade);
        bool revealed = false;
        float impactT = -10f;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            Vector3 shake = Vector3.zero;
            if (textImage != null)
            {
                float k = Mathf.Clamp01((t - textRevealDelay) / revealDur);
                UpdateReveal(k);
                if (k >= 1f && !revealed)
                {
                    revealed = true;
                    impactT = t;
                    if (stomp)
                    {
                        ProceduralSounds.Play(ProceduralSounds.Thud(), card.position, 1f, 0.3f, 0f);
                        XRHandInput.HapticBoth(0.8f, 0.18f);
                    }
                    else XRHandInput.HapticBoth(0.25f, 0.12f);
                    if (debugLogs) Debug.Log("[INTRO] Texto del titulo completo");
                }
                // Tras el pisoton: el texto se aplasta un poco y rebota, y el cartel tiembla
                float u = (t - impactT) / 0.3f;
                if (stomp && u >= 0f && u < 1f)
                {
                    textImage.rectTransform.localScale = Vector3.one * (1f - 0.07f * Mathf.Sin(Mathf.PI * u) * (1f - u));
                    Vector2 r = Random.insideUnitCircle * stompShake * (1f - u);
                    shake = card.right * r.x + card.up * r.y;
                }
            }
            float inK = Mathf.Clamp01(t / titleFade);
            float outK = Mathf.Clamp01((t - titleFade - titleHold) / titleFade);
            // Entrada: aparece creciendo un poco y las letras se juntan; salida: se desvanece agrandandose
            cardGroup.alpha = Mathf.Min(EaseOut(inK), 1f - outK);
            card.localScale = scale0 * (Mathf.Lerp(0.9f, 1f, EaseOutBack(inK)) + 0.04f * outK);
            cardTitle.characterSpacing = Mathf.Lerp(28f, 0f, EaseOut(inK));
            cardSubtitle.alpha = Mathf.Clamp01((t - titleFade * 0.6f) / titleFade) * (1f - outK);
            // Brillo dorado que late y vaiven suave
            cardTitle.color = Color.Lerp(titleColor, Color.Lerp(titleColor, Color.white, 0.35f), 0.5f + 0.5f * Mathf.Sin(t * 2.2f));
            card.position = basePos + Vector3.up * (Mathf.Sin(t * 0.9f) * 0.015f) + shake;
            yield return null;
        }
        card.gameObject.SetActive(false);
        card.localScale = scale0;
    }

    static float EaseOut(float k) => 1f - (1f - k) * (1f - k);
    static float EaseOutBack(float k) { const float c = 1.6f; k -= 1f; return 1f + (c + 1f) * k * k * k + c * k * k; }

    // Version sencilla (si se asigna 'Title Text' a mano)
    IEnumerator SimpleTitle()
    {
        var tt = titleText.transform;
        if (head != null)
        {
            Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            tt.position = head.position + f * titleDistance + Vector3.up * titleHeight;
            tt.rotation = Quaternion.LookRotation(f, Vector3.up);
        }
        titleText.text = $"{gameTitle}" + (string.IsNullOrEmpty(subtitle) ? "" : $"\n<size=45%><i>{subtitle}</i></size>");
        titleText.gameObject.SetActive(true);
        float total = titleFade * 2f + titleHold;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            titleText.alpha = Mathf.Clamp01(Mathf.Min(t / titleFade, (total - t) / titleFade));
            yield return null;
        }
        titleText.gameObject.SetActive(false);
    }

    // Cartel de madera con marco dorado: adorno, titulo (fuente pirata, dorado con sombra), adorno y subtitulo
    void BuildCard()
    {
        if (card != null) return;
        var s = PirateStyle.Get();
        bool image = titleImage != null;
        if (image)
        {
            // Imagen propia: el lienzo toma sus proporciones y mide 'titleImageWidth' metros de ancho
            float aspect = titleImage.rect.height / Mathf.Max(1f, titleImage.rect.width);
            card = PirateStyle.MakeCanvas("Titulo (intro, imagen)", transform, new Vector2(1300f, 1300f * aspect),
                                          1300f / Mathf.Max(0.1f, titleImageWidth), titleImage, Color.white);
            var img = card.GetComponent<UnityEngine.UI.Image>();
            img.type = UnityEngine.UI.Image.Type.Simple;
            img.preserveAspect = true;
            if (debugLogs) Debug.Log($"[INTRO] Titulo con imagen propia '{titleImage.name}' ({titleImage.rect.width:F0}x{titleImage.rect.height:F0} px, {titleImageWidth:F1} m de ancho)");
        }
        else
            card = PirateStyle.MakeCanvas("Titulo (intro)", transform, new Vector2(1300, 560), 420f,
                                          s.darkPanel, s.darkPanel != null ? Color.white : new Color(0.16f, 0.09f, 0.04f, 0.95f));
        cardGroup = card.GetComponent<CanvasGroup>();
        cardGroup.alpha = 0f;

        if (!image)
        {
            Ornament(card, 175f, s.gold);
            Ornament(card, -120f, s.gold);
        }

        cardTitle = PirateStyle.MakeText(card, "Titulo", 112, true, titleColor, 70f);
        var trt = cardTitle.rectTransform;
        trt.anchorMin = new Vector2(0f, 0.28f);
        trt.anchorMax = new Vector2(1f, 0.82f);
        trt.offsetMin = new Vector2(70f, 0f);
        trt.offsetMax = new Vector2(-70f, 0f);
        cardTitle.enableAutoSizing = true;
        cardTitle.fontSizeMin = 50f;
        cardTitle.fontSizeMax = 112f;
        cardTitle.lineSpacing = -18f;
        cardTitle.text = gameTitle;
        cardTitle.outlineWidth = 0.12f;
        cardTitle.outlineColor = new Color32(40, 18, 4, 255);
        // Sombra suave debajo de las letras (underlay del shader de TMP)
        var mat = cardTitle.fontMaterial;
        if (mat.HasProperty(ShaderUtilities.ID_UnderlayColor))
        {
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.8f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.5f);
        }

        cardSubtitle = PirateStyle.MakeText(card, "Subtitulo", 40, false, s.paperText, 60f);
        var srt = cardSubtitle.rectTransform;
        srt.anchorMin = new Vector2(0f, 0.04f);
        srt.anchorMax = new Vector2(1f, 0.24f);
        srt.offsetMin = srt.offsetMax = Vector2.zero;
        cardSubtitle.fontStyle = FontStyles.Italic;
        cardSubtitle.characterSpacing = 12f;
        cardSubtitle.text = subtitle;
        cardSubtitle.alpha = 0f;
        if ((image || titleTextImage != null) && !textOverImage) { cardTitle.text = ""; cardSubtitle.text = ""; }   // el nombre ya va dibujado en la imagen
        if (titleTextImage != null) BuildTextImage(s);
        card.gameObject.SetActive(false);
    }

    // Imagen del texto encima del panel, ocupando todo el cartel (por eso las dos imagenes deben medir lo mismo)
    void BuildTextImage(PirateStyle s)
    {
        var go = new GameObject("Texto (imagen)", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(card, false);
        rt.pivot = textReveal == Reveal.Pisoton ? stompCenter : new Vector2(0.5f, 0.5f);   // el pisoton escala desde la palabra
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        textImage = go.AddComponent<UnityEngine.UI.Image>();
        textImage.sprite = titleTextImage;
        textImage.preserveAspect = true;
        textImage.raycastTarget = false;
        if (textReveal == Reveal.Fundido || textReveal == Reveal.Pisoton) textImage.type = UnityEngine.UI.Image.Type.Simple;
        else
        {
            textImage.type = UnityEngine.UI.Image.Type.Filled;
            switch (textReveal)
            {
                case Reveal.IzquierdaADerecha:
                    textImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                    textImage.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
                    break;
                case Reveal.ArribaAAbajo:
                    textImage.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
                    textImage.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Top;
                    break;
                default:
                    textImage.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
                    textImage.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
                    textImage.fillClockwise = true;
                    break;
            }
        }

        // Destello que recorre el borde del barrido (necesita el sprite 'circle' del estilo pirata)
        bool sweep = textReveal == Reveal.IzquierdaADerecha || textReveal == Reveal.ArribaAAbajo;
        if (revealGlow && sweep && s.circle != null)
        {
            var g = new GameObject("Destello", typeof(RectTransform));
            var grt = (RectTransform)g.transform;
            grt.SetParent(card, false);
            Vector2 sz = card.rect.size;
            grt.sizeDelta = textReveal == Reveal.IzquierdaADerecha ? new Vector2(90f, sz.y * 0.8f) : new Vector2(sz.x * 0.8f, 90f);
            glow = g.AddComponent<UnityEngine.UI.Image>();
            glow.sprite = s.circle;
            glow.color = new Color(revealGlowColor.r, revealGlowColor.g, revealGlowColor.b, 0f);
            glow.raycastTarget = false;
        }
        UpdateReveal(0f);
        if (debugLogs)
        {
            Debug.Log($"[INTRO] Texto animado '{titleTextImage.name}' ({textReveal}, empieza a los {textRevealDelay:F1} s y tarda {(textReveal == Reveal.Pisoton ? stompSeconds : textRevealSeconds):F2} s)");
            if (titleImage != null && (Mathf.Abs(titleImage.rect.width - titleTextImage.rect.width) > 1f || Mathf.Abs(titleImage.rect.height - titleTextImage.rect.height) > 1f))
                Debug.LogWarning($"[INTRO] El panel ({titleImage.rect.width:F0}x{titleImage.rect.height:F0} px) y el texto ({titleTextImage.rect.width:F0}x{titleTextImage.rect.height:F0} px) " +
                                 "no miden lo mismo: el texto puede quedar descolocado. Exporta las dos imagenes con el mismo tamano de lienzo.");
        }
    }

    // k = 0 (sin texto) .. 1 (texto entero)
    void UpdateReveal(float k)
    {
        if (textReveal == Reveal.Pisoton)
        {
            // Cae acelerando desde grande hasta su tamano (sin frenar: el golpe es el final) y se hace opaco enseguida
            textImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(stompScale, 1f, k * k * k);
            textImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 3f));
            return;
        }
        float e = k * k * (3f - 2f * k);   // empieza y acaba suave
        if (textReveal == Reveal.Fundido) textImage.color = new Color(1f, 1f, 1f, e);
        else textImage.fillAmount = e;
        if (glow == null) return;
        Vector2 sz = card.rect.size;
        glow.rectTransform.anchoredPosition = textReveal == Reveal.IzquierdaADerecha
            ? new Vector2(Mathf.Lerp(-sz.x * 0.5f, sz.x * 0.5f, e), 0f)
            : new Vector2(0f, Mathf.Lerp(sz.y * 0.5f, -sz.y * 0.5f, e));
        var c = glow.color;
        c.a = k > 0f && k < 1f ? Mathf.Sin(Mathf.PI * k) * 0.7f : 0f;
        glow.color = c;
    }

    // Filete dorado con un rombo en medio y dos puntos: — ◆ —
    static void Ornament(RectTransform parent, float y, Color gold)
    {
        Bar(parent, new Vector2(-190f, y), new Vector2(300f, 4f), 0f, gold);
        Bar(parent, new Vector2(190f, y), new Vector2(300f, 4f), 0f, gold);
        Bar(parent, new Vector2(0f, y), new Vector2(26f, 26f), 45f, gold);
        Bar(parent, new Vector2(-355f, y), new Vector2(10f, 10f), 45f, gold);
        Bar(parent, new Vector2(355f, y), new Vector2(10f, 10f), 45f, gold);
    }

    static void Bar(RectTransform parent, Vector2 pos, Vector2 size, float angle, Color c)
    {
        var go = new GameObject("Adorno", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = c;
        img.raycastTarget = false;
    }

    void FogOn()
    {
        fogBefore = RenderSettings.fog;
        fogColorBefore = RenderSettings.fogColor;
        fogDensityBefore = RenderSettings.fogDensity;
        fogModeBefore = RenderSettings.fogMode;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }

    // La niebla se disipa durante el amanecer y vuelve a como estaba en la escena
    IEnumerator FogOff()
    {
        for (float t = 0f; t < dawnSeconds; t += Time.deltaTime)
        {
            float k = t / Mathf.Max(0.01f, dawnSeconds);
            RenderSettings.fogDensity = Mathf.Lerp(fogDensity, fogBefore ? fogDensityBefore : 0f, k);
            RenderSettings.fogColor = Color.Lerp(fogColor, fogBefore ? fogColorBefore : new Color(0.6f, 0.7f, 0.8f), k);
            yield return null;
        }
        RenderSettings.fog = fogBefore;
        RenderSettings.fogMode = fogModeBefore;
        RenderSettings.fogColor = fogColorBefore;
        RenderSettings.fogDensity = fogDensityBefore;
    }

    IEnumerator FadeInMusic()
    {
        music.volume = 0f;
        music.Play();
        for (float t = 0f; t < musicFadeIn; t += Time.deltaTime)
        {
            music.volume = musicVolume * t / musicFadeIn;
            yield return null;
        }
        music.volume = musicVolume;
        if (debugLogs) Debug.Log($"[INTRO] Musica en marcha ('{(music.clip != null ? music.clip.name : "sin clip")}')");
    }

    // Apaga andar, teletransporte, gravedad, trepar... del XR Origin; deja el giro
    void LockLocomotion(bool on)
    {
        if (on)
        {
            locked.Clear();
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null) { Debug.LogWarning("[INTRO] No encuentro el XR Origin: no se bloquea el movimiento."); return; }
            foreach (var p in origin.GetComponentsInChildren<LocomotionProvider>())
            {
                if (p is SnapTurnProvider || p is ContinuousTurnProvider || !p.enabled) continue;
                p.enabled = false;
                locked.Add(p);
            }
            if (debugLogs) Debug.Log($"[INTRO] Movimiento bloqueado durante la intro ({locked.Count} providers; el giro sigue activo)");
        }
        else
        {
            foreach (var b in locked) if (b != null) b.enabled = true;
            locked.Clear();
        }
    }
}
