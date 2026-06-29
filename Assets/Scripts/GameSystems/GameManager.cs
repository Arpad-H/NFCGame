using System.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;
using Random = System.Random;
using UnityEngine.InputSystem;


public class GameManager : MonoBehaviour
{
    Board board;
    Player activePlayer;
    public Player playerLeft; //TODO temp player representation
    public Player playerRight;
    public int maxCardsPerPortal = 5;
    public bool shufflePortals = false;
    private bool actionTaken = false;
    private BoardEventDispatcher eventDispatcher;
    private int turnCounter = 1;

    [Header("Turn Timer")]
    public float turnTimeLimit = 60f;
    public float lowTimeThreshold = 10f;
    public float intensity2Threshold = 20f;
    public float intensity3Threshold = 10f;
    private float timeRemaining;
    private bool timerRunning;
    private bool intensity2Triggered;
    private bool intensity3Triggered;

    private async void Awake()
    {
        await CardLibrary.Initialize();
        Debug.Log("CardLibrary ready.");

        board = new Board();
        board.shufflePortals = shufflePortals;
      
        eventDispatcher = new BoardEventDispatcher(board);
        activePlayer = new Random().Next(0, 2) == 0 ? playerLeft : playerRight;
        UIManager.Instance.SwitchPlayerTurn(activePlayer.playerSide);

        playerLeft.OnAboutToDrawCard += () => OnPlayerAboutToDrawCard(playerLeft);
        playerRight.OnAboutToDrawCard += () => OnPlayerAboutToDrawCard(playerRight);
        playerLeft.OnCardDrawn += () => OnPlayerDrawsCard(playerLeft);
        playerRight.OnCardDrawn += () => OnPlayerDrawsCard(playerRight);
        playerLeft.OnCardDiscarded += () => OnPlayerDiscardsCard(playerLeft);
        playerRight.OnCardDiscarded += () => OnPlayerDiscardsCard(playerRight);
        if (WebSocketServerBehaviour.Instance == null) SetUpTestEnvironment();
        WebSocketServerBehaviour.Instance.UpdateGameManagerReference(this);
        board.SetUpBoard(maxCardsPerPortal);

        if (Announcer.Instance != null) await Announcer.Instance.AnnouncePlayerTurn(GetDisplayName(activePlayer));
        StartTurnTimer();
    }

    private void Update()
    {
        if (!timerRunning) return;

        timeRemaining -= Time.deltaTime;

        if (!intensity2Triggered && timeRemaining <= intensity2Threshold)
        {
            intensity2Triggered = true;
            AudioManager.Instance.ToggleAdaptiveLayer(2, true);
        }
        if (!intensity3Triggered && timeRemaining <= intensity3Threshold)
        {
            intensity3Triggered = true;
            AudioManager.Instance.ToggleAdaptiveLayer(3, true);
        }

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            timerRunning = false;
            UpdateTurnTimerUI();
            OnTurnTimeExpired();
            return;
        }

        UpdateTurnTimerUI();
    }

    private void StartTurnTimer()
    {
        timeRemaining = turnTimeLimit;
        timerRunning = true;
        intensity2Triggered = false;
        intensity3Triggered = false;
        UpdateTurnTimerUI();
    }

    // Mute the time-pressure adaptive layers once the turn ends (card played or skipped).
    private void DisableTimerAudioLayers()
    {
        AudioManager.Instance.ToggleAdaptiveLayer(2, false);
        AudioManager.Instance.ToggleAdaptiveLayer(3, false);
    }

    private void UpdateTurnTimerUI()
    {
        if (activePlayer == null) return;
        float fill = turnTimeLimit > 0f ? Mathf.Clamp01(timeRemaining / turnTimeLimit) : 0f;
        bool low = timeRemaining < lowTimeThreshold;
        UIManager.Instance.UpdateTurnTimer(activePlayer.playerSide, fill, low);
    }

    // The player ran out of time without playing a card — skip their turn.
    private async void OnTurnTimeExpired()
    {
        DisableTimerAudioLayers();
        await CombatResolution();
    }



    public void SendToPlayer(Player player, string message)
    {
        if (player == null) return;
        WebSocketServerBehaviour.Instance.SendToPlayer(player.playerId, message);
    }
    

    public async Task<bool> HandlePlayerPlayCard(string cardName)
    {
        if (actionTaken) return false;
        CardData cardSource = CardLibrary.GetCard(cardName);
        if (cardSource == null)
        {
            Debug.LogError($"Card: {cardName} not found in library! Did you forget to mark it as adressable and rebuilding the adressables?");
            return false;
        }

        FieldableCardInstance cardToPlay =
            CardFactory.CreateInstance(cardSource, activePlayer, GetOpponent(activePlayer), board, turnCounter);

        // Spells aren't fielded — they play a cast animation and resolve; only
        // minions and items are placed into a portal.
        bool played = cardToPlay is SpellInstance spell
            ? await PlaySpell(spell)
            : await PlayFieldableCard(cardToPlay);

        if (!played)
        {
            Debug.Log("invalid play, try again");
            return false;
        }

        activePlayer.CardPlayed();

        actionTaken = true;
        timerRunning = false; // card played — pause until the next turn starts
        DisableTimerAudioLayers();
        //  await Task.Delay(2000); // Replaced DelayCombatResolution
        await CombatResolution();
        return true;
    }

    private async Task<bool> PlayFieldableCard(FieldableCardInstance cardToPlay)
    {
        if (!await board.PlaceCard(cardToPlay)) return false;

        if (cardToPlay is IGameEventReceiver receiver)
        {
            await receiver.HandleEvent(new GameEvent(GameEventType.OnPlayed, cardToPlay));
        }

        return true;
    }

    private async Task<bool> PlaySpell(SpellInstance spell)
    {
        // A spell can only be cast if the player has a matching-resonance portal,
        // mirroring the placement rule for fielded cards.
        Portal portal = board.GetOwnerPortal(spell);
        if (portal == null) return false;

        // Not placed in the portal, but its effect still resolves from that lane
        // (e.g. "heal own lane").
        spell.SetSourcePortal(portal).SetTargetLane(board.GetLaneForPortal(portal));

        // Slide the spell up from the bottom of the screen to the center; the
        // effect resolves only after it finishes and leaves the screen.
        await SpellCastAnimator.Instance.Play(spell, activePlayer.playerSide, portal.tempCardPrefab);

        await spell.HandleEvent(new GameEvent(GameEventType.OnPlayed, spell));
        return true;
    }

    private async Task CombatResolution()
    {
        if (Announcer.Instance != null) await Announcer.Instance.AnnounceFight();
        await eventDispatcher.CombatResolution();
        await EndTurn();
    }

    private async Task EndTurn()
    {
        await eventDispatcher.RoundEnd();
        await StartTurn();
    }

    private async Task StartTurn()
    {
        turnCounter++;
        activePlayer = GetOpponent(activePlayer);
        UIManager.Instance.SwitchPlayerTurn(activePlayer.playerSide);
        actionTaken = false;
        // Announce the turn first; the player isn't prompted to act (below) until
        // the banner has finished, so they can't play during the announcement.
        if (Announcer.Instance != null) await Announcer.Instance.AnnouncePlayerTurn(GetDisplayName(activePlayer));
        await eventDispatcher.RoundStart(turnCounter);
        activePlayer.DrawCard(1);
        SendToPlayer(activePlayer, "ACTION_PLAY_A_CARD");
        SendToPlayer(GetOpponent(activePlayer), "ACTION_WAIT");
        StartTurnTimer();
    }

    public async void OnSkipTurn()
    {
        timerRunning = false;
        DisableTimerAudioLayers();
        await CombatResolution();
    }

    Player GetOpponent(Player player)
    {
        return player.playerSide == PlayerSide.Left ? playerRight : playerLeft;
    }

    // Resolves a player's display name from the connected-player roster, falling
    // back to their board side if no name is registered (e.g. test setups).
    private string GetDisplayName(Player player)
    {
        PlayerData data = WebSocketServerBehaviour.Instance?.ConnectedPlayers.Find(p => p.id == player.playerId);
        return string.IsNullOrEmpty(data?.name) ? player.playerSide.ToString() : data.name;
    }
    private void OnPlayerAboutToDrawCard(Player player)
    {
        SendToPlayer(player, "ACTION_DRAW_A_CARD");
    }
    private async void OnPlayerDrawsCard(Player player)
    {
        Debug.Log($"{player} drew a card.");
        await eventDispatcher.CardDrawn(player);
    }

    private async void OnPlayerDiscardsCard(Player player)
    {
        Debug.Log($"{player} discarded a card.");
        await eventDispatcher.CardDiscarded(player);
    }

    //TODO Temporary Testing methods
    private void SetUpTestEnvironment()
    {
        this.gameObject.AddComponent<WebSocketServerBehaviour>();
        //Create mock players and assign them to the server's connected players list
        PlayerData player1 = new PlayerData(1, "testLeft");
        PlayerData player2 = new PlayerData(2, "testRight");
        playerLeft.playerId = 1;
        playerRight.playerId = 2;
        player2.resonances = new List<ResonanceType> { ResonanceType.Darkness, ResonanceType.Psychic, ResonanceType.Life };
        player1.resonances = new List<ResonanceType>
            { ResonanceType.Death, ResonanceType.Holy, ResonanceType.Plague  };
        WebSocketServerBehaviour.Instance.ConnectedPlayers.Add(player1);
        WebSocketServerBehaviour.Instance.ConnectedPlayers.Add(player2);
        playerLeft.DrawCard(3);
        playerRight.DrawCard(3);
    }

    public async void TestAddMinionLeft()
    {
        var cards = CardLibrary.GetCards();
        bool valid = false;
        int maxTries = 20;
        int tries = 0;
        CardData card = cards[UnityEngine.Random.Range(0, cards.Count)];

        while (!valid && tries < maxTries)
        {
            if (card.cardType is MinionType)
            {
                valid = await HandlePlayerPlayCard(card.cardName);
                if (!valid) card = cards[UnityEngine.Random.Range(0, cards.Count)];
            }
            else
            {
                card = cards[UnityEngine.Random.Range(0, cards.Count)];
            }
            tries++;
        }
    }

    public async void TestAddMinionRight()
    {
        var cards = CardLibrary.GetCards();
        bool valid = false;
        int maxTries = 20;
        int tries = 0;
        CardData card = cards[UnityEngine.Random.Range(0, cards.Count)];

        while (!valid && tries < maxTries)
        {
            if (card.cardType is MinionType)
            {
                valid = await HandlePlayerPlayCard(card.cardName);
                if (!valid) card = cards[UnityEngine.Random.Range(0, cards.Count)];
            }
            else
            {
                card = cards[UnityEngine.Random.Range(0, cards.Count)];
            }
            tries++;
        }
    }

    public async void TestAddItemLeft()
    {
        var cards = CardLibrary.GetCards();
        bool valid = false;
        int maxTries = 20;
        int tries = 0;
        CardData card = cards[UnityEngine.Random.Range(0, cards.Count)];

        while (!valid && tries < maxTries)
        {
            if (card.cardType is ItemType)
            {
                valid = await HandlePlayerPlayCard(card.cardName);
                if (!valid) card = cards[UnityEngine.Random.Range(0, cards.Count)];
            }
            else
            {
                card = cards[UnityEngine.Random.Range(0, cards.Count)];
            }
            tries++;
        }
    }

    public async void TestAddItemRight()
    {
        var cards = CardLibrary.GetCards();
        bool valid = false;
        int maxTries = 20;
        int tries = 0;
        CardData card = cards[UnityEngine.Random.Range(0, cards.Count)];

        while (!valid && tries < maxTries)
        {
            if (card.cardType is ItemType)
            {
                valid = await HandlePlayerPlayCard(card.cardName);
                if (!valid) card = cards[UnityEngine.Random.Range(0, cards.Count)];
            }
            else
            {
                card = cards[UnityEngine.Random.Range(0, cards.Count)];
            }
            tries++;
        }
    }
}

