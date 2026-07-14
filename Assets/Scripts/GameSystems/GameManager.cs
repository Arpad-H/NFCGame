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
    private bool gameOver = false;
    private BoardEventDispatcher eventDispatcher;
    private int turnCounter = 1;

    [Header("Spell cast")]
    [Tooltip("Full readable card prefab (CardV2 + CardVisualizer) shown sliding to center screen when a spell is cast. " +
             "The board uses the simplified BoardToken, so spells keep their own full-card prefab to stay readable.")]
    public GameObject spellCardPrefab;

    [Header("Pacing")]
    [Tooltip("Seconds to pause after combat resolves, before end-of-round effects trigger. " +
             "The other phase transitions get this breathing room for free from the announcer banner.")]
    public float postCombatDelaySeconds = 0.75f;

    // ── Outside-driver seams ─────────────────────────────────────────────────
    // Plain hooks with no knowledge of who is listening. All default to
    // null/unset, so a normal match behaves exactly as before; a scripted
    // driver (e.g. the tutorial director) can observe the turn loop, veto
    // plays, and pin down the otherwise-random setup.

    // Return false to veto a play before anything is created or placed. A
    // vetoed play costs nothing: the turn is not consumed, actionTaken stays
    // false, and CardPlayRejected fires with the card's name.
    [NonSerialized] public Func<string, bool> CardPlayValidator;

    // A play that didn't happen — vetoed by the validator, or placement failed
    // (wrong resonance, portal full, item on empty portal, decided lane).
    public event Action<string> CardPlayRejected;

    // Fired once a turn is fully open for the active player: after activePlayer
    // is set, the banner has finished, and the app prompts went out. Also fired
    // for the very first turn at the end of Awake (turn 1 never runs StartTurn).
    public event Action<Player> TurnStarted;

    // A card was placed / a spell resolved; fires before combat resolution.
    public event Action<string> CardPlayedSuccessfully;

    // Combat and its post-combat delay finished and the game continues.
    // Not fired when that combat ended the match — GameOver fires instead.
    public event Action CombatResolved;

    // A lane was just awarded (Lane.WonBy is set) and cleared.
    public event Action<Lane> LaneWon;

    // The match ended with a 2-of-3 lane win; the argument is the winner.
    public event Action<Player> GameOver;

    // Forces which side takes the first turn; unset = random (normal play).
    [NonSerialized] public PlayerSide? startingSideOverride;

    public Player ActivePlayer => activePlayer;
    public bool IsGameOver => gameOver;

    [Header("Turn Timer")]
    [Tooltip("Untick to give players unlimited time (scripted/tutorial setups). The timer UI stays full.")]
    public bool turnTimerEnabled = true;
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
        activePlayer = startingSideOverride switch
        {
            PlayerSide.Left => playerLeft,
            PlayerSide.Right => playerRight,
            _ => new Random().Next(0, 2) == 0 ? playerLeft : playerRight
        };
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
        // Turn 1's TurnStarted — subsequent turns fire it from StartTurn, which
        // turn 1 never runs through.
        TurnStarted?.Invoke(activePlayer);
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
        timerRunning = turnTimerEnabled;
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
        if (gameOver) return;
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
        if (gameOver || actionTaken) return false;

        if (CardPlayValidator != null && !CardPlayValidator(cardName))
        {
            CardPlayRejected?.Invoke(cardName);
            return false;
        }

        CardData cardSource = CardLibrary.GetCard(cardName);
        if (cardSource == null)
        {
            Debug.LogError($"Card: {cardName} not found in library! Did you forget to mark it as adressable and rebuilding the adressables?");
            return false;
        }

        // Claim the turn's single action up-front, before the first await below.
        // This method yields while placing the card / playing its cast animation,
        // so a second play dispatched on the same frame (rapid PLAY_CARD messages
        // or repeated AddTestMinion clicks) would otherwise pass the actionTaken
        // guard above and field another card before combat resolves. Released
        // again on the invalid-play path so a vetoed/failed placement still costs
        // nothing.
        actionTaken = true;

        FieldableCardInstance cardToPlay =
            CardFactory.CreateInstance(cardSource, activePlayer, GetOpponent(activePlayer), board, turnCounter);

        // Spells aren't fielded — they play a cast animation and resolve; only
        // minions and items are placed into a portal.
        bool played = cardToPlay is SpellInstance spell
            ? await PlaySpell(spell)
            : await PlayFieldableCard(cardToPlay);

        if (!played)
        {
            actionTaken = false; // placement failed — turn not consumed, let them retry
            Debug.Log("invalid play, try again");
            CardPlayRejected?.Invoke(cardName);
            return false;
        }

        activePlayer.CardPlayed();

        timerRunning = false; // card played — pause until the next turn starts
        DisableTimerAudioLayers();
        CardPlayedSuccessfully?.Invoke(cardName);
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

        // A single-card action with no targets yet. When you later collect the
        // minions a battlecry affected, pass them as the targets list to get the
        // multi-target tile (see PlaySpell).
        GameHistory.Record(new HistoryEntry(HistoryKind.Play, HistoryActor.FromCard(cardToPlay)));

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
        await SpellCastAnimator.Instance.Play(spell, activePlayer.playerSide, spellCardPrefab);

        await spell.HandleEvent(new GameEvent(GameEventType.OnPlayed, spell));

        // The spell resolved. For now this is a single-card "played" tile; to get
        // the multi-target tile, collect the minions the spell hit and pass them:
        //   GameHistory.Record(new HistoryEntry(HistoryKind.Play,
        //       HistoryActor.FromCard(spell), affected.Select(HistoryActor.FromCard).ToList()));
        // The cleanest source of "affected" is an action scope around effect
        // resolution (see notes) so any damage/heal auto-collects its targets.
        GameHistory.Record(new HistoryEntry(HistoryKind.Play, HistoryActor.FromCard(spell)));
        return true;
    }

    private async Task CombatResolution()
    {
        if (Announcer.Instance != null) await Announcer.Instance.AnnounceFight();
        await eventDispatcher.CombatResolution(activePlayer.playerSide);

        // Award/clear any lane whose portal fell this combat, and check for a
        // 2-of-3 win or a 1-1 showdown. If the game just ended, stop the turn
        // loop here.
        if (await HandlePostCombat())
        {
            timerRunning = false;
            return;
        }

        // Let the fight land before end-of-round effects start firing: the
        // corpses have only just been cleared and the damage numbers are still
        // floating. Every other phase boundary is paced by an announcer banner.
        if (postCombatDelaySeconds > 0f)
        {
            await Task.Delay(Mathf.CeilToInt(postCombatDelaySeconds * 1000f));
        }

        CombatResolved?.Invoke();
        await EndTurn();
    }

    // Resolves lane outcomes after combat: announces & clears each newly won
    // lane, then ends the game on a 2-of-3 win or opens showdown on a 1-1 split.
    // Returns true when the game is over, so the caller stops the turn loop.
    private async Task<bool> HandlePostCombat()
    {
        foreach (var lane in board.ResolveDecidedLanes())
        {
            Player winner = lane.WonBy == PlayerSide.Left ? playerLeft : playerRight;
            if (Announcer.Instance != null) await Announcer.Instance.AnnounceLaneWon(GetDisplayName(winner));
            await board.ClearLane(lane);
            LaneWon?.Invoke(lane);
        }

        int leftWon = board.CountLanesWon(PlayerSide.Left);
        int rightWon = board.CountLanesWon(PlayerSide.Right);

        if (leftWon >= 2 || rightWon >= 2)
        {
            Player gameWinner = leftWon >= 2 ? playerLeft : playerRight;
            gameOver = true;
            GameOver?.Invoke(gameWinner);
            if (Announcer.Instance != null) await Announcer.Instance.AnnounceVictory(GetDisplayName(gameWinner));
            Debug.Log($"Game over — {GetDisplayName(gameWinner)} wins ({leftWon}-{rightWon} lanes).");
            return true;
        }

        // 1-1 with a single lane left: open it up to any card, any resonance.
        if (!board.IsShowdown && leftWon == 1 && rightWon == 1)
        {
            board.EnterShowdown();
            if (Announcer.Instance != null) await Announcer.Instance.AnnounceShowdown();
            Debug.Log("Showdown! All cards can now be played into the last contested lane.");
        }

        return false;
    }

    private async Task EndTurn()
    {
        await eventDispatcher.RoundEnd();
        // Blind ticks at every turn end, exactly like status durations.
        playerLeft.TickBlind();
        playerRight.TickBlind();
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
        // The turn is now fully open for activePlayer — the scripted enemy queue
        // and any other observer act on this. Turn 1 fires the equivalent from
        // Awake (it never runs through StartTurn).
        TurnStarted?.Invoke(activePlayer);
    }

    public async void OnSkipTurn()
    {
        if (gameOver) return;
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

