using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [Tooltip("Boton del antiguo 'Viaje con la carta' (la carta se quito): se oculta al empezar")]
    public Button travelButton;
    public Button timeButton;
    public Button volumeDownButton;
    public Button volumeUpButton;
    public Button restartButton;
    public Button closeButton;
    public TMP_Text volumeText;

    [Header("Pestanas (las crea la herramienta)")]
    [Tooltip("Botones de las pestanas, en el mismo orden que 'Tab Pages' (Movimiento, Confort, Partida)")]
    public Button[] tabButtons;
    public GameObject[] tabPages;
    public Color tabNormal = new Color(0.62f, 0.47f, 0.3f);
    public Color tabActive = new Color(1f, 0.82f, 0.42f);

    [Header("Respuesta al pulsar")]
    [Tooltip("Sonido al pulsar un boton. Vacio = un clic generado por codigo")]
    public AudioClip clickClip;
    [Tooltip("Vibracion pequena del mando derecho al pasar el rayo por encima de un boton")]
    public bool hoverHaptics = true;

    [Header("Diagnostico")]
    [Tooltip("Al empezar, viste el menu como una carta nautica (azul noche, lineas doradas, pildoras) y muestra " +
             "todos los valores de cada opcion con el elegido en oro, aunque el menu se creara con un estilo anterior")]
    public bool applyStyle = true;
    public bool debugLogs = true;

    [Tooltip("Lo marca la herramienta: el menu se creo ya como carta nautica (filas con selector de valores 'Valores')")]
    [HideInInspector] public bool chartLayout;

    bool chartStyle;   // vestido de carta nautica: los botones muestran todos los valores
    string chartText = "#CDD6E4", chartSoft = "#CDD6E499", chartNavy = "#0A1424", goldHex = "#FFC752";
    bool smoothMove, smoothTurnOn, vignetteOn = true;
    int snapIndex = 1, speedIndex = 1, tab;
    float volume = 1f, restartTimer, refreshTimer;
    bool prevY;

    const string Prefix = "SGI.Menu2.";   // "2": se ignoran las opciones guardadas por la version anterior

    // Carta nautica: fondo azul noche con lineas doradas, pestanas en pildoras, filas con iconos en medallones.
    // Funciona sobre el menu que ya hay en la escena (no hace falta volver a crearlo con la herramienta).
    void ApplyStyle()
    {
        if (panel == null) return;
        var s = PirateStyle.Get();
        if (s.chartFrame == null || s.pill == null || s.row == null || s.medallion == null)
        {
            Debug.LogWarning("[MENU] Faltan las texturas de la carta nautica: pulsa Isla Pirata > Estilo pirata (fuera de Play). El menu sale sin vestir.");
            return;
        }
        chartStyle = true;
        chartText = "#" + ColorUtility.ToHtmlStringRGB(s.chartText);
        chartSoft = "#" + ColorUtility.ToHtmlStringRGB(s.chartText) + "99";
        chartNavy = "#" + ColorUtility.ToHtmlStringRGB(s.chartNavy);
        goldHex = "#" + ColorUtility.ToHtmlStringRGB(s.gold);
        if (chartLayout)
        {
            if (debugLogs) Debug.Log("[MENU] Menu de carta nautica (creado con la herramienta)");
            return;   // ya esta montado como en la plantilla
        }

        var bg = panel.GetComponent<Image>();
        if (bg != null) { bg.sprite = s.chartFrame; bg.type = Image.Type.Sliced; bg.color = Color.white; }
        if (s.chartLines != null && panel.transform.Find("Carta (lineas)") == null)
        {
            var lines = new GameObject("Carta (lineas)", typeof(RectTransform), typeof(Image));
            lines.layer = panel.layer;
            var lrt = (RectTransform)lines.transform;
            lrt.SetParent(panel.transform, false);
            lrt.SetAsFirstSibling();   // detras de todo
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var li = lines.GetComponent<Image>();
            li.sprite = s.chartLines;
            li.raycastTarget = false;
        }

        foreach (var b in panel.GetComponentsInChildren<Button>(true))
        {
            var img = b.GetComponent<Image>();
            if (img == null) continue;
            var t = b.GetComponentInChildren<TMP_Text>(true);
            bool isTab = tabButtons != null && System.Array.IndexOf(tabButtons, b) >= 0;
            bool small = ((RectTransform)b.transform).rect.width < 100f;
            var c = b.colors;
            if (b == closeButton)
            {
                img.sprite = s.medallion;
                img.type = Image.Type.Simple;
                img.color = s.gold;
            }
            else if (isTab || small)
            {
                img.sprite = s.pill;
                img.type = Image.Type.Sliced;
                img.color = new Color(s.gold.r, s.gold.g, s.gold.b, 0.6f);
                if (t != null) t.color = s.gold;
            }
            else
            {
                // Fila: casi invisible; se ilumina en oro al pasar el rayo
                img.sprite = s.row;
                img.type = Image.Type.Sliced;
                img.color = b == restartButton ? new Color(1f, 0.55f, 0.45f) : Color.white;
                c.normalColor = new Color(1f, 1f, 1f, 0f);
                c.highlightedColor = new Color(1f, 0.85f, 0.45f, 0.9f);
                c.pressedColor = new Color(1f, 0.8f, 0.4f, 1f);
                c.selectedColor = c.normalColor;
                c.disabledColor = new Color(1f, 1f, 1f, 0.02f);
                if (t != null) t.alignment = TextAlignmentOptions.Left;
            }
            b.colors = c;
        }

        foreach (var img in panel.GetComponentsInChildren<Image>(true))
        {
            if (img.name != "Icono") continue;
            img.color = s.gold;
            // Medallon de trazos detras de los iconos de las filas (no en el de cerrar, que ya es un medallon)
            var parent = img.transform.parent;
            if (parent == null || (closeButton != null && parent == closeButton.transform) || parent.Find("Medallon") != null) continue;
            var m = new GameObject("Medallon", typeof(RectTransform), typeof(Image));
            m.layer = img.gameObject.layer;
            var mrt = (RectTransform)m.transform;
            mrt.SetParent(parent, false);
            mrt.SetSiblingIndex(img.transform.GetSiblingIndex());
            var irt = (RectTransform)img.transform;
            mrt.anchorMin = irt.anchorMin;
            mrt.anchorMax = irt.anchorMax;
            mrt.anchoredPosition = irt.anchoredPosition;
            mrt.sizeDelta = irt.sizeDelta * 1.35f;
            irt.sizeDelta *= 0.8f;
            var mi = m.GetComponent<Image>();
            mi.sprite = s.medallion;
            mi.color = new Color(s.gold.r, s.gold.g, s.gold.b, 0.75f);
            mi.raycastTarget = false;
        }

        foreach (var t in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.GetComponentInParent<Button>() != null && t.color == s.gold) continue;   // pestanas y botones pequenos
            bool title = t.name == "Titulo" && t.transform.parent == panel.transform;
            t.color = title ? s.gold : s.chartText;
            if (!title) PirateStyle.ApplyFont(t, PirateStyle.FontRole.Body);
            if (t.name == "Ayuda") t.color = new Color(s.chartText.r, s.chartText.g, s.chartText.b, 0.6f);
        }
        if (tabButtons != null)
            foreach (var tb in tabButtons)
                if (tb != null) PirateStyle.ApplyFont(tb.GetComponentInChildren<TMP_Text>(true), PirateStyle.FontRole.Heading);
        tabNormal = new Color(s.gold.r, s.gold.g, s.gold.b, 0.35f);
        tabActive = s.gold;
        if (debugLogs) Debug.Log("[MENU] Menu vestido como carta nautica");
    }

    // "Nombre" pequeno encima y todos los valores en una linea; el elegido, en una pildora de oro con letra azul noche
    string Choices(string name, string[] values, int selected)
    {
        var sb = new System.Text.StringBuilder($"<size=78%><color={chartText}>{name}</color></size>\n");
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append("   ");
            if (i == selected) sb.Append($"<mark={goldHex}FF padding=\"8,8,2,2\"><color={chartNavy}><b>{values[i]}</b></color></mark>");
            else sb.Append($"<color={chartSoft}>{values[i]}</color>");
        }
        return sb.ToString();
    }

    void Start()
    {
        ReadSceneDefaults();   // sin opciones guardadas, el menu respeta lo que ya habia en el XR Origin
        Load();
        Bind(moveButton, () => { smoothMove = !smoothMove; Changed("Movimiento " + (smoothMove ? "continuo" : "teletransporte")); });
        Bind(turnButton, () => { smoothTurnOn = !smoothTurnOn; Changed("Giro " + (smoothTurnOn ? "suave" : "por saltos")); });
        Bind(snapButton, () => { snapIndex = (snapIndex + 1) % snapAngles.Length; Changed($"Salto de giro {snapAngles[snapIndex]:F0} grados"); });
        Bind(speedButton, () => { speedIndex = (speedIndex + 1) % moveSpeeds.Length; Changed($"Velocidad {moveSpeeds[speedIndex]:F1} m/s"); });
        Bind(vignetteButton, () => { vignetteOn = !vignetteOn; Changed("Vineta " + (vignetteOn ? "SI" : "NO")); });
        if (travelButton != null) travelButton.gameObject.SetActive(false);   // la carta se quito
        Bind(volumeDownButton, () => { volume = Mathf.Clamp01(volume - volumeStep); Changed($"Volumen {volume * 100f:F0} %"); });
        Bind(volumeUpButton, () => { volume = Mathf.Clamp01(volume + volumeStep); Changed($"Volumen {volume * 100f:F0} %"); });
        Bind(timeButton, ToggleTime);
        Bind(restartButton, Restart);
        Bind(closeButton, () => SetVisible(false));
        if (tabButtons != null)
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int k = i;
                Bind(tabButtons[i], () => ShowTab(k));
            }
        if (clickClip == null) clickClip = ProceduralSounds.Click();
        if (applyStyle) ApplyStyle();
        ShowTab(0);

        Apply();
        Refresh();
        SetVisible(startVisible);

        if (debugLogs)
            Debug.Log($"[MENU] Listo. Y = abrir/cerrar. Managers izq {Name(leftHandManager)} der {Name(rightHandManager)} | " +
                      $"move {Name(moveProvider)} | snap {Name(snapTurn)} | suave {Name(smoothTurn)} | vineta {Name(vignette)} | dia/noche {Name(dayNight)}");
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
        b.onClick.AddListener(() =>
        {
            XRHandInput.Haptic(XRNode.RightHand, 0.3f, 0.04f);
            if (clickClip != null) ProceduralSounds.Play(clickClip, b.transform.position, 0.6f, 0.5f, 0.05f);
        });
        if (hoverHaptics)
        {
            var trigger = b.GetComponent<EventTrigger>();
            if (trigger == null) trigger = b.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { if (b.interactable) XRHandInput.Haptic(XRNode.RightHand, 0.08f, 0.02f); });
            trigger.triggers.Add(enter);
        }
    }

    /// Ensena una pestana (0 = Movimiento, 1 = Confort, 2 = Partida)
    public void ShowTab(int i)
    {
        if (tabPages == null || tabPages.Length == 0) return;
        tab = Mathf.Clamp(i, 0, tabPages.Length - 1);
        for (int k = 0; k < tabPages.Length; k++)
        {
            if (tabPages[k] != null) tabPages[k].SetActive(k == tab);
            if (tabButtons != null && k < tabButtons.Length && tabButtons[k] != null)
            {
                tabButtons[k].GetComponent<Image>().color = k == tab ? tabActive : tabNormal;
                var tt = tabButtons[k].GetComponentInChildren<TMP_Text>(true);
                if (chartStyle && tt != null) tt.color = k == tab ? tabActive : new Color(0.80f, 0.84f, 0.89f, 0.6f);
                var ti = chartLayout ? tabButtons[k].transform.Find("Contenido/Icono") : null;
                if (ti != null) ti.GetComponent<Image>().color = k == tab ? tabActive : new Color(0.80f, 0.84f, 0.89f, 0.6f);
            }
        }
        restartTimer = 0f;
        Refresh();
    }

    void Changed(string what)
    {
        Apply();
        Save();
        Refresh();
        if (debugLogs) Debug.Log("[MENU] " + what);
    }

    // Valores de partida = como esta configurado el XR Origin en la escena (el rig de XRI anda con el joystick izquierdo)
    void ReadSceneDefaults()
    {
        if (leftHandManager != null) smoothMove = leftHandManager.smoothMotionEnabled;
        if (rightHandManager != null) smoothTurnOn = rightHandManager.smoothTurnEnabled;
        if (snapTurn != null) snapIndex = Closest(snapAngles, snapTurn.turnAmount);
        if (moveProvider != null) speedIndex = Closest(moveSpeeds, moveProvider.moveSpeed);
        if (vignette != null) vignetteOn = vignette.gameObject.activeSelf;
        volume = AudioListener.volume;
        if (debugLogs) Debug.Log($"[MENU] Valores de la escena: movimiento {(smoothMove ? "continuo" : "teletransporte")}, giro {(smoothTurnOn ? "suave" : "por saltos")}");
    }

    static int Closest(float[] values, float v)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++) if (Mathf.Abs(values[i] - v) < Mathf.Abs(values[best] - v)) best = i;
        return best;
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
        if (chartStyle) { RefreshChart(); return; }
        Label(moveButton, "Movimiento\n<b>" + (smoothMove ? "Continuo" : "Teletransporte") + "</b>");
        Label(turnButton, "Giro\n<b>" + (smoothTurnOn ? "Suave" : "Por saltos") + "</b>");
        if (snapAngles.Length > 0) Label(snapButton, $"Salto de giro\n<b>{snapAngles[Mathf.Clamp(snapIndex, 0, snapAngles.Length - 1)]:F0} grados</b>");
        if (snapButton != null) snapButton.interactable = !smoothTurnOn;
        string speed = speedIndex < speedNames.Length ? speedNames[speedIndex] : $"{moveSpeeds[speedIndex]:F1} m/s";
        Label(speedButton, "Velocidad\n<b>" + speed + "</b>");
        Label(vignetteButton, "Viñeta de confort\n<b>" + (vignetteOn ? "Sí" : "No") + "</b>");
        if (volumeText != null) volumeText.text = $"Volumen {volume * 100f:F0} %";

        if (timeButton != null)
        {
            bool can = dayNight != null && allowTimeChange;
            timeButton.interactable = can && !dayNight.IsTransitioning;
            Label(timeButton, !can ? "Hora\n<b>solo con el reloj de arena</b>"
                : dayNight.IsTransitioning ? "Hora\n<b>Cambiando...</b>"
                : dayNight.IsNight ? "Hora: Noche\n<b>pulsa = hacer de día</b>" : "Hora: Día\n<b>pulsa = hacer de noche</b>");
        }
        if (restartTimer <= 0f) Label(restartButton, "Reiniciar partida");
    }

    // Carta nautica: cada opcion muestra todos sus valores (como el telegrafo de maquinas de un barco)
    void RefreshChart()
    {
        Option(moveButton, "Movimiento", new[] { "Teletransporte", "Continuo" }, smoothMove ? 1 : 0);
        Option(turnButton, "Giro", new[] { "Por saltos", "Suave" }, smoothTurnOn ? 1 : 0);
        if (snapAngles.Length > 0)
        {
            var angles = new string[snapAngles.Length];
            for (int i = 0; i < angles.Length; i++) angles[i] = $"{snapAngles[i]:F0}°";
            Option(snapButton, smoothTurnOn ? "Salto de giro (solo con giro por saltos)" : "Salto de giro", angles, snapIndex, smoothTurnOn);
        }
        if (snapButton != null) snapButton.interactable = !smoothTurnOn;
        var speeds = new string[moveSpeeds.Length];
        for (int i = 0; i < speeds.Length; i++) speeds[i] = i < speedNames.Length ? speedNames[i].Replace("Rapida", "Rápida") : $"{moveSpeeds[i]:F1} m/s";
        Option(speedButton, "Velocidad", speeds, speedIndex);
        Option(vignetteButton, "Viñeta de confort", new[] { "Sí", "No" }, vignetteOn ? 0 : 1);
        if (volumeText != null) volumeText.text = chartLayout ? $"{volume * 100f:F0} %" : $"Volumen <b>{volume * 100f:F0} %</b>";

        if (timeButton != null)
        {
            bool can = dayNight != null && allowTimeChange;
            timeButton.interactable = can && !dayNight.IsTransitioning;
            if (!can) Option(timeButton, "Hora", new[] { "solo con el reloj de arena" }, -1, true);
            else if (dayNight.IsTransitioning) Option(timeButton, "Hora", new[] { "Cambiando..." }, 0);
            else Option(timeButton, "Hora", new[] { "Día", "Noche" }, dayNight.IsNight ? 1 : 0);
        }
        if (restartTimer <= 0f) Option(restartButton, "Partida", new[] { "Reiniciar partida" }, -1);
    }

    // Menu creado como carta nautica: nombre arriba y el selector "Valores" (el elegido relleno de oro).
    // Menu antiguo, sin selector: todo en el texto del boton.
    void Option(Button b, string name, string[] values, int selected, bool dim = false)
    {
        if (b == null) return;
        if (SetValues(b, values, selected, dim))
        {
            var n = b.transform.Find("Nombre");
            if (n != null) n.GetComponent<TMP_Text>().text = name;
        }
        else Label(b, Choices(name, values, selected));
    }

    bool SetValues(Button b, string[] values, int selected, bool dim)
    {
        var box = b.transform.Find("Valores");
        if (box == null) return false;
        var s = PirateStyle.Get();
        int n = Mathf.Min(values.Length, box.childCount);
        for (int i = 0; i < box.childCount; i++)
        {
            var seg = box.GetChild(i);
            seg.gameObject.SetActive(i < n);
            if (i >= n) continue;
            bool on = i == selected;
            var img = seg.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = s.Segment(i, n, on);
                img.color = new Color(s.gold.r, s.gold.g, s.gold.b, on ? (dim ? 0.45f : 1f) : (dim ? 0.15f : 0.35f));
            }
            var t = seg.GetComponentInChildren<TMP_Text>(true);
            if (t != null)
            {
                t.text = values[i];
                t.color = on ? s.chartNavy : new Color(s.chartText.r, s.chartText.g, s.chartText.b, dim ? 0.3f : 0.6f);
                t.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }
        }
        return true;
    }

    void Label(Button b, string s)
    {
        if (b == null) return;
        if (SetValues(b, new[] { s.Replace("\n", " ") }, 0, false)) return;   // carta nautica: el aviso en una pildora de oro
        var t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.text = s.Replace("\n", t.rectTransform.rect.height > 50f ? "\n" : ": ");   // botones bajos (menu antiguo): en una linea
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
        foreach (var k in new[] { "smoothMove", "smoothTurn", "vignette", "snap", "speed", "volume" }) PlayerPrefs.DeleteKey(Prefix + k);
        Debug.Log("[MENU] Opciones guardadas borradas");
    }
}
