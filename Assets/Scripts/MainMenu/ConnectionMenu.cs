using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionMenu : MonoBehaviour
{
    public int countdownSeconds = 5;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI player1NameText;
    public GameObject player1connectedText;
    public GameObject player2connectedText;
    public TextMeshProUGUI player2NameText;
    public GameObject qrCodeDisplayPlayer1;
    public GameObject qrCodeDisplayPlayer2;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // When this menu wakes up, tell the persistent server to look at ME now
        if (WebSocketServerBehaviour.Instance != null)
        {
            WebSocketServerBehaviour.Instance.UpdateMenuReference();
        }

        this.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void Show()
    {
        this.gameObject.SetActive(true);
        RefreshUI();
    }

    public void Hide()
    {
        this.gameObject.SetActive(false);
    }

    public void RefreshUI()
    {
        player1connectedText.SetActive(true);
        player1connectedText.SetActive(true);
        qrCodeDisplayPlayer1.gameObject.SetActive(true);
        qrCodeDisplayPlayer2.gameObject.SetActive(true);
        player1NameText.text = "Player 1";
        player2NameText.text = "Player 2";
        statusText.text = "Waiting for players...";

        // 2. Re-build list from the Server's Master List
        foreach (var player in WebSocketServerBehaviour.Instance.ConnectedPlayers)
        {
            if (player.id == 1)
            {
                player1NameText.text = player.name;
                qrCodeDisplayPlayer1.gameObject.SetActive(false);
                player1connectedText.SetActive(false);
            }
            else if (player.id == 2)
            {
                player2NameText.text = player.name;
                qrCodeDisplayPlayer2.gameObject.SetActive(false);
                player2connectedText.SetActive(false);
            }
        }
    }

    public void StartGameWithCountdown()
    {   
      
        StartCoroutine(CountdownAndStart());
    }

    private IEnumerator CountdownAndStart()
    {
       while (countdownSeconds > 0)
       {
           statusText.text = $"Starting in {countdownSeconds}...";
           yield return new WaitForSeconds(1);
           countdownSeconds--;
       }
         statusText.text = "Starting game!";
        
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
         
    }
}