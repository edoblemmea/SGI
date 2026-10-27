using UnityEngine;
using UnityEngine.XR;

/// Natacion: cuando el agua cubre mas de 'Swim Depth', el jugador flota y nada braceando
/// (mantener GRIP y tirar de las manos hacia atras, como al remar; avanzas hacia donde miras).
/// Ponlo en el XR Origin (XR Rig). Usa su CharacterController para moverse.
public class SwimmingLocomotion : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Main Camera")]
    public Transform head;
    [Tooltip("Objeto cuya altura Y es la superficie del agua (el plano de agua)")]
    public Transform waterSurface;
    [Tooltip("Opcional: Box Collider que marca donde hay agua. Vacio = agua en todas partes a esa altura")]
    public Collider waterArea;
    [Tooltip("Capas del suelo/terreno para medir la profundidad (NO incluyas la capa del agua)")]
    public LayerMask groundMask = ~0;
    [Tooltip("Providers a desactivar nadando: Gravity, Jump, Climb... (el Move con joystick puede quedarse)")]
    public Behaviour[] disableWhileSwimming;
    public Transform leftHandFallback, rightHandFallback;

    [Header("Natacion")]
    [Tooltip("Profundidad de agua (m) a partir de la cual se nada")]
    public float swimDepth = 1.3f;
    [Tooltip("La cabeza flota esta altura por encima de la superficie")]
    public float headAboveWater = 0.15f;
    public float strokePower = 1.5f;
    public float minStrokeSpeed = 0.3f;
    public float maxSwimSpeed = 2.5f;
    public float waterDrag = 1.5f;
    public float buoyancy = 3f;
    [Range(0f, 1f)] public float gripThreshold = 0.5f;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool IsSwimming { get; private set; }

    CharacterController cc;
    Transform trackingSpace;
    Vector3 velocity, prevL, prevR;
    bool hasPrev;

    void Awake() => cc = GetComponent<CharacterController>();

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        trackingSpace = head != null ? head.parent : null;
        if (cc == null) Debug.LogWarning("[NADAR] Este objeto no tiene CharacterController: ponlo en el XR Origin.");
        if (waterSurface == null) Debug.LogWarning("[NADAR] Falta 'Water Surface': no se podra nadar.");
    }

    void Update()
    {
        bool should = ShouldSwim(out float depth);
        if (should != IsSwimming) SetSwimming(should, depth);
        if (!IsSwimming) return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Stroke(XRNode.LeftHand, leftHandFallback, ref prevL, dt);
        Stroke(XRNode.RightHand, rightHandFallback, ref prevR, dt);
        hasPrev = true;

        // Flotar: muelle que lleva la cabeza a la superficie
        float targetHeadY = waterSurface.position.y + headAboveWater;
        velocity.y += (targetHeadY - head.position.y) * buoyancy * dt;

        velocity *= Mathf.Exp(-waterDrag * dt);
        velocity = Vector3.ClampMagnitude(velocity, maxSwimSpeed);
        cc.Move(velocity * dt);
    }

    void Stroke(XRNode node, Transform fallback, ref Vector3 prev, float dt)
    {
        if (!XRHandInput.TryGetWorldPosition(node, trackingSpace, fallback, out Vector3 w)) return;
        Vector3 local = transform.InverseTransformPoint(w);   // relativo al rig: se descuenta nuestro propio avance
        if (hasPrev && XRHandInput.Grip(node) > gripThreshold)
        {
            Vector3 handVel = transform.TransformDirection(local - prev) / dt;
            // Empujar el agua hacia atras = avanzar hacia delante
            if (handVel.magnitude > minStrokeSpeed)
            {
                Vector3 push = -handVel * strokePower * dt;
                push.y *= 0.3f;   // casi todo horizontal: la flotacion se encarga de la altura
                velocity += push;
                XRHandInput.Haptic(node, Mathf.Clamp01(handVel.magnitude * 0.15f), 0.03f);
            }
        }
        prev = local;
    }

    bool ShouldSwim(out float depth)
    {
        depth = 0f;
        if (cc == null || !cc.enabled || waterSurface == null || head == null) return false;   // a bordo el CC esta apagado
        Vector3 h = head.position;
        if (waterArea != null)
        {
            Bounds b = waterArea.bounds;
            if (h.x < b.min.x || h.x > b.max.x || h.z < b.min.z || h.z > b.max.z) return false;
        }
        float surface = waterSurface.position.y;
        float groundY = Physics.Raycast(new Vector3(h.x, surface + 1f, h.z), Vector3.down, out RaycastHit hit, 50f, groundMask, QueryTriggerInteraction.Ignore)
            ? hit.point.y : surface - 50f;
        depth = surface - groundY;
        // Histeresis para no entrar/salir sin parar en la orilla
        return depth > (IsSwimming ? swimDepth - 0.2f : swimDepth);
    }

    void SetSwimming(bool on, float depth)
    {
        IsSwimming = on;
        hasPrev = false;
        velocity = Vector3.zero;
        // Si salimos del agua porque hemos subido al barco (CC apagado), NO reactivar: ya lo hara BoatRowing al bajar
        bool restore = cc != null && cc.enabled;
        foreach (var b in disableWhileSwimming)
            if (b != null && (on || restore)) b.enabled = !on;
        if (debugLogs) Debug.Log(on ? $"[NADAR] Al agua (profundidad {depth:F1} m): bracea con el grip" : "[NADAR] Fuera del agua");
    }
}
