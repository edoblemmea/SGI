using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Catalejo extensible para buscar el barco pirata.
/// - Con UNA mano: lo coges, lo mueves y lo giras (XR Grab Interactable).
/// - Con DOS manos: al SEPARARLAS los tubos salen (el catalejo se alarga) y aumenta el zoom; al juntarlas se recoge.
/// - En el ocular hay una lente que muestra lo que ve una camara pequena en la punta, con mas aumento cuanto mas
///   largo esta el catalejo. Acercate el ocular al ojo.
/// - Si el barco pirata ('Target') queda en el centro de la lente con suficiente zoom: "¡Barco a la vista!".
/// Montaje: menu "Isla Pirata > Telescopio" (crea Assets/Prefabs/Telescopio.prefab).
public class Telescope : MonoBehaviour
{
    [System.Serializable]
    public class Segment
    {
        [Tooltip("Las piezas de este tramo (una por cada LOD del modelo)")]
        public Transform[] parts;
        [Tooltip("Desplazamiento local de cada pieza con el catalejo totalmente extendido")]
        public Vector3 openOffset;
        [HideInInspector] public Vector3[] rest;
    }

    [Header("Tramos que se extienden")]
    public List<Segment> segments = new List<Segment>();
    [Range(0f, 1f)] public float extension = 0f;
    [Tooltip("Cuanto hay que separar las manos (m) para pasar de recogido a extendido del todo")]
    public float handTravel = 0.35f;

    [Header("Vista")]
    [Tooltip("Camara pequena en la punta del catalejo (renderiza a una textura)")]
    public Camera lensCamera;
    [Tooltip("Malla de la lente en el ocular (un Quad), con un material Unlit")]
    public Renderer lensRenderer;
    [Tooltip("Pieza que lleva el ocular (la lente la sigue)")]
    public Transform eyepiece;
    public float baseFov = 40f;
    public float minZoom = 1.5f;
    public float maxZoom = 8f;
    public int textureSize = 512;

    [Header("Buscar el barco")]
    [Tooltip("Lo que hay que encontrar (el barco pirata)")]
    public Transform target;
    [Tooltip("Zoom minimo para reconocerlo")]
    public float zoomToSpot = 3f;
    [Tooltip("Objetivo del GameManager al verlo (vacio = ninguno)")]
    public string objectiveOnSpot = "";
    public UnityEvent onTargetSpotted;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip extendClip;
    public AudioClip spottedClip;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public float Zoom => Mathf.Lerp(minZoom, maxZoom, extension);
    public bool Spotted { get; private set; }

    XRGrabInteractable grab;
    RenderTexture rt;
    Material lensMat;
    float startDistance, startExtension, lastStepExt, logTimer;
    bool twoHands;
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        if (grab == null) Debug.LogWarning($"[TELESCOPIO] '{name}' no tiene XR Grab Interactable.");
        foreach (var s in segments)
        {
            s.rest = new Vector3[s.parts.Length];
            for (int i = 0; i < s.parts.Length; i++)
                if (s.parts[i] != null) s.rest[i] = s.parts[i].localPosition;
        }
        if (lensRenderer != null) lensMat = lensRenderer.material;   // copia propia
        if (lensCamera != null)
        {
            lensCamera.stereoTargetEye = StereoTargetEyeMask.None;   // camara normal, no la del casco
            lensCamera.enabled = false;
        }
        ApplyExtension();
        SetLensOn(false);
    }

    void OnEnable()
    {
        if (grab == null) return;
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        if (grab == null) return;
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    void OnDestroy()
    {
        if (rt != null) rt.Release();
    }

    void Start()
    {
        if (lensCamera == null || lensRenderer == null) Debug.LogWarning($"[TELESCOPIO] '{name}': falta 'Lens Camera' o 'Lens Renderer': no se vera el zoom.");
        if (target == null) Debug.LogWarning($"[TELESCOPIO] '{name}': falta 'Target' (el barco pirata): no avisara al encontrarlo.");
        if (debugLogs) Debug.Log($"[TELESCOPIO] Listo. {segments.Count} tramos extensibles, zoom {minZoom}x - {maxZoom}x.");
    }

    void OnGrab(SelectEnterEventArgs a)
    {
        XRHandInput.Haptic(Node(a.interactorObject), 0.3f, 0.05f);
        SetLensOn(true);
        if (grab.interactorsSelecting.Count >= 2) BeginTwoHands();
        if (debugLogs) Debug.Log($"[TELESCOPIO] Cogido con {grab.interactorsSelecting.Count} mano(s)" +
                                 (grab.interactorsSelecting.Count == 1 ? ". Cogelo con la otra mano y separalas para alargarlo" : ""));
    }

    void OnRelease(SelectExitEventArgs a)
    {
        twoHands = grab.interactorsSelecting.Count >= 2;
        if (grab.interactorsSelecting.Count == 0) SetLensOn(false);
    }

    void BeginTwoHands()
    {
        twoHands = true;
        startDistance = HandDistance();
        startExtension = extension;
    }

    float HandDistance()
    {
        var s = grab.interactorsSelecting;
        return s.Count >= 2 ? Vector3.Distance(s[0].transform.position, s[1].transform.position) : 0f;
    }

    static XRNode Node(IXRInteractor i) =>
        i != null && i.handedness == InteractorHandedness.Left ? XRNode.LeftHand : XRNode.RightHand;

    void Update()
    {
        if (grab == null) return;

        // Dos manos: separar = alargar, juntar = recoger
        if (grab.interactorsSelecting.Count >= 2)
        {
            if (!twoHands) BeginTwoHands();
            float target01 = Mathf.Clamp01(startExtension + (HandDistance() - startDistance) / Mathf.Max(handTravel, 0.01f));
            if (!Mathf.Approximately(target01, extension))
            {
                extension = target01;
                ApplyExtension();
                // "clic" de vibracion cada 10 % de extension, en las dos manos
                if (Mathf.Abs(extension - lastStepExt) >= 0.1f)
                {
                    lastStepExt = extension;
                    foreach (var i in grab.interactorsSelecting) XRHandInput.Haptic(Node(i), 0.25f, 0.02f);
                    if (audioSource != null && extendClip != null) audioSource.PlayOneShot(extendClip, 0.5f);
                }
            }
        }
        else twoHands = false;

        if (lensCamera != null && lensCamera.enabled)
        {
            lensCamera.fieldOfView = baseFov / Zoom;
            CheckTarget();
        }

        if (debugLogs && grab.isSelected)
        {
            logTimer += Time.deltaTime;
            if (logTimer > 1f) { logTimer = 0f; Debug.Log($"[TELESCOPIO] extension {extension * 100f:F0}% | zoom {Zoom:F1}x | manos {grab.interactorsSelecting.Count}"); }
        }
    }

    void LateUpdate()
    {
        // La lente sigue al ocular (sin heredar escalas raras)
        if (lensRenderer != null && eyepiece != null)
            lensRenderer.transform.SetPositionAndRotation(eyepiece.position, eyepiece.rotation);
    }

    void ApplyExtension()
    {
        foreach (var s in segments)
        {
            if (s.parts == null || s.rest == null) continue;
            for (int i = 0; i < s.parts.Length; i++)
                if (s.parts[i] != null) s.parts[i].localPosition = s.rest[i] + s.openOffset * extension;
        }
    }

    void SetLensOn(bool on)
    {
        if (lensCamera != null)
        {
            if (on && rt == null)
            {
                rt = new RenderTexture(textureSize, textureSize, 16) { name = "VistaTelescopio" };
                lensCamera.targetTexture = rt;
                if (lensMat != null && lensMat.HasProperty(BaseMap)) lensMat.SetTexture(BaseMap, rt);
                else if (lensMat != null) lensMat.mainTexture = rt;
            }
            lensCamera.enabled = on;   // solo gasta rendimiento mientras lo tienes en la mano
        }
        if (lensMat != null && lensMat.HasProperty(BaseColor))
            lensMat.SetColor(BaseColor, on ? Color.white : new Color(0.05f, 0.08f, 0.1f));
    }

    void CheckTarget()
    {
        if (Spotted || target == null || Zoom < zoomToSpot) return;
        Vector3 to = target.position - lensCamera.transform.position;
        float angle = Vector3.Angle(lensCamera.transform.forward, to);
        if (angle > lensCamera.fieldOfView * 0.35f) return;

        Spotted = true;
        foreach (var i in grab.interactorsSelecting) XRHandInput.Haptic(Node(i), 0.9f, 0.3f);
        if (audioSource != null && spottedClip != null) audioSource.PlayOneShot(spottedClip);
        if (debugLogs) Debug.Log($"[TELESCOPIO] ¡Barco a la vista! '{target.name}' a {to.magnitude:F0} m (zoom {Zoom:F1}x)");
        if (!string.IsNullOrEmpty(objectiveOnSpot) && GameManager.Instance != null)
            GameManager.Instance.CompleteObjective(objectiveOnSpot);
        onTargetSpotted?.Invoke();
    }
}
