using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;

/// Barra de vida que aparece al recibir dano y se desvanece al rato.
///  - Enemigos: flotando sobre la cabeza, siempre mirando al jugador.
///  - Jugador (Health en el XR Origin): pequena, abajo en la vista; se queda fija si queda poca vida.
/// Una barra blanca "de rastro" baja despacio detras de la de color, para que se vea cuanto ha quitado el golpe.
/// La anade sola Health (casilla 'Show Health Bar'), no hace falta montarla.
public class HealthBar : MonoBehaviour
{
    [Tooltip("Vacio = el Health de este objeto")]
    public Health health;
    [Tooltip("Barra de jugador (en la vista). Se marca sola si este objeto es el XR Origin")]
    public bool playerHud;

    [Header("Cuando se ve")]
    [Tooltip("Segundos que se ve tras cada golpe")]
    public float showSeconds = 3f;
    public float fadeSeconds = 0.5f;
    [Tooltip("Por debajo de esta fraccion de vida la barra no se esconde (0 = siempre se esconde)")]
    [Range(0f, 1f)] public float alwaysShowBelow = 0.3f;

    [Header("Aspecto")]
    [Tooltip("Ancho de la barra en metros (enemigos)")]
    public float width = 0.6f;
    [Tooltip("Metros por encima de la parte mas alta del modelo (enemigos)")]
    public float heightOffset = 0.25f;
    [Tooltip("Posicion en la vista del jugador (local de la camara)")]
    public Vector3 hudOffset = new Vector3(0f, -0.2f, 0.7f);
    public float hudWidth = 0.22f;
    public Color fullColor = new Color(0.3f, 0.85f, 0.3f);
    public Color lowColor = new Color(0.9f, 0.15f, 0.1f);

    public bool debugLogs = false;

    Transform head, bar;
    RectTransform fill, trail;
    Image fillImg;
    CanvasGroup group;
    EnemyAI ai;
    float shownUntil = -999f, trailValue = 1f, topY;

    void Start()
    {
        if (health == null) health = GetComponent<Health>();
        if (health == null) { enabled = false; return; }
        if (GetComponent<XROrigin>() != null) playerHud = true;
        ai = GetComponent<EnemyAI>();
        if (Camera.main != null) head = Camera.main.transform;
        if (playerHud && head == null) { Debug.LogWarning("[VIDA] Barra del jugador: no hay Main Camera."); enabled = false; return; }

        if (!playerHud)
        {
            // Altura de la cabeza respecto a la raiz, calculada una vez de pie
            Bounds b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            topY = b.max.y - transform.position.y + heightOffset;
        }
        Build();
        trailValue = health.Normalized;
        Refresh();
        group.alpha = 0f;
        health.onDamaged.AddListener(OnDamaged);
        if (debugLogs) Debug.Log($"[VIDA] Barra de vida de '{name}' ({(playerHud ? "jugador, en la vista" : $"enemigo, a {topY:F2} m")})");
    }

    void OnDestroy()
    {
        if (health != null) health.onDamaged.RemoveListener(OnDamaged);
        if (bar != null) Destroy(bar.gameObject);
    }

    void OnDamaged(float remaining) => shownUntil = Time.time + showSeconds;

    void Build()
    {
        var go = new GameObject($"Barra de vida ({name})", typeof(RectTransform));
        bar = go.transform;
        // Jugador: hija de la camara. Enemigo: suelta en la escena (asi no hereda escala ni giro del modelo)
        if (playerHud) { bar.SetParent(head, false); bar.localPosition = hudOffset; bar.localRotation = Quaternion.identity; }
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        group = go.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        var rt = (RectTransform)bar;
        rt.sizeDelta = new Vector2(100f, 10f);
        float w = playerHud ? hudWidth : width;
        bar.localScale = Vector3.one * (w / 100f);

        Image(rt, "Marco", new Color(0f, 0f, 0f, 0.75f), -1.5f);
        Image(rt, "Fondo", new Color(0.15f, 0.02f, 0.02f, 0.9f), 0f);
        trail = Image(rt, "Rastro", new Color(1f, 0.95f, 0.8f, 0.9f), 0f).rectTransform;
        fillImg = Image(rt, "Vida", fullColor, 0f);
        fill = fillImg.rectTransform;
    }

    static Image Image(RectTransform parent, string n, Color c, float pad)
    {
        var go = new GameObject(n, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
        var img = go.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    void LateUpdate()
    {
        if (bar == null || head == null) return;
        // Muerto, o IA apagada (SkeletonCrumble la apaga al desmoronarse; el nido, mientras la arana sale de la roca)
        bool gone = health.IsDead || !health.enabled || (ai != null && !ai.enabled);
        if (gone) { group.alpha = 0f; return; }

        float n = health.Normalized;
        bool hitOnce = shownUntil > 0f;
        bool visible = Time.time < shownUntil || (hitOnce && n < alwaysShowBelow);
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
        if (group.alpha <= 0f) return;

        trailValue = Mathf.MoveTowards(trailValue, n, Time.deltaTime * 0.5f);
        if (trailValue < n) trailValue = n;
        Refresh();

        if (!playerHud)
        {
            bar.position = transform.position + Vector3.up * topY;
            Vector3 toHead = bar.position - head.position;
            if (toHead.sqrMagnitude > 0.0001f) bar.rotation = Quaternion.LookRotation(toHead, Vector3.up);
        }
    }

    void Refresh()
    {
        float n = health.Normalized;
        fill.anchorMax = new Vector2(n, 1f);
        trail.anchorMax = new Vector2(trailValue, 1f);
        fillImg.color = Color.Lerp(lowColor, fullColor, Mathf.InverseLerp(0.2f, 0.7f, n));
    }
}
