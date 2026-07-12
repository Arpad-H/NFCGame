using System.Collections.Generic;
using System.Threading.Tasks;
using GameSystems;
using UnityEngine;

namespace Riftborn.Tutorial
{
    // The scripted enemy: an ordered queue of card names, one played per enemy
    // turn through the director's pre-approved path. With an empty queue the
    // enemy skips its turn (existing minions still fight). The enemy is never
    // a connected app — it is driven entirely from here.
    [DefaultExecutionOrder(-4000)]
    public class ScriptedEnemyQueue : MonoBehaviour
    {
        [Tooltip("Seconds the enemy 'thinks' after its turn starts before playing.")]
        public float playDelaySeconds = 1.5f;

        [Tooltip("Cards the enemy plays in order, one per enemy turn. Must match the enemy's forced resonances. Empty = the enemy skips.")]
        // The M6 script's tuned queue: five deterministic Life-lane sponges
        // (1-atk Bruisers out-heal the chip damage; Plantkeeper is a vanilla
        // 2/8 — its printed heal is not implemented). The enemy never contests
        // lanes 0/1, so the player's two lane wins stay on schedule, and the
        // lane-2 portal caps out at exactly five cards on the enemy's last
        // play. T12 finds the queue empty → skip, whose combat ends the game.
        public List<string> initialPlays = new()
            { "Bruiser", "Plantkeeper", "Bruiser", "Bruiser", "Bruiser" };

        public int QueuedCount => queue.Count;
        public IReadOnlyCollection<string> QueuedCards => queue;

        private readonly Queue<string> queue = new();
        private GameManager gm;
        private TutorialDirector director;

        private PlayerSide EnemySide =>
            director != null && director.humanSide == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left;

        private void Awake()
        {
            gm = FindAnyObjectByType<GameManager>();
            director = FindAnyObjectByType<TutorialDirector>();
            if (gm == null || director == null)
            {
                Debug.LogError("[Tutorial] ScriptedEnemyQueue needs a GameManager and a TutorialDirector in the scene.");
                enabled = false;
                return;
            }

            foreach (string card in initialPlays) Enqueue(card);
            gm.TurnStarted += OnTurnStarted;
        }

        private void OnDestroy()
        {
            if (gm != null) gm.TurnStarted -= OnTurnStarted;
        }

        public void Enqueue(string cardName)
        {
            if (!string.IsNullOrWhiteSpace(cardName)) queue.Enqueue(cardName.Trim());
        }

        public void ClearQueue()
        {
            queue.Clear();
        }

        private void OnTurnStarted(Player player)
        {
            if (!enabled || player.playerSide != EnemySide) return;
            TakeEnemyTurn();
        }

        // Detached from GameManager's awaited turn chain (TurnStarted fires
        // inside it); the delay paces the enemy's "thinking" and unwinds the
        // call stack before the enemy's own play starts a new chain.
        // async void so an exception in the play chain surfaces in the console
        // instead of dying inside a discarded Task.
        private async void TakeEnemyTurn()
        {
            await Task.Delay(Mathf.CeilToInt(Mathf.Max(0.05f, playDelaySeconds) * 1000f));
            if (gm == null || gm.IsGameOver) return;

            if (queue.Count == 0)
            {
                Debug.Log("[Tutorial] Enemy queue empty — enemy skips its turn.");
                gm.OnSkipTurn();
                return;
            }

            string card = queue.Dequeue();
            bool played = await director.PlayScriptedCard(card);
            if (!played)
            {
                Debug.LogError(
                    $"[Tutorial] Scripted enemy play '{card}' failed (bad name / wrong resonance / full portal?) — enemy skips instead. Fix the queue.");
                gm.OnSkipTurn();
            }
        }
    }
}
