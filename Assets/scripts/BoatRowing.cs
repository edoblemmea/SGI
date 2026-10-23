using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// Remar con los mandos (version corregida).
/// - Mano hacia atras respecto al barco con el grip pulsado = empuje hacia delante.
/// - Mano hacia delante con el grip pulsado = marcha atras (mas floja). Suelta el grip para recuperar el remo.
/// - Una sola mano = el barco gira hacia el lado contrario. Una mano atras y otra delante = giro sobre si mismo.
/// Colocar en el GameObject del barco (el que tiene el Rigidbody). Unity 6 (linearVelocity / linearDamping).
[RequireComponent(typeof(Rigidbody))]
public class BoatRowing : MonoBehaviour
{
    [Header("Manos (los controllers del XR Origin)")]
    public Transform leftHand;
    public Transform rightHand;

    [Header("Grip (recomendado: sin grip el barco rema con cualquier gesto)")]
    public InputActionReference leftGrip;
    public InputActionReference rightGrip;
    [Range(0f, 1f)] public float gripThreshold = 0.5f;

    [Header("Eje del barco (opcional)")]
    [Tooltip("Si el eje Z del barco NO apunta a la proa, crea un hijo vacio con su Z hacia la proa y arrastralo aqui.")]
    public Transform bowReference;

    [Header("Ajustes de remada")]
    public float strokeSpeedThreshold = 0.3f; // m/s minimos de la mano para contar como remada
    public float maxStrokeSpeed = 3f;         // tope de velocidad de mano que se tiene en cuenta
    public float pushForce = 6f;
    public float reverseMultiplier = 0.6f;    // la marcha atras empuja menos que el avance
    public float turnForce = 3f;
    public float maxSpeed = 2f;               // velocidad maxima hacia delante (m/s)
    public float maxAngularSpeed = 0.6f;      // giro maximo (rad/s)

    [Header("Frenado y estabilidad")]
    public float linearDamping = 0.8f;        // para que el barco se detenga solo
    public float angularDamping = 2f;
    public float keelDamping = 2f;            // evita que derive de lado como un jabon

    [Header("Subir / bajar del barco")]
    public Transform xrOrigin;
    public Behaviour[] disableWhileOnBoard;   // p. ej. Continuous Move Provider, Gravity Provider
    public bool startOnBoard = false;         // actívalo solo para probar sin anclas
    public bool stopBoatOnDisembark = true;

    Rigidbody rb;
    CharacterController rigCC;
    Vector3 prevLeft, prevRight;
    bool hasPrev;
    bool boarded;
    float strokeL, strokeR;                   // positivo = mano hacia atras

    Transform Ref => bowReference != null ? bowReference : transform;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        if (xrOrigin != null) rigCC = xrOrigin.GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        if (leftGrip != null) leftGrip.action.Enable();
        if (rightGrip != null) rightGrip.action.Enable();
        hasPrev = false;
    }

    void Start()
    {
        if (startOnBoard)
        {
            Board();
        }
        else
        {
            // Forzamos al controlador a reconocer que estamos fuera del barco
            if (rigCC != null) rigCC.enabled = true;
        }
    }
    // Se muestrea en Update porque el tracking de las manos se actualiza por frame,
    // no en el paso fijo de fisica (con FixedUpdate las lecturas salen irregulares).
    void Update()
    {
        if (!boarded || leftHand == null || rightHand == null)
        {
            strokeL = strokeR = 0f;
            hasPrev = false;
            return;
        }

        Vector3 l = Ref.InverseTransformPoint(leftHand.position);
        Vector3 r = Ref.InverseTransformPoint(rightHand.position);

        if (!hasPrev)
        {
            prevLeft = l; prevRight = r; hasPrev = true;
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        strokeL = Stroke(-(l.z - prevLeft.z) / dt, leftGrip);
        strokeR = Stroke(-(r.z - prevRight.z) / dt, rightGrip);
        prevLeft = l; prevRight = r;
    }

    float Stroke(float backSpeed, InputActionReference grip)
    {
        if (!GripPressed(grip)) return 0f;
        if (Mathf.Abs(backSpeed) < strokeSpeedThreshold) return 0f;
        return Mathf.Clamp(backSpeed, -maxStrokeSpeed, maxStrokeSpeed);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Quilla: amortigua el deslizamiento lateral para que el barco siga su proa
        Vector3 local = Ref.InverseTransformDirection(rb.linearVelocity);
        local.x *= Mathf.Exp(-keelDamping * dt);
        rb.linearVelocity = Ref.TransformDirection(local);

        if (!boarded) return;

        float l = Weigh(strokeL);
        float r = Weigh(strokeR);
        if (l == 0f && r == 0f) return;

        // Avance / marcha atras
        float thrust = (l + r) * 0.5f * pushForce;
        float fwdSpeed = Vector3.Dot(rb.linearVelocity, Ref.forward);
        bool canForward = thrust > 0f && fwdSpeed < maxSpeed;
        bool canReverse = thrust < 0f && fwdSpeed > -maxSpeed * 0.5f;
        if (canForward || canReverse)
            rb.AddForce(Ref.forward * thrust, ForceMode.Acceleration);

        // Giro: mas remada a la izquierda gira a la derecha, y viceversa
        float turn = (l - r) * turnForce;
        if (Mathf.Abs(rb.angularVelocity.y) < maxAngularSpeed)
            rb.AddTorque(Vector3.up * turn, ForceMode.Acceleration);

        if (Mathf.Abs(l) > 0f) Haptic(XRNode.LeftHand, Mathf.Clamp01(Mathf.Abs(l) * 0.2f));
        if (Mathf.Abs(r) > 0f) Haptic(XRNode.RightHand, Mathf.Clamp01(Mathf.Abs(r) * 0.2f));
    }

    // Marcha atras mas floja que el avance
    float Weigh(float s) => s >= 0f ? s : s * reverseMultiplier;

    bool GripPressed(InputActionReference a)
    {
        if (a == null) return true; // sin grip asignado: siempre activo
        return a.action.ReadValue<float>() > gripThreshold;
    }

    void Haptic(XRNode node, float amplitude)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid) device.SendHapticImpulse(0, amplitude, 0.05f);
    }

    // ---- Llamar desde eventos de los Teleportation Anchors (Teleporting) ----

    /// Subir al barco: el rig pasa a ser hijo del barco y se desactiva el movimiento por joystick.
    public void Board()
    {
        if (boarded) return;
        boarded = true;

        if (xrOrigin != null)
        {
            xrOrigin.SetParent(transform, true);
            if (rigCC != null) rigCC.enabled = false;
        }

        foreach (var b in disableWhileOnBoard)
            if (b != null) b.enabled = false;
    }

    /// Bajar del barco: se suelta el rig y se reactiva TODO lo que Board() desactivo.
    public void Disembark()
    {
        if (!boarded) return;
        boarded = false;

        if (xrOrigin != null)
        {
            xrOrigin.SetParent(null, true);
            // Dejar el rig vertical aunque el barco estuviera inclinado
            xrOrigin.rotation = Quaternion.Euler(0f, xrOrigin.eulerAngles.y, 0f);
            if (rigCC != null) rigCC.enabled = true;   // <- antes no se reactivaba
        }

        foreach (var b in disableWhileOnBoard)
            if (b != null) b.enabled = true;

        if (stopBoatOnDisembark)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}