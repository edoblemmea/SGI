using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// Remar con las manos: mantener el GRIP y tirar del mando HACIA TU CUERPO.
///
/// Diferencias con el remo antiguo de BoatRowing:
///  - La direccion del tiron se mide respecto a HACIA DONDE MIRAS (no respecto al eje del barco), asi funciona
///    aunque estes girado o mirando hacia otro lado dentro del barco.
///  - Lee el grip y la posicion de las manos DIRECTAMENTE del casco (XR InputDevices), de modo que funciona aunque
///    los objetos Left/Right Controller esten desactivados por el XR Input Modality Manager.
///  - Da vibracion en los mandos: un "clic" al agarrar el grip y un zumbido mientras la remada cuenta.
///  - El barco siempre avanza hacia la proa (BowRef).
///
/// USO: ponlo en el MISMO objeto que BoatRowing y el Rigidbody (OldWoodenRowboat_Complete), no en un hijo.
/// BoatRowing solo embarca/desembarca y amortigua; este es el UNICO script que empuja el barco.
[RequireComponent(typeof(Rigidbody))]
public class BoatHandRowing : MonoBehaviour
{
    [Header("Referencias")]
    public Transform xrOrigin;            // XR Origin (XR Rig)
    public Transform head;                // Main Camera (si se deja vacio se busca sola)
    public Transform leftHandFallback;    // Left Controller (solo se usa si el casco no da la posicion)
    public Transform rightHandFallback;   // Right Controller
    public Transform bowReference;        // BowRef (su flecha azul debe mirar a la proa)

    [Header("Grip")]
    public InputActionReference leftGrip;   // opcional: tambien se lee directamente del casco
    public InputActionReference rightGrip;
    [Range(0f, 1f)] public float gripThreshold = 0.5f;
    [Tooltip("Si se marca, una mano solo rema si tiene AGARRADO un remo (componente Oar). Si no, basta con apretar el grip")]
    public bool requireOarGrab = false;

    [Header("Remada")]
    public float minPullSpeed = 0.15f;      // m/s minimos de la mano hacia tu cuerpo para contar
    public float maxPullSpeed = 2.5f;
    public float pushForce = 6f;            // aceleracion por cada m/s de remada
    public float reverseMultiplier = 0.3f;  // empujar el mando hacia delante con el grip = marcha atras suave
    public float turnForce = 1.5f;          // giro por cada m/s de diferencia entre manos
    public float maxSpeed = 3f;
    public float maxAngularSpeed = 0.8f;
    [Range(0.05f, 1f)] public float responsiveness = 0.5f;   // 1 = sin suavizado

    [Header("Diagnostico")]
    public bool debugLogs = true;
    public bool haptics = true;

    Rigidbody rb;
    Transform trackingSpace;
    Vector3 prevL, prevR;
    bool hasPrev, wasHeldL, wasHeldR, warned;
    float pullL, pullR, strokeL, strokeR, rawL, rawR, gripL, gripR, logTimer, notBoardTimer;
    string srcL = "-", srcR = "-";

    Transform Ref => bowReference != null ? bowReference : transform;
    bool OnBoard => xrOrigin != null && xrOrigin.IsChildOf(transform);

    void Awake() => rb = GetComponent<Rigidbody>();

    void OnEnable()
    {
        if (leftGrip != null) leftGrip.action.Enable();
        if (rightGrip != null) rightGrip.action.Enable();
    }

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        trackingSpace = head != null ? head.parent : null;   // "Camera Offset"

        Debug.Log($"[MANO] Listo. head={(head != null ? head.name : "NULL")}, " +
                  $"xrOrigin={(xrOrigin != null ? xrOrigin.name : "NULL")}, " +
                  $"trackingSpace={(trackingSpace != null ? trackingSpace.name : "NULL")}");
    }

    void Update()
    {
        if (!OnBoard || head == null)
        {
            notBoardTimer += Time.deltaTime;
            if (notBoardTimer > 3f)
            {
                notBoardTimer = 0f;
                Debug.LogWarning("[MANO] NO ESTAS A BORDO, no hago nada. " +
                                 (xrOrigin == null ? "El campo 'Xr Origin' de este script esta VACIO. " :
                                  "El XR Origin NO es hijo de '" + name + "' (ahora cuelga de '" + (xrOrigin.parent != null ? xrOrigin.parent.name : "la raiz") + "'). ") +
                                 (head == null ? "Ademas no encuentro la camara (Main Camera)." : ""));
            }
            ResetState();
            return;
        }
        notBoardTimer = 0f;

        Vector3 wl, wr;
        bool okL = TryHand(XRNode.LeftHand, leftHandFallback, out wl, out srcL);
        bool okR = TryHand(XRNode.RightHand, rightHandFallback, out wr, out srcR);
        if (!okL || !okR)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[MANO] No se pueden leer las manos: el casco no las rastrea y los controllers estan desactivados. " +
                                 "Revisa Quest Link / OpenXR y el XR Input Modality Manager.");
            }
            ResetState();
            return;
        }

        // Posicion de cada mano en el espacio del barco (asi se descuenta el movimiento del propio barco)
        Vector3 l = Ref.InverseTransformPoint(wl);
        Vector3 r = Ref.InverseTransformPoint(wr);

        // Hacia donde mira el jugador, en horizontal y en el espacio del barco
        Vector3 f = Ref.InverseTransformDirection(head.forward);
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) { hasPrev = false; return; }
        f.Normalize();

        gripL = Grip(leftGrip, XRNode.LeftHand);
        gripR = Grip(rightGrip, XRNode.RightHand);
        // Con remos: cuenta si esa mano tiene un remo Y aprieta el grip (pala en el agua); sin grip, recuperas sin frenar
        bool heldL = requireOarGrab ? Oar.HeldByHand(XRNode.LeftHand) != null && gripL > gripThreshold : gripL > gripThreshold;
        bool heldR = requireOarGrab ? Oar.HeldByHand(XRNode.RightHand) != null && gripR > gripThreshold : gripR > gripThreshold;

        if (haptics)
        {
            if (heldL && !wasHeldL) Haptic(XRNode.LeftHand, 0.3f, 0.06f);   // "clic": el grip se detecta
            if (heldR && !wasHeldR) Haptic(XRNode.RightHand, 0.3f, 0.06f);
        }
        wasHeldL = heldL;
        wasHeldR = heldR;

        if (!hasPrev)
        {
            prevL = l;
            prevR = r;
            hasPrev = true;
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // Positivo = la mano se mueve hacia tu cuerpo (en sentido contrario a donde miras)
        rawL = -Vector3.Dot(l - prevL, f) / dt;
        rawR = -Vector3.Dot(r - prevR, f) / dt;
        prevL = l;
        prevR = r;

        pullL = Mathf.Lerp(pullL, heldL ? rawL : 0f, responsiveness);
        pullR = Mathf.Lerp(pullR, heldR ? rawR : 0f, responsiveness);

        strokeL = Stroke(pullL);
        strokeR = Stroke(pullR);

        if (haptics)
        {
            if (strokeL > 0f) Haptic(XRNode.LeftHand, Mathf.Clamp01(strokeL * 0.2f), 0.03f);
            if (strokeR > 0f) Haptic(XRNode.RightHand, Mathf.Clamp01(strokeR * 0.2f), 0.03f);
        }

        if (debugLogs) Log(heldL || heldR, dt);
    }

    void FixedUpdate()
    {
        if (!OnBoard) return;

        float l = Weigh(strokeL);
        float r = Weigh(strokeR);

        float thrust = (l + r) * 0.5f * pushForce;
        float turn = (l - r) * turnForce;     // remar solo con la izquierda gira a la derecha

        if (Mathf.Approximately(thrust, 0f) && Mathf.Approximately(turn, 0f)) return;

        // Siempre hacia la proa, en horizontal
        Vector3 bow = Vector3.ProjectOnPlane(Ref.forward, Vector3.up).normalized;
        float fwd = Vector3.Dot(rb.linearVelocity, bow);
        if ((thrust > 0f && fwd < maxSpeed) || (thrust < 0f && fwd > -maxSpeed * 0.5f))
            rb.AddForce(bow * thrust, ForceMode.Acceleration);

        float w = rb.angularVelocity.y;
        if (!Mathf.Approximately(turn, 0f) && (Mathf.Abs(w) < maxAngularSpeed || Mathf.Sign(turn) != Mathf.Sign(w)))
            rb.AddTorque(Vector3.up * turn, ForceMode.Acceleration);
    }

    float Stroke(float v)
    {
        if (Mathf.Abs(v) < minPullSpeed) return 0f;
        return Mathf.Clamp(v, -maxPullSpeed, maxPullSpeed);
    }

    float Weigh(float s) => s >= 0f ? s : s * reverseMultiplier;

    void ResetState()
    {
        pullL = pullR = strokeL = strokeR = 0f;
        hasPrev = false;
        wasHeldL = wasHeldR = false;
    }

    // Posicion de la mano en el mundo: primero directamente del casco; si no, del objeto Controller si esta activo
    bool TryHand(XRNode node, Transform fallback, out Vector3 world, out string source)
    {
        world = default;
        source = "-";

        var dev = InputDevices.GetDeviceAtXRNode(node);
        Vector3 p;
        if (dev.isValid && trackingSpace != null && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out p))
        {
            world = trackingSpace.TransformPoint(p);
            source = "casco";
            return true;
        }

        if (fallback != null && fallback.gameObject.activeInHierarchy)
        {
            world = fallback.position;
            source = "transform";
            return true;
        }

        return false;
    }

    // Grip: el mayor entre la accion asignada y la lectura directa del casco
    float Grip(InputActionReference a, XRNode node)
    {
        float v = 0f;
        if (a != null) v = a.action.ReadValue<float>();

        var dev = InputDevices.GetDeviceAtXRNode(node);
        float g;
        if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out g))
            v = Mathf.Max(v, g);

        return v;
    }

    void Haptic(XRNode node, float amplitude, float duration)
    {
        var dev = InputDevices.GetDeviceAtXRNode(node);
        if (dev.isValid) dev.SendHapticImpulse(0, amplitude, duration);
    }

    void Log(bool holding, float dt)
    {
        logTimer += dt;
        if (logTimer < (holding ? 0.25f : 2f)) return;
        logTimer = 0f;
        string oars = requireOarGrab
            ? $"remo izq {(Oar.HeldByHand(XRNode.LeftHand) != null ? "SI" : "no")} der {(Oar.HeldByHand(XRNode.RightHand) != null ? "SI" : "no")} | "
            : "";
        Debug.Log($"[MANO] {oars}grip izq {gripL:F2} der {gripR:F2} | tirar izq {rawL:F2} der {rawR:F2} m/s | " +
                  $"remada izq {strokeL:F2} der {strokeR:F2} | fuente {srcL}/{srcR}");
    }
}