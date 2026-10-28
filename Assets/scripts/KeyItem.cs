using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Marca un objeto agarrable como llave. Ponlo en la llave (rust_key), que debe tener Rigidbody + XR Grab Interactable.
/// La primera vez que se coge completa el objetivo 'objectiveOnGrab' del GameManager.
public class KeyItem : MonoBehaviour
{
    [Tooltip("Debe coincidir con 'Required Key Id' del ChestLock")]
    public string keyId = "cofre";
    [Tooltip("Objetivo que se completa al cogerla por primera vez (vacio = ninguno)")]
    public string objectiveOnGrab = "llave";
    [Tooltip("Si ninguna palmera sacudible tiene esta llave en 'Hidden Key', se esconde sola en una al azar al empezar")]
    public bool hideInRandomPalm = true;
    [Tooltip("Marcado: cogida con el rayo desde lejos se queda en la punta del rayo y el joystick de esa mano la aleja o la acerca. " +
             "Desmarcado: siempre viene a la mano")]
    public bool keepAtDistanceWhenFarGrabbed = true;
    public bool debugLogs = true;

    XRGrabInteractable grab;
    bool pickedOnce;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (grab == null) Debug.LogWarning($"[LLAVE] '{name}' no tiene XR Grab Interactable: no se podra coger.");
        // Cogida con el rayo: a distancia (el joystick la aleja/acerca) o siempre a la mano
        else grab.farAttachMode = keepAtDistanceWhenFarGrabbed ? InteractableFarAttachMode.DeferToInteractor : InteractableFarAttachMode.Near;
    }

    void Start()
    {
        if (!hideInRandomPalm) return;
        var palms = FindObjectsByType<ShakeablePalm>(FindObjectsInactive.Exclude);
        foreach (var p in palms) if (p.hiddenKey == transform) return;   // ya hay una palmera con la llave
        if (palms.Length == 0)
        {
            Debug.LogWarning($"[LLAVE] '{name}' no esta escondida y no hay palmeras sacudibles: se queda donde esta.");
            return;
        }
        var palm = palms[Random.Range(0, palms.Length)];
        palm.HideKey(transform);
        if (debugLogs) Debug.Log($"[LLAVE] Ninguna palmera tenia la llave: la escondo en '{palm.name}'");
    }

    void OnEnable() { if (grab != null) grab.selectEntered.AddListener(OnGrabbed); }
    void OnDisable() { if (grab != null) grab.selectEntered.RemoveListener(OnGrabbed); }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (pickedOnce) return;
        pickedOnce = true;
        if (debugLogs) Debug.Log($"[LLAVE] Has cogido la llave '{keyId}' con '{args.interactorObject.transform.name}'");
        if (!string.IsNullOrEmpty(objectiveOnGrab) && GameManager.Instance != null)
            GameManager.Instance.CompleteObjective(objectiveOnGrab);
    }
}
