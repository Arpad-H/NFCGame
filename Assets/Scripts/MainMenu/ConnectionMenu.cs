using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public enum LobbyType
{
    BLIND_PICK,
    DRAFT_PICK
}

public class ConnectionMenu : MonoBehaviour
{
    public GameObject topLevelMenu;
    public int countdownSeconds = 5;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI player1NameText;
    public GameObject player1connectedText;
    public GameObject player2connectedText;
    public TextMeshProUGUI player2NameText;
    public QRCodeDisplay qrCodeDisplay;
    public GameObject qrCodeDisplayPlayer1;
    public GameObject qrCodeDisplayPlayer2;
    public TextMeshProUGUI player1selectedResonancesText;
    public TextMeshProUGUI player2selectedResonancesText;

    [Header("Resonance coin reveal")]
    public ResonanceCoinReveal player1Coins;
    public ResonanceCoinReveal player2Coins;
    [Tooltip("Coin sprite shown for each resonance when a player's picks land.")]
    public ResonanceSprite[] resonanceSprites;

    private LobbyType lobbyType;

    // Which resonance set is currently shown on each player's coins, so RefreshUI (called
    // on every server event) only re-plays the toss when the picks actually change.
    private readonly List<ResonanceType> player1Shown = new List<ResonanceType>();
    private readonly List<ResonanceType> player2Shown = new List<ResonanceType>();
    private Dictionary<ResonanceType, Sprite> spriteLookup;

    [System.Serializable]
    public struct ResonanceSprite
    {
        public ResonanceType type;
        public Sprite sprite;
    }

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

    public void Show(LobbyType type)
    {
        this.gameObject.SetActive(true);
        this.lobbyType = type;

        // Fresh lobby: forget what was shown and reset the coins to their placeholders so a
        // reconnecting player re-plays the toss.
        player1Shown.Clear();
        player2Shown.Clear();
        if (player1Coins != null) player1Coins.ResetToPlaceholder();
        if (player2Coins != null) player2Coins.ResetToPlaceholder();

        RefreshUI();
    }

    public void Hide()
    {
        this.gameObject.SetActive(false);
        topLevelMenu.SetActive(true);
    }

    public void RefreshUI()
    {
        player1connectedText.SetActive(true);
        player1connectedText.SetActive(true);
        qrCodeDisplay.DisplayQRCodes(lobbyType);
        qrCodeDisplayPlayer1.gameObject.SetActive(true);
        qrCodeDisplayPlayer2.gameObject.SetActive(true);
        player1NameText.text = "Player 1";
        player2NameText.text = "Player 2";
        statusText.text = "Waiting for players...";
        player1selectedResonancesText.gameObject.SetActive(false);
        player2selectedResonancesText.gameObject.SetActive(false);

        // 2. Re-build list from the Server's Master List
        foreach (var player in WebSocketServerBehaviour.Instance.ConnectedPlayers)
        {
            if (player.id == 1)
            {
                player1NameText.text = player.name;
                qrCodeDisplayPlayer1.gameObject.SetActive(false);
                player1connectedText.SetActive(false);
                player1selectedResonancesText.gameObject.SetActive(true);
                player1selectedResonancesText.text = $"Selected Resonances: {string.Join(", ", player.resonances)}";
                RevealResonances(player, player1Coins, player1Shown);
            }
            else if (player.id == 2)
            {
                player2NameText.text = player.name;
                qrCodeDisplayPlayer2.gameObject.SetActive(false);
                player2connectedText.SetActive(false);
                player2selectedResonancesText.gameObject.SetActive(true);
                player2selectedResonancesText.text = $"Selected Resonances: {string.Join(", ", player.resonances)}";
                RevealResonances(player, player2Coins, player2Shown);
            }
        }
    }

    // Play the coin-toss reveal for a player, but only when their picks first arrive or
    // change — RefreshUI runs on every server event, so unchanged picks are skipped.
    private void RevealResonances(PlayerData player, ResonanceCoinReveal coins, List<ResonanceType> shown)
    {
        if (coins == null) return;

        List<ResonanceType> picks = player.resonances;
        if (picks == null || picks.Count == 0) return;
        if (SameSequence(shown, picks)) return;

        shown.Clear();
        shown.AddRange(picks);

        var faces = new List<Sprite>(picks.Count);
        foreach (ResonanceType type in picks)
            faces.Add(SpriteFor(type));

        coins.ShowResonances(faces);
    }

    private Sprite SpriteFor(ResonanceType type)
    {
        if (spriteLookup == null)
        {
            spriteLookup = new Dictionary<ResonanceType, Sprite>();
            if (resonanceSprites != null)
                foreach (ResonanceSprite entry in resonanceSprites)
                    spriteLookup[entry.type] = entry.sprite;
        }
        return spriteLookup.TryGetValue(type, out Sprite sprite) ? sprite : null;
    }

    private static bool SameSequence(List<ResonanceType> a, List<ResonanceType> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
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
        WebSocketServerBehaviour.Instance.BroadcastToPlayers("INITIATE_GAME_STATE");
    }
}