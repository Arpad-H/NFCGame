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

        if (await board.PlaceCard(cardToPlay)) //TODO if spell or item decide wether it can be played without a minion. 
        {
            if (cardToPlay is IGameEventReceiver receiver)
            {
                await receiver.HandleEvent(new GameEvent(GameEventType.OnPlayed, cardToPlay));
            }
            

            activePlayer.CardPlayed();

            actionTaken = true;
            //  await Task.Delay(2000); // Replaced DelayCombatResolution
            await CombatResolution();
            return true;
        }

        Debug.Log("invalid play, try again");
        return false;
    }

    private async Task CombatResolution()
    {
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
        await eventDispatcher.RoundStart(turnCounter);
        activePlayer.DrawCard(1);
        SendToPlayer(activePlayer, "ACTION_PLAY_A_CARD");
        SendToPlayer(GetOpponent(activePlayer), "ACTION_WAIT");
    }

    public async void OnSkipTurn()
    {
        await CombatResolution();
    }

    Player GetOpponent(Player player)
    {
        return player.playerSide == PlayerSide.Left ? playerRight : playerLeft;
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
        player1.resonances = new List<ResonanceType> { ResonanceType.Darkness, ResonanceType.Death, ResonanceType.Plague };
        player2.resonances = new List<ResonanceType>
            { ResonanceType.Psychic, ResonanceType.Life, ResonanceType.Holy };
        WebSocketServerBehaviour.Instance.ConnectedPlayers.Add(player1);
        WebSocketServerBehaviour.Instance.ConnectedPlayers.Add(player2);
        playerLeft.DrawCard(3);
        playerRight.DrawCard(3);
    }

    public void TestAddMinionLeft()
    {
        var cards = CardLibrary.GetCards();
        //try until luck into one
        while (HandlePlayerPlayCard(cards[UnityEngine.Random.Range(0, cards.Count)].cardName).Result == false) ;
    }

    public void TestAddMinionRight()
    {
       
        var cards = CardLibrary.GetCards();
        //try until luck into one
        while (HandlePlayerPlayCard(cards[UnityEngine.Random.Range(0, cards.Count)].cardName).Result == false) ;
    }
    public void TestAddItemLeft()
    {
       
        var cards = CardLibrary.GetCards();
        //try until luck into one
        while (HandlePlayerPlayCard(cards[UnityEngine.Random.Range(0, cards.Count)].cardName).Result == false) ;
    }

    public void TestAddItemRight()
    {
      
        var cards = CardLibrary.GetCards();
        //try until luck into one
        while (HandlePlayerPlayCard(cards[UnityEngine.Random.Range(0, cards.Count)].cardName).Result == false) ;
    }
}

