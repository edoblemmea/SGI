using UnityEngine;
using UnityEngine.Events;

/// Zona que completa un objetivo cuando 'Watch' entra en ella (p. ej. el barco llega a la playa de la isla).
/// No depende de capas ni de triggers: cada 0.2 s comprueba si la posicion de 'Watch' esta dentro del
/// Box Collider de este objeto (marcalo como Is Trigger para que no estorbe).
public class ObjectiveZone : MonoBehaviour
{
    [Tooltip("Objeto a vigilar: el barco (OldWoodenRowboat_Complete) o el XR Origin")]
    public Transform watch;
    public string objectiveId = "isla";
    [Tooltip("Si se marca, solo cuenta si el jugador va a bordo (XR Origin hijo de 'Watch')")]
    public bool requirePlayerOnBoard = true;
    public Transform xrOrigin;
    public UnityEvent onEntered;
    public bool debugLogs = true;

    Collider zone;
    bool done;
    float timer;

    void Awake()
    {
        zone = GetComponent<Collider>();
        if (zone == null) Debug.LogWarning($"[ZONA] '{name}' necesita un Box Collider que marque la zona.");
    }

    void Update()
    {
        if (done || zone == null || watch == null) return;
        timer += Time.deltaTime;
        if (timer < 0.2f) return;
        timer = 0f;

        if (!zone.bounds.Contains(watch.position)) return;
        if (requirePlayerOnBoard && (xrOrigin == null || !xrOrigin.IsChildOf(watch))) return;

        done = true;
        if (debugLogs) Debug.Log($"[ZONA] '{watch.name}' ha llegado a '{name}' -> objetivo '{objectiveId}'");
        if (!string.IsNullOrEmpty(objectiveId) && GameManager.Instance != null)
            GameManager.Instance.CompleteObjective(objectiveId);
        onEntered?.Invoke();
    }
}
