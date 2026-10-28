using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Palmera que se puede SACUDIR: agarra el tronco con el grip y mueve la mano con fuerza a los lados.
/// La palmera se balancea, el mando vibra y, al acumular suficiente "sacudida", se suelta un coco de la copa.
/// Montaje (lo hace solo el menu Isla Pirata > Cocos > Palmeras sacudibles):
///  - Raiz: XR Simple Interactable + este script.
///  - Hijo con el modelo de la palmera y su Capsule Collider (el tronco). Es lo que se balancea ('Sway Pivot').
///  - Hijo vacio 'Copa' en lo alto, dentro del modelo: ahi cuelgan los cocos.
public class ShakeablePalm : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Modelo de la palmera (su pivote debe estar en la base del tronco)")]
    public Transform swayPivot;
    [Tooltip("Punto de la copa donde cuelgan los cocos (hijo del modelo, para que se balanceen con el)")]
    public Transform crown;
    public Coconut coconutPrefab;
    [Tooltip("Main Camera (para que los cocos que caen puedan dar en la cabeza)")]
    public Transform playerHead;
    public Health playerHealth;

    [Header("Cocos")]
    public int coconutsInCrown = 4;
    [Tooltip("Separacion de los cocos alrededor del centro de la copa (m)")]
    public float crownSpread = 0.35f;
    [Tooltip("Segundos para que vuelvan a crecer los cocos (0 = nunca)")]
    public float regrowTime = 45f;
    [Tooltip("Segundos que dura un coco en el suelo antes de desaparecer")]
    public float droppedLifetime = 60f;

    [Header("Llave escondida (opcional)")]
    [Tooltip("La llave (con Rigidbody y XR Grab Interactable). Al empezar se cuelga en la copa y cae al sacudir")]
    public Transform hiddenKey;
    [Tooltip("La llave cae despues de soltar este numero de cocos (0 = con la primera sacudida buena)")]
    public int dropKeyAfterCoconuts = 2;
    [Tooltip("Sonido al soltarse la llave y al tocar el suelo. Si se deja vacio se genera un 'cling' metalico")]
    public AudioClip keyClip;
    [Range(0f, 1f)] public float keyVolume = 0.9f;

    [Header("Sacudida")]
    [Tooltip("Velocidad minima de la mano (m/s) para que cuente como sacudida")]
    public float minShakeSpeed = 0.8f;
    [Tooltip("Sacudida acumulada (m de movimiento rapido) para soltar un coco")]
    public float shakePerCoconut = 1.2f;
    [Tooltip("Distancia maxima de la mano al tronco para poder sacudirla (evita sacudirla con el rayo desde lejos)")]
    public float maxHandDistance = 1.2f;

    [Header("Balanceo")]
    public float maxSwayAngle = 6f;
    public float swayStrength = 4f;
    public float swayStiffness = 18f;
    public float swayDamping = 3f;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    XRSimpleInteractable interactable;
    Transform hand;
    XRNode handNode;
    Vector3 prevHand;
    bool holding;
    float shake, regrowTimer, logTimer;
    Vector2 sway, swayVel;              // inclinacion actual (grados) en X/Z y su velocidad
    Quaternion pivotRest;
    readonly List<Coconut> hanging = new List<Coconut>();
    int dropped;
    bool keyHanging;

    public int CoconutsLeft => hanging.Count;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null) Debug.LogWarning($"[PALMERA] '{name}' necesita un XR Simple Interactable para poder agarrarla.");
        if (swayPivot == null) swayPivot = transform;
        pivotRest = swayPivot.localRotation;
    }

    void OnEnable()
    {
        if (interactable == null) return;
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        if (interactable == null) return;
        interactable.selectEntered.RemoveListener(OnGrab);
        interactable.selectExited.RemoveListener(OnRelease);
    }

    void Start()
    {
        if (coconutPrefab == null) Debug.LogWarning($"[PALMERA] '{name}': falta 'Coconut Prefab'.");
        if (crown == null) Debug.LogWarning($"[PALMERA] '{name}': falta 'Crown' (la copa).");
        for (int i = 0; i < coconutsInCrown; i++) GrowCoconut();
        if (hiddenKey != null) HangKey();
    }

    /// Esconder una llave en esta palmera desde otro script (KeyItem lo hace si ninguna palmera la tiene)
    public void HideKey(Transform key)
    {
        if (key == null || keyHanging) return;
        hiddenKey = key;
        HangKey();
    }

    void HangKey()
    {
        if (crown == null) { Debug.LogWarning($"[PALMERA] '{name}' tiene la llave pero no tiene 'Crown'."); return; }
        hiddenKey.SetParent(crown, true);
        hiddenKey.position = crown.position + Vector3.down * 0.2f;
        SetBodyHanging(hiddenKey.gameObject, true);
        keyHanging = true;
        if (debugLogs) Debug.Log($"[PALMERA] La llave '{hiddenKey.name}' esta escondida en la copa de '{name}' (cae tras {dropKeyAfterCoconuts} cocos)");
    }

    void DropKey()
    {
        keyHanging = false;
        hiddenKey.SetParent(null, true);
        SetBodyHanging(hiddenKey.gameObject, false);
        var rb = hiddenKey.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.ProjectOnPlane(Random.onUnitSphere, Vector3.up).normalized * 0.5f;
        XRHandInput.Haptic(handNode, 1f, 0.3f);
        PlayCling(hiddenKey.position, 1f);
        StartCoroutine(ClingOnLanding(rb));
        if (debugLogs) Debug.Log($"[PALMERA] ¡Ha caido la llave de '{name}'! Recogela y llevala al cofre");
    }

    // Segundo "cling" (mas suave) cuando la llave deja de caer = ha tocado el suelo
    System.Collections.IEnumerator ClingOnLanding(Rigidbody rb)
    {
        if (rb == null) yield break;
        bool falling = false;
        for (float t = 0f; t < 6f; t += Time.deltaTime)
        {
            if (rb.linearVelocity.y < -1f) falling = true;
            else if (falling && rb.linearVelocity.y > -0.2f)
            {
                PlayCling(rb.position, 0.6f);
                if (debugLogs) Debug.Log($"[PALMERA] La llave ha llegado al suelo en {rb.position}");
                yield break;
            }
            yield return null;
        }
    }

    static AudioClip generatedCling;

    void PlayCling(Vector3 pos, float volume)
    {
        var clip = keyClip != null ? keyClip : (generatedCling != null ? generatedCling : (generatedCling = MakeCling()));
        var go = new GameObject("Sonido llave");
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = keyVolume * volume;
        src.spatialBlend = 0.6f;   // se oye de donde cae, pero tambien desde lejos
        src.minDistance = 4f;
        src.maxDistance = 40f;
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }

    // "Cling" metalico: unos tonos agudos no armonicos que se apagan rapido (como una llave contra una piedra)
    static AudioClip MakeCling()
    {
        const int rate = 44100;
        float length = 0.7f;
        var data = new float[(int)(rate * length)];
        float[] freqs = { 2637f, 3951f, 5274f, 6890f };
        float[] amps = { 0.5f, 0.3f, 0.2f, 0.12f };
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)rate, s = 0f;
            for (int k = 0; k < freqs.Length; k++) s += amps[k] * Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * Mathf.Exp(-t * (6f + k * 3f));
            data[i] = s * Mathf.Clamp01(t * 2000f);   // ataque corto, sin chasquido
        }
        var clip = AudioClip.Create("Cling", data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnGrab(SelectEnterEventArgs a)
    {
        var t = a.interactorObject.transform;
        var col = GetComponentInChildren<Collider>();
        float dist = col != null ? Vector3.Distance(col.ClosestPoint(t.position), t.position) : 0f;
        if (dist > maxHandDistance)
        {
            if (debugLogs) Debug.Log($"[PALMERA] Agarrada desde {dist:F1} m: demasiado lejos, acercate al tronco");
            return;
        }
        hand = t;
        handNode = a.interactorObject.handedness == InteractorHandedness.Left ? XRNode.LeftHand : XRNode.RightHand;
        prevHand = hand.position;
        holding = true;
        XRHandInput.Haptic(handNode, 0.3f, 0.05f);
        if (debugLogs) Debug.Log($"[PALMERA] Tronco de '{name}' agarrado. Quedan {hanging.Count} cocos. Sacude la mano a los lados. " + KeyStatus());
    }

    void OnRelease(SelectExitEventArgs a)
    {
        if (a.interactorObject.transform != hand) return;
        holding = false;
        hand = null;
    }

    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector2 push = Vector2.zero;

        if (holding && hand != null)
        {
            Vector3 v = (hand.position - prevHand) / dt;
            prevHand = hand.position;
            v.y = 0f;   // solo cuenta la sacudida horizontal
            float speed = v.magnitude;

            if (speed > minShakeSpeed)
            {
                shake += speed * dt;
                // Empujar el tronco hacia un lado lo inclina hacia ese lado
                Vector3 local = swayPivot.parent != null ? swayPivot.parent.InverseTransformDirection(v) : v;
                push = new Vector2(local.z, -local.x) * swayStrength;
                XRHandInput.Haptic(handNode, Mathf.Clamp01(0.15f + speed * 0.12f), 0.03f);
            }

            if (shake >= shakePerCoconut)
            {
                shake = 0f;
                DropCoconut();
            }

            if (debugLogs)
            {
                logTimer += dt;
                if (logTimer > 0.5f) { logTimer = 0f; Debug.Log($"[PALMERA] mano {speed:F2} m/s | sacudida {shake:F2}/{shakePerCoconut:F2} | cocos {hanging.Count}" + (keyHanging ? " | ESTA TIENE LA LLAVE" : "")); }
            }
        }
        else shake = Mathf.MoveTowards(shake, 0f, dt * 0.5f);

        // Muelle amortiguado para el balanceo
        swayVel += (push - sway * swayStiffness - swayVel * swayDamping) * dt;
        sway += swayVel * dt;
        sway = Vector2.ClampMagnitude(sway, maxSwayAngle);
        swayPivot.localRotation = pivotRest * Quaternion.Euler(sway.x, 0f, sway.y);

        // Volver a crecer cocos
        if (regrowTime > 0f && hanging.Count < coconutsInCrown)
        {
            regrowTimer += dt;
            if (regrowTimer >= regrowTime) { regrowTimer = 0f; GrowCoconut(); }
        }
    }

    void GrowCoconut()
    {
        if (coconutPrefab == null || crown == null) return;
        float ang = Random.Range(0f, Mathf.PI * 2f);
        Vector3 pos = crown.position + new Vector3(Mathf.Cos(ang), Random.Range(-0.3f, 0f), Mathf.Sin(ang)) * crownSpread;

        var c = Instantiate(coconutPrefab, pos, Random.rotation);
        c.transform.SetParent(crown, true);   // conserva su tamano aunque la palmera este escalada
        c.lifetime = 0f;   // colgado no caduca (se pone antes de su Start)
        c.playerHead = playerHead;
        c.playerHealth = playerHealth;
        SetHanging(c, true);
        hanging.Add(c);
    }

    string KeyStatus()
    {
        if (keyHanging) return $"*** ESTA PALMERA ESCONDE LA LLAVE: cae tras {Mathf.Max(0, dropKeyAfterCoconuts - dropped)} coco(s) mas ***";
        if (hiddenKey != null) return "(la llave de esta palmera ya ha caido)";
        return "(aqui no esta la llave)";
    }

    [ContextMenu("Sacudir ahora (prueba sin casco)")]
    void ShakeFromInspector()
    {
        if (!Application.isPlaying) { Debug.Log("[PALMERA] Dale a Play primero."); return; }
        swayVel += new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * 40f;
        DropCoconut();
    }

    void DropCoconut()
    {
        hanging.RemoveAll(c => c == null);
        if (keyHanging && (dropped >= dropKeyAfterCoconuts || hanging.Count == 0))
        {
            DropKey();
            return;
        }
        if (hanging.Count == 0)
        {
            if (debugLogs) Debug.Log("[PALMERA] No quedan cocos en esta palmera");
            return;
        }
        dropped++;
        var c = hanging[hanging.Count - 1];
        hanging.RemoveAt(hanging.Count - 1);

        c.transform.SetParent(null, true);
        SetHanging(c, false);
        var rb = c.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 outward = Vector3.ProjectOnPlane(c.transform.position - crown.position, Vector3.up).normalized;
            rb.linearVelocity = outward * Random.Range(0.3f, 1f);
        }
        c.lifetime = droppedLifetime;
        c.StartLifetime();
        regrowTimer = 0f;

        XRHandInput.Haptic(handNode, 0.8f, 0.15f);
        if (debugLogs) Debug.Log($"[PALMERA] ¡Cae un coco! Quedan {hanging.Count}");
    }

    static void SetHanging(Coconut c, bool hang) => SetBodyHanging(c.gameObject, hang);

    static void SetBodyHanging(GameObject c, bool hang)
    {
        var rb = c.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = hang;
            rb.useGravity = !hang;
        }
        var grab = c.GetComponent<XRGrabInteractable>();
        if (grab != null) grab.enabled = !hang;   // colgado no se puede coger (habria que trepar)
    }
}
