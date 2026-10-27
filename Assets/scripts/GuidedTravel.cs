using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// Viaje guiado (navegacion target-based "steering"): te desliza por el suelo hasta un destino, mas rapido que andando.
/// Lo usa la carta (TreasureMap) cuando el modo de viaje es "Deslizarse" en vez de teletransporte.
///  - Velocidad constante con arranque y frenada suaves; no te gira la cabeza (mas comodo).
///  - Sigue la altura del suelo (raycast); atraviesa los obstaculos pequenos.
///  - Activa la vineta de confort si esta encendida.
///  - Boton B (mando derecho) = parar.
///  - Los objetos de 'carryAlong' (la carta) viajan contigo si no los tienes cogidos.
/// Lo anade solo la carta si no existe.
public class GuidedTravel : MonoBehaviour, ITunnelingVignetteProvider
{
    [Header("Jugador")]
    public Transform xrOrigin;
    public Transform head;
    [Tooltip("Si vas en la barca, primero desembarcas")]
    public BoatRowing boat;

    [Header("Movimiento")]
    [Tooltip("m/s (andar con el joystick es unos 2,5)")]
    public float speed = 7f;
    [Tooltip("Segundos de arranque y de frenada")]
    public float easeTime = 0.8f;
    [Tooltip("Capas del suelo para seguir su altura")]
    public LayerMask groundMask = ~0;
    [Tooltip("Parar con el boton B del mando derecho")]
    public bool cancelWithB = true;
    [Tooltip("Objetos que viajan contigo (la carta) si no los tienes en la mano")]
    public Transform[] carryAlong;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool IsTraveling { get; private set; }
    public VignetteParameters vignetteParameters => null;   // la vineta usa sus valores por defecto

    Coroutine routine;
    TunnelingVignetteController vignette;
    readonly List<Behaviour> disabled = new List<Behaviour>();

    public void TravelTo(Transform destination, string label)
    {
        if (destination == null || xrOrigin == null || head == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Travel(destination, label));
    }

    public void Stop() => cancel = true;
    bool cancel;

    IEnumerator Travel(Transform dest, string label)
    {
        if (boat != null && boat.IsBoarded) boat.Disembark();
        Begin();
        cancel = false;
        Vector3 start = Flat(head.position);
        float total = Vector3.Distance(start, Flat(dest.position));
        if (debugLogs) Debug.Log($"[VIAJE] Hacia {label}: {total:F0} m a {speed:F0} m/s (unos {total / Mathf.Max(speed, 0.1f):F0} s). B = parar.");

        bool prevB = true;   // que no cuente si ya estaba pulsado
        float t = 0f, timeout = total / Mathf.Max(speed, 0.1f) + 10f;
        while (!cancel && t < timeout)
        {
            t += Time.deltaTime;
            if (cancelWithB)
            {
                bool b = XRHandInput.SecondaryButton(XRNode.RightHand);
                if (b && !prevB) { cancel = true; break; }
                prevB = b;
            }

            Vector3 toDest = Flat(dest.position) - Flat(head.position);
            float left = toDest.magnitude;
            if (left < 0.15f) break;

            // Arranque y frenada suaves
            float done = total - left;
            float k = Mathf.Clamp01(Mathf.Min((done + 0.5f) / (speed * easeTime), left / (speed * easeTime * 0.5f)));
            float step = Mathf.Min(left, speed * Mathf.Max(k, 0.15f) * Time.deltaTime);
            Vector3 delta = toDest / left * step;

            // Altura: seguir el suelo bajo la cabeza (si no hay suelo, p. ej. agua, se mantiene)
            Vector3 probe = head.position + delta;
            probe.y = xrOrigin.position.y + 1f;   // desde la rodilla: no pisar copas de palmeras ni la carta
            if (Ground(probe, out float groundY))
                delta.y = Mathf.Lerp(xrOrigin.position.y, groundY, 10f * Time.deltaTime) - xrOrigin.position.y;

            Move(delta);
            yield return null;
        }

        if (!cancel)
        {
            // Llegada exacta: la cabeza encima del destino, a su altura
            Vector3 off = head.position - xrOrigin.position;
            Move(new Vector3(dest.position.x - off.x, dest.position.y, dest.position.z - off.z) - xrOrigin.position);
        }
        End();
        if (debugLogs) Debug.Log(cancel ? "[VIAJE] Parado" : $"[VIAJE] Has llegado a {label}");
        XRHandInput.HapticBoth(0.4f, 0.08f);
        routine = null;
    }

    void Move(Vector3 delta)
    {
        xrOrigin.position += delta;
        if (carryAlong == null) return;
        foreach (var c in carryAlong)
        {
            if (c == null) continue;
            var g = c.GetComponent<XRGrabInteractable>();
            if (g != null && g.isSelected) continue;   // si la llevas en la mano ya viene contigo
            var rb = c.GetComponent<Rigidbody>();
            if (rb != null) rb.position += delta;
            c.position += delta;
        }
    }

    bool Ground(Vector3 at, out float y)
    {
        y = 0f;
        var hits = Physics.RaycastAll(at, Vector3.down, 30f, groundMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(xrOrigin)) continue;
            bool carried = false;
            if (carryAlong != null) foreach (var c in carryAlong) if (c != null && h.transform.IsChildOf(c)) carried = true;
            if (carried) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; }
        }
        return best < float.MaxValue;
    }

    void Begin()
    {
        IsTraveling = true;
        disabled.Clear();
        var cc = xrOrigin.GetComponent<CharacterController>();
        if (cc != null && cc.enabled) { cc.enabled = false; disabled.Add(null); }
        // Apaga Move, Gravity, Jump, Teleport... (menos el giro) para que nada luche con el viaje
        foreach (var p in xrOrigin.GetComponentsInChildren<LocomotionProvider>())
            if (p.enabled && !(p is SnapTurnProvider) && !(p is ContinuousTurnProvider)) { p.enabled = false; disabled.Add(p); }
        vignette = xrOrigin.GetComponentInChildren<TunnelingVignetteController>();   // null si la vineta esta apagada en el menu
        if (vignette != null) vignette.BeginTunnelingVignette(this);
    }

    void End()
    {
        IsTraveling = false;
        foreach (var b in disabled)
        {
            if (b == null) { var cc = xrOrigin.GetComponent<CharacterController>(); if (cc != null) cc.enabled = true; }
            else b.enabled = true;
        }
        disabled.Clear();
        if (vignette != null) vignette.EndTunnelingVignette(this);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
