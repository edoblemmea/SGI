using UnityEngine;
using UnityEngine.Events;

/// Ciclo dia/noche: gira la luz direccional (sol), cambia su intensidad y color, la luz ambiente y la
/// exposicion del skybox. Ponlo en un objeto vacio "DayNight" y arrastra la Directional Light a 'Sun'.
// DEMO: la Directional Light y el skybox vienen de la Demo Scene de Island; en la escena propia asigna los nuevos.
public class DayNightCycle : MonoBehaviour
{
    [Header("Referencias")]
    public Light sun;
    [Tooltip("Luz azulada opcional para la noche (Directional Light con poca intensidad)")]
    public Light moon;

    [Header("Tiempo")]
    [Range(0f, 24f)] public float hour = 9f;
    [Tooltip("Minutos reales que dura un dia completo (0 = el tiempo no avanza)")]
    public float dayLengthMinutes = 10f;
    [Tooltip("Orientacion horizontal del sol (grados)")]
    public float sunYaw = 170f;
    public float nightStartsAt = 20f;
    public float dayStartsAt = 6.5f;

    [Header("Aspecto")]
    [Tooltip("Intensidad del sol segun la hora (0..1 del dia en X)")]
    public AnimationCurve sunIntensity = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.25f, 0f), new Keyframe(0.32f, 0.8f),
        new Keyframe(0.5f, 1.3f), new Keyframe(0.68f, 0.8f), new Keyframe(0.77f, 0f), new Keyframe(1f, 0f));
    public Gradient sunColor = DefaultSunColor();
    public float moonIntensity = 0.25f;
    public float dayAmbient = 1f;
    public float nightAmbient = 0.25f;
    public float daySkyExposure = 1.3f;
    public float nightSkyExposure = 0.15f;

    [Header("Eventos")]
    public UnityEvent onNightStarted;
    public UnityEvent onDayStarted;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool IsNight { get; private set; }
    public bool IsTransitioning { get; private set; }
    /// 0..1 durante una transicion (para animar el reloj de arena)
    public float TransitionProgress { get; private set; }

    Material sky;
    Coroutine transition;
    static readonly int Exposure = Shader.PropertyToID("_Exposure");

    void Start()
    {
        if (sun == null) Debug.LogWarning("[DIANOCHE] Falta 'Sun' (la Directional Light).");
        // Copia del skybox para no modificar el material del asset al jugar en el editor
        if (RenderSettings.skybox != null)
        {
            sky = new Material(RenderSettings.skybox);
            RenderSettings.skybox = sky;
            if (!sky.HasProperty(Exposure) && debugLogs)
                Debug.Log($"[DIANOCHE] El skybox '{sky.shader.name}' no tiene _Exposure: solo cambiara la luz.");
        }
        IsNight = IsNightHour(hour);
        Apply();
        if (debugLogs) Debug.Log($"[DIANOCHE] Empieza a las {hour:F1} h ({(IsNight ? "noche" : "dia")}). Dia de {dayLengthMinutes} min.");
    }

    /// Avanza el reloj hasta 'targetHour' en 'seconds' segundos (siempre hacia delante, como el tiempo real)
    public void TransitionTo(float targetHour, float seconds)
    {
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(Transition(Mathf.Repeat(targetHour, 24f), seconds));
    }

    /// De dia -> 23 h; de noche -> 9 h (para botones del menu)
    public void ToggleDayNight(float seconds) => TransitionTo(IsNight ? 9f : 23f, seconds);

    // Para probar sin casco: clic derecho en el componente
    [ContextMenu("Hacer de noche")] void DebugNight() => TransitionTo(23f, 3f);
    [ContextMenu("Hacer de dia")] void DebugDay() => TransitionTo(9f, 3f);

    System.Collections.IEnumerator Transition(float target, float seconds)
    {
        IsTransitioning = true;
        float start = hour;
        float delta = Mathf.Repeat(target - start, 24f);
        if (debugLogs) Debug.Log($"[DIANOCHE] Pasa el tiempo: {start:F1} h -> {target:F1} h en {seconds:F1} s");
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(seconds, 0.01f))
        {
            TransitionProgress = t;
            hour = Mathf.Repeat(start + delta * Mathf.SmoothStep(0f, 1f, t), 24f);
            yield return null;
        }
        hour = target;
        TransitionProgress = 1f;
        IsTransitioning = false;
        transition = null;
    }

    void Update()
    {
        if (dayLengthMinutes > 0f && !IsTransitioning)
            hour = Mathf.Repeat(hour + 24f * Time.deltaTime / (dayLengthMinutes * 60f), 24f);

        bool night = IsNightHour(hour);
        if (night != IsNight)
        {
            IsNight = night;
            if (debugLogs) Debug.Log(night ? "[DIANOCHE] Anochece" : "[DIANOCHE] Amanece");
            (night ? onNightStarted : onDayStarted)?.Invoke();
        }
        Apply();
    }

    bool IsNightHour(float h) => nightStartsAt > dayStartsAt ? (h >= nightStartsAt || h < dayStartsAt) : (h >= nightStartsAt && h < dayStartsAt);

    void Apply()
    {
        float t = hour / 24f;
        float dayAmount = Mathf.Clamp01(sunIntensity.Evaluate(t));

        if (sun != null)
        {
            // 6h = sale por el horizonte, 12h = arriba, 18h = se pone
            sun.transform.rotation = Quaternion.Euler(t * 360f - 90f, sunYaw, 0f);
            sun.intensity = sunIntensity.Evaluate(t);
            sun.color = sunColor.Evaluate(t);
            sun.enabled = sun.intensity > 0.01f;
        }
        if (moon != null)
        {
            moon.transform.rotation = Quaternion.Euler(t * 360f + 90f, sunYaw, 0f);
            moon.intensity = moonIntensity * (1f - dayAmount);
            moon.enabled = moon.intensity > 0.01f;
        }

        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbient, dayAmbient, dayAmount);
        if (sky != null && sky.HasProperty(Exposure))
            sky.SetFloat(Exposure, Mathf.Lerp(nightSkyExposure, daySkyExposure, dayAmount));
    }

    static Gradient DefaultSunColor()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.45f, 0.2f), 0.25f),
                new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0.4f),
                new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0.6f),
                new GradientColorKey(new Color(1f, 0.45f, 0.2f), 0.75f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
