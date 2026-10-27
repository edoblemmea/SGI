using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// Remo de la barca. Funcionamiento (pensado para que sea comodo en VR):
/// 1) COGER: acerca cualquier mano a la empunadura de cualquier remo y aprieta el GRIP -> coges LOS DOS remos a la vez,
///    cada uno con su mano (el izquierdo con la izquierda y el derecho con la derecha), y embarcas si no ibas a bordo.
/// 2) Los remos se quedan en tus manos (no se pueden perder ni caer) hasta que DESEMBARCAS.
/// 3) REMAR: el grip ahora significa "pala en el agua": aprieta el grip y tira hacia ti para avanzar;
///    suelta el grip para volver las manos hacia delante sin frenar (como levantar la pala). Lo calcula BoatHandRowing.
/// Cada remo gira sobre su chumacera (su pivote) siguiendo a su mano.
/// Ponlo en cada remo (Paddle / Paddle (1)); lo configura el menu "Isla Pirata > Barca".
public class Oar : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Hijo vacio en el extremo del remo que se agarra (dentro de la barca)")]
    public Transform handle;
    [Tooltip("Main Camera. Si se deja vacio se busca sola")]
    public Transform head;
    [Tooltip("Mano que maneja este remo (el remo izquierdo con la izquierda)")]
    public XRNode preferredHand = XRNode.LeftHand;
    [Tooltip("Left Controller del XR Origin: la posicion de la mano sale de aqui (lo que ves en el casco)")]
    public Transform leftHand;
    public Transform rightHand;
    [Tooltip("Acciones de grip de XRI (XRI Left/Right Interaction/Select Value). Tambien se lee del casco")]
    public InputActionReference leftGrip;
    public InputActionReference rightGrip;

    [Header("Agarre")]
    [Tooltip("Distancia maxima de la mano a una empunadura para coger los remos (m)")]
    public float grabRadius = 0.3f;
    [Range(0f, 1f)] public float gripThreshold = 0.5f;

    [Header("Limites de giro (grados respecto al reposo)")]
    public float maxYaw = 60f;
    public float maxPitch = 30f;
    [Tooltip("Velocidad con la que vuelve al reposo al soltarlo")]
    public float returnSpeed = 4f;

    [Header("Ayuda visual")]
    [Tooltip("Bolita en la empunadura: gris = libre, amarilla = mano cerca, verde = cogido, azul = pala en el agua")]
    public bool showHandleMarker = true;
    public float markerSize = 0.07f;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool IsHeld { get; private set; }
    public XRNode HeldBy { get; private set; }
    /// Pala en el agua: el remo esta cogido y su mano aprieta el grip
    public bool BladeInWater => IsHeld && GripValue(HeldBy) > gripThreshold;

    Transform trackingSpace;
    Quaternion restLocal;
    Vector3 restHandleDir;          // direccion pivote -> empunadura en reposo (espacio del padre)
    BoatRowing boat;
    Oar pair;                       // el otro remo de la barca
    Renderer marker;
    bool prevGripL, prevGripR, near, nearL, nearR;
    float logTimer, distL = -1f, distR = -1f;
    static Oar heldByLeft, heldByRight;

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
        if (boat != null)
        {
            foreach (var o in boat.GetComponentsInChildren<Oar>()) if (o != this) pair = o;
            boat.onDisembarked.AddListener(ReleaseBoth);
        }
        if (showHandleMarker) CreateMarker();
        if (debugLogs) Debug.Log($"[REMO] '{name}' listo (mano {(preferredHand == XRNode.LeftHand ? "izquierda" : "derecha")}). " +
                                 $"Empunadura a {restHandleDir.magnitude:F2} m del pivote. Pareja: {(pair != null ? pair.name : "NINGUNA")}.");
    }

    void OnDestroy()
    {
        if (boat != null) boat.onDisembarked.RemoveListener(ReleaseBoth);
    }

    void Update()
    {
        // Coger: solo cuando no hay remos cogidos (los dos remos comparten la logica; lo comprueba cada uno)
        if (!IsHeld && (pair == null || !pair.IsHeld))
        {
            CheckGrab(XRNode.LeftHand, ref prevGripL);
            CheckGrab(XRNode.RightHand, ref prevGripR);
        }

        if (IsHeld && HandPosition(HeldBy, out Vector3 hand))
            Follow(hand);
        else if (!IsHeld)
            transform.localRotation = Quaternion.Slerp(transform.localRotation, restLocal, returnSpeed * Time.deltaTime);

        UpdateMarker();
        if (debugLogs && IsHeld)
        {
            // Para saber si el remo gira de verdad (si el angulo cambia pero no lo ves moverse, la malla esta congelada: Static)
            logTimer += Time.deltaTime;
            if (logTimer > 1.5f)
            {
                logTimer = 0f;
                bool okHand = HandPosition(HeldBy, out Vector3 h);
                Debug.Log($"[REMO] '{name}' cogido con la mano {(HeldBy == XRNode.LeftHand ? "izq" : "der")} | " +
                          (okHand ? $"mano a {Vector3.Distance(h, handle.position):F2} m de la empunadura" : "NO llega la posicion de esa mano") +
                          $" | giro respecto al reposo {Quaternion.Angle(restLocal, transform.localRotation):F0} grados | static={gameObject.isStatic}");
            }
        }
        if (debugLogs && !IsHeld)
        {
            logTimer += Time.deltaTime;
            bool close = (distL >= 0f && distL < 1f) || (distR >= 0f && distR < 1f) || (distL < 0f && distR < 0f);
            if (logTimer > 1.5f && close)
            {
                logTimer = 0f;
                Debug.Log($"[REMO] '{name}': mano izq a {Fmt(distL)} | der a {Fmt(distR)} | grip izq {GripValue(XRNode.LeftHand):F2} der {GripValue(XRNode.RightHand):F2} " +
                          $"| hay que estar a < {grabRadius:F2} m y apretar el grip");
            }
        }
    }

    static string Fmt(float d) => d < 0f ? "?? (no llega la posicion)" : $"{d:F2} m";

    void CheckGrab(XRNode node, ref bool prevGrip)
    {
        bool down = GripValue(node) > gripThreshold;
        bool justPressed = down && !prevGrip;
        prevGrip = down;

        if (!HandPosition(node, out Vector3 hand))
        {
            if (node == XRNode.LeftHand) distL = -1f; else distR = -1f;
            return;
        }
        float dist = Vector3.Distance(hand, handle.position);
        if (node == XRNode.LeftHand) distL = dist; else distR = dist;

        // Cada mano por separado (antes la mano lejana apagaba el aviso de la cercana)
        bool isNear = dist < grabRadius;
        bool wasNear = node == XRNode.LeftHand ? nearL : nearR;
        if (isNear && !wasNear) XRHandInput.Haptic(node, 0.15f, 0.03f);   // "estas tocando la empunadura"
        if (node == XRNode.LeftHand) nearL = isNear; else nearR = isNear;
        near = nearL || nearR;

        if (justPressed && isNear) GrabBoth();
    }

    /// Coge los dos remos: cada uno con su mano (nunca cruzados)
    void GrabBoth()
    {
        if (boat != null && !boat.IsBoarded)
        {
            // Coger los remos = sentarse a remar. Asi se apagan Move/Grab Move/Gravity y viajas con la barca.
            if (debugLogs) Debug.Log("[REMO] No ibas a bordo: embarcas al coger los remos");
            boat.Board();
        }
        // Cada mano al remo que tiene mas cerca (asi nunca se cruzan, aunque 'Preferred Hand' estuviera al reves)
        XRNode mine = preferredHand;
        if (pair != null && HandPosition(XRNode.LeftHand, out Vector3 l) && HandPosition(XRNode.RightHand, out Vector3 r))
        {
            float straight = Vector3.Distance(l, handle.position) + Vector3.Distance(r, pair.handle.position);
            float crossed = Vector3.Distance(r, handle.position) + Vector3.Distance(l, pair.handle.position);
            mine = straight <= crossed ? XRNode.LeftHand : XRNode.RightHand;
        }
        XRNode other = mine == XRNode.LeftHand ? XRNode.RightHand : XRNode.LeftHand;
        GrabBy(mine);
        if (pair != null) pair.GrabBy(other);
        XRHandInput.HapticBoth(0.5f, 0.1f);
        if (debugLogs) Debug.Log("[REMO] Remos cogidos. Aprieta el grip y tira hacia ti para remar; suelta el grip para volver. Se sueltan al desembarcar.");
    }

    void GrabBy(XRNode node)
    {
        if (IsHeld) return;
        IsHeld = true;
        HeldBy = node;
        if (node == XRNode.LeftHand) heldByLeft = this; else heldByRight = this;
    }

    void ReleaseBoth()
    {
        Release();
        if (pair != null) pair.Release();
    }

    void Release()
    {
        if (!IsHeld) return;
        if (debugLogs) Debug.Log($"[REMO] '{name}' soltado");
        if (heldByLeft == this) heldByLeft = null;
        if (heldByRight == this) heldByRight = null;
        IsHeld = false;
    }

    void OnDisable() => Release();

    /// Remo que tiene esa mano (o null)
    public static Oar HeldByHand(XRNode node) => node == XRNode.LeftHand ? heldByLeft : heldByRight;

    // Posicion de la mano: primero el objeto Left/Right Controller (lo que ves), si no, directamente del casco
    bool HandPosition(XRNode node, out Vector3 world)
    {
        var t = node == XRNode.LeftHand ? leftHand : rightHand;
        if (t != null && t.gameObject.activeInHierarchy) { world = t.position; return true; }
        return XRHandInput.TryGetWorldPosition(node, trackingSpace, null, out world);
    }

    public float GripValue(XRNode node)
    {
        var a = node == XRNode.LeftHand ? leftGrip : rightGrip;
        float v = a != null ? a.action.ReadValue<float>() : 0f;
        return Mathf.Max(v, XRHandInput.Grip(node));
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
        Color c = BladeInWater ? new Color(0.2f, 0.5f, 1f)
                : IsHeld ? new Color(0.2f, 1f, 0.3f)
                : near ? new Color(1f, 0.9f, 0.2f)
                : new Color(0.8f, 0.8f, 0.8f);
        if (marker.material.HasProperty("_BaseColor")) marker.material.SetColor("_BaseColor", c);
        else marker.material.color = c;
    }

    void OnDrawGizmosSelected()
    {
        if (handle == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(handle.position, grabRadius);
    }
}
