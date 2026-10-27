using UnityEngine;
using UnityEngine.Events;

/// La maldicion del barco fantasma. Mientras esta activa, los totems brillan y los esqueletos vigilan el cofre.
/// Se rompe al abrir el cofre bajo la luna (ChestLock con 'Breaks Curse'): los totems se apagan y los esqueletos
/// se desmoronan. Pon UNO en la escena (objeto vacio "Maldicion"); los demas lo encuentran con Curse.Instance.
public class Curse : MonoBehaviour
{
    public static Curse Instance { get; private set; }

    public UnityEvent onBroken;
    [Header("Sonido (opcional)")]
    public AudioSource audioSource;
    public AudioClip breakClip;
    public bool debugLogs = true;

    public bool Broken { get; private set; }

    /// Para suscribirse desde codigo (totems, esqueletos)
    public event System.Action Broke;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[MALDICION] Hay dos objetos Curse; se ignora '{name}'.");
            enabled = false;
            return;
        }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    [ContextMenu("Romper maldicion")]
    public void Break()
    {
        if (Broken) return;
        Broken = true;
        if (debugLogs) Debug.Log("[MALDICION] ¡La maldicion se ha roto! Los esqueletos descansan en paz.");
        if (audioSource != null && breakClip != null) audioSource.PlayOneShot(breakClip);
        XRHandInput.HapticBoth(1f, 0.5f);
        Broke?.Invoke();
        onBroken?.Invoke();
    }
}
