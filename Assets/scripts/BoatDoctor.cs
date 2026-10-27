using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Diagnostico completo de por que el barco no se mueve. NO usa mandos ni XR.
/// Ponlo en el MISMO objeto que el Rigidbody del barco (OldWoodenRowboat_Complete), dale a Play y mira la Console
/// (escribe "[DOCTOR]" en el buscador de la Console).
///
/// 1) Al empezar escribe un informe: fisica global, Rigidbody, padres/hijos con Rigidbody, scripts y Animators del asset,
///    joints (cuerdas/anclajes), colliders, que toca el casco y si el terreno esta por encima del fondo del casco.
/// 2) A los 'startDelay' segundos hace dos pruebas, FORZANDO la velocidad del Rigidbody hacia la proa:
///    A = con colisiones normales, B = ignorando todas las colisiones. Mide si se mueve el Rigidbody, si se mueve la malla
///    visible y si se mueve tu camara, y da un veredicto.
/// 3) Crea postes rojos fijos en el mundo para que VEAS pasar el barco (en el agua plana no hay referencia visual).
public class BoatDoctor : MonoBehaviour
{
    public Transform bowReference;      // BowRef (opcional; si no, se usa la flecha azul del propio objeto)
    public float startDelay = 3f;
    public float phaseSeconds = 3f;
    public float testSpeed = 2f;        // m/s que se fuerzan
    public bool runTest = true;
    public bool spawnMarkers = true;

    Rigidbody rb;
    Renderer[] renderers;
    Camera cam;
    readonly HashSet<string> contacts = new HashSet<string>();

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            // Si esta en un hijo de la barca, se copia al objeto del Rigidbody y se quita de aqui
            var parentRb = GetComponentInParent<Rigidbody>();
            if (parentRb != null && parentRb.GetComponent<BoatDoctor>() == null)
            {
                Debug.Log($"[DOCTOR] Estaba en '{name}' (sin Rigidbody): me paso a '{parentRb.name}', que es el que tiene el Rigidbody.");
                var copy = parentRb.gameObject.AddComponent<BoatDoctor>();
                copy.bowReference = bowReference;
                copy.startDelay = startDelay;
                copy.phaseSeconds = phaseSeconds;
                copy.testSpeed = testSpeed;
                copy.runTest = runTest;
                copy.spawnMarkers = spawnMarkers;
                Destroy(this);
                return;
            }
            Debug.LogError("[DOCTOR] Este objeto NO tiene Rigidbody. Pon el script en el objeto que lo tiene.");
            enabled = false;
            return;
        }
        cam = Camera.main;
        renderers = GetComponentsInChildren<Renderer>();
        Report();
        if (runTest) StartCoroutine(TestRoutine());
    }

    // ---------------------------------------------------------------- INFORME
    void Report()
    {
        // 1. Fisica global
        Debug.Log($"[DOCTOR] 1/7 FISICA GLOBAL: simulationMode={Physics.simulationMode} timeScale={Time.timeScale} fixedDelta={Time.fixedDeltaTime} gravedad={Physics.gravity}" +
                  (Physics.simulationMode != SimulationMode.FixedUpdate ? "  <-- PROBLEMA: la fisica no corre sola (Project Settings > Physics > Simulation Mode)" : "") +
                  (Time.timeScale == 0f ? "  <-- PROBLEMA: timeScale = 0" : ""));

        // 2. Rigidbody
        Debug.Log($"[DOCTOR] 2/7 RIGIDBODY '{Ruta(transform)}': kinematic={rb.isKinematic} gravedad={rb.useGravity} constraints={rb.constraints} masa={rb.mass} " +
                  $"linDamp={rb.linearDamping} angDamp={rb.angularDamping} interpolacion={rb.interpolation} detectCollisions={rb.detectCollisions} escala={transform.lossyScale}" +
                  (rb.isKinematic ? "  <-- PROBLEMA: es kinematic, las fuerzas no hacen nada" : "") +
                  (!rb.detectCollisions ? "  <-- detectCollisions desactivado" : ""));
        foreach (var r in GetComponentsInParent<Rigidbody>(true))
            if (r != rb) Debug.LogWarning($"[DOCTOR]   PADRE con Rigidbody: '{Ruta(r.transform)}' kinematic={r.isKinematic}  <-- el barco depende de este; mueve ESE o quita uno de los dos");
        foreach (var r in GetComponentsInChildren<Rigidbody>(true))
            if (r != rb) Debug.Log($"[DOCTOR]   hijo con Rigidbody: '{Ruta(r.transform)}' kinematic={r.isKinematic} gravedad={r.useGravity} (si no es kinematic puede pelearse con el casco)");
        var ab = GetComponentInParent<ArticulationBody>(true);
        if (ab != null) Debug.LogWarning($"[DOCTOR]   Hay un ArticulationBody en '{Ruta(ab.transform)}': no se mueve con AddForce de Rigidbody");

        // 3. Jerarquia
        Debug.Log($"[DOCTOR] 3/7 JERARQUIA: {Ruta(transform)}  | mallas visibles bajo este objeto: {renderers.Length}" +
                  (renderers.Length == 0 ? "  <-- PROBLEMA: la malla visible NO es hija de este Rigidbody; se mueve el objeto pero no lo que ves" : ""));
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.gameObject.isStatic)
                Debug.LogWarning($"[DOCTOR] 3/7 PROBLEMA: '{t.name}' esta marcado STATIC: con Static Batching su malla no se mueve aunque el Rigidbody si. " +
                                 "Usa Isla Pirata > Barca > 2. Preparar barca y remos (lo quita).");

        // 4. Scripts y animators
        var sb = new StringBuilder("[DOCTOR] 4/7 SCRIPTS del asset (en este objeto, hijos y padres):\n");
        var mbs = new List<MonoBehaviour>(GetComponentsInParent<MonoBehaviour>(true));
        foreach (var m in GetComponentsInChildren<MonoBehaviour>(true)) if (!mbs.Contains(m)) mbs.Add(m);
        int n = 0;
        foreach (var m in mbs)
        {
            if (m == null) { sb.AppendLine("  [SCRIPT FALTANTE (Missing)]"); continue; }
            if (m is BoatDoctor) continue;
            n++;
            sb.AppendLine($"  {(m.enabled ? "ON " : "off")} {m.GetType().FullName}   en '{Ruta(m.transform)}'");
        }
        if (n == 0) sb.AppendLine("  (ninguno)");
        Debug.Log(sb.ToString());
        foreach (var a in GetComponentsInParent<Animator>(true))
            Debug.LogWarning($"[DOCTOR]   ANIMATOR en '{Ruta(a.transform)}' enabled={a.enabled} controller={(a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "ninguno")} rootMotion={a.applyRootMotion}  <-- puede pisar la posicion cada frame");
        foreach (var a in GetComponentsInChildren<Animator>(true))
            if (a.transform != transform && !a.transform.IsChildOf(transform) == false)
                Debug.LogWarning($"[DOCTOR]   ANIMATOR en '{Ruta(a.transform)}' enabled={a.enabled} controller={(a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "ninguno")}  <-- si anima el objeto raiz, pisa la posicion");
        foreach (var a in GetComponentsInChildren<Animation>(true))
            Debug.LogWarning($"[DOCTOR]   ANIMATION (legacy) en '{Ruta(a.transform)}' enabled={a.enabled}");

        // 5. Joints
        int jc = 0;
        foreach (var j in FindObjectsByType<Joint>(FindObjectsSortMode.None))
        {
            bool mine = j.transform.IsChildOf(transform) || transform.IsChildOf(j.transform) || j.connectedBody == rb;
            if (!mine) continue;
            jc++;
            Debug.LogWarning($"[DOCTOR] 5/7 JOINT {j.GetType().Name} en '{Ruta(j.transform)}' conectado a: " +
                             (j.connectedBody != null ? $"'{Ruta(j.connectedBody.transform)}'" : "NADA = ANCLADO AL MUNDO  <-- esto clava el barco"));
        }
        if (jc == 0) Debug.Log("[DOCTOR] 5/7 JOINTS: ninguno relacionado con el barco (bien)");

        // 6. Colliders
        var cols = GetComponentsInChildren<Collider>(true);
        var layersUsed = new HashSet<int>();
        Bounds hull = new Bounds(transform.position, Vector3.zero);
        bool hullInit = false;
        int solid = 0;
        foreach (var c in cols)
        {
            string mat = c.sharedMaterial != null ? $"friccion {c.sharedMaterial.dynamicFriction:F2}" : "material por defecto (friccion 0.6)";
            Debug.Log($"[DOCTOR] 6/7 COLLIDER {c.GetType().Name} '{Ruta(c.transform)}' enabled={c.enabled} trigger={c.isTrigger} capa={LayerMask.LayerToName(c.gameObject.layer)} {mat} tamano={c.bounds.size}");
            if (c is CharacterController && c.enabled)
                Debug.LogWarning("[DOCTOR]   El CharacterController del XR Origin esta ACTIVO dentro del barco: forma parte del casco y choca con terreno/muelle. Debe estar desactivado a bordo.");
            if (c.enabled && !c.isTrigger && !(c is CharacterController))
            {
                solid++;
                layersUsed.Add(c.gameObject.layer);
                if (!hullInit) { hull = c.bounds; hullInit = true; } else hull.Encapsulate(c.bounds);
            }
        }
        if (solid == 0) Debug.LogWarning("[DOCTOR]   El barco NO tiene colliders solidos activos (todo trigger o desactivado).");
        var sbl = new StringBuilder($"[DOCTOR]   Capa del barco '{LayerMask.LayerToName(gameObject.layer)}' CHOCA con las capas: ");
        for (int i = 0; i < 32; i++)
            if (!Physics.GetIgnoreLayerCollision(gameObject.layer, i) && !string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                sbl.Append(LayerMask.LayerToName(i)).Append(", ");
        Debug.Log(sbl.ToString());

        // 7. Que toca el casco y terreno
        if (hullInit)
        {
            var hits = Physics.OverlapBox(hull.center, hull.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            var seen = new HashSet<string>();
            foreach (var h in hits)
                if (!h.transform.IsChildOf(transform) && seen.Add(h.name))
                    Debug.LogWarning($"[DOCTOR] 7/7 El volumen del casco se SOLAPA con '{Ruta(h.transform)}' (capa {LayerMask.LayerToName(h.gameObject.layer)})  <-- puede frenarlo/empujarlo");
            if (seen.Count == 0) Debug.Log("[DOCTOR] 7/7 El volumen del casco no se solapa con nada ajeno (bien)");

            var terr = Terrain.activeTerrain;
            if (terr != null)
            {
                float maxH = float.MinValue;
                for (int ix = 0; ix <= 4; ix++)
                    for (int iz = 0; iz <= 4; iz++)
                    {
                        var p = new Vector3(Mathf.Lerp(hull.min.x, hull.max.x, ix / 4f), 0f, Mathf.Lerp(hull.min.z, hull.max.z, iz / 4f));
                        maxH = Mathf.Max(maxH, terr.SampleHeight(p) + terr.transform.position.y);
                    }
                Debug.Log($"[DOCTOR] 7/7 TERRENO: fondo del casco y={hull.min.y:F2} | terreno mas alto bajo el casco y={maxH:F2}" +
                          (maxH > hull.min.y ? "  <-- EL TERRENO SUBE POR ENCIMA DEL FONDO DEL CASCO: el barco esta varado, tocando tierra" : "  (el terreno queda por debajo, bien)"));
            }
        }
    }

    // ---------------------------------------------------------------- PRUEBA
    IEnumerator TestRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        if (spawnMarkers) SpawnMarkers(startPos);

        Debug.Log("[DOCTOR] ===== PRUEBA: fuerzo la velocidad hacia la proa. Mira los postes rojos del agua ===== ");
        float a = 0f, b = 0f;
        float expected = testSpeed * phaseSeconds;

        yield return Phase("A (colisiones normales)", false, r => a = r);
        Restore(startPos, startRot);
        yield return new WaitForSeconds(0.5f);
        yield return Phase("B (IGNORANDO todas las colisiones)", true, r => b = r);
        Restore(startPos, startRot);

        bool okA = a > expected * 0.5f;
        bool okB = b > expected * 0.5f;
        string v;
        if (okA) v = "El barco SI se mueve con fisica normal. Si 'no lo ves', es porque viajas DENTRO (XR Origin hijo): mira los postes/muelle, no el barco. El problema entonces esta en como aplicamos la fuerza con los mandos.";
        else if (okB) v = "Con colisiones ignoradas SI avanza y con colisiones NO: algo lo FRENA (terreno, muelle, CharacterController, remos...). Mira los contactos listados arriba.";
        else v = "NO avanza ni ignorando colisiones: no es un choque. Sospecha de: joint anclado al mundo, Animator/script del asset que fija la posicion, Rigidbody kinematic, padre con otro Rigidbody, o fisica global parada. Revisa los avisos amarillos del informe.";
        Debug.Log($"[DOCTOR] ===== VEREDICTO ===== A avanzo {a:F2} m | B avanzo {b:F2} m | esperado {expected:F2} m\n{v}");
    }

    IEnumerator Phase(string label, bool ignore, System.Action<float> result)
    {
        var layers = new HashSet<int> { gameObject.layer };
        foreach (var c in GetComponentsInChildren<Collider>(true)) layers.Add(c.gameObject.layer);
        var prev = new Dictionary<long, bool>();
        if (ignore)
            foreach (int l in layers)
                for (int i = 0; i < 32; i++)
                {
                    prev[l * 100L + i] = Physics.GetIgnoreLayerCollision(l, i);
                    Physics.IgnoreLayerCollision(l, i, true);
                }

        contacts.Clear();
        Vector3 dir = Bow();
        Vector3 p0 = rb.position, m0 = VisualCenter(), c0 = cam != null ? cam.transform.position : Vector3.zero;
        float t = 0f, next = 0.5f;
        Debug.Log($"[DOCTOR] --- Fase {label} --- direccion {dir}");

        while (t < phaseSeconds)
        {
            if (!rb.isKinematic)
            {
                Vector3 v = rb.linearVelocity;
                rb.linearVelocity = new Vector3(dir.x * testSpeed, v.y, dir.z * testSpeed);
            }
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
            if (t >= next)
            {
                next += 0.5f;
                Debug.Log($"[DOCTOR]   t={t:F1}s vel_real={rb.linearVelocity.magnitude:F2} m/s | Rigidbody avanzo {(rb.position - p0).magnitude:F2} m | " +
                          $"malla visible avanzo {(VisualCenter() - m0).magnitude:F2} m | camara avanzo {(cam != null ? (cam.transform.position - c0).magnitude : 0f):F2} m");
            }
        }

        float moved = (rb.position - p0).magnitude;
        Debug.Log($"[DOCTOR]   Fase {label}: Rigidbody avanzo {moved:F2} m. Contactos: " + (contacts.Count == 0 ? "ninguno" : string.Join(", ", contacts)));

        if (ignore)
            foreach (var kv in prev)
                Physics.IgnoreLayerCollision((int)(kv.Key / 100), (int)(kv.Key % 100), kv.Value);

        result(moved);
    }

    void Restore(Vector3 pos, Quaternion rot)
    {
        if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        transform.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();
    }

    Vector3 Bow()
    {
        Transform r = bowReference != null ? bowReference : transform;
        Vector3 d = Vector3.ProjectOnPlane(r.forward, Vector3.up);
        return d.sqrMagnitude < 0.0001f ? transform.forward : d.normalized;
    }

    Vector3 VisualCenter()
    {
        if (renderers == null || renderers.Length == 0) return transform.position;
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) if (renderers[i] != null) b.Encapsulate(renderers[i].bounds);
        return b.center;
    }

    void SpawnMarkers(Vector3 origin)
    {
        Vector3 dir = Bow();
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        var root = new GameObject("[DOCTOR] postes").transform;
        for (int k = 1; k <= 6; k++)
            foreach (float side in new[] { -4f, 4f })
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root, true);
                g.transform.position = origin + dir * (k * 3f) + right * side + Vector3.up * 1.5f;
                g.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);
                g.GetComponent<Renderer>().material.color = Color.red;
            }
    }

    void OnCollisionEnter(Collision c) { contacts.Add($"{c.collider.name} ({LayerMask.LayerToName(c.collider.gameObject.layer)})"); }
    void OnCollisionStay(Collision c) { contacts.Add($"{c.collider.name} ({LayerMask.LayerToName(c.collider.gameObject.layer)})"); }

    static string Ruta(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}