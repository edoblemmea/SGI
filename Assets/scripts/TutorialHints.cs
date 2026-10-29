using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Pistas de controles en contexto: cada una sale UNA vez, cuando hace falta (al acercarte a una palmera, ver la llave,
/// subir a la barca, llegar a la escala, al cofre...). El texto flota un poco por debajo de la vista y te sigue suavemente.
/// Espera a que el totem narrador termine de hablar. Busca los objetos por tipo: no hay que asignar nada.
/// Lo crea solo el GameManager al empezar (casilla 'Tutorial Hints').
public class TutorialHints : MonoBehaviour
{
    [Tooltip("Segundos que se ve cada pista")]
    public float secondsPerHint = 8f;
    [Tooltip("Distancia del texto delante de los ojos (m)")]
    public float distance = 1.1f;
    [Tooltip("Cuanto por debajo de la mirada (m)")]
    public float below = 0.25f;
    public bool debugLogs = true;

    class Hint
    {
        public string id, text;
        public System.Func<bool> when;
        public bool done;
    }

    readonly List<Hint> hints = new List<Hint>();
    readonly Queue<Hint> queue = new Queue<Hint>();
    Transform head;
    TextMeshPro label;
    float showTimer, checkTimer, gap, startTime;
    bool damaged;

    ShakeablePalm[] palms;
    KeyItem[] keys;
    Sword[] swords;
    BoatRowing boat;
    RopeLadder[] ladders;
    ChestLock[] chests;
    StoryTotem[] totems;
    KeyPocket pocket;

    void Start()
    {
        if (Camera.main != null) head = Camera.main.transform;
        if (head == null) { enabled = false; return; }
        startTime = Time.time;
        palms = FindObjectsByType<ShakeablePalm>(FindObjectsInactive.Exclude);
        keys = FindObjectsByType<KeyItem>(FindObjectsInactive.Exclude);
        swords = FindObjectsByType<Sword>(FindObjectsInactive.Exclude);
        boat = FindAnyObjectByType<BoatRowing>();
        ladders = FindObjectsByType<RopeLadder>(FindObjectsInactive.Exclude);
        chests = FindObjectsByType<ChestLock>(FindObjectsInactive.Exclude);
        totems = FindObjectsByType<StoryTotem>(FindObjectsInactive.Exclude);
        pocket = FindAnyObjectByType<KeyPocket>();
        var playerHealth = head.GetComponentInParent<Health>();
        if (playerHealth != null) playerHealth.onDamaged.AddListener(_ => damaged = true);
        CreateLabel();
        Define();
        if (debugLogs) Debug.Log($"[PISTAS] Listo: {hints.Count} pistas de controles");
    }

    // ------------------------------------------------------------------ las pistas
    void Define()
    {
        Add("andar", "Joystick IZQUIERDO: andar.\nJoystick DERECHO hacia delante: teletransportarte (apunta y suelta).\nJoystick DERECHO a los lados: girar.",
            () => Time.time - startTime > 3f);
        Add("menu", "Botón Y (mando izquierdo): menú de opciones\n(tipo de movimiento, giro, volumen...).",
            () => Time.time - startTime > 20f);
        Add("palmera", "Para sacudir una palmera: acerca la mano al tronco,\naprieta el GRIP (botón lateral) y mueve la mano\na los lados con fuerza.",
            () => Near(palms, 3f));
        Add("llave", "¡La llave! Cógela con el GRIP\n(o apúntala con el rayo y aprieta el GRIP).",
            () => KeyOnGround(3f));
        Add("bolsillo", "Para guardar la llave: suelta el GRIP\njunto a tu cadera DERECHA (bolita verde).\nPara sacarla, cógela de ahí o apúntala con el rayo.",
            () => KeyHeld());
        Add("espada", "Una espada: cógela con el GRIP.\nDa tajos RÁPIDOS para hacer daño.\nEnfúndala soltándola junto a tu cadera IZQUIERDA.",
            () => Near(swords, 2.5f));
        Add("aranas", "¡Arañas! Lánzales cocos (coge uno, lanza y suelta el GRIP)\no dales tajos con la espada.\nUn coco encima de una araña la aplasta.",
            () => SpiderChasing());
        Add("barca", "Para subir a la barca: acerca una mano a un remo\ny aprieta el GRIP. Cogerás los dos remos.",
            () => boat != null && !boat.IsBoarded && Dist(boat.transform) < 3.5f);
        Add("remar", "Remar: GRIP apretado y tira de las manos hacia ti;\nsuéltalo para volver.   Un solo remo = girar.\nB: empujón atrás.   A / X: girar 90°.",
            () => boat != null && boat.IsBoarded);
        Add("bajar", "Para bajar de la barca: joystick DERECHO hacia delante,\napunta al muelle o a la cubierta y suelta.",
            () => boat != null && boat.IsBoarded && Done("remar") && Time.time - DoneTime("remar") > 25f);
        Add("escala", "Apunta a la escala con el rayo y aprieta el GRIP para ir a ella.\nTrepar: agarra con el GRIP y tira hacia ABAJO,\nalternando las manos.",
            () => Near(ladders, 6f));
        Add("cofre", "Para abrir el cofre: saca la llave de tu cadera derecha\ny toca el candado con ella... pero solo de noche.",
            () => Near(chests, 4f));
        Add("reloj", "Para que anochezca: coge el RELOJ DE ARENA con el GRIP\ny dale la vuelta.",
            () => Done("cofre") && Time.time - DoneTime("cofre") > 10f && !NightNow());
        Add("dano", "¡Te atacan! El velo rojo es el daño que recibes.\nAléjate o defiéndete.",
            () => damaged);
    }

    // ------------------------------------------------------------------ condiciones
    readonly Dictionary<string, float> doneAt = new Dictionary<string, float>();
    bool Done(string id) => doneAt.ContainsKey(id);
    float DoneTime(string id) => doneAt.TryGetValue(id, out float t) ? t : 0f;

    float Dist(Transform t)
    {
        Vector3 d = t.position - head.position;
        d.y = 0f;
        return d.magnitude;
    }

    bool Near<T>(T[] list, float r) where T : Component
    {
        if (list == null) return false;
        foreach (var c in list) if (c != null && c.gameObject.activeInHierarchy && Dist(c.transform) < r) return true;
        return false;
    }

    bool KeyOnGround(float r)
    {
        if (keys == null) return false;
        foreach (var k in keys)
        {
            if (k == null || !k.gameObject.activeInHierarchy || k.GetComponentInParent<ShakeablePalm>() != null) continue;   // aun en la palmera
            if (pocket != null && pocket.HasKey) continue;
            var g = k.GetComponent<XRGrabInteractable>();
            if (g != null && g.isSelected) continue;
            if (Vector3.Distance(k.transform.position, head.position) < r) return true;
        }
        return false;
    }

    bool KeyHeld()
    {
        if (keys == null) return false;
        foreach (var k in keys)
        {
            var g = k != null ? k.GetComponent<XRGrabInteractable>() : null;
            if (g != null && g.isSelected) return true;
        }
        return false;
    }

    bool SpiderChasing()
    {
        foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude))
        {
            if (e.Current != EnemyAI.State.Chase && e.Current != EnemyAI.State.Attack) continue;
            var c = e.GetComponent<Collider>();
            if (c != null && c.bounds.size.y < 1f) return true;
        }
        return false;
    }

    bool NightNow()
    {
        foreach (var c in chests) if (c != null && c.dayNight != null) return c.dayNight.IsNight;
        return false;
    }

    bool TotemTalking()
    {
        if (totems == null) return false;
        foreach (var t in totems) if (t != null && t.IsTalking) return true;
        return false;
    }

    void Add(string id, string text, System.Func<bool> when) => hints.Add(new Hint { id = id, text = text, when = when });

    // ------------------------------------------------------------------ mostrar
    void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = 0.3f;
            foreach (var h in hints)
            {
                if (h.done) continue;
                bool ok;
                try { ok = h.when(); } catch { ok = false; }
                if (ok) { h.done = true; doneAt[h.id] = Time.time; queue.Enqueue(h); }
            }
        }

        if (showTimer > 0f)
        {
            showTimer -= Time.deltaTime;
            if (showTimer <= 0f) { label.gameObject.SetActive(false); gap = 1f; }
        }
        else if ((gap -= Time.deltaTime) <= 0f && queue.Count > 0 && !TotemTalking())
        {
            var h = queue.Dequeue();
            label.text = h.text;
            label.gameObject.SetActive(true);
            showTimer = secondsPerHint;
            XRHandInput.HapticBoth(0.1f, 0.05f);
            if (debugLogs) Debug.Log("[PISTAS] " + h.text.Replace("\n", " "));
        }
    }

    // El texto sigue a la cabeza con suavidad (no pegado: marea menos)
    void LateUpdate()
    {
        if (label == null || !label.gameObject.activeSelf) return;
        Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (f.sqrMagnitude < 0.001f) f = head.up;
        Vector3 target = head.position + f.normalized * distance + Vector3.down * below;
        Transform t = label.transform;
        t.position = Vector3.Lerp(t.position, target, 1f - Mathf.Exp(-4f * Time.deltaTime));
        t.rotation = Quaternion.LookRotation(t.position - head.position);
    }

    void CreateLabel()
    {
        var go = new GameObject("Pista (texto)");
        go.transform.SetParent(transform, false);
        label = go.AddComponent<TextMeshPro>();
        label.fontSize = 0.45f;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(1.3f, 0.5f);
        label.color = new Color(1f, 0.95f, 0.75f);
        label.outlineWidth = 0.25f;
        label.outlineColor = Color.black;
        label.gameObject.SetActive(false);
        go.transform.position = head.position + head.forward * distance;
    }
}
