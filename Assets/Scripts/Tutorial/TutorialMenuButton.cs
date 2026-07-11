using UnityEngine;

namespace Riftborn.Tutorial
{
    // Drop this on a menu UI Button and wire onClick → LaunchTutorial to give
    // the main menu a real (styled) tutorial entry. The dev IMGUI corner
    // button (TutorialLauncher.cs) covers iteration until that exists.
    public class TutorialMenuButton : MonoBehaviour
    {
        public void LaunchTutorial()
        {
            TutorialLauncher.Launch();
        }
    }
}
