using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Funda en la cadera IZQUIERDA para llevar la espada mientras remas o trepas (la derecha es el bolsillo de la llave).
///  - Envainar: suelta la espada (deja de apretar el grip) con la empunadura junto a la cadera izquierda -> se queda colgada, punta abajo.
///  - Desenvainar: cogela de la cadera con el grip, o apuntala con el rayo y aprieta el grip (viene a la mano).
///  - Si cae al agua (por debajo de 'lostBelowY') vuelve sola a la funda.
/// Lo monta "Isla Pirata > Espada > 3. Funda".
public class SwordHolster : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("La espada de la escena (XR Grab Interactable con 'Attach Transform' = su Agarre)")]
    public XRGrabInteractable sword;
    [Tooltip("Main Camera. Si se deja vacio se busca sola")]
    public Transform head;

    [Header("Posicion (respecto a la cabeza, girando con ella)")]
    public Vector3 offset = new Vector3(-0.25f, -0.6f, -0.05f);
    [Tooltip("Distancia de la empunadura a la funda para que se envaine al soltarla (m)")]
    public float storeRadius = 0.35f;
    [Tooltip("Inclinacion de la espada colgada (grados hacia atras desde la vertical)")]
    public float tilt = 20f;

    [Header("Espada perdida")]
    [Tooltip("Si baja de esta altura (el agua) vuelve a la funda. Lo pone la herramienta")]
    public float lostBelowY = -100f;

    [Header("Ayuda visual")]
    [Tooltip("Bolita en la cadera mientras llevas la espada en la mano; verde = sueltala aqui")]
    public bool showMarker = true;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool HasSword { get; private set; }

    Rigidbody rb;
    Collider[] swordCols;
    Collider[] ignoreWhileStored;
    Renderer marker;
    bool wasStored;

    Transform Grip => sword.attachTransform != null ? sword.attachTransform : sword.transform;

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (sword == null) { Debug.LogWarning("[FUNDA] Falta 'Sword' (la espada de la escena). Usa Isla Pirata > Espada > 3."); enabled = false; return; }
        rb = sword.GetComponent<Rigidbody>();
        swordCols = sword.GetComponentsInChildren<Collider>(true);
        var ignore = new System.Collections.Generic.List<Collider>();
        var boat = FindAnyObjectByType<BoatRowing>();
        if (boat != null) ignore.AddRange(boat.GetComponentsInChildren<Collider>(true));
        var cc = head != null ? head.GetComponentInParent<CharacterController>() : null;
        if (cc != null) ignore.Add(cc);
        foreach (var l in FindObjectsByType<RopeLadder>(FindObjectsInactive.Exclude)) ignore.AddRange(l.GetComponentsInChildren<Collider>(true));
        ignoreWhileStored = ignore.ToArray();
        sword.selectEntered.AddListener(OnGrabbed);
        sword.selectExited.AddListener(OnReleased);
        if (showMarker) CreateMarker();
        if (debugLogs) Debug.Log("[FUNDA] Lista: suelta la espada con la empunadura junto a tu cadera izquierda para envainarla.");
    }

    void OnDestroy()
    {
        if (sword == null) return;
        sword.selectEntered.RemoveListener(OnGrabbed);
        sword.selectExited.RemoveListener(OnReleased);
    }

    void LateUpdate()
    {
        if (head == null || sword == null) return;
        Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (f.sqrMagnitude < 0.0001f) f = transform.forward;
        Quaternion yaw = Quaternion.LookRotation(f.normalized, Vector3.up);
        transform.SetPositionAndRotation(head.position + yaw * offset, yaw);

        if (HasSword) PlaceSword();
        else if (!sword.isSelected && sword.transform.position.y < lostBelowY) Store("la espada se habia caido");

        if (marker != null)
        {
            bool holding = sword.isSelected;
            marker.enabled = holding;
            if (holding)
            {
                bool near = Vector3.Distance(Grip.position, transform.position) < storeRadius;
                var c = near ? new Color(0.2f, 1f, 0.3f, 0.6f) : new Color(1f, 0.9f, 0.3f, 0.35f);
                if (marker.material.HasProperty("_BaseColor")) marker.material.SetColor("_BaseColor", c);
                else marker.material.color = c;
            }
        }
    }

    // Colgada: la empunadura en la funda y la hoja (Z del Agarre) hacia abajo, un poco hacia atras
    void PlaceSword()
    {
        Vector3 down = Quaternion.AngleAxis(-tilt, transform.right) * Vector3.down;
        Quaternion gripRot = Quaternion.LookRotation(down, transform.forward);
        Quaternion localGrip = Quaternion.Inverse(sword.transform.rotation) * Grip.rotation;
        Vector3 localGripPos = sword.transform.InverseTransformPoint(Grip.position);
        Quaternion rootRot = gripRot * Quaternion.Inverse(localGrip);
        sword.transform.SetPositionAndRotation(transform.position - rootRot * Vector3.Scale(localGripPos, sword.transform.lossyScale), rootRot);
    }

    void OnGrabbed(SelectEnterEventArgs a)
    {
        if (a.interactorObject is XRSocketInteractor || !HasSword) return;
        HasSword = false;
        wasStored = true;
        // No se vuelven a activar los choques con la barca, el jugador y la escala: la espada en la mano tampoco debe chocar con ellos (Sword)
        if (debugLogs) Debug.Log("[FUNDA] Desenvainas la espada");
    }

    void OnReleased(SelectExitEventArgs a)
    {
        if (a.interactorObject is XRSocketInteractor) return;
        float d = Vector3.Distance(Grip.position, transform.position);
        if (d < storeRadius) { Store(null); return; }
        if (debugLogs && d < 1.2f) Debug.Log($"[FUNDA] Espada soltada a {d:F2} m de la funda (hace falta < {storeRadius:F2}): acerca la empunadura a la cadera izquierda");
        // Al sacarla de la funda estaba kinematic: XRI lo restaura asi al soltarla. Que vuelva a caer
        if (wasStored && rb != null) { rb.isKinematic = false; rb.useGravity = true; }
        wasStored = false;
    }

    void Store(string why)
    {
        HasSword = true;
        if (rb != null)
        {
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
        }
        PlaceSword();
        SetStoredCollisions(true);
        XRHandInput.HapticBoth(0.3f, 0.08f);
        if (debugLogs) Debug.Log("[FUNDA] Espada envainada en la cadera izquierda" + (why != null ? $" ({why})" : "") + ".");
    }

    // Sin triggers (el rayo de XRI los ignora): solo se ignoran los choques con la barca, el jugador y la escala
    void SetStoredCollisions(bool stored)
    {
        foreach (var sc in swordCols)
            foreach (var other in ignoreWhileStored)
                if (sc != null && other != null && sc != other) Physics.IgnoreCollision(sc, other, stored);
    }

    void CreateMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Marca Funda";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * 0.08f;
        marker = go.GetComponent<Renderer>();
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh != null) marker.material = new Material(sh);
        marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        marker.enabled = false;
    }
}
