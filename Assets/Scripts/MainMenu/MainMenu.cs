using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] GameObject TopLevelMenu;
    [SerializeField] GameObject GameModesSelection;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       if (!TopLevelMenu) TopLevelMenu = GameObject.Find("TopLevelMenu");
       if (!GameModesSelection) GameModesSelection = GameObject.Find("GameModesSelection");
       
       GameModesSelection.SetActive(false);
       TopLevelMenu.SetActive(true);
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
}
