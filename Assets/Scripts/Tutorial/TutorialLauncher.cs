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

        public static void Launch()
        {
            if (Object.FindAnyObjectByType<TutorialConnectScreen>() != null) return;
            new GameObject("TutorialConnectScreen").AddComponent<TutorialConnectScreen>();
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Dev-only boot path: a small IMGUI button in the corner of the main menu,
    // installed at startup without touching the menu scene. It runs the real
    // connect flow (the screen has its own dev skip). The styled menu button
    // is M7 polish (wire it to TutorialMenuButton.LaunchTutorial).
    internal static class TutorialDevEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("TutorialDevEntry");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<TutorialDevEntryButton>();
        }
    }

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
