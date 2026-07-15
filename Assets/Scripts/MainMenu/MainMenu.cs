using UnityEngine;
using Riftborn.Tutorial;

public class MainMenu : MonoBehaviour
{
    [SerializeField] GameObject TopLevelMenu;
    [SerializeField] GameObject GameModesSelection;

    [Header("Tutorial")]
    [Tooltip("Editor/dev-only convenience: adds the corner \"Tutorial (dev)\" IMGUI " +
             "button that launches the tutorial. The real Tutorial menu button works " +
             "regardless of this — it's just an always-available launch point for " +
             "iteration. No effect in release builds.")]
    [SerializeField] bool addTutorialDevButton = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       if (!TopLevelMenu) TopLevelMenu = GameObject.Find("TopLevelMenu");
       if (!GameModesSelection) GameModesSelection = GameObject.Find("GameModesSelection");

       GameModesSelection.SetActive(false);
       TopLevelMenu.SetActive(true);

       if (addTutorialDevButton) TutorialDevEntry.InstallDevButton();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void ShowGameModesSelection()
    {
        GameModesSelection.SetActive(true);
        TopLevelMenu.SetActive(false);
    }
    public void HideGameModesSelection()
    {
        GameModesSelection.SetActive(false);
        TopLevelMenu.SetActive(true);
    }

    // Wired to the Tutorial button's onClick in MainMenu.unity. Runs the
    // one-player QR connect flow over the menu (TutorialConnectScreen), which
    // then loads TutorialScene.
    public void LaunchTutorial()
    {
        TutorialLauncher.Launch();
    }
}
