using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Prueba de fisica del barco, SIN mandos ni XR.
/// Ponlo en el mismo objeto que BoatRowing (el que tiene el Rigidbody).
/// A los 'autoDelay' segundos de empezar Play (o al pulsar P con la Game view enfocada, o con clic derecho en el
/// titulo del componente > "Empujar ahora") da un impulso hacia la proa y escribe en la Console que pasa.
[RequireComponent(typeof(Rigidbody))]
public class BoatPushTest : MonoBehaviour
{
    public Transform bowReference;   // el mismo BowRef que usa BoatRowing (opcional)
    public float impulse = 3f;       // velocidad (m/s) que se anade de golpe
    public bool autoPush = true;
    public float autoDelay = 3f;

    Rigidbody rb;
    float timer;
    bool autoDone;
    float reportUntil;
    float nextReport;
    Vector3 pushStartPos;
    readonly HashSet<string> seen = new HashSet<string>();

    void Awake() => rb = GetComponent<Rigidbody>();

    void Start()
    {
        var up = GetComponentsInParent<Rigidbody>();
        var down = GetComponentsInChildren<Rigidbody>();
        Debug.Log($"[PUSH] kinematic={rb.isKinematic} gravedad={rb.useGravity} constraints={rb.constraints} masa={rb.mass} " +
                  $"| Rigidbodies en padres={up.Length} (debe ser 1) | en hijos={down.Length} (debe ser 1) " +
                  $"| padre={(transform.parent != null ? transform.parent.name : "ninguno")} | escala={transform.lossyScale}");
    }

    void Update()
    {
        timer += Time.deltaTime;

        bool key = Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame;
        if (key || (autoPush && !autoDone && timer >= autoDelay))
        {
            autoDone = true;
            PushNow();
        }

        if (Time.time < reportUntil && Time.time >= nextReport)
        {
            nextReport = Time.time + 0.5f;
            Debug.Log($"[PUSH] velocidad={rb.linearVelocity.magnitude:F2} m/s | desplazado={(transform.position - pushStartPos).magnitude:F2} m | durmiendo={rb.IsSleeping()}");
        }
    }

    [ContextMenu("Empujar ahora")]
    void PushNow()
    {
        Transform r = bowReference != null ? bowReference : transform;
        Vector3 raw = r.forward;
        Vector3 dir = Vector3.ProjectOnPlane(raw, Vector3.up).normalized;

        if (Mathf.Abs(raw.y) > 0.3f)
            Debug.LogWarning($"[PUSH] La flecha azul (Z) de '{r.name}' NO esta horizontal (y={raw.y:F2}). Si es BowRef, giralo para que mire a la proa en horizontal.");

        seen.Clear();
        pushStartPos = transform.position;
        rb.AddForce(dir * impulse, ForceMode.VelocityChange);
        reportUntil = Time.time + 4f;
        nextReport = 0f;
        Debug.Log($"[PUSH] Impulso de {impulse} m/s hacia {dir} (flecha azul de '{r.name}' = {raw})");
    }

    // Que objetos tocan al barco durante la prueba (por ejemplo, el muelle o el fondo)
    void OnCollisionStay(Collision c)
    {
        if (Time.time < reportUntil && seen.Add(c.collider.name))
            Debug.Log($"[PUSH] El barco esta en contacto con: '{c.collider.name}'");
    }
}
