using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameSystems;
using UnityEngine;

namespace Riftborn.Tutorial
{
    // Runs the ordered tutorial steps by observing GameManager's outside-driver
    // seams. Owns the play validator — an off-script play is rejected without
    // consuming the turn — and the trusted path the scripted enemy plays
    // through. Never reaches into gameplay code beyond those seams.
    //
    // Executes before GameManager.Awake so the validator and event
    // subscriptions are in place before the first turn can start.
    [DefaultExecutionOrder(-4000)]
    public class TutorialDirector : MonoBehaviour
    {
        [Tooltip("Board side the human plays on; the other side is the scripted enemy.")]
        public PlayerSide humanSide = PlayerSide.Left;

        [Header("Content")]
        [Tooltip("Inspector-authored steps. If unset, the director loads the asset named below from a Resources folder, then falls back to the built-in code sequence.")]
        [SerializeField] private TutorialSequenceAsset sequenceAsset;
        [Tooltip("Resources path loaded when no asset is wired above (Assets/Resources/TutorialSequence.asset → \"TutorialSequence\").")]
        [SerializeField] private string sequenceResourcePath = "TutorialSequence";

        public event Action<TutorialStep> StepEntered;
        public event Action SequenceFinished;

        // Player-facing explanation for a rejected play. The debug overlay
        // shows it now; NotificationView (M3) will pop it on the board.
        public event Action<string> PlayRejected;

        public TutorialStep CurrentStep =>
            steps != null && stepIndex >= 0 && stepIndex < steps.Count ? steps[stepIndex] : null;
        public int StepIndex => stepIndex;
        public int StepCount => steps?.Count ?? 0;
        public Player CurrentTurnPlayer { get; private set; }
        public string LastRejectionMessage { get; private set; } = "";

        private GameManager gm;
        private List<TutorialStep> steps;
        private int stepIndex = -1;
        private Coroutine holdRoutine;

        // Presentation layer (M3/M4) — optional: the director runs fine without
        // them (debug overlay only), each is null-checked before use.
        private NotificationView notificationView;
        private HighlightSystem highlightSystem;
        private TutorialCamera tutorialCamera;
        private Portal[] portals;
        private TutorialAnchor[] anchors;
        private readonly List<HighlightRequest> highlightRequests = new();
        private readonly List<DimHole> dimHoles = new();

        // One-shot bypass for the enemy queue: the next validation of exactly
        // this card is approved regardless of the current step.
        private string pendingScriptedCard;
        private bool lastValidatedPlayWasScripted;

        private void Awake()
        {
            gm = FindAnyObjectByType<GameManager>();
            if (gm == null)
            {
                Debug.LogError("[Tutorial] No GameManager in scene — director disabled.");
                enabled = false;
                return;
            }

            steps = ResolveSteps();

            notificationView = FindAnyObjectByType<NotificationView>();
            highlightSystem = FindAnyObjectByType<HighlightSystem>();
            tutorialCamera = FindAnyObjectByType<TutorialCamera>();

            gm.CardPlayValidator = ValidatePlay;
            gm.CardPlayRejected += OnCardPlayRejected;
            gm.TurnStarted += OnTurnStarted;
            gm.CardPlayedSuccessfully += OnCardPlayed;
            gm.CombatResolved += OnCombatResolved;
            gm.LaneWon += OnLaneWon;
            gm.GameOver += OnGameOver;
        }

        private void Start()
        {
            EnterStep(0);
        }

        // Steps come from, in order: a wired asset, a Resources asset at
        // sequenceResourcePath, else the built-in code sequence. The empty-list
        // guard means a half-authored asset never yields a blank tutorial.
        private List<TutorialStep> ResolveSteps()
        {
            TutorialSequenceAsset asset = sequenceAsset != null
                ? sequenceAsset
                : Resources.Load<TutorialSequenceAsset>(sequenceResourcePath);

            if (asset != null && asset.Steps != null && asset.Steps.Count > 0)
                return asset.Steps;

            return TutorialSequence.Build();
        }

        private void OnDestroy()
        {
            if (gm == null) return;
            gm.CardPlayValidator = null; // the director is the only installer in this scene
            gm.CardPlayRejected -= OnCardPlayRejected;
            gm.TurnStarted -= OnTurnStarted;
            gm.CardPlayedSuccessfully -= OnCardPlayed;
            gm.CombatResolved -= OnCombatResolved;
            gm.LaneWon -= OnLaneWon;
            gm.GameOver -= OnGameOver;
        }

        // ── Scripted enemy path ──────────────────────────────────────────────

        // Plays a card as the current active player, pre-approved past the
        // validator. Only the enemy queue calls this; the returned task
        // completes once the resulting combat chain has handed the turn over.
        public async Task<bool> PlayScriptedCard(string cardName)
        {
            pendingScriptedCard = cardName;
            try
            {
                return await gm.HandlePlayerPlayCard(cardName);
            }
            finally
            {
                pendingScriptedCard = null;
            }
        }

        // ── Validation ───────────────────────────────────────────────────────

        private bool ValidatePlay(string cardName)
        {
            lastValidatedPlayWasScripted = false;

            if (pendingScriptedCard != null &&
                string.Equals(pendingScriptedCard, cardName, StringComparison.OrdinalIgnoreCase))
            {
                pendingScriptedCard = null; // one-shot
                lastValidatedPlayWasScripted = true;
                return true;
            }

            // HandlePlayerPlayCard acts for whoever's turn it is, so a card the
            // human plays during the enemy's turn would be fielded FOR the
            // enemy. Only the scripted path above may act on enemy turns.
            if (CurrentTurnPlayer != null && CurrentTurnPlayer.playerSide != humanSide) return false;

            TutorialStep step = CurrentStep;
            if (step == null) return true; // sequence finished — free play
            if (step.AllowAnyCard) return true;
            return !string.IsNullOrEmpty(step.ExpectedCard) &&
                   string.Equals(step.ExpectedCard, cardName, StringComparison.OrdinalIgnoreCase);
        }

        private void OnCardPlayRejected(string cardName)
        {
            TutorialStep step = CurrentStep;
            string message;
            if (CurrentTurnPlayer != null && CurrentTurnPlayer.playerSide != humanSide)
            {
                message = "Hold on — it's not your turn yet.";
            }
            else if (!string.IsNullOrEmpty(step?.ExpectedCard) &&
                     !string.Equals(step.ExpectedCard, cardName, StringComparison.OrdinalIgnoreCase))
            {
                message = $"Not yet — play {step.ExpectedCard}.";
            }
            else
            {
                // The right card (or a free play) that still failed placement:
                // wrong resonance, full portal, item on an empty portal…
                message = $"{cardName} couldn't be played there.";
            }

            LastRejectionMessage = message;
            Debug.Log($"[Tutorial] Rejected '{cardName}': {message}");
            if (notificationView != null) notificationView.ShowToast(message);
            PlayRejected?.Invoke(message);
        }

        // ── Step flow ────────────────────────────────────────────────────────

        // Debug-overlay escape hatch; also useful for Manual steps.
        public void ForceAdvance()
        {
            AdvanceStep();
        }

        private void AdvanceStep()
        {
            if (steps == null || stepIndex >= steps.Count) return;
            EnterStep(stepIndex + 1);
        }

        private void EnterStep(int index)
        {
            if (holdRoutine != null)
            {
                StopCoroutine(holdRoutine);
                holdRoutine = null;
            }

            ApplyExitHooks(CurrentStep);
            stepIndex = index;

            TutorialStep step = CurrentStep;
            if (step == null)
            {
                Debug.Log("[Tutorial] Sequence finished.");
                if (notificationView != null) notificationView.Hide();
                if (highlightSystem != null) highlightSystem.Clear();
                if (tutorialCamera != null) tutorialCamera.FrameFullBoard();
                SequenceFinished?.Invoke();
                return;
            }

            Debug.Log($"[Tutorial] Step {stepIndex + 1}/{steps.Count}: {step.Id}");
            ApplyStepPresentation(step);
            ApplyEnterHooks(step);
            StepEntered?.Invoke(step);

            switch (step.Advance)
            {
                case StepAdvance.Auto:
                    AdvanceStep();
                    break;
                case StepAdvance.Hold:
                    holdRoutine = StartCoroutine(HoldThenAdvance(step.HoldSeconds));
                    break;
            }
        }

        private IEnumerator HoldThenAdvance(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            holdRoutine = null;
            AdvanceStep();
        }

        // Serializable stand-in for the old OnEnter/OnExit delegates. Only the
        // final step sets these (mark complete on enter, return to menu on exit).
        private static void ApplyEnterHooks(TutorialStep step)
        {
            if (step != null && (step.Hooks & StepHooks.MarkCompleteOnEnter) != 0)
                TutorialState.MarkComplete();
        }

        private static void ApplyExitHooks(TutorialStep step)
        {
            if (step != null && (step.Hooks & StepHooks.ReturnToMenuOnExit) != 0)
                TutorialLauncher.ReturnToMenu();
        }

        // ── Presentation (M3/M4) ─────────────────────────────────────────────

        private void ApplyStepPresentation(TutorialStep step)
        {
            if (tutorialCamera != null)
            {
                if (step.Camera == CameraShot.FullBoard) tutorialCamera.FrameFullBoard();
                else if (step.Camera == CameraShot.SingleLane) tutorialCamera.FrameLane(step.CameraLane);
                else if (step.Camera == CameraShot.Custom) tutorialCamera.MoveTo(step.CameraPosition, step.CameraOrthoSize);
            }

            if (notificationView != null)
            {
                if (!string.IsNullOrEmpty(step.Body)) notificationView.Show(step.Body);
                else notificationView.Hide();
            }

            if (highlightSystem != null)
            {
                BuildHighlightRequests(step);
                BuildDimHoles(step);
                if (highlightRequests.Count > 0 || dimHoles.Count > 0)
                    highlightSystem.Show(highlightRequests, step.DimBackground, dimHoles);
                else
                    highlightSystem.Clear();
            }
        }

        // Turns a step's authored highlights into resolved draw requests. Uses the
        // list when present, else the hidden legacy single Highlight (so pre-list
        // assets still work). Unresolvable targets are skipped.
        private void BuildHighlightRequests(TutorialStep step)
        {
            highlightRequests.Clear();

            List<HighlightTarget> list = step.Highlights;
            if (list != null && list.Count > 0)
            {
                foreach (HighlightTarget h in list) AddHighlightRequest(h);
            }
            else if (step.Highlight.Kind != HighlightKind.None)
            {
                AddHighlightRequest(step.Highlight);
            }
        }

        private void AddHighlightRequest(HighlightTarget h)
        {
            if (!TryResolveAnchor(h, out Transform anchor)) return;

            highlightRequests.Add(new HighlightRequest
            {
                Anchor = anchor,
                ShowRing = h.Parts != HighlightParts.ArrowOnly,
                ShowArrow = h.Parts != HighlightParts.RingOnly,
                ArrowClock = h.ArrowClock,
                WorldRadius = h.WorldRadius,
            });
        }

        // Resolves a step's DimZones into DimHole draw data. Portal
        // and Anchor zones reuse the same target resolution as highlights (so a zone
        // can sit on a lane portal or a named TutorialAnchor); ScreenRect zones pass
        // straight through. Unresolvable/degenerate zones are skipped.
        private void BuildDimHoles(TutorialStep step)
        {
            dimHoles.Clear();

            List<DimZone> zones = step.DimZones;
            if (zones == null) return;
            foreach (DimZone z in zones) AddDimHole(z);
        }

        private void AddDimHole(DimZone zone)
        {
            if (zone.Kind == DimZoneKind.ScreenRect)
            {
                if (zone.ScreenRect.width <= 0f || zone.ScreenRect.height <= 0f)
                {
                    Debug.LogWarning("[Tutorial] Dim zone (ScreenRect) has a zero-size rect — skipped.");
                    return;
                }
                dimHoles.Add(new DimHole { IsScreenRect = true, ScreenRect = zone.ScreenRect });
                return;
            }

            Transform anchor = zone.Kind == DimZoneKind.Portal
                ? ResolvePortal(zone.Side, zone.Lane)
                : ResolveAnchorId(zone.AnchorId);
            if (anchor == null) return; // resolver already logged why

            dimHoles.Add(new DimHole { Anchor = anchor, WorldHalf = zone.WorldSize });
        }

        private bool TryResolveAnchor(HighlightTarget highlight, out Transform anchor)
        {
            anchor = highlight.Kind switch
            {
                HighlightKind.Portal => ResolvePortal(highlight.Side, highlight.Lane),
                HighlightKind.Anchor => ResolveAnchorId(highlight.AnchorId),
                _ => null,
            };
            return anchor != null;
        }

        private Transform ResolvePortal(HighlightSide relativeSide, int lane)
        {
            // Relative side → concrete board side (You = the human's side).
            PlayerSide side = relativeSide == HighlightSide.You ? humanSide : Opposite(humanSide);

            portals ??= FindObjectsByType<Portal>(FindObjectsSortMode.None);
            foreach (Portal portal in portals)
            {
                if (portal.ownerSide == side && portal.laneIndex == lane)
                    return portal.transform;
            }

            Debug.LogWarning($"[Tutorial] No portal for {relativeSide} ({side}) lane {lane}.");
            return null;
        }

        private Transform ResolveAnchorId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[Tutorial] Highlight anchor has no id.");
                return null;
            }

            anchors ??= FindObjectsByType<TutorialAnchor>(FindObjectsSortMode.None);
            foreach (TutorialAnchor a in anchors)
            {
                if (a != null && string.Equals(a.id, id, StringComparison.OrdinalIgnoreCase))
                    return a.transform;
            }

            Debug.LogWarning($"[Tutorial] No TutorialAnchor with id '{id}' in the scene.");
            return null;
        }

        private static PlayerSide Opposite(PlayerSide side) =>
            side == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left;

        // ── Seam event handlers ──────────────────────────────────────────────

        private void OnTurnStarted(Player player)
        {
            CurrentTurnPlayer = player;
        }

        private void OnCardPlayed(string cardName)
        {
            if (lastValidatedPlayWasScripted) return;
            if (CurrentStep?.Advance == StepAdvance.CardPlayed) AdvanceStep();
        }

        private void OnCombatResolved()
        {
            if (CurrentStep?.Advance == StepAdvance.CombatResolved) AdvanceStep();
        }

        private void OnLaneWon(Lane lane)
        {
            if (CurrentStep?.Advance == StepAdvance.LaneWon) AdvanceStep();
        }

        private void OnGameOver(Player winner)
        {
            if (CurrentStep?.Advance == StepAdvance.GameOver) AdvanceStep();
        }
    }
}
