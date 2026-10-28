using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Bolsillo en la cadera para llevar la llave mientras remas o trepas (necesitas las dos manos).
///  - Guardar: suelta la llave (deja de apretar el grip) cerca de tu cadera derecha -> se queda ahi y te sigue.
///  - Sacar: acerca la mano a la cadera y cogela con el grip, como cualquier objeto.
///  - Si la llave cae al agua o se pierde por debajo de 'lostBelowY', vuelve sola al bolsillo.
/// Va en un objeto propio (no hace falta que sea hijo del XR Origin: se coloca cada frame junto a la cabeza).
/// Lo monta "Isla Pirata > Llave y cofre > 4. Bolsillo".
public class KeyPocket : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("La llave (con XR Grab Interactable y Rigidbody)")]
    public XRGrabInteractable key;
    [Tooltip("Main Camera. Si se deja vacio se busca sola")]
    public Transform head;

    [Header("Posicion (respecto a la cabeza, girando con ella)")]
    public Vector3 offset = new Vector3(0.2f, -0.65f, 0.05f);
    [Tooltip("Distancia a la que hay que soltar la llave para que se guarde (m)")]
    public float storeRadius = 0.3f;

    [Header("Llave perdida")]
    [Tooltip("Si la llave baja de esta altura (el agua) vuelve al bolsillo. Lo pone la herramienta (altura del agua)")]
    public float lostBelowY = -100f;
    [Tooltip("Tambien vuelve si queda a mas de esta distancia del jugador y nadie la tiene (0 = nunca)")]
    public float returnIfFartherThan = 0f;

    [Header("Ayuda visual")]
    [Tooltip("Bolita en la cadera: se ve mientras llevas la llave en la mano; verde = sueltala aqui")]
    public bool showMarker = true;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool HasKey { get; private set; }

    Rigidbody keyRb;
    Collider[] keyCols;
    bool[] keyColsTrigger;
    Renderer marker;
    bool wasStored;

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (key == null) { Debug.LogWarning("[BOLSILLO] Falta 'Key' (la llave). Usa Isla Pirata > Llave y cofre > 4."); enabled = false; return; }
        keyRb = key.GetComponent<Rigidbody>();
        keyCols = key.GetComponentsInChildren<Collider>(true);
        keyColsTrigger = new bool[keyCols.Length];
        for (int i = 0; i < keyCols.Length; i++) keyColsTrigger[i] = keyCols[i].isTrigger;
        key.selectEntered.AddListener(OnKeyGrabbed);
        key.selectExited.AddListener(OnKeyReleased);
        if (showMarker) CreateMarker();
        if (debugLogs) Debug.Log("[BOLSILLO] Listo: suelta la llave junto a tu cadera derecha para guardarla.");
    }

    void OnDestroy()
    {
        if (key == null) return;
        key.selectEntered.RemoveListener(OnKeyGrabbed);
        key.selectExited.RemoveListener(OnKeyReleased);
    }

    void LateUpdate()
    {
        if (head == null) return;
        Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (f.sqrMagnitude < 0.0001f) f = transform.forward;
        Quaternion yaw = Quaternion.LookRotation(f.normalized, Vector3.up);
        transform.SetPositionAndRotation(head.position + yaw * offset, yaw);

        if (HasKey) key.transform.SetPositionAndRotation(transform.position, transform.rotation * Quaternion.Euler(0f, 0f, 90f));
        else if (key != null && !key.isSelected && Lost()) Store("la llave se habia perdido");

        if (marker != null)
        {
            bool holding = key != null && key.isSelected && !InChestLock();
            marker.enabled = holding;
            if (holding)
            {
                bool near = Vector3.Distance(key.transform.position, transform.position) < storeRadius;
                var c = near ? new Color(0.2f, 1f, 0.3f, 0.6f) : new Color(1f, 0.9f, 0.3f, 0.35f);
                if (marker.material.HasProperty("_BaseColor")) marker.material.SetColor("_BaseColor", c);
                else marker.material.color = c;
            }
        }
    }

    bool Lost()
    {
        if (key.transform.position.y < lostBelowY) return true;
        return returnIfFartherThan > 0f && head != null && Vector3.Distance(key.transform.position, head.position) > returnIfFartherThan && keyRb != null && keyRb.IsSleeping();
    }

    // La llave encajada en la cerradura del cofre tambien esta "seleccionada" (por el socket): no es una mano
    bool InChestLock()
    {
        if (key == null || !key.isSelected) return false;
        foreach (var i in key.interactorsSelecting)
            if (i is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor) return true;
        return false;
    }

    void OnKeyGrabbed(SelectEnterEventArgs a)
    {
        if (!HasKey) return;
        HasKey = false;
        wasStored = true;
        SetTrigger(false);
        if (debugLogs) Debug.Log("[BOLSILLO] Has sacado la llave del bolsillo");
    }

    void OnKeyReleased(SelectExitEventArgs a)
    {
        if (a.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor) return;
        if (Vector3.Distance(key.transform.position, transform.position) < storeRadius) { Store(null); return; }

        // Al sacarla del bolsillo el Rigidbody estaba quieto (kinematic): XRI lo restaura asi al soltarla. Que vuelva a caer.
        if (wasStored && keyRb != null)
        {
            keyRb.isKinematic = false;
            keyRb.useGravity = true;
        }
        wasStored = false;
    }

    /// Guarda la llave en el bolsillo desde otro script (p. ej. el cofre la devuelve si es de dia)
    public void StoreNow(string why) => Store(why);

    void Store(string why)
    {
        HasKey = true;
        if (keyRb != null)
        {
            if (!keyRb.isKinematic) { keyRb.linearVelocity = Vector3.zero; keyRb.angularVelocity = Vector3.zero; }
            keyRb.isKinematic = true;
        }
        key.transform.SetPositionAndRotation(transform.position, transform.rotation);
        SetTrigger(true);   // guardada no debe empujar la barca ni al jugador (sigue pudiendose coger)
        XRHandInput.HapticBoth(0.3f, 0.08f);
        if (debugLogs) Debug.Log("[BOLSILLO] Llave guardada en la cadera" + (why != null ? $" ({why})" : "") + ". Cogela de ahi con el grip cuando la necesites.");
    }

    void SetTrigger(bool stored)
    {
        for (int i = 0; i < keyCols.Length; i++)
            if (keyCols[i] != null) keyCols[i].isTrigger = stored || keyColsTrigger[i];
    }

    void CreateMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Marca Bolsillo";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * 0.08f;
        marker = go.GetComponent<Renderer>();
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh != null)
        {
            var m = new Material(sh);
            m.SetFloat("_Surface", 1f);   // transparente
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            marker.material = m;
        }
        marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        marker.enabled = false;
    }
}
