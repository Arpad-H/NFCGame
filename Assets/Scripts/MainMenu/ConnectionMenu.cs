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
    [Tooltip("Seconds between each step of the 'Waiting for players...' dot animation (. / .. / ...).")]
    public float waitingDotsInterval = 0.5f;
    public TextMeshProUGUI player1NameText;
 //   public GameObject player1connectedText;
  //  public GameObject player2connectedText;
    public TextMeshProUGUI player2NameText;
    public QRCodeDisplay qrCodeDisplay;
    public GameObject qrCodeDisplayPlayer1;
    public GameObject qrCodeDisplayPlayer2;
 //   public TextMeshProUGUI player1selectedResonancesText;
 //   public TextMeshProUGUI player2selectedResonancesText;

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

    // Whether each player's QR panel has been dropped. Set the moment the drop starts —
    // RefreshUI runs on every server event, so this is what stops a later event from
    // restarting the animation mid-flight or re-summoning a panel that is already gone.
    private bool player1QrDismissed;
    private bool player2QrDismissed;

    // Drives the "Waiting for players..." dot animation independently of RefreshUI, which
    // fires on every server event and would otherwise reset the text mid-animation.
    private Coroutine waitingDotsRoutine;

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

        // Cut any app still connected from a previous visit and empty the roster, so the
        // clean visual reset below isn't immediately undone by a stale player and the
        // returning player is forced to re-scan the QR.
        if (WebSocketServerBehaviour.Instance != null)
            WebSocketServerBehaviour.Instance.ResetLobby();

        // Fresh lobby: forget what was shown and reset the coins to their placeholders so a
        // reconnecting player re-plays the toss, and let the QR panels drop again.
        player1Shown.Clear();
        player2Shown.Clear();
        player1QrDismissed = false;
        player2QrDismissed = false;
        if (player1Coins != null) player1Coins.ResetToPlaceholder();
        if (player2Coins != null) player2Coins.ResetToPlaceholder();

        StartWaitingDotsAnimation();
        RefreshUI();
    }

    public void Hide()
    {
        StopWaitingDotsAnimation();

        // Going back to the menu: cut the app clients and clear the roster so they can't
        // linger into the next screen (e.g. the tutorial reading this as a ready player).
        // Only the "back" button reaches Hide(); starting the game loads GameScene instead,
        // so this never drops a player who is actually about to play.
        if (WebSocketServerBehaviour.Instance != null)
            WebSocketServerBehaviour.Instance.ResetLobby();

        this.gameObject.SetActive(false);
        topLevelMenu.SetActive(true);
    }

    private void StartWaitingDotsAnimation()
    {
        StopWaitingDotsAnimation();
        waitingDotsRoutine = StartCoroutine(AnimateWaitingDots());
    }

    private void StopWaitingDotsAnimation()
    {
        if (waitingDotsRoutine == null) return;
        StopCoroutine(waitingDotsRoutine);
        waitingDotsRoutine = null;
    }

    private IEnumerator AnimateWaitingDots()
    {
        int dots = 1;
        while (true)
        {
            statusText.text = "Waiting for players" + new string('.', dots);
            yield return new WaitForSeconds(waitingDotsInterval);
            dots = dots % 3 + 1;
        }
    }

    public void RefreshUI()
    {
    //    player1connectedText.SetActive(true);
    //    player1connectedText.SetActive(true);
        qrCodeDisplay.DisplayQRCodes(lobbyType);
        // Only panels still waiting on their player come back — one that has already been
        // dropped must stay gone, or the next server event would re-summon it.
        if (!player1QrDismissed) qrCodeDisplayPlayer1.SetActive(true);
        if (!player2QrDismissed) qrCodeDisplayPlayer2.SetActive(true);
        player1NameText.text = "Player 1";
        player2NameText.text = "Player 2";
    //    player1selectedResonancesText.gameObject.SetActive(false);
    //    player2selectedResonancesText.gameObject.SetActive(false);

        // 2. Re-build list from the Server's Master List
        foreach (var player in WebSocketServerBehaviour.Instance.ConnectedPlayers)
        {
            if (player.id == 1)
            {
                player1NameText.text = player.name;
                DismissQrPanel(qrCodeDisplayPlayer1, ref player1QrDismissed);
        //        player1connectedText.SetActive(false);
        //        player1selectedResonancesText.gameObject.SetActive(true);
        //        player1selectedResonancesText.text = $"Selected Resonances: {string.Join(", ", player.resonances)}";
                RevealResonances(player, player1Coins, player1Shown);
            }
            else if (player.id == 2)
            {
                player2NameText.text = player.name;
                DismissQrPanel(qrCodeDisplayPlayer2, ref player2QrDismissed);
         //       player2connectedText.SetActive(false);
        //        player2selectedResonancesText.gameObject.SetActive(true);
         //       player2selectedResonancesText.text = $"Selected Resonances: {string.Join(", ", player.resonances)}";
                RevealResonances(player, player2Coins, player2Shown);
            }
        }
    }

    // Send a player's QR panel away now that they're in: it pops toward the camera, then
    // freefalls off the bottom of the screen and disables itself. Fires once, on the event
    // where the player first shows up. Falls back to hiding the panel outright if the
    // motion component isn't on the prefab.
    private void DismissQrPanel(GameObject panel, ref bool dismissed)
    {
        if (dismissed || panel == null) return;
        dismissed = true;

        if (!panel.activeInHierarchy)
        {
            panel.SetActive(false);
            return;
        }

        var motion = panel.GetComponent<QRDismissMotion>();
        if (motion == null) motion = panel.AddComponent<QRDismissMotion>();
        motion.Play();
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
        StopWaitingDotsAnimation();

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