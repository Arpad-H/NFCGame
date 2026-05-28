using UnityEngine;
using UnityEngine.UI;

public class GameModeSelection : MonoBehaviour
{
    public Button StartBlindPickButton;
    public Button StartDraftPickButton;
    private ConnectionMenu connectionMenu;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        connectionMenu = FindAnyObjectByType<ConnectionMenu>();
        StartBlindPickButton.onClick.AddListener(() => connectionMenu.Show(LobbyType.BLIND_PICK));
        StartDraftPickButton.onClick.AddListener(() => connectionMenu.Show(LobbyType.DRAFT_PICK));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
