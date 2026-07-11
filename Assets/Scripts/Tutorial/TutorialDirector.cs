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

            steps = TutorialSequence.Build(this);

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
            return step.ExpectedCard != null &&
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
            else if (step?.ExpectedCard != null &&
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

            CurrentStep?.OnExit?.Invoke();
            stepIndex = index;

            TutorialStep step = CurrentStep;
            if (step == null)
            {
                Debug.Log("[Tutorial] Sequence finished.");
                SequenceFinished?.Invoke();
                return;
            }

            Debug.Log($"[Tutorial] Step {stepIndex + 1}/{steps.Count}: {step.Id}");
            step.OnEnter?.Invoke();
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
