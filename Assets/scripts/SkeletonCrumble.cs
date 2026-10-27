using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// Hace que un esqueleto se DESMORONE (cae como un muneco de trapo) al morir o cuando se rompe la maldicion.
/// Al desmoronarse anade en tiempo real un Rigidbody + collider + CharacterJoint a cada hueso de 'Bones'
/// (en orden: el primero es la pelvis), desactiva Animator, IA y NavMeshAgent, y al rato se hunde en el suelo.
/// La lista 'Bones' la rellena el menu "Isla Pirata > Esqueletos piratas" (huesos Bip001 del modelo).
public class SkeletonCrumble : MonoBehaviour
{
    [Tooltip("Pelvis primero; despues columna, cabeza, brazos y piernas")]
    public Transform[] bones;
    public float totalMass = 20f;
    [Tooltip("Empujon aleatorio al desmoronarse (m/s)")]
    public float scatter = 1.5f;
    [Tooltip("Segundos tirado en el suelo antes de hundirse")]
    public float lieTime = 4f;
    public float sinkTime = 2f;
    public bool collapseWhenCurseBreaks = true;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip crumbleClip;
    public bool debugLogs = true;

    Health health;
    bool done;

    void Awake() => health = GetComponent<Health>();

    void OnEnable() { if (health != null) health.onDeath.AddListener(Crumble); }
    void OnDisable() { if (health != null) health.onDeath.RemoveListener(Crumble); }

    void Start()
    {
        if (bones == null || bones.Length == 0) Debug.LogWarning($"[ESQUELETO] '{name}' no tiene huesos asignados: solo desaparecera.");
        if (collapseWhenCurseBreaks && Curse.Instance != null) Curse.Instance.Broke += Crumble;
    }

    void OnDestroy()
    {
        if (Curse.Instance != null) Curse.Instance.Broke -= Crumble;
    }

    [ContextMenu("Desmoronar ahora")]
    public void Crumble()
    {
        if (done) return;
        done = true;
        if (debugLogs) Debug.Log($"[ESQUELETO] '{name}' se desmorona");
        if (audioSource != null && crumbleClip != null) audioSource.PlayOneShot(crumbleClip);

        // Apagar lo que lo mantiene en pie
        var anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = false;
        var ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        foreach (var c in GetComponents<Collider>()) c.enabled = false;
        var rootRb = GetComponent<Rigidbody>();
        if (rootRb != null) rootRb.isKinematic = true;

        // Muneco de trapo hueso a hueso
        float mass = totalMass / Mathf.Max(1, bones != null ? bones.Length : 1);
        var made = new System.Collections.Generic.Dictionary<Transform, Rigidbody>();
        if (bones != null)
        {
            foreach (var b in bones)
            {
                if (b == null) continue;
                var rb = b.gameObject.AddComponent<Rigidbody>();
                rb.mass = mass;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                AddCollider(b);

                // Unir al hueso padre mas cercano que ya tenga Rigidbody
                for (var p = b.parent; p != null && p != transform; p = p.parent)
                    if (made.TryGetValue(p, out var parentRb))
                    {
                        var j = b.gameObject.AddComponent<CharacterJoint>();
                        j.connectedBody = parentRb;
                        j.enablePreprocessing = false;
                        break;
                    }
                rb.linearVelocity = Random.insideUnitSphere * scatter;
                made[b] = rb;
            }
        }
        StartCoroutine(SinkAndDestroy());
    }

    static void AddCollider(Transform b)
    {
        // Capsula desde el hueso hasta su primer hijo (si no tiene hijo, una esfera pequena)
        Transform child = b.childCount > 0 ? b.GetChild(0) : null;
        if (child == null || b.name.ToLower().Contains("head"))
        {
            var s = b.gameObject.AddComponent<SphereCollider>();
            s.radius = 0.1f / Mathf.Max(b.lossyScale.x, 0.0001f);
            return;
        }
        Vector3 local = b.InverseTransformPoint(child.position);
        var c = b.gameObject.AddComponent<CapsuleCollider>();
        Vector3 a = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        c.direction = a.x >= a.y && a.x >= a.z ? 0 : (a.y >= a.z ? 1 : 2);
        c.center = local * 0.5f;
        c.height = local.magnitude;
        c.radius = Mathf.Max(local.magnitude * 0.2f, 0.03f / Mathf.Max(b.lossyScale.x, 0.0001f));
    }

    IEnumerator SinkAndDestroy()
    {
        yield return new WaitForSeconds(lieTime);
        // Que los huesos dejen de simular y el esqueleto se hunda
        foreach (var rb in GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Vector3 start = transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(sinkTime, 0.01f))
        {
            transform.position = start + Vector3.down * t;
            yield return null;
        }
        Destroy(gameObject);
    }
}
