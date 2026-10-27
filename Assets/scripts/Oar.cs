using UnityEngine;
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
        if (debugLogs) Debug.Log($"[REMO] '{name}' listo. Empunadura a {restHandleDir.magnitude:F2} m del pivote.");
    }

    void Update()
    {
        UpdateHand(XRNode.LeftHand, ref prevGripL, ref nearL);
        UpdateHand(XRNode.RightHand, ref prevGripR, ref nearR);

        if (IsHeld && XRHandInput.TryGetWorldPosition(HeldBy, trackingSpace, null, out Vector3 hand))
            Follow(hand);
        else if (!IsHeld)
            transform.localRotation = Quaternion.Slerp(transform.localRotation, restLocal, returnSpeed * Time.deltaTime);
    }

    void UpdateHand(XRNode node, ref bool prevGrip, ref bool near)
    {
        // Grip con histeresis: se "pulsa" al pasar gripThreshold y se "suelta" al bajar de releaseThreshold
        float g = XRHandInput.Grip(node);
        bool wasDown = prevGrip;
        bool down = wasDown ? g > releaseThreshold : g > gripThreshold;
        prevGrip = down;
        bool justPressed = down && !wasDown;

        if (IsHeld && HeldBy == node)
        {
            if (!down) Release();
            return;
        }
        if (!XRHandInput.TryGetWorldPosition(node, trackingSpace, null, out Vector3 hand)) return;

        bool isNear = Vector3.Distance(hand, handle.position) < grabRadius;
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
