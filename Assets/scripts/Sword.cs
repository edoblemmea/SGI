using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Espada del jugador. Solo hace dano si la tienes en la mano y la mueves rapido (un tajo de verdad).
/// Montaje: objeto raiz con Rigidbody + XR Grab Interactable + este script. Hijo "Blade" con un Box Collider
/// que cubra la hoja marcado como Is Trigger. Hijo vacio "Tip" en la punta. Los enemigos necesitan collider
/// (y mejor un Rigidbody kinematic) para que el trigger detecte el golpe.
public class Sword : MonoBehaviour
{
    [Tooltip("Punta de la espada: se mide su velocidad")]
    public Transform tip;
    [Tooltip("Velocidad minima de la punta (m/s) para que cuente como golpe")]
    public float minSwingSpeed = 2f;
    public float damagePerMeterPerSecond = 8f;
    public float maxDamage = 50f;
    [Tooltip("Segundos antes de poder golpear otra vez al mismo enemigo")]
    public float hitCooldown = 0.5f;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip hitClip;

    public bool debugLogs = true;

    XRGrabInteractable grab;
    XRNode hand = XRNode.RightHand;
    bool held;
    Vector3 prevTip;
    float tipSpeed;
    readonly Dictionary<Health, float> lastHit = new Dictionary<Health, float>();
    Health ownerHealth;   // el jugador: la espada no le hace dano a quien la lleva

    Vector3 TipPos => tip != null ? tip.position : transform.position;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (grab == null) Debug.LogWarning($"[ESPADA] '{name}' no tiene XR Grab Interactable.");
        if (tip == null) Debug.LogWarning($"[ESPADA] '{name}': falta 'Tip'; se medira la velocidad del centro (golpes mas flojos).");
        prevTip = TipPos;
    }

    void Start()
    {
        if (Camera.main != null) ownerHealth = Camera.main.GetComponentInParent<Health>();
        // Si nadie ha creado la funda (Isla Pirata > Espada > 3), la creo aqui: cadera izquierda
        if (grab != null && FindAnyObjectByType<SwordHolster>() == null)
        {
            var h = new GameObject("Funda espada (auto)").AddComponent<SwordHolster>();
            h.sword = grab;
            var boat = FindAnyObjectByType<BoatRowing>();
            if (boat != null)
            {
                // DEMO: el agua no tiene collider; su altura ~ el fondo del casco de la barca de remos
                float minY = float.MaxValue;
                foreach (var r in boat.GetComponentsInChildren<Renderer>()) minY = Mathf.Min(minY, r.bounds.min.y);
                if (minY < float.MaxValue) h.lostBelowY = minY - 0.3f;
            }
            if (debugLogs) Debug.Log("[ESPADA] No habia funda en la escena: la creo en la cadera izquierda");
        }
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

    void OnGrab(SelectEnterEventArgs a)
    {
        held = true;
        hand = a.interactorObject.handedness == InteractorHandedness.Left ? XRNode.LeftHand : XRNode.RightHand;
        if (debugLogs) Debug.Log($"[ESPADA] Cogida con la mano {(hand == XRNode.LeftHand ? "izquierda" : "derecha")}");
    }

    void OnRelease(SelectExitEventArgs a) => held = false;

    void Update()
    {
        Vector3 p = TipPos;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        tipSpeed = Mathf.Lerp(tipSpeed, (p - prevTip).magnitude / dt, 0.5f);
        prevTip = p;
    }

    void OnTriggerEnter(Collider other) => TryHit(other);
    void OnCollisionEnter(Collision c) => TryHit(c.collider);

    void TryHit(Collider other)
    {
        if (!held || tipSpeed < minSwingSpeed) return;
        var h = other.GetComponentInParent<Health>();
        if (h == null || h.IsDead || other.transform.IsChildOf(transform)) return;
        if (h == ownerHealth) return;   // no cortarse a uno mismo
        if (lastHit.TryGetValue(h, out float t) && Time.time - t < hitCooldown) return;
        lastHit[h] = Time.time;

        float dmg = Mathf.Min(maxDamage, tipSpeed * damagePerMeterPerSecond);
        if (!h.TakeDamage(dmg, gameObject)) return;

        XRHandInput.Haptic(hand, 0.9f, 0.12f);
        if (audioSource != null && hitClip != null) audioSource.PlayOneShot(hitClip);
        if (debugLogs) Debug.Log($"[ESPADA] Tajo a {tipSpeed:F1} m/s contra '{h.name}' -> {dmg:F0} de dano");
    }
}
