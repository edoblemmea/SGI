using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// Objetivos de la partida, victoria / derrota y reinicio.
/// Los demas scripts avisan con GameManager.Instance.CompleteObjective("id") (KeyItem, ChestLock, ObjectiveZone,
/// EnemyAI...). Cuando todos los objetivos estan completos se gana; si muere el jugador se pierde.
/// Pon UN solo GameManager en la escena (objeto vacio "GameManager").
public class GameManager : MonoBehaviour
{
    [System.Serializable]
    public class Objective
    {
        [Tooltip("Identificador que usan los otros scripts, p. ej. llave, cofre, isla, aranas")]
        public string id = "llave";
        public string description = "Encuentra la llave";
        [Min(1)] public int required = 1;
        [HideInInspector] public int progress;
        public bool Done => progress >= required;
    }

    public static GameManager Instance { get; private set; }

    [Header("Objetivos (en orden)")]
    public List<Objective> objectives = new List<Objective>
  {
    new Objective { id = "llave", description = "Sacude las palmeras y encuentra la llave" },
    new Objective { id = "barco", description = "Rema hasta el barco fantasma" },
    new Objective { id = "cofre", description = "Abre el cofre bajo la luna" },
  };

    [Header("Jugador")]
    [Tooltip("Health del XR Origin: si muere, se pierde la partida")]
    public Health playerHealth;

    [Header("Interfaz (opcional)")]
    [Tooltip("Texto TMP de un Canvas World Space (p. ej. en la muneca izquierda) con la lista de objetivos")]
    public TMP_Text objectivesText;
    [Tooltip("Panel de fin de partida (se activa al ganar o perder)")]
    public GameObject endPanel;
    public TMP_Text endText;
    public string winMessage = "¡Has encontrado el tesoro!\nPulsa A para jugar otra vez";
    public string loseMessage = "Has muerto\nPulsa A para reintentar";

    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip objectiveClip;
    public AudioClip winClip;
    public AudioClip loseClip;
    [Tooltip("Olas del mar de fondo (generadas por codigo) durante toda la partida")]
    public bool ambientWaves = true;
    [Range(0f, 1f)] public float wavesVolume = 0.25f;
    [Tooltip("Pistas de controles en contexto (TutorialHints): se crean solas al empezar")]
    public bool tutorialHints = true;

    [Header("Eventos")]
    public UnityEvent<string> onObjectiveCompleted;
    public UnityEvent onWin;
    public UnityEvent onLose;

    [Header("Diagnostico")]
    public bool debugLogs = true;

    public bool IsGameOver { get; private set; }
    public bool Won { get; private set; }

    float endTime;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[JUEGO] Hay DOS GameManager en la escena; se desactiva el de '" + name + "'.");
            enabled = false;
            return;
        }
        Instance = this;
        if (endPanel != null) endPanel.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable() { if (playerHealth != null) playerHealth.onDeath.AddListener(OnPlayerDeath); }
    void OnDisable() { if (playerHealth != null) playerHealth.onDeath.RemoveListener(OnPlayerDeath); }

    void Start()
    {
        if (debugLogs) Debug.Log($"[JUEGO] Partida empezada con {objectives.Count} objetivos. playerHealth={(playerHealth != null ? playerHealth.name : "NULL (no se podra perder)")}");
        RefreshUI();
        if (tutorialHints && FindAnyObjectByType<TutorialHints>() == null)
            new GameObject("Pistas de controles").AddComponent<TutorialHints>();
        if (ambientWaves)
        {
            var src = new GameObject("Olas (ambiente)").AddComponent<AudioSource>();
            src.transform.SetParent(transform, false);
            src.clip = ProceduralSounds.Waves();
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = wavesVolume;
            src.Play();
        }
    }

    void Update()
    {
        // Reiniciar con A o X un segundo despues de acabar
        if (IsGameOver && Time.time - endTime > 1f &&
      (XRHandInput.PrimaryButton(XRNode.RightHand) || XRHandInput.PrimaryButton(XRNode.LeftHand)))
            Restart();
    }

    public void CompleteObjective(string id)
    {
        if (IsGameOver) return;
        var o = objectives.Find(x => x.id == id);
        if (o == null)
        {
            if (debugLogs) Debug.LogWarning($"[JUEGO] Nadie tiene el objetivo '{id}' (revisa los ids en el GameManager)");
            return;
        }
        if (o.Done) return;

        o.progress++;
        if (debugLogs) Debug.Log($"[JUEGO] Objetivo '{id}' {o.progress}/{o.required}");
        if (o.Done)
        {
            Play(objectiveClip != null ? objectiveClip : ProceduralSounds.Chime(true));
            XRHandInput.HapticBoth(0.5f, 0.15f);
            onObjectiveCompleted?.Invoke(id);
        }
        RefreshUI();

        if (objectives.TrueForAll(x => x.Done)) Win();
    }

    public bool IsDone(string id)
    {
        var o = objectives.Find(x => x.id == id);
        return o != null && o.Done;
    }

    public void Win() => End(true);
    public void Lose() => End(false);

    void OnPlayerDeath() => Lose();

    void End(bool won)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        Won = won;
        endTime = Time.time;
        if (debugLogs) Debug.Log(won ? "[JUEGO] ===== VICTORIA =====" : "[JUEGO] ===== DERROTA =====");
        AudioClip endClip = won ? winClip : loseClip;
        Play(endClip != null ? endClip : ProceduralSounds.Chime(won));
        if (endPanel != null) endPanel.SetActive(true);
        if (endText != null) endText.text = won ? winMessage : loseMessage;
        (won ? onWin : onLose)?.Invoke();
    }

    public void Restart()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex < 0)
        {
            Debug.LogError($"[JUEGO] No puedo reiniciar: la escena '{scene.name}' no esta en File > Build Profiles > Scene List.");
            return;
        }
        if (debugLogs) Debug.Log("[JUEGO] Reiniciando escena");
        SceneManager.LoadScene(scene.buildIndex);
    }

    void RefreshUI()
    {
        if (objectivesText == null) return;
        var sb = new StringBuilder("<b>Objetivos</b>\n");
        foreach (var o in objectives)
        {
            sb.Append(o.Done ? "<color=#7CFC00>[x] " : "[ ] ").Append(o.description);
            if (o.required > 1) sb.Append($" ({o.progress}/{o.required})");
            if (o.Done) sb.Append("</color>");
            sb.Append('\n');
        }
        objectivesText.text = sb.ToString();
    }

    void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource != null) audioSource.PlayOneShot(clip);
        else if (Camera.main != null) ProceduralSounds.Play(clip, Camera.main.transform.position, 0.8f, 0f, 0f);
    }
}