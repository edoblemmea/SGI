using UnityEngine;
using UnityEngine.AI;

/// IA comun de aranas y piratas: patrulla -> persigue -> ataca -> muere. Se mueve con NavMeshAgent.
/// Requiere en el MISMO objeto: NavMeshAgent, Health y un Collider (Capsule) para recibir golpes.
/// Recomendado: Rigidbody con Is Kinematic marcado (para que espada y cocos detecten el golpe).
///
/// Arana:  attackWindup 0,    attackRange 1.2, damage 10, chaseSpeed 4.
/// Pirata: attackWindup 0.5,  attackRange 1.8, damage 20, chaseSpeed 3 (el golpe llega tras levantar la espada).
///
/// Animator (opcional): crea un Animator Controller con un float 'Speed' (Idle <-> Walk), un trigger 'Attack' y
/// un trigger 'Die'. Si el Animator no tiene alguno de esos parametros simplemente se ignora.
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack, Dead }

    [Header("Objetivo (jugador)")]
    [Tooltip("Main Camera del XR Origin (la cabeza del jugador)")]
    public Transform target;
    [Tooltip("Health del XR Origin")]
    public Health targetHealth;

    [Header("Patrulla")]
    [Tooltip("Puntos de patrulla (vacios hijos de la escena). Si no hay, deambula alrededor de su posicion inicial")]
    public Transform[] patrolPoints;
    public float wanderRadius = 8f;
    public float patrolSpeed = 1.5f;
    public float waitAtPoint = 2f;

    [Header("Persecucion")]
    public float detectRange = 10f;
    [Tooltip("Distancia a la que deja de perseguir")]
    public float loseRange = 18f;
    public float chaseSpeed = 4f;
    [Tooltip("Si el jugador va en el barco o esta mas alto que esto (m), no puede atacarle")]
    public float maxHeightDifference = 2.5f;

    [Header("Ataque")]
    public float attackRange = 1.2f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    [Tooltip("Segundos entre empezar el ataque y que el golpe haga dano (0 = inmediato)")]
    public float attackWindup = 0f;

    [Header("Ataque visible (salto)")]
    [Tooltip("Al atacar, el modelo se encoge, salta hacia el jugador y vuelve: se ve claramente que ataca. El dano llega al saltar")]
    public bool lunge = true;
    public float lungeDistance = 0.45f;
    public float lungeHeight = 0.2f;
    [Tooltip("Duracion del salto completo (s)")]
    public float lungeTime = 0.45f;
    [Tooltip("Lo que se mueve en el salto. Vacio = el objeto del Animator si es un hijo; si no, el enemigo entero (con el agente en pausa)")]
    public Transform visual;

    [Header("Animacion (opcional)")]
    public Animator animator;
    public string speedParam = "Speed";
    public string attackTrigger = "Attack";
    public string dieTrigger = "Die";
    [Tooltip("Si el Animator no tiene trigger de muerte, el cuerpo se congela y cae de espaldas")]
    public bool fallOverIfNoDieAnim = true;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip alertClip;
    public AudioClip attackClip;

    [Header("Juego")]
    [Tooltip("Objetivo del GameManager que suma 1 al morir (p. ej. 'aranas'). Vacio = ninguno")]
    public string objectiveOnDeath = "";

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public State Current { get; private set; } = State.Patrol;
    Vector3 visualRest;
    bool noNav;   // sin NavMesh debajo: se mueve en linea recta (UpdateWithoutNavMesh)

    NavMeshAgent agent;
    Health health;
    Vector3 home;
    int patrolIndex;
    float waitTimer, attackTimer, windupTimer = -1f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        home = transform.position;
    }

    /// Centro de la patrulla (p. ej. la salida del nido del que ha salido)
    public void SetHome(Vector3 p) => home = p;

    void OnEnable() { if (health != null) { health.onDeath.AddListener(Die); health.onDamaged.AddListener(OnHurt); } }
    void OnDisable() { if (health != null) { health.onDeath.RemoveListener(Die); health.onDamaged.RemoveListener(OnHurt); } }

    void Start()
    {
        if (agent == null) { Debug.LogError($"[ENEMIGO] '{name}' no tiene NavMeshAgent."); enabled = false; return; }
        if (agent.enabled && !agent.isOnNavMesh)
        {
            // Buscar NavMesh cerca; si no hay (p. ej. la cubierta del barco sin hornear), perseguir en linea recta
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit nh, 2f, NavMesh.AllAreas)) agent.Warp(nh.position);
            if (!agent.isOnNavMesh)
            {
                noNav = true;
                agent.enabled = false;
                Debug.LogWarning($"[ENEMIGO] '{name}' NO esta sobre el NavMesh: te perseguira en linea recta. Para que esquive obstaculos, hornea el NavMesh (NavMesh Surface > Bake).");
            }
        }
        // Si no le han puesto objetivo (p. ej. un prefab colocado a mano), va a por el jugador: la camara y su Health
        if (target == null && Camera.main != null) target = Camera.main.transform;
        if (targetHealth == null && target != null) targetHealth = target.GetComponentInParent<Health>();
        if (target == null) Debug.LogWarning($"[ENEMIGO] '{name}': campo 'Target' VACIO, solo patrullara.");
        else if (targetHealth == null) Debug.LogWarning($"[ENEMIGO] '{name}': el jugador no tiene Health: atacara pero no hara dano (Isla Pirata > Juego).");
        if (debugLogs) Debug.Log($"[ENEMIGO] '{name}' listo: objetivo '{(target != null ? target.name : "-")}', le hace dano a '{(targetHealth != null ? targetHealth.name : "-")}', " +
                                 $"te ve a {detectRange:F0} m y ataca a {attackRange:F1} m");
        if (health == null) Debug.LogWarning($"[ENEMIGO] '{name}' no tiene Health: no se le podra matar.");
        if (agent.enabled) agent.speed = patrolSpeed;
        // Que se pare bien dentro del alcance del ataque (antes se quedaba justo en el borde y tardaba en morder)
        agent.stoppingDistance = Mathf.Min(agent.stoppingDistance, attackRange * 0.6f);
        if (visual == null && animator != null && animator.transform != transform) visual = animator.transform;
        if (visual != null) visualRest = visual.localPosition;
    }

    void Update()
    {
        if (Current == State.Dead) return;
        if (noNav) { UpdateWithoutNavMesh(); return; }
        if (!agent.isOnNavMesh) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) { agent.isStopped = true; return; }

        attackTimer -= Time.deltaTime;
        float dist = TargetDistance(out bool reachable);

        switch (Current)
        {
            case State.Patrol:
                Patrol();
                if (reachable && dist < detectRange) SetState(State.Chase);
                break;

            case State.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                agent.SetDestination(target.position);
                if (!reachable || dist > loseRange) SetState(State.Patrol);
                else if (dist <= attackRange) SetState(State.Attack);
                break;

            case State.Attack:
                agent.isStopped = true;
                FaceTarget();
                if (windupTimer >= 0f)
                {
                    windupTimer -= Time.deltaTime;
                    if (windupTimer < 0f && reachable && dist <= attackRange * 1.3f) Hit();
                }
                else if (dist > attackRange * 1.3f || !reachable) SetState(State.Chase);
                else if (attackTimer <= 0f) StartAttack();
                break;
        }

        SetAnimSpeed(agent.velocity.magnitude);
    }

    // Sin NavMesh: quieto hasta que te ve; luego va en linea recta hacia ti apoyado en el suelo de debajo, y ataca igual
    void UpdateWithoutNavMesh()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        attackTimer -= Time.deltaTime;
        float dist = TargetDistance(out bool reachable);
        float speed = 0f;
        switch (Current)
        {
            case State.Patrol:
                if (reachable && dist < detectRange) SetState(State.Chase);
                break;
            case State.Chase:
                if (!reachable || dist > loseRange) { SetState(State.Patrol); break; }
                if (dist <= attackRange) { SetState(State.Attack); break; }
                FaceTarget();
                Vector3 d = target.position - transform.position;
                d.y = 0f;
                Vector3 step = d.normalized * Mathf.Min(chaseSpeed * Time.deltaTime, Mathf.Max(0f, dist - attackRange * 0.6f));
                Vector3 next = transform.position + step;
                // Apoyarse en el suelo de debajo (cubierta, terreno); si no hay suelo delante, no avanza (no se tira al agua)
                bool ground = false;
                foreach (var h in Physics.RaycastAll(next + Vector3.up * 1f, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.transform.IsChildOf(transform) || (target != null && h.transform.IsChildOf(target.root))) continue;
                    next.y = h.point.y;
                    ground = true;
                    break;
                }
                if (ground) { transform.position = next; speed = step.magnitude / Mathf.Max(Time.deltaTime, 0.0001f); }
                break;
            case State.Attack:
                FaceTarget();
                if (windupTimer >= 0f)
                {
                    windupTimer -= Time.deltaTime;
                    if (windupTimer < 0f && reachable && dist <= attackRange * 1.3f) Hit();
                }
                else if (dist > attackRange * 1.3f || !reachable) SetState(State.Chase);
                else if (attackTimer <= 0f) StartAttack();
                break;
        }
        SetAnimSpeed(speed);
    }

    float TargetDistance(out bool reachable)
    {
        reachable = false;
        if (target == null || (targetHealth != null && targetHealth.IsDead)) return float.MaxValue;
        Vector3 d = target.position - transform.position;
        reachable = Mathf.Abs(d.y) < maxHeightDifference;
        d.y = 0f;
        return d.magnitude;
    }

    void Patrol()
    {
        agent.isStopped = false;
        agent.speed = patrolSpeed;
        if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.2f) return;

        waitTimer += Time.deltaTime;
        if (waitTimer < waitAtPoint) return;
        waitTimer = 0f;

        Vector3 next;
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            next = patrolPoints[patrolIndex] != null ? patrolPoints[patrolIndex].position : home;
        }
        else
        {
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            next = home + new Vector3(r.x, 0f, r.y);
        }
        if (NavMesh.SamplePosition(next, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    void StartAttack()
    {
        attackTimer = attackCooldown;
        SetTrigger(attackTrigger);
        if (audioSource != null && attackClip != null) audioSource.PlayOneShot(attackClip);
        else ProceduralSounds.Play(VoiceClip(), transform.position, 0.9f);
        if (debugLogs) Debug.Log($"[ENEMIGO] '{name}' ataca");
        if (lunge) { StartCoroutine(Lunge()); return; }
        if (attackWindup <= 0f) Hit();
        else windupTimer = attackWindup;
    }

    // Encogerse (atras y arriba), saltar hacia delante (golpe) y volver. Solo mueve el modelo, no el agente
    System.Collections.IEnumerator Lunge()
    {
        float total = Mathf.Max(lungeTime, attackWindup + 0.15f);
        float hitAt = attackWindup > 0f ? attackWindup / total : 0.6f;   // fraccion del salto en la que muerde
        bool hit = false;
        bool whole = visual == null;   // el Animator esta en la raiz (la arana): se mueve todo, con el agente en pausa
        Transform parent = whole ? null : (visual.parent != null ? visual.parent : transform);
        Vector3 basePos = transform.position;
        if (whole && agent.enabled) agent.updatePosition = false;
        for (float t = 0f; t < 1f; t += Time.deltaTime / total)
        {
            if (Current == State.Dead) break;
            Vector3 fwd = transform.forward, up = Vector3.up, off;
            if (t < hitAt * 0.8f)        // preparar: atras y arriba
            {
                float k = Mathf.SmoothStep(0f, 1f, t / (hitAt * 0.8f));
                off = (-fwd * 0.12f + up * lungeHeight) * k;
            }
            else if (t < hitAt)          // salto hacia delante
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - hitAt * 0.8f) / (hitAt * 0.2f));
                off = Vector3.Lerp(-fwd * 0.12f + up * lungeHeight, fwd * lungeDistance + up * lungeHeight * 0.4f, k);
            }
            else                         // volver
            {
                if (!hit)
                {
                    hit = true;
                    if (TargetDistance(out bool ok) <= attackRange * 1.5f && ok) Hit();
                }
                float k = Mathf.SmoothStep(0f, 1f, (t - hitAt) / (1f - hitAt));
                off = Vector3.Lerp(fwd * lungeDistance + up * lungeHeight * 0.4f, Vector3.zero, k);
            }
            if (whole) transform.position = basePos + off;
            else visual.localPosition = visualRest + parent.InverseTransformVector(off);
            yield return null;
        }
        if (whole)
        {
            if (Current != State.Dead) transform.position = basePos;   // muerta a mitad del salto: se queda donde cae
            if (agent.enabled && agent.isOnNavMesh) agent.nextPosition = transform.position;
            if (agent.enabled) agent.updatePosition = true;
        }
        else if (visual != null) visual.localPosition = visualRest;
    }

    void Hit()
    {
        windupTimer = -1f;
        if (targetHealth == null) return;
        if (targetHealth.TakeDamage(attackDamage, gameObject) && debugLogs)
            Debug.Log($"[ENEMIGO] '{name}' te golpea ({attackDamage:F0})");
    }

    void FaceTarget()
    {
        Vector3 d = target.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 8f * Time.deltaTime);
    }

    void SetState(State s)
    {
        if (s == Current) return;
        if (s == State.Chase && Current == State.Patrol)
        {
            if (audioSource != null && alertClip != null) audioSource.PlayOneShot(alertClip);
            else ProceduralSounds.Play(VoiceClip(), transform.position, 0.6f);
        }
        if (debugLogs) Debug.Log($"[ENEMIGO] '{name}' {Current} -> {s}");
        Current = s;
        windupTimer = -1f;
    }

    // Si le pegan mientras patrulla, va a por el jugador aunque este lejos
    // Sin sonidos asignados: las aranas (bajitas) sisean y los esqueletos hacen ruido de huesos
    AudioClip VoiceClip()
    {
        var c = GetComponent<Collider>();
        bool small = c != null ? c.bounds.size.y < 1f : transform.lossyScale.y < 1f;
        return small ? ProceduralSounds.Hiss() : ProceduralSounds.Rattle();
    }

    void OnHurt(float remaining)
    {
        ProceduralSounds.Play(ProceduralSounds.Thud(), transform.position, 0.8f);
        if (Current == State.Patrol && target != null) SetState(State.Chase);
    }

    void Die()
    {
        ProceduralSounds.Play(VoiceClip(), transform.position, 1f, 0.7f, 0.2f);
        SetState(State.Dead);
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        foreach (var c in GetComponents<Collider>()) c.enabled = false;
        SetTrigger(dieTrigger);
        SetAnimSpeed(0f);
        bool hasDieAnim = animator != null && HasParam(dieTrigger, AnimatorControllerParameterType.Trigger);
        if (!hasDieAnim && fallOverIfNoDieAnim) StartCoroutine(FallOver());
        if (!string.IsNullOrEmpty(objectiveOnDeath) && GameManager.Instance != null)
            GameManager.Instance.CompleteObjective(objectiveOnDeath);
    }

    System.Collections.IEnumerator FallOver()
    {
        if (animator != null) animator.enabled = false;   // congela la postura
        Quaternion from = transform.rotation;
        Quaternion to = Quaternion.AngleAxis(-85f, transform.right) * from;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            transform.rotation = Quaternion.Slerp(from, to, t * t);
            yield return null;
        }
        transform.rotation = to;
    }

    void SetAnimSpeed(float v)
    {
        if (animator != null && HasParam(speedParam, AnimatorControllerParameterType.Float)) animator.SetFloat(speedParam, v);
    }

    void SetTrigger(string p)
    {
        if (animator != null && HasParam(p, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(p);
    }

    bool HasParam(string p, AnimatorControllerParameterType type)
    {
        if (string.IsNullOrEmpty(p) || animator.runtimeAnimatorController == null) return false;
        foreach (var par in animator.parameters)
            if (par.name == p && par.type == type) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
