using UnityEngine;
public class OverAnchor : MonoBehaviour
{
    [ContextMenu("Probar")]
    void Probe()
    {
        var hits = Physics.RaycastAll(transform.position + Vector3.up * 5f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits) Debug.Log($"{h.distance:F2} m: {h.collider.name} (trigger: {h.collider.isTrigger})", h.collider);
    }
}