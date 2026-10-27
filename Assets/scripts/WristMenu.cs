using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// Menu de la muneca (mando izquierdo): configuracion de la navegacion y control de la partida.
///  - Boton Y del mando izquierdo = abrir / cerrar. Se pulsa con el rayo del mando derecho y el gatillo.
///  - Movimiento: teletransporte o continuo (joystick izquierdo). El joystick derecho siempre teletransporta,
///    porque hace falta para subir y bajar de la barca.
///  - Giro por saltos o suave, grados del salto, velocidad al andar, vineta de confort.
///  - Viaje con la carta: teletransporte o deslizarse hasta el lugar.
///  - Hora del dia (dia <-> noche), volumen y reiniciar (pide confirmacion).
/// Las opciones se guardan en este dispositivo (PlayerPrefs). Lo monta "Isla Pirata > Menu muneca".
public class WristMenu : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Objeto con el Canvas que se muestra y se oculta (hijo de este)")]
    public GameObject panel;
    public bool startVisible = false;
    [Tooltip("Abrir / cerrar con el boton Y del mando izquierdo")]
    public bool toggleWithY = true;

    [Header("Locomocion (del XR Origin)")]
    [Tooltip("Controller Input Action Manager del Left Controller: su joystick es el que pasa a movimiento continuo")]
    public ControllerInputActionManager leftHandManager;
    public ControllerInputActionManager rightHandManager;
    public ContinuousMoveProvider moveProvider;
    public SnapTurnProvider snapTurn;
    public ContinuousTurnProvider smoothTurn;
    [Tooltip("Tunneling Vignette (hijo de Main Camera)")]
    public TunnelingVignetteController vignette;
    [Tooltip("La carta: su modo de viaje (teletransporte / deslizarse)")]
    public TreasureMap map;

    [Header("Control de la partida")]
    public DayNightCycle dayNight;
    [Tooltip("Si se desmarca, la hora solo cambia con el reloj de arena")]
    public bool allowTimeChange = true;
    public float timeTransitionSeconds = 4f;

    [Header("Opciones")]
    public float[] snapAngles = { 30f, 45f, 90f };
    public float[] moveSpeeds = { 1.5f, 2.5f, 3.5f };
    public string[] speedNames = { "Lenta", "Normal", "Rapida" };
    [Range(0.05f, 0.5f)] public float volumeStep = 0.1f;
    [Tooltip("Recordar las opciones entre partidas (en este dispositivo)")]
    public bool saveSettings = true;

    [Header("Botones (los crea la herramienta)")]
    public Button moveButton;
    public Button turnButton;
    public Button snapButton;
    public Button speedButton;
    public Button vignetteButton;
    public Button travelButton;
    public Button timeButton;
    public Button volumeDownButton;
    public Button volumeUpButton;
    public Button restartButton;
    public Button closeButton;
    public TMP_Text volumeText;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    bool smoothMove, smoothTurnOn, vignetteOn = true, glideTravel;
    int snapIndex = 1, speedIndex = 1;
    float volume = 1f, restartTimer, refreshTimer;
    bool prevY;

    const string Prefix = "SGI.Menu.";

    void Start()
    {
        Load();
        Bind(moveButton, () => { smoothMove = !smoothMove; Changed("Movimiento " + (smoothMove ? "continuo" : "teletransporte")); });
        Bind(turnButton, () => { smoothTurnOn = !smoothTurnOn; Changed("Giro " + (smoothTurnOn ? "suave" : "por saltos")); });
        Bind(snapButton, () => { snapIndex = (snapIndex + 1) % snapAngles.Length; Changed($"Salto de giro {snapAngles[snapIndex]:F0} grados"); });
        Bind(speedButton, () => { speedIndex = (speedIndex + 1) % moveSpeeds.Length; Changed($"Velocidad {moveSpeeds[speedIndex]:F1} m/s"); });
        Bind(vignetteButton, () => { vignetteOn = !vignetteOn; Changed("Vineta " + (vignetteOn ? "SI" : "NO")); });
        Bind(travelButton, () => { glideTravel = !glideTravel; Changed("Viaje con la carta: " + (glideTravel ? "deslizarse" : "teletransporte")); });
        Bind(volumeDownButton, () => { volume = Mathf.Clamp01(volume - volumeStep); Changed($"Volumen {volume * 100f:F0} %"); });
        Bind(volumeUpButton, () => { volume = Mathf.Clamp01(volume + volumeStep); Changed($"Volumen {volume * 100f:F0} %"); });
        Bind(timeButton, ToggleTime);
        Bind(restartButton, Restart);
        Bind(closeButton, () => SetVisible(false));

        Apply();
        Refresh();
        SetVisible(startVisible);

        if (debugLogs)
            Debug.Log($"[MENU] Listo. Y = abrir/cerrar. Managers izq {Name(leftHandManager)} der {Name(rightHandManager)} | " +
                      $"carta {Name(map)} | move {Name(moveProvider)} | snap {Name(snapTurn)} | suave {Name(smoothTurn)} | vineta {Name(vignette)} | dia/noche {Name(dayNight)}");
        if (leftHandManager == null) Debug.LogWarning("[MENU] Falta 'Left Hand Manager': no se podra cambiar a movimiento continuo. Usa Isla Pirata > Menu muneca.");
    }

    static string Name(Object o) => o != null ? o.name : "FALTA";

    void Update()
    {
        if (toggleWithY && panel != null)
        {
            bool y = XRHandInput.SecondaryButton(XRNode.LeftHand);
            if (y && !prevY) SetVisible(!panel.activeSelf);
            prevY = y;
        }

        // Reiniciar pide confirmacion: si no se vuelve a pulsar en 3 s, se cancela
        if (restartTimer > 0f)
        {
            restartTimer -= Time.deltaTime;
            if (restartTimer <= 0f) Refresh();
        }

        // La hora puede cambiar por el reloj de arena: actualizo el texto de vez en cuando
        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f && panel != null && panel.activeSelf)
        {
            refreshTimer = 0.5f;
            Refresh();
        }
    }

    public void SetVisible(bool v)
    {
        if (panel == null) return;
        panel.SetActive(v);
        restartTimer = 0f;
        if (v) Refresh();
        XRHandInput.Haptic(XRNode.LeftHand, 0.2f, 0.04f);
        if (debugLogs) Debug.Log(v ? "[MENU] Abierto" : "[MENU] Cerrado");
    }

    void Bind(Button b, UnityEngine.Events.UnityAction a)
    {
        if (b == null) return;
        b.onClick.AddListener(a);
        b.onClick.AddListener(() => XRHandInput.Haptic(XRNode.RightHand, 0.25f, 0.03f));
    }

    void Changed(string what)
    {
        Apply();
        Save();
        Refresh();
        if (debugLogs) Debug.Log("[MENU] " + what);
    }

    // ------------------------------------------------------------------ aplicar las opciones
    void Apply()
    {
        // El joystick izquierdo cambia entre teletransporte y andar; el derecho sigue teletransportando (barca)
        if (leftHandManager != null) leftHandManager.smoothMotionEnabled = smoothMove;
        if (leftHandManager != null) leftHandManager.smoothTurnEnabled = smoothTurnOn;
        if (rightHandManager != null) rightHandManager.smoothTurnEnabled = smoothTurnOn;

        if (snapTurn != null && snapAngles.Length > 0) snapTurn.turnAmount = snapAngles[Mathf.Clamp(snapIndex, 0, snapAngles.Length - 1)];
        if (moveProvider != null && moveSpeeds.Length > 0) moveProvider.moveSpeed = moveSpeeds[Mathf.Clamp(speedIndex, 0, moveSpeeds.Length - 1)];
        if (vignette != null) vignette.gameObject.SetActive(vignetteOn);
        if (map != null) map.glideTravel = glideTravel;
        AudioListener.volume = volume;
    }

    void ToggleTime()
    {
        if (dayNight == null || !allowTimeChange || dayNight.IsTransitioning) return;
        dayNight.ToggleDayNight(timeTransitionSeconds);
        if (debugLogs) Debug.Log("[MENU] Cambio de hora: " + (dayNight.IsNight ? "amanece" : "anochece"));
        Refresh();
    }

    void Restart()
    {
        if (restartTimer <= 0f)
        {
            restartTimer = 3f;
            Label(restartButton, "¿Seguro? Pulsa otra vez");
            return;
        }
        if (GameManager.Instance != null) GameManager.Instance.Restart();
        else Debug.LogWarning("[MENU] No hay GameManager en la escena: no puedo reiniciar (Isla Pirata > Juego).");
    }

    // ------------------------------------------------------------------ textos
    void Refresh()
    {
        Label(moveButton, "Movimiento: " + (smoothMove ? "Continuo" : "Teletransporte"));
        Label(turnButton, "Giro: " + (smoothTurnOn ? "Suave" : "Por saltos"));
        if (snapAngles.Length > 0) Label(snapButton, $"Salto de giro: {snapAngles[Mathf.Clamp(snapIndex, 0, snapAngles.Length - 1)]:F0} grados");
        if (snapButton != null) snapButton.interactable = !smoothTurnOn;
        string speed = speedIndex < speedNames.Length ? speedNames[speedIndex] : $"{moveSpeeds[speedIndex]:F1} m/s";
        Label(speedButton, "Velocidad: " + speed);
        Label(vignetteButton, "Viñeta de confort: " + (vignetteOn ? "Sí" : "No"));
        Label(travelButton, "Viaje con la carta: " + (glideTravel ? "Deslizarse" : "Teletransporte"));
        if (travelButton != null) travelButton.interactable = map != null;
        if (volumeText != null) volumeText.text = $"Volumen {volume * 100f:F0} %";

        if (timeButton != null)
        {
            bool can = dayNight != null && allowTimeChange;
            timeButton.interactable = can && !dayNight.IsTransitioning;
            Label(timeButton, !can ? "Hora: (solo con el reloj de arena)"
                : dayNight.IsTransitioning ? "Cambiando la hora..."
                : dayNight.IsNight ? "Hora: Noche (pulsa = día)" : "Hora: Día (pulsa = noche)");
        }
        if (restartTimer <= 0f) Label(restartButton, "Reiniciar partida");
    }

    static void Label(Button b, string s)
    {
        if (b == null) return;
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = s;
    }

    // ------------------------------------------------------------------ guardar (solo en este dispositivo)
    void Load()
    {
        if (!saveSettings) return;
        try
        {
            smoothMove = PlayerPrefs.GetInt(Prefix + "smoothMove", smoothMove ? 1 : 0) == 1;
            smoothTurnOn = PlayerPrefs.GetInt(Prefix + "smoothTurn", smoothTurnOn ? 1 : 0) == 1;
            vignetteOn = PlayerPrefs.GetInt(Prefix + "vignette", vignetteOn ? 1 : 0) == 1;
            glideTravel = PlayerPrefs.GetInt(Prefix + "glide", map != null && map.glideTravel ? 1 : 0) == 1;
            snapIndex = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "snap", snapIndex), 0, Mathf.Max(0, snapAngles.Length - 1));
            speedIndex = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "speed", speedIndex), 0, Mathf.Max(0, moveSpeeds.Length - 1));
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volume", volume));
        }
        catch (System.Exception e) { Debug.LogWarning("[MENU] No pude leer las opciones guardadas: " + e.Message); }
    }

    void Save()
    {
        if (!saveSettings) return;
        try
        {
            PlayerPrefs.SetInt(Prefix + "smoothMove", smoothMove ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "smoothTurn", smoothTurnOn ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "vignette", vignetteOn ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "glide", glideTravel ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "snap", snapIndex);
            PlayerPrefs.SetInt(Prefix + "speed", speedIndex);
            PlayerPrefs.SetFloat(Prefix + "volume", volume);
            PlayerPrefs.Save();
        }
        catch (System.Exception e) { Debug.LogWarning("[MENU] No pude guardar las opciones: " + e.Message); }
    }

    [ContextMenu("Abrir / cerrar")]
    void ToggleFromInspector() => SetVisible(panel != null && !panel.activeSelf);

    [ContextMenu("Borrar opciones guardadas")]
    void ClearSaved()
    {
        foreach (var k in new[] { "smoothMove", "smoothTurn", "vignette", "glide", "snap", "speed", "volume" }) PlayerPrefs.DeleteKey(Prefix + k);
        Debug.Log("[MENU] Opciones guardadas borradas");
    }
}
