using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

// Center-screen announcement banner ("Player's Turn", "Draw a Card", "Fight!", ...).
// Each banner punches in with a configurable sound, holds fully visible for a
// configurable time, then fades out. Every Announce* call returns a Task that
// completes only once the banner has finished hiding, so the game flow can
// `await` it to gate input — e.g. a turn waits on AnnouncePlayerTurn before the
// player is allowed to act.
//
// The component is a self-bootstrapping singleton: drop it on a RectTransform
// under a Canvas and it builds its own label/CanvasGroup if none are assigned.
// Assign your own styled references in the Inspector to override the defaults.
public class Announcer : MonoBehaviour
{
    public static Announcer Instance { get; private set; }

    // One configurable announcement: its message, sound and how long it lingers.
    [Serializable]
    public struct Style
    {
        [Tooltip("Text to display. Use {0} as a placeholder (e.g. the player name).")]
        public string message;
        [Tooltip("Sound played as the banner punches in. Leave empty for silent.")]
        public AudioClip sound;
        [Tooltip("Seconds to stay fully visible. 0 = use Default Hold Seconds.")]
        public float holdSeconds;
    }

    [Header("References (auto-created if left empty)")]
    [SerializeField] private CanvasGroup canvasGroup; // drives the fade
    [SerializeField] private RectTransform panel;      // the transform that punches in
    [SerializeField] private TMP_Text label;

    [Header("Animation")]
    [Tooltip("How long the punch-in takes, in seconds.")]
    [SerializeField] private float punchInDuration = 0.35f;
    [Tooltip("Overshoot strength of the punch; higher = more pronounced bounce before settling.")]
    [SerializeField] private float punchOvershoot = 1.70158f;
    [Tooltip("How long the fade-out takes, in seconds.")]
    [SerializeField] private float fadeOutDuration = 0.25f;
    [Tooltip("Default time a banner stays up when a Style leaves Hold Seconds at 0.")]
    [SerializeField] private float defaultHoldSeconds = 1.5f;

    [Header("Messages")]
    public Style playerTurn = new Style { message = "{0}'s Turn" };
    public Style drawCard = new Style { message = "Draw a Card" };
    public Style discardCard = new Style { message = "Discard a Card" };
    public Style returnCard = new Style { message = "Return a Card to Hand" };
    public Style fight = new Style { message = "Fight!" };

    // Announcements are chained so overlapping requests queue instead of colliding.
    private Task tail = Task.CompletedTask;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        EnsureUi();
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // --- Public API ----------------------------------------------------------

    public Task AnnouncePlayerTurn(string playerName) => Announce(Format(playerTurn.message, playerName), playerTurn);
    public Task AnnounceDrawCard() => Announce(drawCard.message, drawCard);
    public Task AnnounceDiscardCard() => Announce(discardCard.message, discardCard);
    public Task AnnounceReturnCard() => Announce(returnCard.message, returnCard);
    public Task AnnounceFight() => Announce(fight.message, fight);

    public Task Announce(string message, Style style)
        => Announce(message, style.sound, style.holdSeconds > 0f ? style.holdSeconds : defaultHoldSeconds);

    // Core entry point. Returns a Task that completes after the banner has fully
    // shown and hidden. Requests are serialized so two announcements never play
    // at once.
    public Task Announce(string message, AudioClip sound, float holdSeconds)
    {
        tail = RunAfter(tail, message, sound, holdSeconds);
        return tail;
    }

    private async Task RunAfter(Task previous, string message, AudioClip sound, float holdSeconds)
    {
        // A faulted/earlier banner must not block the next one.
        try { await previous; } catch { /* ignored */ }

        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(PlayRoutine(message, sound, holdSeconds, tcs));
        await tcs.Task;
    }

    // --- Animation -----------------------------------------------------------

    private IEnumerator PlayRoutine(string message, AudioClip sound, float holdSeconds,
        TaskCompletionSource<bool> tcs)
    {
        label.text = message;
        canvasGroup.alpha = 1f;
        if (sound != null) AudioManager.Instance?.PlaySound(sound);

        // Punch in: scale 0 -> 1 with an ease-out-back so it overshoots then settles.
        float t = 0f;
        while (t < punchInDuration)
        {
            float p = punchInDuration > 0f ? t / punchInDuration : 1f;
            panel.localScale = Vector3.one * EaseOutBack(p, punchOvershoot);
            t += Time.deltaTime;
            yield return null;
        }
        panel.localScale = Vector3.one;

        // Hold fully visible.
        float hold = 0f;
        while (hold < holdSeconds)
        {
            hold += Time.deltaTime;
            yield return null;
        }

        // Fade + shrink out.
        float f = 0f;
        while (f < fadeOutDuration)
        {
            float p = fadeOutDuration > 0f ? f / fadeOutDuration : 1f;
            canvasGroup.alpha = 1f - p;
            panel.localScale = Vector3.one * Mathf.Lerp(1f, 0.85f, p);
            f += Time.deltaTime;
            yield return null;
        }

        Hide();
        tcs.SetResult(true);
    }

    // Standard ease-out-back: rises from 0 to exactly 1 at p=1, overshooting
    // above 1 along the way. `overshoot` scales the size of that overshoot.
    private static float EaseOutBack(float p, float overshoot)
    {
        float c1 = overshoot;
        float c3 = c1 + 1f;
        float x = p - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private void Hide()
    {
        canvasGroup.alpha = 0f;
        panel.localScale = Vector3.zero;
    }

    private static string Format(string fmt, string arg)
    {
        if (string.IsNullOrEmpty(fmt)) return arg;
        try { return string.Format(fmt, arg); }
        catch { return fmt; }
    }

    // --- Setup ---------------------------------------------------------------

    // Wires up missing references so an empty GameObject "just works". The panel
    // is this RectTransform; a CanvasGroup and a centered label are created if
    // they weren't assigned in the Inspector.
    private void EnsureUi()
    {
        if (panel == null) panel = transform as RectTransform;

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false; // never eat clicks meant for the board

        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label == null) label = CreateLabel();
    }

    private TMP_Text CreateLabel()
    {
        var go = new GameObject("Label", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(panel, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(1200f, 300f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 120f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }
}
