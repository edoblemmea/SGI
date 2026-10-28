using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Reloj de arena: cogelo y DALE LA VUELTA para que pase el tiempo. Si es de dia, anochece; si es de noche, amanece.
/// Es una "herramienta" de control (cambia el estado de la aplicacion) con forma de objeto del mundo:
/// elegir la herramienta = cogerla (apuntes 3DUI, Tools / TUI).
public class Hourglass : MonoBehaviour
{
    public DayNightCycle dayNight;
    [Tooltip("Hora a la que se va al dar la vuelta de dia")]
    public float nightHour = 23f;
    [Tooltip("Hora a la que se va al dar la vuelta de noche")]
    public float dayHour = 9f;
    public float transitionSeconds = 4f;
    [Tooltip("Cuanto hay que girarlo (grados) para que cuente como 'darle la vuelta'")]
    public float flipAngle = 140f;

    [Header("Arena (opcional)")]
    [Tooltip("Arena de arriba y de abajo: se vacia/llena durante la transicion")]
    public Transform sandTop;
    public Transform sandBottom;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip flipClip;
    public bool debugLogs = true;

    XRGrabInteractable grab;
    XRNode hand = XRNode.RightHand;
    bool upsideDown;
    Vector3 topScale, bottomScale;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (sandTop != null) topScale = sandTop.localScale;
        if (sandBottom != null) bottomScale = sandBottom.localScale;
    }

    void OnEnable() { if (grab != null) grab.selectEntered.AddListener(OnGrab); }
    void OnDisable() { if (grab != null) grab.selectEntered.RemoveListener(OnGrab); }

    void Start()
    {
        if (dayNight == null) Debug.LogWarning($"[RELOJ] '{name}': falta 'Day Night' (el ciclo dia/noche). No hara nada.");
        if (grab == null) Debug.LogWarning($"[RELOJ] '{name}' no tiene XR Grab Interactable.");
        upsideDown = Vector3.Dot(transform.up, Vector3.up) < 0f;
    }

    void OnGrab(SelectEnterEventArgs a)
    {
        hand = a.interactorObject.handedness == InteractorHandedness.Left ? XRNode.LeftHand : XRNode.RightHand;
        if (debugLogs) Debug.Log("[RELOJ] Cogido. Dale la vuelta para que pase el tiempo");
    }

    void Update()
    {
        if (dayNight == null) return;

        // Detectar "darle la vuelta": el eje Y del reloj pasa de mirar arriba a mirar abajo (o al reves)
        float angle = Vector3.Angle(upsideDown ? -transform.up : transform.up, Vector3.up);
        if (angle > flipAngle && grab != null && grab.isSelected && !dayNight.IsTransitioning)
        {
            upsideDown = !upsideDown;
            Flip();
        }

        // Animacion de la arena mientras dura la transicion
        if (sandTop != null && sandBottom != null)
        {
            float t = dayNight.IsTransitioning ? dayNight.TransitionProgress : 1f;
            sandTop.localScale = new Vector3(topScale.x, topScale.y * (1f - t), topScale.z);
            sandBottom.localScale = new Vector3(bottomScale.x, bottomScale.y * Mathf.Max(t, 0.05f), bottomScale.z);
        }
    }

    void Flip()
    {
        bool toNight = !dayNight.IsNight;
        dayNight.TransitionTo(toNight ? nightHour : dayHour, transitionSeconds);
        XRHandInput.Haptic(hand, 0.7f, 0.2f);
        if (audioSource != null && flipClip != null) audioSource.PlayOneShot(flipClip);
        else ProceduralSounds.Play(ProceduralSounds.Sand(), transform.position, 0.7f);
        if (debugLogs) Debug.Log(toNight ? "[RELOJ] Le das la vuelta... cae la noche" : "[RELOJ] Le das la vuelta... amanece");
    }
}
