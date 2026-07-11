using System.Collections.Generic;
using GameSystems;
using UnityEngine;

namespace Riftborn.Tutorial
{
    // Deterministic match setup for the tutorial. Runs before GameManager.Awake
    // (execution order) so everything it forces is in place when the engine
    // reads it: the server singleton exists, both roster entries carry the
    // fixed tutorial resonances (the scripted enemy is id 2 and never has a
    // socket — SendToPlayer to it is a harmless no-op), the player side goes
    // first, the turn timer is off, and portal HP is shrunk so lanes resolve
    // in a few hits instead of grinding through 15.
    [DefaultExecutionOrder(-5000)]
    public class TutorialBootstrap : MonoBehaviour
    {
        [Header("Players")]
        public string playerName = "You";
        public string enemyName = "Rift Warden";
        public List<ResonanceType> playerResonances = new()
            { ResonanceType.Death, ResonanceType.Holy, ResonanceType.Plague };
        public List<ResonanceType> enemyResonances = new()
            { ResonanceType.Darkness, ResonanceType.Psychic, ResonanceType.Life };

        [Header("Pacing")]
        [Tooltip("Portal HP for the tutorial. Normal matches keep the Portal prefab's own value (default 15).")]
        public int portalHealth = 4;
        [Tooltip("Opening hand count shown in the UI; the real hand is the physical cards the player assembles.")]
        public int openingHandSize = 3;

        private GameManager gm;

        private void Awake()
        {
            gm = FindAnyObjectByType<GameManager>();
            if (gm == null)
            {
                Debug.LogError("[Tutorial] No GameManager in scene — bootstrap disabled.");
                return;
            }

            if (playerResonances.Count != 3 || enemyResonances.Count != 3)
            {
                Debug.LogError("[Tutorial] Each side needs exactly 3 resonances (one per lane).");
                return;
            }

            // Creating the server here means GameManager skips its own
            // SetUpTestEnvironment; the bootstrap is the single source of the
            // tutorial's fabricated setup. When the tutorial is entered from
            // the menu, the instance already exists and is reused.
            if (WebSocketServerBehaviour.Instance == null)
            {
                new GameObject("WebSocketServer (Tutorial)").AddComponent<WebSocketServerBehaviour>();
            }

            ForceRosterEntry(1, playerName, playerResonances);
            ForceRosterEntry(2, enemyName, enemyResonances);
            gm.playerLeft.playerId = 1;
            gm.playerRight.playerId = 2;

            gm.startingSideOverride = PlayerSide.Left;
            gm.turnTimerEnabled = false;

            foreach (Portal portal in FindObjectsByType<Portal>(FindObjectsSortMode.None))
            {
                portal.SetMaxHealth(portalHealth);
            }
        }

        private void Start()
        {
            if (gm == null) return;

            // Hand counts only — Unity never models the actual hand.
            _ = gm.playerLeft.DrawCard(openingHandSize);
            _ = gm.playerRight.DrawCard(openingHandSize);

            // The companion app expects this once the game scene is up; with no
            // connected sessions it's a no-op (editor/debug runs).
            WebSocketServerBehaviour.Instance.BroadcastToPlayers("INITIATE_GAME_STATE");
        }

        // Reuses an existing roster entry (the player who connected in the menu
        // keeps their name) but always forces the tutorial resonances; creates
        // the entry when missing (editor play straight into the tutorial scene,
        // and always for the socketless enemy).
        private void ForceRosterEntry(int id, string fallbackName, List<ResonanceType> resonances)
        {
            List<PlayerData> roster = WebSocketServerBehaviour.Instance.ConnectedPlayers;
            PlayerData entry = roster.Find(p => p.id == id);
            if (entry == null)
            {
                entry = new PlayerData(id, fallbackName);
                roster.Add(entry);
            }

            entry.resonances = new List<ResonanceType>(resonances);
        }
    }
}
