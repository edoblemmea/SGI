using UnityEngine;

/// Totem de la maldicion (palo con calaveras, StaffOfPain). Mientras la maldicion esta activa, su luz roja late;
/// cuando se rompe, se apaga poco a poco. Feedback visual de que el cofre esta maldito y de que se ha liberado.
public class CurseTotem : MonoBehaviour
{
    [Tooltip("Luz roja en las calaveras")]
    public Light glow;
    public Color curseColor = new Color(1f, 0.15f, 0.1f);
    public float baseIntensity = 2f;
    [Tooltip("Cuanto varia la intensidad al latir")]
    public float pulse = 1f;
    public float pulseSpeed = 2f;
    [Tooltip("Mas brillo de noche (la maldicion es mas fuerte bajo la luna)")]
    public DayNightCycle dayNight;
    public float nightBoost = 1.5f;
    public float fadeOutSeconds = 2.5f;

    float phase, fade = 1f;
    bool broken;

    void Start()
    {
        phase = Random.value * 10f;   // que no latan todos a la vez
        if (glow == null) glow = GetComponentInChildren<Light>();
        if (glow == null) { Debug.LogWarning($"[MALDICION] Totem '{name}' sin luz."); enabled = false; return; }
        glow.color = curseColor;
        if (Curse.Instance != null)
        {
            Curse.Instance.Broke += OnBroken;
            if (Curse.Instance.Broken) OnBroken();
        }
        else Debug.LogWarning($"[MALDICION] Totem '{name}': no hay objeto Curse en la escena; brillara siempre.");
    }

    void OnDestroy()
    {
        if (Curse.Instance != null) Curse.Instance.Broke -= OnBroken;
    }

    void OnBroken() => broken = true;

    void Update()
    {
        if (broken) fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime / Mathf.Max(fadeOutSeconds, 0.01f));
        float boost = dayNight != null && dayNight.IsNight ? nightBoost : 1f;
        float i = (baseIntensity + Mathf.Sin(Time.time * pulseSpeed + phase) * pulse) * boost * fade;
        glow.intensity = Mathf.Max(0f, i);
        glow.enabled = fade > 0.01f;
        if (broken && fade <= 0f) enabled = false;
    }
}
