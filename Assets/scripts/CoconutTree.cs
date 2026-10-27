using UnityEngine;

/// Palmera que deja caer cocos cuando el jugador pasa por debajo.
/// Ponlo en un objeto vacio en la copa de la palmera (ese es el punto desde donde caen).
public class CoconutTree : MonoBehaviour
{
    [Tooltip("Prefab del coco (con Rigidbody, collider, XR Grab Interactable y Coconut)")]
    public Coconut coconutPrefab;
    [Tooltip("Main Camera del XR Origin")]
    public Transform playerHead;
    [Tooltip("Health del XR Origin")]
    public Health playerHealth;

    [Header("Cuando caen")]
    [Tooltip("Distancia horizontal al tronco para que empiece a soltar cocos")]
    public float triggerRadius = 3f;
    public float minInterval = 2f;
    public float maxInterval = 5f;
    [Tooltip("Cocos maximos vivos de esta palmera a la vez")]
    public int maxAlive = 3;
    [Tooltip("Dispersion horizontal del punto de caida (m)")]
    public float spread = 0.8f;
    [Tooltip("Probabilidad de apuntar justo encima de la cabeza del jugador")]
    [Range(0f, 1f)] public float aimAtPlayer = 0.5f;

    public bool debugLogs = true;

    float timer;
    readonly System.Collections.Generic.List<Coconut> alive = new System.Collections.Generic.List<Coconut>();

    void Start()
    {
        if (coconutPrefab == null) Debug.LogWarning($"[COCO] '{name}': falta el prefab del coco.");
        if (playerHead == null) Debug.LogWarning($"[COCO] '{name}': falta 'Player Head' (Main Camera).");
        timer = Random.Range(minInterval, maxInterval);
    }

    void Update()
    {
        if (coconutPrefab == null || playerHead == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        Vector3 d = playerHead.position - transform.position;
        d.y = 0f;
        if (d.magnitude > triggerRadius) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Random.Range(minInterval, maxInterval);

        alive.RemoveAll(c => c == null);
        if (alive.Count >= maxAlive) return;

        Vector3 p = transform.position;
        if (Random.value < aimAtPlayer) { p.x = playerHead.position.x; p.z = playerHead.position.z; }
        Vector2 r = Random.insideUnitCircle * spread;
        p += new Vector3(r.x, 0f, r.y);

        var c = Instantiate(coconutPrefab, p, Random.rotation);
        c.playerHead = playerHead;
        c.playerHealth = playerHealth;
        alive.Add(c);
        if (debugLogs) Debug.Log($"[COCO] '{name}' suelta un coco");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
