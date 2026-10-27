using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Coco: hace dano a cualquier Health (arana, pirata o jugador) al chocar con fuerza.
/// - Si cae de la palmera, puede dar al jugador en la cabeza (Player Head / Player Health los rellena CoconutTree).
/// - Si lo coges (XR Grab Interactable) y lo lanzas, dana a los enemigos; mientras lo tienes en la mano no hace nada.
/// Prefab: esfera con Rigidbody (masa 1), Sphere Collider, XR Grab Interactable (Throw On Detach marcado) y este script.
public class Coconut : MonoBehaviour
{
    [Header("Dano")]
    public float damagePerMeterPerSecond = 5f;
    public float minImpactSpeed = 3f;
    public float maxDamage = 40f;

    [Header("Golpe en la cabeza al caer")]
    public Transform playerHead;
    public Health playerHealth;
    public float headRadius = 0.3f;

    [Header("Vida del coco")]
    [Tooltip("Segundos hasta desaparecer si nadie lo coge (0 = nunca)")]
    public float lifetime = 40f;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip hitClip;

    public bool debugLogs = true;

    Rigidbody rb;
    XRGrabInteractable grab;
    bool held;
    float lastGrabTime = -999f;
    readonly HashSet<Health> hitThisFlight = new HashSet<Health>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        if (grab == null) return;
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        if (grab == null) return;
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    void Start()
    {
        if (lifetime > 0f) Invoke(nameof(Expire), lifetime);
    }

    void OnGrab(SelectEnterEventArgs a) { held = true; lastGrabTime = Time.time; CancelInvoke(nameof(Expire)); }

    void OnRelease(SelectExitEventArgs a)
    {
        held = false;
        hitThisFlight.Clear();   // un lanzamiento nuevo puede volver a golpear
        if (lifetime > 0f) Invoke(nameof(Expire), lifetime);
    }

    void Expire() { if (!held) Destroy(gameObject); }

    /// Empieza a contar 'lifetime' desde ahora (lo usa ShakeablePalm al soltar un coco que estaba colgado)
    public void StartLifetime()
    {
        CancelInvoke(nameof(Expire));
        if (lifetime > 0f) Invoke(nameof(Expire), lifetime);
    }

    void Update()
    {
        // Coco cayendo de la palmera sobre la cabeza del jugador (sin depender de colliders del rig)
        if (held || playerHead == null || playerHealth == null || rb == null) return;
        if (Time.time - lastGrabTime < 1f) return;   // recien soltado por el propio jugador
        if (rb.linearVelocity.y < -minImpactSpeed &&
            (transform.position - playerHead.position).sqrMagnitude < headRadius * headRadius)
            DamageTarget(playerHealth, rb.linearVelocity.magnitude);
    }

    void OnCollisionEnter(Collision c)
    {
        if (held) return;
        float speed = c.relativeVelocity.magnitude;
        if (speed < minImpactSpeed) return;
        var h = c.collider.GetComponentInParent<Health>();
        if (h != null)
        {
            if (h == playerHealth && Time.time - lastGrabTime < 1f) return;
            DamageTarget(h, speed);
        }
        else if (audioSource != null && hitClip != null) audioSource.PlayOneShot(hitClip, 0.4f);
    }

    void DamageTarget(Health h, float speed)
    {
        if (!hitThisFlight.Add(h)) return;
        float dmg = Mathf.Min(maxDamage, speed * damagePerMeterPerSecond);
        if (h.TakeDamage(dmg, gameObject))
        {
            if (debugLogs) Debug.Log($"[COCO] Impacto a {speed:F1} m/s contra '{h.name}' -> {dmg:F0} de dano");
            if (audioSource != null && hitClip != null) audioSource.PlayOneShot(hitClip);
        }
    }
}
