using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Marca un objeto agarrable como llave. Ponlo en la llave (rust_key), que debe tener Rigidbody + XR Grab Interactable.
/// La primera vez que se coge completa el objetivo 'objectiveOnGrab' del GameManager.
public class KeyItem : MonoBehaviour
{
    [Tooltip("Debe coincidir con 'Required Key Id' del ChestLock")]
    public string keyId = "cofre";
    [Tooltip("Objetivo que se completa al cogerla por primera vez (vacio = ninguno)")]
    public string objectiveOnGrab = "llave";
    public bool debugLogs = true;

    XRGrabInteractable grab;
    bool pickedOnce;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (grab == null) Debug.LogWarning($"[LLAVE] '{name}' no tiene XR Grab Interactable: no se podra coger.");
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
