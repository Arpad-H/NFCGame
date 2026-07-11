using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riftborn.Tutorial
{
    // Single entry point into the tutorial. The scene must be registered in
    // Build Settings (it is — see ProjectSettings/EditorBuildSettings.asset).
    public static class TutorialLauncher
    {
        public const string SceneName = "TutorialScene";

        public static void Launch()
        {
            SceneManager.LoadScene(SceneName);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Dev-only boot path: a small IMGUI button in the corner of the main menu,
    // installed at startup without touching the menu scene. The styled menu
    // button is M5/M7 polish (wire it to TutorialMenuButton.LaunchTutorial).
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
            if (SceneManager.GetActiveScene().name != "MainMenu") return;
            if (GUI.Button(new Rect(10, Screen.height - 42, 160, 32), "Tutorial (dev)"))
            {
                TutorialLauncher.Launch();
            }
        }
    }
#endif
}
