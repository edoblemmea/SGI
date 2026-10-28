using UnityEngine;

/// Rescata al jugador de sitios de los que no puede salir:
///  - Si cae al MAR (no hay natacion), vuelve a su asiento de la barca de remos y embarca.
///  - Si cae a la BODEGA del barco pirata (por debajo de la cubierta), vuelve a la cubierta.
///  - Al empezar, activa el CharacterController y la locomocion que la barca apaga a bordo, por si se guardaron
///    desactivados en la escena (pasaba: no podias andar hasta embarcar y desembarcar).
/// Lo monta "Isla Pirata > Rescate del jugador".
public class PlayerRescue : MonoBehaviour
{
    [Header("Jugador")]
    public Transform xrOrigin;
    [Tooltip("Main Camera. Si se deja vacio se busca sola")]
    public Transform head;

    [Header("Mar -> barca")]
    public BoatRowing boat;
    [Tooltip("Asiento de la barca (el ancla 'anclaBarco')")]
    public Transform boatSeat;
    [Tooltip("Altura del agua (m). Por debajo de 'Water Level - Under Water' cuenta como caido al mar")]
    public float waterLevel = 4.3f;
    public float underWater = 0.6f;

    [Header("Bodega -> cubierta")]
    [Tooltip("Barco pirata (para saber si estas dentro de su casco)")]
    public Transform ship;
    [Tooltip("Punto de la cubierta al que vuelves")]
    public Transform deckPoint;
    [Tooltip("Metros por debajo de la cubierta que cuentan como 'caido a la bodega'")]
    public float belowDeck = 1.2f;

    [Header("General")]
    [Tooltip("Segundos en esa situacion antes de rescatar (para no saltar por un tropiezo)")]
    public float delay = 1.5f;
    public bool fixLocomotionAtStart = true;
    public bool debugLogs = true;

    CharacterController cc;
    Bounds shipBounds;
    bool hasShip;
    float seaTimer, holdTimer;
    RopeLadder[] ladders;

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (xrOrigin != null) cc = xrOrigin.GetComponent<CharacterController>();
        ladders = FindObjectsByType<RopeLadder>(FindObjectsInactive.Exclude);
        if (ship != null)
        {
            var rends = ship.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                shipBounds = rends[0].bounds;
                foreach (var r in rends) shipBounds.Encapsulate(r.bounds);
                hasShip = true;
            }
        }

        if (fixLocomotionAtStart && boat != null && !boat.IsBoarded)
        {
            int fixedCount = 0;
            if (cc != null && !cc.enabled) { cc.enabled = true; fixedCount++; }
            if (boat.disableWhileOnBoard != null)
                foreach (var b in boat.disableWhileOnBoard)
                    if (b != null && !b.enabled) { b.enabled = true; fixedCount++; }
            if (debugLogs && fixedCount > 0)
                Debug.Log($"[RESCATE] Al empezar estaban desactivados {fixedCount} componentes de movimiento (Move, CharacterController...): activados.");
        }
        if (debugLogs) Debug.Log($"[RESCATE] Listo. Mar por debajo de y={waterLevel - underWater:F2}" + (hasShip && deckPoint != null ? $", bodega por debajo de y={deckPoint.position.y - belowDeck:F2}" : ""));
    }

    void Update()
    {
        if (xrOrigin == null || (boat != null && boat.IsBoarded) || Climbing()) { seaTimer = holdTimer = 0f; return; }
        Vector3 p = xrOrigin.position;

        // Al mar
        if (boat != null && boatSeat != null && p.y < waterLevel - underWater)
        {
            seaTimer += Time.deltaTime;
            if (seaTimer > delay) { seaTimer = 0f; ToBoat(); }
        }
        else seaTimer = 0f;

        // A la bodega del barco pirata
        bool inHull = hasShip && deckPoint != null && p.x > shipBounds.min.x && p.x < shipBounds.max.x &&
                      p.z > shipBounds.min.z && p.z < shipBounds.max.z && p.y < deckPoint.position.y - belowDeck && p.y > waterLevel - underWater;
        if (inHull)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer > delay) { holdTimer = 0f; MoveHeadTo(deckPoint.position, deckPoint.forward); Say("Has caido a la bodega: vuelves a la cubierta"); }
        }
        else holdTimer = 0f;
    }

    bool Climbing()
    {
        foreach (var l in ladders) if (l != null && l.IsClimbing) return true;
        return false;
    }

    void ToBoat()
    {
        MoveHeadTo(boatSeat.position, boatSeat.forward);
        boat.Board();
        Say("Te has caido al mar: vuelves a la barca");
    }

    // Coloca al jugador de forma que su cabeza quede encima de 'pos', mirando hacia 'forward'
    void MoveHeadTo(Vector3 pos, Vector3 forward)
    {
        bool ccOn = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        Vector3 f = Vector3.ProjectOnPlane(forward, Vector3.up);
        Vector3 look = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : Vector3.zero;
        if (head != null && f.sqrMagnitude > 0.001f && look.sqrMagnitude > 0.001f)
            xrOrigin.RotateAround(head.position, Vector3.up, Vector3.SignedAngle(look, f, Vector3.up));
        Vector3 off = head != null ? head.position - xrOrigin.position : Vector3.zero;
        off.y = 0f;
        xrOrigin.position = pos - off;
        if (cc != null) cc.enabled = ccOn;
        Physics.SyncTransforms();
    }

    void Say(string msg)
    {
        XRHandInput.HapticBoth(0.5f, 0.15f);
        if (debugLogs) Debug.Log("[RESCATE] " + msg);
    }
}
