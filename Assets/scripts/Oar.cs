using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// Remo que se AGARRA: acerca la mano a la empunadura, aprieta el GRIP y el remo sigue a tu mano girando
/// sobre su chumacera (el pivote del remo, en la borda). Al soltar el grip vuelve a su posicion de reposo.
/// No usa XR Interactables: lee mano y grip directamente del casco (igual que BoatHandRowing), asi funciona
/// aunque los interactores de los mandos esten desactivados.
/// BoatHandRowing solo cuenta la remada de una mano si esa mano tiene agarrado un remo.
///
/// Ponlo en cada remo (Paddle / Paddle (1) de la barca). El pivote del remo debe ser su propio origen
/// (en el modelo OldWoodenRowboat ya lo es). Lo configura solo el menu "Isla Pirata > Barca".
public class Oar : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Hijo vacio en el extremo del remo que se agarra (dentro de la barca)")]
    public Transform handle;
    [Tooltip("Main Camera. Si se deja vacio se busca sola")]
    public Transform head;
    [Tooltip("Mano que se recomienda para este remo (solo para los logs; puede cogerlo cualquiera)")]
    public XRNode preferredHand = XRNode.LeftHand;
    [Tooltip("Left Controller del XR Origin: si se asigna, la posicion de la mano sale de aqui (lo que ves en el casco)")]
    public Transform leftHand;
    public Transform rightHand;
    [Tooltip("Acciones de grip de XRI (p. ej. XRI Left Interaction/Select Value). Opcional: tambien se lee del casco")]
    public InputActionReference leftGrip;
    public InputActionReference rightGrip;

    [Header("Ayuda visual")]
    [Tooltip("Bolita en la empunadura: gris = libre, amarilla = mano cerca, verde = agarrada")]
    public bool showHandleMarker = true;
    public float markerSize = 0.07f;

    [Header("Agarre")]
    [Tooltip("Distancia maxima de la mano a la empunadura para poder cogerla (m)")]
    public float grabRadius = 0.22f;
    [Range(0f, 1f)] public float gripThreshold = 0.5f;
    public float releaseThreshold = 0.3f;

    [Header("Limites de giro (grados respecto al reposo)")]
    public float maxYaw = 60f;
    public float maxPitch = 30f;
    [Tooltip("Velocidad con la que vuelve al reposo al soltarlo")]
    public float returnSpeed = 4f;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool IsHeld { get; private set; }
    public XRNode HeldBy { get; private set; }

    Transform trackingSpace;
    Quaternion restLocal;
    Vector3 restHandleDir;          // direccion pivote -> empunadura en reposo (espacio del padre)
    bool prevGripL, prevGripR, nearL, nearR;
    static Oar heldByLeft, heldByRight;   // una mano no puede coger dos remos
    Renderer marker;
    BoatRowing boat;
    float logTimer, distL = -1f, distR = -1f, gripValL, gripValR;
    string srcL = "-", srcR = "-";

    void OnEnable()
    {
        if (leftGrip != null) leftGrip.action.Enable();
        if (rightGrip != null) rightGrip.action.Enable();
    }

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        trackingSpace = head != null ? head.parent : null;
        restLocal = transform.localRotation;
        if (handle == null)
        {
            Debug.LogWarning($"[REMO] '{name}': falta 'Handle' (la empunadura). Usa Isla Pirata > Barca para crearla.");
            enabled = false;
            return;
        }
        restHandleDir = ParentDir(handle.position - transform.position);
        boat = GetComponentInParent<BoatRowing>();
        if (showHandleMarker) CreateMarker();
        if (debugLogs) Debug.Log($"[REMO] '{name}' listo. Empunadura a {restHandleDir.magnitude:F2} m del pivote. " +
                                 $"Manos: {(leftHand != null ? leftHand.name : "del casco")}/{(rightHand != null ? rightHand.name : "del casco")}");
    }

    void Update()
    {
        UpdateHand(XRNode.LeftHand, ref prevGripL, ref nearL);
        UpdateHand(XRNode.RightHand, ref prevGripR, ref nearR);

        if (IsHeld && HandPosition(HeldBy, out Vector3 hand, out _))
            Follow(hand);
        else if (!IsHeld)
            transform.localRotation = Quaternion.Slerp(transform.localRotation, restLocal, returnSpeed * Time.deltaTime);

        UpdateMarker();
        if (debugLogs)
        {
            logTimer += Time.deltaTime;
            // Solo cuando alguna mano esta a menos de 1 m (para no llenar la Console)
            bool close = (distL >= 0f && distL < 1f) || (distR >= 0f && distR < 1f) || (distL < 0f && distR < 0f);
            if (logTimer > 1.5f && close && !IsHeld)
            {
                logTimer = 0f;
                Debug.Log($"[REMO] '{name}': mano izq a {Fmt(distL)} (grip {gripValL:F2}, {srcL}) | der a {Fmt(distR)} (grip {gripValR:F2}, {srcR}) | " +
                          $"hay que estar a < {grabRadius:F2} m y apretar el grip");
            }
        }
    }

    static string Fmt(float d) => d < 0f ? "?? (no llega la posicion)" : $"{d:F2} m";

    // Posicion de la mano: primero el objeto Left/Right Controller (lo que ves), si no, directamente del casco
    bool HandPosition(XRNode node, out Vector3 world, out string source)
    {
        var t = node == XRNode.LeftHand ? leftHand : rightHand;
        if (t != null && t.gameObject.activeInHierarchy) { world = t.position; source = "controller"; return true; }
        source = "casco";
        return XRHandInput.TryGetWorldPosition(node, trackingSpace, null, out world);
    }

    float GripValue(XRNode node)
    {
        var a = node == XRNode.LeftHand ? leftGrip : rightGrip;
        float v = a != null ? a.action.ReadValue<float>() : 0f;
        return Mathf.Max(v, XRHandInput.Grip(node));
    }

    void CreateMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Marca Empunadura";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(handle, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one * markerSize / Mathf.Max(handle.lossyScale.x, 0.0001f);
        marker = go.GetComponent<Renderer>();
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh != null) marker.material = new Material(sh);
        marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void UpdateMarker()
    {
        if (marker == null) return;
        Color c = IsHeld ? new Color(0.2f, 1f, 0.3f) : (nearL || nearR) ? new Color(1f, 0.9f, 0.2f) : new Color(0.8f, 0.8f, 0.8f);
        if (marker.material.HasProperty("_BaseColor")) marker.material.SetColor("_BaseColor", c);
        else marker.material.color = c;
    }

    void UpdateHand(XRNode node, ref bool prevGrip, ref bool near)
    {
        // Grip con histeresis: se "pulsa" al pasar gripThreshold y se "suelta" al bajar de releaseThreshold
        float g = GripValue(node);
        if (node == XRNode.LeftHand) gripValL = g; else gripValR = g;
        bool wasDown = prevGrip;
        bool down = wasDown ? g > releaseThreshold : g > gripThreshold;
        prevGrip = down;
        bool justPressed = down && !wasDown;

        if (IsHeld && HeldBy == node)
        {
            if (!down) Release();
            return;
        }
        if (!HandPosition(node, out Vector3 hand, out string src))
        {
            if (node == XRNode.LeftHand) distL = -1f; else distR = -1f;
            return;
        }
        float dist = Vector3.Distance(hand, handle.position);
        if (node == XRNode.LeftHand) { distL = dist; srcL = src; } else { distR = dist; srcR = src; }

        bool isNear = dist < grabRadius;
        if (isNear && !near && !IsHeld) XRHandInput.Haptic(node, 0.15f, 0.03f);   // "estas tocando la empunadura"
        near = isNear;

        if (justPressed && isNear && !IsHeld && HandFree(node)) Grab(node);
    }

    static bool HandFree(XRNode node) => node == XRNode.LeftHand ? heldByLeft == null : heldByRight == null;

    void Grab(XRNode node)
    {
        IsHeld = true;
        HeldBy = node;
        if (node == XRNode.LeftHand) heldByLeft = this; else heldByRight = this;

        // Coger los remos = sentarse a remar: si aun no ibas a bordo, embarcas (asi se apagan Move/Grab Move/Gravity
        // y el XR Origin pasa a ser hijo de la barca; si no, el Grab Move te sacaria de la barca al tirar)
        if (boat != null && !boat.IsBoarded)
        {
            if (debugLogs) Debug.Log("[REMO] No ibas a bordo: embarcas al coger el remo");
            boat.Board();
        }
        XRHandInput.Haptic(node, 0.5f, 0.08f);
        if (debugLogs) Debug.Log($"[REMO] '{name}' agarrado con la mano {(node == XRNode.LeftHand ? "izquierda" : "derecha")}" +
                                 (node != preferredHand ? " (es el remo del otro lado, tambien vale)" : ""));
    }

    void Release()
    {
        if (debugLogs) Debug.Log($"[REMO] '{name}' soltado");
        if (heldByLeft == this) heldByLeft = null;
        if (heldByRight == this) heldByRight = null;
        IsHeld = false;
    }

    void OnDisable()
    {
        if (IsHeld) Release();
    }

    // Gira el remo sobre su pivote para que la empunadura apunte hacia la mano (con limites)
    void Follow(Vector3 handWorld)
    {
        Vector3 want = ParentDir(handWorld - transform.position);
        if (want.sqrMagnitude < 0.0001f) return;

        Quaternion delta = Quaternion.FromToRotation(restHandleDir.normalized, want.normalized);
        Vector3 e = delta.eulerAngles;
        float yaw = Mathf.Clamp(Mathf.DeltaAngle(0f, e.y), -maxYaw, maxYaw);
        float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), -maxPitch, maxPitch);
        float roll = Mathf.Clamp(Mathf.DeltaAngle(0f, e.z), -maxPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, yaw, roll) * restLocal;
    }

    Vector3 ParentDir(Vector3 world) => transform.parent != null ? transform.parent.InverseTransformDirection(world) : world;

    /// Remo que tiene agarrado esa mano (o null)
    public static Oar HeldByHand(XRNode node) => node == XRNode.LeftHand ? heldByLeft : heldByRight;

    void OnDrawGizmosSelected()
    {
        if (handle == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(handle.position, grabRadius);
    }
}
