using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Aviso grande de objetivos en un pergamino delante del jugador:
///  - al acabar la intro: "Tu objetivo: ...";
///  - al completar uno: "¡Objetivo cumplido! ..." y debajo el siguiente.
/// Aparece, se queda unos segundos y se desvanece; sigue la mirada con suavidad.
/// Lo crea solo el GameManager al empezar (casilla 'Objective Toasts').
public class ObjectiveToast : MonoBehaviour
{
    [Tooltip("Segundos que se ve cada aviso")]
    public float seconds = 4.5f;
    public float fade = 0.4f;
    [Tooltip("Distancia delante de los ojos (m)")]
    public float distance = 1.6f;
    [Tooltip("Cuanto por encima de la mirada (m)")]
    public float above = 0.15f;
    [Tooltip("Mostrar el primer objetivo al acabar la intro")]
    public bool showFirstObjective = true;
    public bool debugLogs = true;

    Transform head;
    RectTransform board;
    CanvasGroup group;
    TextMeshProUGUI title, body;
    readonly Queue<(string, string)> queue = new Queue<(string, string)>();
    bool showing;

    void Start()
    {
        if (Camera.main != null) head = Camera.main.transform;
        if (head == null || GameManager.Instance == null) { enabled = false; return; }
        Build();
        GameManager.Instance.onObjectiveCompleted.AddListener(OnCompleted);
        if (showFirstObjective) StartCoroutine(FirstObjective());
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.onObjectiveCompleted.RemoveListener(OnCompleted);
    }

    IEnumerator FirstObjective()
    {
        yield return null;
        while (GameIntro.Running) yield return null;
        yield return new WaitForSeconds(0.8f);
        var next = GameManager.Instance.CurrentObjective;
        if (next != null) Show("Tu objetivo", next.description);
    }

    void OnCompleted(string id)
    {
        var gm = GameManager.Instance;
        var o = gm.objectives.Find(x => x.id == id);
        var next = gm.CurrentObjective;
        if (next == null) return;   // era el ultimo: sale la pantalla de victoria
        string done = o != null ? $"<s>{o.description}</s>\n\n" : "";
        Show("¡Objetivo cumplido!", done + $"<b>Ahora:</b> {next.description}");
    }

    public void Show(string titleText, string bodyText)
    {
        queue.Enqueue((titleText, bodyText));
        if (debugLogs) Debug.Log($"[OBJETIVO] {titleText}: {bodyText.Replace("\n", " ")}");
        if (!showing) StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        showing = true;
        while (queue.Count > 0)
        {
            var (t, b) = queue.Dequeue();
            title.text = t;
            body.text = b;
            Place(true);
            board.gameObject.SetActive(true);
            XRHandInput.HapticBoth(0.3f, 0.08f);
            float total = seconds + fade * 2f;
            for (float k = 0f; k < total; k += Time.deltaTime)
            {
                group.alpha = Mathf.Clamp01(Mathf.Min(k / fade, (total - k) / fade));
                Place(false);
                yield return null;
            }
            board.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.3f);
        }
        showing = false;
    }

    // Sigue la mirada con retraso (pegado a la cara marea)
    void Place(bool snap)
    {
        Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (f.sqrMagnitude < 0.001f) f = head.up;
        Vector3 target = head.position + f.normalized * distance + Vector3.up * above;
        board.position = snap ? target : Vector3.Lerp(board.position, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
        board.rotation = Quaternion.LookRotation(board.position - head.position);
    }

    void Build()
    {
        var s = PirateStyle.Get();
        board = PirateStyle.MakePanel("Aviso de objetivo", transform, new Vector2(560, 230), 800f, out RectTransform content);
        group = board.GetComponent<CanvasGroup>();

        title = PirateStyle.MakeText(content, "Titulo", 46, true, s.gold, 30f);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0f, 0.62f);
        title.alignment = TextAlignmentOptions.Center;
        PirateStyle.MakeRule(content, 0.64f);

        body = PirateStyle.MakeText(content, "Texto", 26, false, s.paperText, 36f);
        body.rectTransform.anchorMax = new Vector2(1f, 0.6f);
        body.alignment = TextAlignmentOptions.Top;
        board.gameObject.SetActive(false);
    }
}
