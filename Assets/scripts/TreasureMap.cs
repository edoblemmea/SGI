using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// La carta del capitan (pergamino con dos varillas). Reune varias tecnicas de los apuntes de 3DUI:
/// - Wayfinding: vista cenital de la isla con "estas aqui" (icono que gira con tu cabeza) y los lugares (MapMarker).
/// - Manipulacion: se coge por las varillas; con DOS manos, al separarlas la carta se AGRANDA (escalado) y al
///   juntarlas se encoge (XRGeneralGrabTransformer con Allow Two Handed Scaling).
/// - Navegacion target-based: apunta con el rayo a un lugar YA DESCUBIERTO y pulsa el gatillo -> viajas alli.
///   El barco fantasma no permite viaje rapido: hay que llegar remando.
/// - Objetivos y pistas: el texto de abajo lo rellena el GameManager (campo Objectives Text).
/// - Boton MENU del mando izquierdo: la carta vuelve delante de ti (no se pierde nunca).
/// Al soltarla se queda flotando donde la dejas (Rigidbody kinematic, sin gravedad).
/// Montaje: menu "Isla Pirata > Carta".
public class TreasureMap : MonoBehaviour
{
    [Header("Zona del mundo que cubre la imagen (XZ)")]
    public Vector2 worldMin;
    public Vector2 worldMax;

    [Header("Interfaz de la carta")]
    [Tooltip("Rect de la imagen de la isla dentro del Canvas de la carta")]
    public RectTransform mapArea;
    [Tooltip("Icono 'estas aqui' (hijo de Map Area)")]
    public RectTransform playerIcon;
    [Tooltip("Plantilla de icono de lugar: Button + Image + TMP de hijo (desactivada)")]
    public Button markerTemplate;
    [Tooltip("Texto para avisos ('Has descubierto...', 'Hay que remar...')")]
    public TMP_Text statusText;

    [Header("Jugador")]
    public Transform head;
    public Transform xrOrigin;
    public TeleportationProvider teleportation;
    [Tooltip("Si viajas estando en la barca, primero desembarcas")]
    public BoatRowing boat;

    [Header("Recuperar la carta")]
    public bool recallWithMenuButton = true;
    public float recallDistance = 0.45f;

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip discoverClip;
    public AudioClip travelClip;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    class Entry { public MapMarker marker; public Button button; public Image image; public TMP_Text text; }
    readonly List<Entry> entries = new List<Entry>();
    Rigidbody rb;
    XRGrabInteractable grab;
    bool prevMenu;
    float statusTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
    }

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (teleportation == null) Debug.LogWarning("[CARTA] Falta 'Teleportation' (TeleportationProvider del XR Origin): no habra viaje rapido.");
        if (markerTemplate != null) markerTemplate.gameObject.SetActive(false);

        foreach (var m in FindObjectsByType<MapMarker>(FindObjectsInactive.Exclude)) AddEntry(m);
        if (GameManager.Instance != null) GameManager.Instance.onObjectiveCompleted.AddListener(OnObjective);
        RefreshAll();
        if (debugLogs) Debug.Log($"[CARTA] Lista con {entries.Count} lugares. Menu del mando izquierdo = traer la carta.");
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.onObjectiveCompleted.RemoveListener(OnObjective);
    }

    void AddEntry(MapMarker m)
    {
        if (markerTemplate == null || mapArea == null) return;
        var b = Instantiate(markerTemplate, mapArea);
        b.name = "Lugar " + m.label;
        var e = new Entry { marker = m, button = b, image = b.GetComponent<Image>(), text = b.GetComponentInChildren<TMP_Text>(true) };
        b.onClick.AddListener(() => TravelTo(e.marker));
        ((RectTransform)b.transform).anchoredPosition = WorldToMap(m.transform.position);
        entries.Add(e);
    }

    void Update()
    {
        // "Estas aqui"
        if (playerIcon != null && head != null)
        {
            playerIcon.anchoredPosition = WorldToMap(head.position);
            Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (f.sqrMagnitude > 0.001f) playerIcon.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg);
        }

        // Descubrir lugares al acercarse
        if (head != null)
            foreach (var e in entries)
            {
                if (e.marker == null || e.marker.Discovered) continue;
                Vector3 d = e.marker.transform.position - head.position;
                d.y = 0f;
                if (d.magnitude < e.marker.discoverRadius) Discover(e);
            }

        // Boton menu: traer la carta
        if (recallWithMenuButton)
        {
            var dev = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool menu = dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool m) && m;
            if (menu && !prevMenu) Recall();
            prevMenu = menu;
        }

        if (statusText != null && statusTimer > 0f)
        {
            statusTimer -= Time.deltaTime;
            if (statusTimer <= 0f) statusText.text = "";
        }
    }

    Vector2 WorldToMap(Vector3 world)
    {
        if (mapArea == null) return Vector2.zero;
        Vector2 size = worldMax - worldMin;
        float u = size.x != 0f ? (world.x - worldMin.x) / size.x : 0.5f;
        float v = size.y != 0f ? (world.z - worldMin.y) / size.y : 0.5f;
        Rect r = mapArea.rect;
        return new Vector2((Mathf.Clamp01(u) - 0.5f) * r.width, (Mathf.Clamp01(v) - 0.5f) * r.height);
    }

    void OnObjective(string id)
    {
        bool changed = false;
        foreach (var e in entries)
            if (e.marker != null && e.marker.hiddenUntilObjective == id) { changed = true; Say($"Nuevo en la carta: {e.marker.label}"); }
        if (changed) { XRHandInput.HapticBoth(0.4f, 0.1f); RefreshAll(); }
    }

    bool Visible(MapMarker m) =>
        string.IsNullOrEmpty(m.hiddenUntilObjective) || (GameManager.Instance != null && GameManager.Instance.IsDone(m.hiddenUntilObjective));

    void Discover(Entry e)
    {
        e.marker.Discovered = true;
        if (debugLogs) Debug.Log($"[CARTA] Lugar descubierto: {e.marker.label}");
        Say($"Descubierto: {e.marker.label}" + (e.marker.allowFastTravel ? " (ya puedes viajar aqui)" : ""));
        if (audioSource != null && discoverClip != null) audioSource.PlayOneShot(discoverClip);
        RefreshAll();
    }

    void RefreshAll()
    {
        foreach (var e in entries)
        {
            if (e.marker == null) { e.button.gameObject.SetActive(false); continue; }
            bool visible = Visible(e.marker);
            e.button.gameObject.SetActive(visible);
            if (!visible) continue;
            bool canTravel = e.marker.Discovered && e.marker.allowFastTravel;
            e.button.interactable = canTravel;
            if (e.image != null) e.image.color = e.marker.Discovered ? e.marker.color : new Color(0.4f, 0.35f, 0.3f, 0.7f);
            if (e.text != null)
            {
                string name = e.marker.Discovered || !string.IsNullOrEmpty(e.marker.hiddenUntilObjective) ? e.marker.label : "?";
                string extra = !string.IsNullOrEmpty(e.marker.hint) ? $"\n<size=70%><i>{e.marker.hint}</i></size>" : "";
                e.text.text = name + extra;
            }
        }
    }

    /// Viaje rapido a un lugar descubierto (lo llama el boton del lugar en la carta)
    public void TravelTo(MapMarker m)
    {
        if (m == null) return;
        if (!m.allowFastTravel) { Say($"A {m.label} hay que llegar remando"); return; }
        if (!m.Discovered) { Say("Todavia no has estado ahi"); return; }
        if (teleportation == null) { Say("No se puede viajar (falta TeleportationProvider)"); return; }

        if (boat != null && boat.IsBoarded) boat.Disembark();
        var dest = m.Destination;
        bool ok = teleportation.QueueTeleportRequest(new TeleportRequest
        {
            destinationPosition = dest.position,
            destinationRotation = dest.rotation,
            matchOrientation = MatchOrientation.TargetUpAndForward,
            requestTime = Time.time,
        });
        if (debugLogs) Debug.Log($"[CARTA] Viaje rapido a {m.label}: {(ok ? "OK" : "rechazado por el TeleportationProvider")}");
        if (ok)
        {
            XRHandInput.HapticBoth(0.5f, 0.1f);
            if (audioSource != null && travelClip != null) audioSource.PlayOneShot(travelClip);
            Recall(0.1f);   // la carta viaja contigo
        }
    }

    /// Pone la carta delante de la cabeza (si no la tienes cogida)
    public void Recall() => Recall(0f);

    void Recall(float delay)
    {
        if (grab != null && grab.isSelected) return;
        if (delay > 0f) { Invoke(nameof(RecallNow), delay); return; }
        RecallNow();
    }

    void RecallNow()
    {
        if (head == null) return;
        Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
        Vector3 pos = head.position + f * recallDistance + Vector3.down * 0.25f;
        Quaternion rot = Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(30f, 0f, 0f);   // inclinada hacia ti
        if (rb != null) { rb.position = pos; rb.rotation = rot; }
        transform.SetPositionAndRotation(pos, rot);
        if (debugLogs) Debug.Log("[CARTA] La carta vuelve contigo");
    }

    void Say(string msg)
    {
        if (debugLogs) Debug.Log("[CARTA] " + msg);
        if (statusText == null) return;
        statusText.text = msg;
        statusTimer = 4f;
    }
}
