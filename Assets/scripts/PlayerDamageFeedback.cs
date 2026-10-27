using UnityEngine;

/// Feedback cuando el JUGADOR recibe dano: vibracion en los dos mandos y un velo rojo delante de los ojos.
/// Ponlo en el XR Origin junto a su Health (en ese Health desmarca "Flash Children If Empty").
/// Velo: crea un Quad hijo de Main Camera a 0.3 m (Position 0,0,0.3), quitale el collider y ponle un material
/// URP/Unlit, Surface Type = Transparent, color rojo con alpha 0. Arrastralo a 'Overlay'.
public class PlayerDamageFeedback : MonoBehaviour
{
    public Health health;
    [Tooltip("Quad rojo transparente hijo de Main Camera (opcional)")]
    public Renderer overlay;
    [Range(0f, 1f)] public float maxAlpha = 0.45f;
    public float fadeSpeed = 1.5f;
    public float hapticAmplitude = 0.8f;
    public float hapticDuration = 0.2f;
    public bool debugLogs = true;

    Material overlayMat;
    float alpha;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        if (overlay != null)
        {
            overlayMat = overlay.material;   // copia propia, no modifica el asset
            overlay.enabled = false;
        }
    }

    void OnEnable() { if (health != null) health.onDamaged.AddListener(OnDamaged); }
    void OnDisable() { if (health != null) health.onDamaged.RemoveListener(OnDamaged); }

    void Start()
    {
        if (health == null) Debug.LogWarning("[JUGADOR] PlayerDamageFeedback no encuentra el Health del jugador.");
    }

    void OnDamaged(float remaining)
    {
        XRHandInput.HapticBoth(hapticAmplitude, hapticDuration);
        alpha = maxAlpha;
        if (debugLogs) Debug.Log($"[JUGADOR] Te han hecho dano. Vida: {remaining:F0}");
    }

    void Update()
    {
        if (overlayMat == null) return;
        alpha = Mathf.MoveTowards(alpha, health != null && health.IsDead ? maxAlpha : 0f, fadeSpeed * Time.deltaTime);
        overlay.enabled = alpha > 0.001f;
        Color c = overlayMat.HasProperty(BaseColor) ? overlayMat.GetColor(BaseColor) : overlayMat.color;
        c.a = alpha;
        if (overlayMat.HasProperty(BaseColor)) overlayMat.SetColor(BaseColor, c);
        else overlayMat.color = c;
    }
}
