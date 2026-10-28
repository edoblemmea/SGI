using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// Nido de aranas en una roca: cuando el jugador se acerca, van saliendo aranas de dentro de la roca
/// (aparecen en 'Hidden Point', dentro, y caminan hasta 'Exit Point', fuera, sobre el NavMesh).
/// De noche salen mas. Lo crea el menu "Isla Pirata > Aranas" (seccion Nidos en rocas).
public class SpiderNest : MonoBehaviour
{
    [Header("Aranas")]
    [Tooltip("Prefab Arana (con NavMeshAgent + EnemyAI + Health)")]
    public GameObject spiderPrefab;
    public Transform playerHead;
    public Health playerHealth;

    [Header("Por donde salen")]
    [Tooltip("Dentro de la roca (donde aparecen)")]
    public Transform hiddenPoint;
    [Tooltip("Fuera de la roca, sobre el NavMesh (hasta donde caminan al salir)")]
    public Transform exitPoint;
    public float emergeSeconds = 1.2f;

    [Header("Cuando salen")]
    [Tooltip("Las aranas no salen hasta que el jugador sacude la primera palmera (si no hay palmeras sacudibles en la escena, no se espera)")]
    public bool waitForFirstPalmShake = true;
    [Tooltip("Distancia del jugador a la roca para que empiecen a salir")]
    public float triggerRadius = 12f;
    public float interval = 6f;
    [Tooltip("Aranas vivas a la vez de este nido")]
    public int maxAlive = 2;
    [Tooltip("Total de aranas que puede soltar (0 = sin limite)")]
    public int totalSpiders = 4;
    [Tooltip("De noche se multiplica el maximo de vivas")]
    public DayNightCycle dayNight;
    public int nightMaxAlive = 4;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip emergeClip;
    public bool debugLogs = true;

    readonly List<GameObject> alive = new List<GameObject>();
    float timer;
    int released;
    bool waitPalm;

    void Start()
    {
        if (spiderPrefab == null) Debug.LogWarning($"[NIDO] '{name}': falta 'Spider Prefab'.");
        if (playerHead == null) Debug.LogWarning($"[NIDO] '{name}': falta 'Player Head'.");
        timer = 1f;   // la primera sale enseguida
        waitPalm = waitForFirstPalmShake && FindAnyObjectByType<ShakeablePalm>() != null;
        if (waitPalm && debugLogs) Debug.Log($"[NIDO] '{name}': esperando a que sacudas la primera palmera");
    }

    void Update()
    {
        if (spiderPrefab == null || playerHead == null || exitPoint == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        if (totalSpiders > 0 && released >= totalSpiders) return;
        if (waitPalm && !ShakeablePalm.AnyShaken) return;

        Vector3 d = playerHead.position - transform.position;
        d.y = 0f;
        if (d.magnitude > triggerRadius) return;

        alive.RemoveAll(s => s == null);
        int max = dayNight != null && dayNight.IsNight ? nightMaxAlive : maxAlive;
        if (alive.Count >= max) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = interval;
        StartCoroutine(Emerge());
    }

    IEnumerator Emerge()
    {
        released++;
        // Se instancia en la salida (sobre el NavMesh, sin avisos del agente) y se lleva dentro de la roca
        var s = Instantiate(spiderPrefab, exitPoint.position, exitPoint.rotation);
        alive.Add(s);
        var agent = s.GetComponent<NavMeshAgent>();
        var ai = s.GetComponent<EnemyAI>();
        if (agent != null) agent.enabled = false;
        if (ai != null)
        {
            ai.enabled = false;
            ai.target = playerHead;
            ai.targetHealth = playerHealth;
            ai.SetHome(exitPoint.position);
        }
        var anim = s.GetComponentInChildren<Animator>();
        SetSpeed(anim, 1f);

        Vector3 from = hiddenPoint != null ? hiddenPoint.position : exitPoint.position - exitPoint.forward;
        Vector3 to = exitPoint.position;
        Quaternion rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(to - from, Vector3.up).normalized + Vector3.forward * 0.0001f);
        if (audioSource != null && emergeClip != null) audioSource.PlayOneShot(emergeClip);
        else ProceduralSounds.Play(ProceduralSounds.Hiss(), transform.position, 0.5f);
        if (debugLogs) Debug.Log($"[NIDO] Sale una arana de '{name}' ({released}{(totalSpiders > 0 ? "/" + totalSpiders : "")})");

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(emergeSeconds, 0.01f))
        {
            if (s == null) yield break;
            s.transform.SetPositionAndRotation(Vector3.Lerp(from, to, t), rot);
            yield return null;
        }
        if (s == null) yield break;
        s.transform.position = to;

        if (agent != null)
        {
            agent.enabled = true;
            if (NavMesh.SamplePosition(to, out NavMeshHit hit, 3f, NavMesh.AllAreas)) agent.Warp(hit.position);
            else Debug.LogWarning($"[NIDO] La salida de '{name}' no esta sobre el NavMesh: mueve 'Exit Point' o vuelve a hornear.");
        }
        if (ai != null) ai.enabled = true;
    }

    static void SetSpeed(Animator a, float v)
    {
        if (a == null || a.runtimeAnimatorController == null) return;
        foreach (var p in a.parameters)
            if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) { a.SetFloat("Speed", v); return; }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
        if (hiddenPoint != null && exitPoint != null) Gizmos.DrawLine(hiddenPoint.position, exitPoint.position);
    }
}
