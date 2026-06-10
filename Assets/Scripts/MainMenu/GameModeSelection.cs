using UnityEngine;
using UnityEngine.UI;

public class GameModeSelection : MonoBehaviour
{
    public Button StartBlindPickButton;
    public Button StartDraftPickButton;
    public ConnectionMenu connectionMenu;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartBlindPickButton.onClick.AddListener(() =>
        {
            Debug.Log(connectionMenu);
            connectionMenu.Show(LobbyType.BLIND_PICK);
        });
        StartDraftPickButton.onClick.AddListener(() => connectionMenu.Show(LobbyType.DRAFT_PICK));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
