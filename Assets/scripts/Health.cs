using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// Vida comun para jugador, aranas, piratas... Quien hace dano busca un Health en el objeto golpeado o sus padres
/// (collider.GetComponentInParent<Health>()), asi que ponlo en la RAIZ del personaje.
/// Para el jugador: ponlo en el XR Origin (XR Rig).
public class Health : MonoBehaviour
{
    [Header("Vida")]
    public float maxHealth = 100f;
    [Tooltip("Segundos sin poder recibir mas dano tras un golpe")]
    public float invulnerableTime = 0.3f;
    [Tooltip("Vida que recupera por segundo (0 = nada)")]
    public float regenPerSecond = 0f;
    [Tooltip("Segundos sin recibir dano antes de empezar a regenerar")]
    public float regenDelay = 5f;

    [Header("Muerte")]
    public bool destroyOnDeath = false;
    public float destroyDelay = 3f;

    [Header("Flash de dano (opcional)")]
    [Tooltip("Mallas que se tinen al recibir dano. Vacio = todas las hijas (no se usa en el jugador).")]
    public Renderer[] flashRenderers;
    public bool flashChildrenIfEmpty = true;
    public Color flashColor = new Color(1f, 0.2f, 0.2f);
    public float flashTime = 0.15f;

    [Header("Barra de vida")]
    [Tooltip("Anade una HealthBar al empezar: sobre la cabeza en enemigos, en la vista en el jugador. Sale al recibir dano")]
    public bool showHealthBar = true;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip hurtClip;
    public AudioClip deathClip;

    [Header("Eventos")]
    [Tooltip("Recibe la vida que queda")]
    public UnityEvent<float> onDamaged;
    public UnityEvent onDeath;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public float Current { get; private set; }
    public bool IsDead { get; private set; }
    public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;

    float lastHitTime = -999f;
    MaterialPropertyBlock block;
    Coroutine flashRoutine;

    void Awake()
    {
        Current = maxHealth;
        if ((flashRenderers == null || flashRenderers.Length == 0) && flashChildrenIfEmpty)
            flashRenderers = GetComponentsInChildren<Renderer>();
        if (showHealthBar && GetComponent<HealthBar>() == null) gameObject.AddComponent<HealthBar>();
    }

    void Update()
    {
        if (IsDead || regenPerSecond <= 0f || Current >= maxHealth) return;
        if (Time.time - lastHitTime > regenDelay)
            Current = Mathf.Min(maxHealth, Current + regenPerSecond * Time.deltaTime);
    }

    /// Devuelve true si el dano se aplico (no estaba muerto ni invulnerable).
    public bool TakeDamage(float amount, GameObject source = null)
    {
        if (IsDead || amount <= 0f) return false;
        if (Time.time - lastHitTime < invulnerableTime) return false;
        lastHitTime = Time.time;

        Current = Mathf.Max(0f, Current - amount);
        if (debugLogs) Debug.Log($"[VIDA] '{name}' recibe {amount:F0} de dano de '{(source != null ? source.name : "?")}'. Le queda {Current:F0}/{maxHealth:F0}");

        onDamaged?.Invoke(Current);
        if (flashRenderers != null && flashRenderers.Length > 0)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
        }

        if (Current <= 0f) Die();
        else Play(hurtClip);
        return true;
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Min(maxHealth, Current + amount);
    }

    public void Kill() => TakeDamage(Current + 1f);

    void Die()
    {
        IsDead = true;
        if (debugLogs) Debug.Log($"[VIDA] '{name}' ha muerto");
        Play(deathClip);
        onDeath?.Invoke();
        if (destroyOnDeath) Destroy(gameObject, destroyDelay);
    }

    void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    IEnumerator Flash()
    {
        block ??= new MaterialPropertyBlock();
        foreach (var r in flashRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", flashColor);   // URP Lit
            block.SetColor("_Color", flashColor);       // shaders Built-in / antiguos
            r.SetPropertyBlock(block);
        }
        yield return new WaitForSeconds(flashTime);
        foreach (var r in flashRenderers)
            if (r != null) r.SetPropertyBlock(null);
        flashRoutine = null;
    }
}
