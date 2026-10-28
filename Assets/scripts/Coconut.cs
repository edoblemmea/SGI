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
    public float minImpactSpeed = 2f;
    public float maxDamage = 40f;
    [Tooltip("Contra enemigos (aranas, esqueletos) el coco lanzado hace este multiplo del dano")]
    public float enemyDamageMultiplier = 2f;
    [Tooltip("Dano minimo de un coco lanzado contra un enemigo (si va a mas de 'Min Impact Speed')")]
    public float minEnemyDamage = 15f;
    [Tooltip("Tope de dano contra enemigos (con 30 de vida, una arana muere de un buen lanzamiento)")]
    public float maxEnemyDamage = 60f;

    [Header("Golpe en la cabeza al caer")]
    public Transform playerHead;
    public Health playerHealth;
    public float headRadius = 0.3f;

    [Header("Que no ruede (hasta el mar)")]
    [Tooltip("Frena el giro: cuanto mas alto, menos rueda")]
    public float rollingDamping = 4f;
    [Tooltip("En el suelo y mas despacio que esto (m/s), el coco se para del todo")]
    public float settleSpeed = 1.2f;
    [Tooltip("Rozamiento del coco con el suelo (0-1)")]
    [Range(0f, 1f)] public float friction = 1f;

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
        if (rb != null) rb.angularDamping = rollingDamping;
        // Mucho rozamiento: en una cuesta se para en vez de rodar hasta el agua
        var mat = new PhysicsMaterial("Coco") { dynamicFriction = friction, staticFriction = friction,
                                                frictionCombine = PhysicsMaterialCombine.Maximum, bounciness = 0.1f };
        foreach (var c in GetComponentsInChildren<Collider>()) c.sharedMaterial = mat;
    }

    float groundedTime, lastGroundContact = -1f;

    void OnCollisionStay(Collision c)
    {
        foreach (var ct in c.contacts)
            if (ct.normal.y > 0.4f) { lastGroundContact = Time.time; return; }
    }

    void FixedUpdate()
    {
        // Apoyado en el suelo y despacio: se para (y deja de rodar cuesta abajo)
        if (held || rb == null || rb.isKinematic) return;
        bool grounded = Time.time - lastGroundContact < 0.1f;
        if (grounded && rb.linearVelocity.magnitude < settleSpeed)
        {
            groundedTime += Time.fixedDeltaTime;
            if (groundedTime > 0.25f)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }
        }
        else groundedTime = 0f;
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
        else if (debugLogs && c.collider.GetComponentInParent<EnemyAI>() != null)
            Debug.LogWarning($"[COCO] Has dado a '{c.collider.name}' pero no tiene Health: no se le puede hacer dano");
        else if (audioSource != null && hitClip != null) audioSource.PlayOneShot(hitClip, 0.4f);
    }

    void DamageTarget(Health h, float speed)
    {
        if (!hitThisFlight.Add(h)) return;
        float dmg = Mathf.Min(maxDamage, speed * damagePerMeterPerSecond);
        bool enemy = h != playerHealth && h.GetComponentInParent<EnemyAI>() != null;
        if (enemy) dmg = Mathf.Clamp(speed * damagePerMeterPerSecond * enemyDamageMultiplier, minEnemyDamage, maxEnemyDamage);
        if (h.TakeDamage(dmg, gameObject))
        {
            if (debugLogs) Debug.Log($"[COCO] Impacto a {speed:F1} m/s contra '{h.name}' -> {dmg:F0} de dano" + (enemy ? $" (le quedan {h.Current:F0})" : ""));
            if (audioSource != null && hitClip != null) audioSource.PlayOneShot(hitClip);
        }
    }
}
