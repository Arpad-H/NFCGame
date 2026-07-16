using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riftborn.Tutorial
{
    // Single entry point into the tutorial. Launch() runs the real onboarding
    // (one-player QR connect screen over the menu, M5); LaunchDirect() skips
    // straight into the scene for editor/debug iteration. Both scenes are
    // registered in Build Settings (ProjectSettings/EditorBuildSettings.asset).
    public static class TutorialLauncher
    {
        public const string SceneName = "TutorialScene";
        public const string MenuSceneName = "MainMenu";

        // The connect screen is a panel authored on the menu canvas, hidden
        // until now — so this finds it rather than building one. Inactive
        // objects included: hidden is its resting state.
        public static void Launch()
        {
            var screen = Object.FindAnyObjectByType<TutorialConnectScreen>(FindObjectsInactive.Include);
            if (screen == null)
            {
                Debug.LogError($"[Tutorial] No TutorialConnectScreen in the '{MenuSceneName}' scene. The " +
                               "connect screen is authored on the menu canvas now — add the panel there " +
                               "and wire up its Status Label and QR Image.");
                return;
            }

            if (screen.gameObject.activeSelf) return; // already open
            screen.Show();
        }

        public static void LaunchDirect()
        {
            SceneManager.LoadScene(SceneName);
        }

        public static void ReturnToMenu()
        {
            SceneManager.LoadScene(MenuSceneName);
        }
    }

    // Dev-only boot path: a small IMGUI button in the corner of the main menu.
    // NOT auto-installed — MainMenu installs it on demand when its
    // `addTutorialDevButton` toggle is on (see MainMenu.cs). It runs the same
    // connect flow as the real menu button (TutorialLauncher.Launch); the connect
    // screen has its own dev "Start without app" skip. No-op in release builds.
    public static class TutorialDevEntry
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool installed;
#endif

        public static void InstallDevButton()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (installed) return;
            installed = true;
            var go = new GameObject("TutorialDevEntry");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<TutorialDevEntryButton>();
#endif
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal class TutorialDevEntryButton : MonoBehaviour
    {
        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name != TutorialLauncher.MenuSceneName) return;
            if (GUI.Button(new Rect(10, Screen.height - 42, 160, 32), "Tutorial (dev)"))
            {
                TutorialLauncher.Launch();
            }
        }
    }
#endif
}
