using UnityEngine;

namespace Riftborn.Tutorial
{
    // Persists whether the player has finished (or skipped) the tutorial, so a
    // menu can stop forcing it on returning players while still allowing an
    // explicit replay. Backed by PlayerPrefs, so it survives quitting even in
    // the middle of the tutorial.
    public static class TutorialState
    {
        private const string CompleteKey = "Riftborn.Tutorial.Completed";

        // True once the tutorial has been completed or skipped at least once.
        // A menu's "first launch → start the tutorial" check reads this.
        public static bool IsComplete => PlayerPrefs.GetInt(CompleteKey, 0) == 1;

        // Reaching the victory step or tapping Skip records the tutorial as seen.
        public static void MarkComplete()
        {
            PlayerPrefs.SetInt(CompleteKey, 1);
            PlayerPrefs.Save();
        }

        // Clears the flag so the tutorial counts as unseen again — a "replay
        // tutorial" / reset-progress entry point (and the dev overlay's reset).
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(CompleteKey);
            PlayerPrefs.Save();
        }
    }
}
