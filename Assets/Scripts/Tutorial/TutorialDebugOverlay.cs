using System.Collections.Generic;
using System.Linq;
using GameSystems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riftborn.Tutorial
{
    // Dev-only control panel for the tutorial scene: shows the current step and
    // board state, force-advances/restarts, and simulates both sides' plays so
    // the whole flow runs in the editor without the companion app.
    // Compiled to an empty component in release builds (the scene still
    // references it, so the class must always exist).
    public class TutorialDebugOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GameManager gm;
        private TutorialDirector director;
        private ScriptedEnemyQueue enemyQueue;
        private TutorialCamera tutorialCamera;
        private NotificationView notificationView;
        private Portal[] portals;

        private string playerCardName = "Rat";
        private string enemyCardName = "Bruiser";
        private string lastRejection = "";
        private bool expanded = true;
        private Vector2 scroll;

        private void Awake()
        {
            gm = FindAnyObjectByType<GameManager>();
            director = FindAnyObjectByType<TutorialDirector>();
            enemyQueue = FindAnyObjectByType<ScriptedEnemyQueue>();
            tutorialCamera = FindAnyObjectByType<TutorialCamera>();
            notificationView = FindAnyObjectByType<NotificationView>();
            portals = FindObjectsByType<Portal>(FindObjectsSortMode.None)
                .OrderBy(p => p.laneIndex).ThenBy(p => p.ownerSide).ToArray();

            if (director != null) director.PlayRejected += message => lastRejection = message;
        }

        private void OnGUI()
        {
            const float width = 340f;
            GUILayout.BeginArea(new Rect(Screen.width - width - 10f, 10f, width, Screen.height - 20f));
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Tutorial Debug</b>", RichLabel());
            if (GUILayout.Button(expanded ? "hide" : "show", GUILayout.Width(50f))) expanded = !expanded;
            GUILayout.EndHorizontal();

            if (expanded)
            {
                scroll = GUILayout.BeginScrollView(scroll);
                DrawStepSection();
                DrawPresentationSection();
                DrawPlayerSection();
                DrawEnemySection();
                DrawBoardSection();
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawStepSection()
        {
            GUILayout.Label("<b>Step</b>", RichLabel());
            if (director == null)
            {
                GUILayout.Label("No TutorialDirector found.");
                return;
            }

            TutorialStep step = director.CurrentStep;
            if (step != null)
            {
                GUILayout.Label($"{director.StepIndex + 1}/{director.StepCount}: {step.Id}  [{step.Advance}]");
                GUILayout.Label(step.Body, WrappedLabel());
                if (step.ExpectedCard != null) GUILayout.Label($"Expected card: {step.ExpectedCard}");
            }
            else
            {
                GUILayout.Label("Sequence finished — free play.");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Force advance")) director.ForceAdvance();
            if (GUILayout.Button("Restart scene")) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(lastRejection)) GUILayout.Label($"Rejected: {lastRejection}", WrappedLabel());
            GUILayout.Space(8f);
        }

        // Manual triggers for the M3/M4 presentation layer, independent of steps.
        private void DrawPresentationSection()
        {
            GUILayout.Label("<b>Camera / UI</b>", RichLabel());

            GUILayout.BeginHorizontal();
            GUI.enabled = tutorialCamera != null;
            if (GUILayout.Button("Full board")) tutorialCamera.FrameFullBoard();
            if (GUILayout.Button("Lane 0")) tutorialCamera.FrameLane(0);
            if (GUILayout.Button("Lane 1")) tutorialCamera.FrameLane(1);
            if (GUILayout.Button("Lane 2")) tutorialCamera.FrameLane(2);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUI.enabled = notificationView != null;
            if (GUILayout.Button("Test rejection toast")) notificationView.ShowToast("Not yet — play Rat. (toast test)");
            GUI.enabled = true;
            GUILayout.Space(8f);
        }

        private void DrawPlayerSection()
        {
            GUILayout.Label("<b>Player (simulate PLAY_CARD)</b>", RichLabel());
            if (gm == null)
            {
                GUILayout.Label("No GameManager found.");
                return;
            }

            GUILayout.BeginHorizontal();
            playerCardName = GUILayout.TextField(playerCardName);
            if (GUILayout.Button("Play", GUILayout.Width(60f))) _ = gm.HandlePlayerPlayCard(playerCardName);
            GUILayout.EndHorizontal();

            string expected = director?.CurrentStep?.ExpectedCard;
            GUI.enabled = expected != null;
            if (GUILayout.Button($"Play expected card{(expected != null ? $" ({expected})" : "")}"))
            {
                _ = gm.HandlePlayerPlayCard(expected);
            }

            GUI.enabled = true;
            if (GUILayout.Button("Skip turn")) gm.OnSkipTurn();
            GUILayout.Space(8f);
        }

        private void DrawEnemySection()
        {
            GUILayout.Label("<b>Scripted enemy</b>", RichLabel());
            if (enemyQueue == null)
            {
                GUILayout.Label("No ScriptedEnemyQueue found.");
                return;
            }

            GUILayout.BeginHorizontal();
            enemyCardName = GUILayout.TextField(enemyCardName);
            if (GUILayout.Button("Enqueue", GUILayout.Width(80f))) enemyQueue.Enqueue(enemyCardName);
            GUILayout.EndHorizontal();

            GUILayout.Label(enemyQueue.QueuedCount > 0
                ? $"Queue: {string.Join(", ", enemyQueue.QueuedCards)}"
                : "Queue empty (enemy skips its turns).", WrappedLabel());
            if (GUILayout.Button("Clear queue")) enemyQueue.ClearQueue();
            GUILayout.Space(8f);
        }

        private void DrawBoardSection()
        {
            GUILayout.Label("<b>Board</b>", RichLabel());
            if (gm != null)
            {
                string turn = gm.ActivePlayer != null ? gm.ActivePlayer.playerSide.ToString() : "—";
                GUILayout.Label($"Turn: {turn}{(gm.IsGameOver ? "  (GAME OVER)" : "")}");
            }

            if (portals == null) return;
            for (int lane = 0; lane < 3; lane++)
            {
                Portal left = portals.FirstOrDefault(p => p.laneIndex == lane && p.ownerSide == PlayerSide.Left);
                Portal right = portals.FirstOrDefault(p => p.laneIndex == lane && p.ownerSide == PlayerSide.Right);
                GUILayout.Label($"Lane {lane}: {Describe(left)}  vs  {Describe(right)}");
            }
        }

        private static string Describe(Portal portal)
        {
            if (portal == null) return "—";
            string resonance = portal.resonance != null ? portal.resonance.ResonanceType.ToString() : "?";
            string destroyed = portal.IsDestroyed ? " ✝" : "";
            return $"{resonance} {portal.CurrentPortalHealth}/{portal.maxPortalHealth}{destroyed}";
        }

        private static GUIStyle RichLabel()
        {
            return new GUIStyle(GUI.skin.label) { richText = true };
        }

        private static GUIStyle WrappedLabel()
        {
            return new GUIStyle(GUI.skin.label) { wordWrap = true };
        }
#endif
    }
}
