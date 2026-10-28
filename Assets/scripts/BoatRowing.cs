using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// Subir / bajar del barco y estabilidad (amortiguacion y quilla). NO empuja el barco: eso lo hace BoatHandRowing.
///
/// - Board(): el XR Origin pasa a ser HIJO del barco, se desactivan su CharacterController y los providers de
///   'disableWhileOnBoard' (Move, Gravity, Jump, Grab Move, Climb...).
/// - Disembark(): se suelta el XR Origin y se reactiva todo.
/// Llamar desde los eventos de los Teleportation Anchors (anclaBarco -> Board, anclaMuelle -> Disembark).
/// Colocar en el GameObject del barco (el que tiene el Rigidbody), junto a BoatHandRowing.
public class BoatRowing : MonoBehaviour
{
    [Header("Eje del barco (opcional)")]
    [Tooltip("BowRef: hijo vacio con su flecha azul (Z) mirando a la proa. Si se deja vacio se usa este objeto.")]
    public Transform bowReference;

    [Header("Frenado y estabilidad")]
    [Tooltip("Se aplica al Rigidbody en Awake: cambialo AQUI, no en el Rigidbody.")]
    public float linearDamping = 0.8f;
    public float angularDamping = 2f;
    [Tooltip("Amortigua el deslizamiento lateral para que el barco siga su proa")]
    public float keelDamping = 2f;

    [Header("Subir / bajar del barco")]
    [Tooltip("XR Origin (XR Rig)")]
    public Transform xrOrigin;
    [Tooltip("Providers a desactivar a bordo: Move, Gravity, Jump, Grab Move, Climb...")]
    public Behaviour[] disableWhileOnBoard;
    [Tooltip("Empezar ya a bordo (para probar sin anclas). El XR Origin debe estar colocado dentro del barco.")]
    public bool startOnBoard = false;
    [Tooltip("Donde empiezas la partida (un objeto vacio; su flecha azul = hacia donde miras). Se pega al suelo de debajo. " +
             "Vacio = el ancla del muelle (si 'Start On Dock' esta marcado)")]
    public Transform startPoint;
    [Tooltip("Sin Start Point: empezar en el ancla del muelle (la que tiene el evento Disembark de esta barca y esta mas cerca)")]
    public bool startOnDock = true;
    public bool stopBoatOnDisembark = true;

    [Header("Eventos")]
    public UnityEvent onBoarded;
    public UnityEvent onDisembarked;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    Rigidbody rb;
    CharacterController rigCC;
    bool boarded;

    public bool IsBoarded => boarded;
    Transform Ref => bowReference != null ? bowReference : transform;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"[BARCO] '{name}' no tiene Rigidbody. BoatRowing debe ir en el objeto del Rigidbody del barco.");
            enabled = false;
            return;
        }
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        if (xrOrigin != null) rigCC = xrOrigin.GetComponent<CharacterController>();
        else Debug.LogWarning("[BARCO] El campo 'Xr Origin' esta VACIO: no se podra embarcar.");

        // Embarcar en Awake (no en Start) para que el Gravity provider no tire al jugador el primer frame
        if (startOnBoard) Board();
        else
        {
            if (startPoint != null) PlaceAt(startPoint, "el punto de inicio");
            else if (startOnDock) PlaceOnDock();
            EnsureLocomotionOn();
        }
    }

    // Empezar sobre las tablas del muelle, como al teletransportarte a el (si no, con gravedad podias caer al agua)
    void PlaceOnDock()
    {
        if (xrOrigin == null) return;
        TeleportationAnchor best = null;
        float bestD = float.MaxValue;
        foreach (var a in FindObjectsByType<TeleportationAnchor>(FindObjectsInactive.Exclude))
        {
            if (a.transform.IsChildOf(transform)) continue;   // el ancla de la propia barca no
            bool disembarks = false;
            for (int i = 0; i < a.teleporting.GetPersistentEventCount(); i++)
                if (a.teleporting.GetPersistentTarget(i) == this && a.teleporting.GetPersistentMethodName(i) == nameof(Disembark)) disembarks = true;
            if (!disembarks) continue;
            float d = Vector3.Distance(a.transform.position, xrOrigin.position);
            if (d < bestD) { bestD = d; best = a; }
        }
        if (best == null) { Debug.LogWarning("[BARCO] No encuentro el ancla del muelle (con Teleporting -> Disembark): empiezas donde esta el XR Origin."); return; }
        PlaceAt(best.teleportAnchorTransform != null ? best.teleportAnchorTransform : best.transform, $"el muelle ('{best.name}')");
    }

    // Coloca el XR Origin en 't', mirando hacia su Z, y lo apoya en el suelo de debajo (si lo hay a menos de 3 m)
    void PlaceAt(Transform t, string what)
    {
        Vector3 pos = t.position;
        foreach (var h in Physics.RaycastAll(pos + Vector3.up * 1f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(xrOrigin) || h.transform.IsChildOf(transform)) continue;
            if (pos.y - h.point.y < 3f) { pos.y = h.point.y; break; }
        }
        Vector3 f = Vector3.ProjectOnPlane(t.forward, Vector3.up);
        xrOrigin.SetPositionAndRotation(pos, f.sqrMagnitude > 0.001f ? Quaternion.LookRotation(f, Vector3.up) : xrOrigin.rotation);
        Physics.SyncTransforms();
        if (debugLogs) Debug.Log($"[BARCO] Empiezas en {what}, en {pos}.");
    }

    // Si la escena se guardo con el CharacterController o el Move desactivados (pasaba: no podias andar hasta
    // embarcar y desembarcar), se activan al empezar, igual que hace Disembark()
    void EnsureLocomotionOn()
    {
        int n = 0;
        if (rigCC != null && !rigCC.enabled) { rigCC.enabled = true; n++; }
        if (disableWhileOnBoard != null)
            foreach (var b in disableWhileOnBoard)
                if (b != null && !b.enabled) { b.enabled = true; n++; }
        if (n > 0) Debug.Log($"[BARCO] Al empezar habia {n} componentes de movimiento desactivados (Move, CharacterController...): activados.");
    }

    void FixedUpdate()
    {
        // Quilla: amortigua el deslizamiento lateral
        Vector3 local = Ref.InverseTransformDirection(rb.linearVelocity);
        local.x *= Mathf.Exp(-keelDamping * Time.fixedDeltaTime);
        rb.linearVelocity = Ref.TransformDirection(local);
    }

    /// Subir al barco: el rig pasa a ser hijo del barco y se desactiva la locomocion.
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

        if (debugLogs) Debug.Log($"[BARCO] A bordo. XR Origin ahora cuelga de '{(xrOrigin != null && xrOrigin.parent != null ? xrOrigin.parent.name : "NADA")}'. " +
                                 $"Desactivados {disableWhileOnBoard?.Length ?? 0} providers.");
        onBoarded?.Invoke();
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
            if (rigCC != null) rigCC.enabled = true;
        }

        foreach (var b in disableWhileOnBoard)
            if (b != null) b.enabled = true;

        if (stopBoatOnDisembark)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (debugLogs) Debug.Log("[BARCO] Desembarcado. Locomocion reactivada.");
        onDisembarked?.Invoke();
    }
}
