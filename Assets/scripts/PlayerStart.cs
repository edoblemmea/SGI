using UnityEngine;

/// Punto de inicio del jugador. Ponlo en un objeto vacio donde quieras empezar la partida:
/// al darle a Play el XR Origin se coloca aqui, apoyado en el suelo de debajo y mirando hacia la flecha azul (Z) de este objeto.
/// Si no hay ningun PlayerStart en la escena, la barca (BoatRowing) te pone en el ancla del muelle.
/// Si la barca tiene 'Start On Board' marcado, se empieza sentado en la barca y esto no hace nada.
public class PlayerStart : MonoBehaviour
{
    [Tooltip("XR Origin (XR Rig). Vacio = se busca solo")]
    public Transform xrOrigin;
    [Tooltip("Apoyar al jugador en el suelo de debajo (si lo hay a menos de 3 m). Asi no caes aunque el punto quede algo alto o bajo")]
    public bool snapToGround = true;
    public bool debugLogs = true;

    void Awake()
    {
        foreach (var b in FindObjectsByType<BoatRowing>(FindObjectsInactive.Exclude))
            if (b.startOnBoard) return;   // empieza sentado en la barca
        if (xrOrigin == null)
        {
            var o = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (o != null) xrOrigin = o.transform;
        }
        if (xrOrigin == null) { Debug.LogWarning("[INICIO] No hay XR Origin en la escena."); return; }
        Vector3 pos = PlaceOrigin(xrOrigin, transform, null, snapToGround);
        if (debugLogs) Debug.Log($"[INICIO] Empiezas en '{name}', en {pos}.");
    }

    /// Coloca el XR Origin en 't', mirando hacia su Z, y (si 'snap') lo apoya en el suelo de debajo.
    /// 'ignore' = colliders que no cuentan como suelo (p. ej. la barca). Devuelve la posicion final.
    public static Vector3 PlaceOrigin(Transform origin, Transform t, Transform ignore, bool snap = true)
    {
        Vector3 pos = t.position;
        if (snap)
        {
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(pos + Vector3.up * 1f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(origin) || (ignore != null && h.transform.IsChildOf(ignore))) continue;
                if (h.distance < best && pos.y - h.point.y < 3f) { best = h.distance; }
            }
            if (best < float.MaxValue) pos.y = pos.y + 1f - best;
        }
        Vector3 f = Vector3.ProjectOnPlane(t.forward, Vector3.up);
        origin.SetPositionAndRotation(pos, f.sqrMagnitude > 0.001f ? Quaternion.LookRotation(f, Vector3.up) : origin.rotation);
        Physics.SyncTransforms();
        return pos;
    }

    void OnDrawGizmos()
    {
        // Un jugador de 1,7 m y una flecha hacia donde mirara
        Gizmos.color = new Color(0.2f, 0.8f, 1f);
        Vector3 p = transform.position;
        Gizmos.DrawWireSphere(p + Vector3.up * 0.25f, 0.25f);
        Gizmos.DrawWireSphere(p + Vector3.up * 1.6f, 0.15f);
        Gizmos.DrawLine(p + Vector3.up * 0.25f, p + Vector3.up * 1.45f);
        Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Gizmos.DrawLine(p + Vector3.up * 1.6f, p + Vector3.up * 1.6f + f * 0.6f);
    }
}
