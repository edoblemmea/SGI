using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Escala de cuerda para subir de la barca de remos a la cubierta del barco pirata.
/// Trepar: acerca una mano a la escala, aprieta el GRIP y tira hacia ABAJO (como en una escalera de verdad).
/// Se puede ir alternando las manos. Al asomar la cabeza por encima de la borda apareces en la cubierta ('deckPoint').
/// Si sueltas las dos manos abajo del todo, vuelves a la barca.
/// Desde lejos: apunta a la escala con el rayo y aprieta el GRIP -> te pones delante de la escala, al pie, y desde ahi trepas
/// con los mandos como siempre (XR Simple Interactable). Si no la agarras en 'grabWait' segundos, vuelves a la barca.
/// Lee las manos directamente del casco (XRHandInput), igual que los remos. Lo monta "Isla Pirata > Escala de cuerda".
public class RopeLadder : MonoBehaviour
{
    [Header("Escala")]
    [Tooltip("Extremo de abajo de la escala (junto al agua)")]
    public Transform bottom;
    [Tooltip("Extremo de arriba (en la borda)")]
    public Transform top;
    [Tooltip("Donde apareces en la cubierta al terminar de subir")]
    public Transform deckPoint;
    [Tooltip("Distancia maxima de la mano a la escala para agarrarla (m)")]
    public float grabRadius = 0.35f;
    [Range(0f, 1f)] public float gripThreshold = 0.5f;

    [Header("Ir a la escala apuntando desde lejos")]
    [Tooltip("Apuntar a la escala con el rayo y apretar el grip = te pones al pie de la escala (luego trepas con los mandos). Necesita un XR Simple Interactable y un collider en este objeto (los pone la herramienta)")]
    public bool climbByPointing = true;
    [Tooltip("A que distancia de la escala te pone (m): lo justo para agarrarla con las manos")]
    public float autoClimbDistance = 0.45f;
    [Tooltip("Segundos para agarrar la escala despues de ir a ella; si no, vuelves a la barca")]
    public float grabWait = 15f;

    [Header("Jugador")]
    public Transform xrOrigin;
    [Tooltip("Main Camera. Si se deja vacio se busca sola")]
    public Transform head;
    [Tooltip("Barca de remos: si vas a bordo, al agarrar la escala desembarcas")]
    public BoatRowing boat;
    [Tooltip("Providers a apagar mientras trepas: Move, Gravity, Jump, Grab Move, Climb...")]
    public Behaviour[] disableWhileClimbing;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool IsClimbing { get; private set; }

    Transform trackingSpace;
    CharacterController cc;
    bool cameFromBoat, holdL, holdR, prevGripL, prevGripR;
    Vector3 prevL, prevR;
    float startY, logTimer;
    XRSimpleInteractable pointTarget;
    float waitGrab;                 // > 0: te ha puesto en la escala y aun no la has agarrado
    Vector3 boatLocalPos;
    Quaternion boatLocalRot;

    void Awake() => pointTarget = GetComponent<XRSimpleInteractable>();

    void OnEnable() { if (pointTarget != null) pointTarget.selectEntered.AddListener(OnPointed); }
    void OnDisable() { if (pointTarget != null) pointTarget.selectEntered.RemoveListener(OnPointed); }

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        trackingSpace = head != null ? head.parent : null;
        if (xrOrigin != null) cc = xrOrigin.GetComponent<CharacterController>();
        if (bottom == null || top == null || deckPoint == null || xrOrigin == null)
        {
            Debug.LogWarning($"[ESCALA] '{name}': faltan Bottom, Top, Deck Point o Xr Origin. Usa Isla Pirata > Escala de cuerda.");
            enabled = false;
            return;
        }
        if (climbByPointing && pointTarget == null)
            Debug.LogWarning("[ESCALA] No hay XR Simple Interactable en la escala: no se podra subir apuntando desde lejos. Vuelve a pulsar Isla Pirata > Escala de cuerda.");
        if (debugLogs) Debug.Log($"[ESCALA] Lista: {Vector3.Distance(bottom.position, top.position):F1} m de alto. Agarra con el grip y tira hacia abajo, o apunta desde lejos con el rayo y aprieta el grip.");
    }

    void Update()
    {
        bool okL = HandLocal(XRNode.LeftHand, out Vector3 l);
        bool okR = HandLocal(XRNode.RightHand, out Vector3 r);
        Step(XRNode.LeftHand, okL, l, ref holdL, ref prevL, ref prevGripL);
        Step(XRNode.RightHand, okR, r, ref holdR, ref prevR, ref prevGripR);

        if (!IsClimbing) return;

        // Arriba del todo: a la cubierta
        if (head.position.y >= top.position.y + 0.1f) { Finish(true); return; }

        if (waitGrab > 0f)
        {
            // Recien llegado a la escala desde lejos: esperar a que la agarre
            if (holdL || holdR) waitGrab = 0f;
            else
            {
                waitGrab -= Time.deltaTime;
                if (waitGrab <= 0f)
                {
                    if (debugLogs) Debug.Log("[ESCALA] No has agarrado la escala a tiempo");
                    Finish(false);
                }
                return;
            }
        }
        // Las dos manos sueltas abajo del todo: vuelves a la barca (o te quedas colgado si estas a media escala)
        else if (!holdL && !holdR && xrOrigin.position.y <= startY + 0.05f) { Finish(false); return; }

        if (debugLogs)
        {
            logTimer += Time.deltaTime;
            if (logTimer > 1f)
            {
                logTimer = 0f;
                Debug.Log($"[ESCALA] trepando | izq {(holdL ? "SI" : "no")} der {(holdR ? "SI" : "no")} | cabeza a {top.position.y + 0.1f - head.position.y:F2} m de la borda");
            }
        }
    }

    void Step(XRNode node, bool ok, Vector3 local, ref bool hold, ref Vector3 prev, ref bool prevGrip)
    {
        bool grip = ok && XRHandInput.Grip(node) > gripThreshold;
        bool pressed = grip && !prevGrip;
        prevGrip = grip;

        if (!grip) { hold = false; return; }

        if (pressed && !hold && Near(trackingSpace.TransformPoint(local)))
        {
            hold = true;
            prev = local;
            XRHandInput.Haptic(node, 0.4f, 0.05f);
            if (!IsClimbing) Begin();
            return;
        }
        if (!hold || !IsClimbing) return;

        // La mano se mueve en el espacio del jugador: el jugador se mueve al reves (solo en vertical)
        float dy = trackingSpace.TransformVector(local - prev).y;
        prev = local;
        Vector3 p = xrOrigin.position;
        p.y = Mathf.Max(startY, p.y - dy);
        xrOrigin.position = p;
        if (dy < -0.01f) XRHandInput.Haptic(node, 0.1f, 0.02f);
    }

    // Rayo de XRI sobre la escala + grip: te pone al pie de la escala, mirando al casco. Trepar ya es cosa tuya (Step)
    void OnPointed(SelectEnterEventArgs a)
    {
        if (!climbByPointing || IsClimbing || !isActiveAndEnabled) return;
        if (Near(a.interactorObject.transform.position)) return;   // ya la estas tocando: se trepa a mano

        Begin();   // desembarca y apaga la locomocion (sin gravedad: te quedas en la escala)
        holdL = holdR = false;
        Vector3 face = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;   // la Z de este objeto mira al barco
        Vector3 look = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (face.sqrMagnitude > 0.001f && look.sqrMagnitude > 0.001f)
            xrOrigin.RotateAround(head.position, Vector3.up, Vector3.SignedAngle(look.normalized, face, Vector3.up));
        Vector3 standAt = bottom.position - face * autoClimbDistance;
        Vector3 off = head.position - xrOrigin.position;
        off.y = 0f;
        xrOrigin.position = new Vector3(standAt.x - off.x, Mathf.Max(xrOrigin.position.y, bottom.position.y - 0.5f), standAt.z - off.z);
        startY = xrOrigin.position.y;
        waitGrab = grabWait;
        XRHandInput.HapticBoth(0.4f, 0.1f);
        if (debugLogs) Debug.Log($"[ESCALA] Estas al pie de la escala ({top.position.y + 0.1f - head.position.y:F1} m hasta la borda). " +
                                 "Suelta el grip, agarra la escala con una mano y tira hacia abajo.");
    }

    bool Near(Vector3 world)
    {
        Vector3 a = bottom.position, b = top.position, ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(world - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
        return Vector3.Distance(world, a + ab * t) < grabRadius;
    }

    bool HandLocal(XRNode node, out Vector3 local)
    {
        local = default;
        if (trackingSpace == null) return false;
        var dev = InputDevices.GetDeviceAtXRNode(node);
        return dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out local);
    }

    void Begin()
    {
        cameFromBoat = boat != null && boat.IsBoarded;
        if (cameFromBoat)
        {
            // Recordar el sitio en la barca para volver a el si sueltas la escala abajo
            boatLocalPos = boat.transform.InverseTransformPoint(xrOrigin.position);
            boatLocalRot = Quaternion.Inverse(boat.transform.rotation) * xrOrigin.rotation;
            boat.Disembark();
        }   // los remos se sueltan solos
        if (cc != null) cc.enabled = false;
        foreach (var b in disableWhileClimbing) if (b != null) b.enabled = false;
        startY = xrOrigin.position.y;
        IsClimbing = true;
        if (debugLogs) Debug.Log($"[ESCALA] Empiezas a trepar{(cameFromBoat ? " (desde la barca)" : "")}. Tira hacia abajo con la mano que agarra.");
    }

    void Finish(bool onDeck)
    {
        IsClimbing = false;
        holdL = holdR = false;
        waitGrab = 0f;
        if (onDeck)
        {
            // Que la cabeza quede encima del punto de la cubierta
            Vector3 offset = head.position - xrOrigin.position;
            offset.y = 0f;
            xrOrigin.position = deckPoint.position - offset;
        }
        foreach (var b in disableWhileClimbing) if (b != null) b.enabled = true;
        if (cc != null) cc.enabled = true;
        if (!onDeck && cameFromBoat && boat != null)
        {
            xrOrigin.SetPositionAndRotation(boat.transform.TransformPoint(boatLocalPos), boat.transform.rotation * boatLocalRot);
            boat.Board();
        }
        XRHandInput.HapticBoth(0.5f, 0.1f);
        if (debugLogs) Debug.Log(onDeck ? "[ESCALA] ¡Arriba! Estas en la cubierta del barco pirata." : (cameFromBoat ? "[ESCALA] Vuelves a la barca." : "[ESCALA] Has soltado la escala."));
    }

    void OnDrawGizmosSelected()
    {
        if (bottom == null || top == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(bottom.position, top.position);
        Gizmos.DrawWireSphere(top.position, grabRadius);
        if (deckPoint != null) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(deckPoint.position, 0.3f); }
    }
}
