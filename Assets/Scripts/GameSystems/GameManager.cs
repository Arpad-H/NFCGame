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
        board.SetUpBoard(maxCardsPerPortal);
        eventDispatcher = new BoardEventDispatcher(board);
        activePlayer = new Random().Next(0, 2) == 0 ? playerLeft : playerRight;
        UIManager.Instance.SwitchPlayerTurn(activePlayer.playerSide);
        if (WebSocketServerBehaviour.Instance ==
            null)
        {
            SetUpTestEnvironment();
        }

        WebSocketServerBehaviour.Instance.UpdateGameManagerReference(this);
    }


    private void Start()
    {
        // playerLeft = new Player(WebSocketServerBehaviour.Instance.ConnectedPlayers[0]);
    }

    public void HandlePlayerPlayCard(string cardName)
    {
        if (actionTaken)
        {
            Debug.Log("Action already taken this turn, please wait for next turn.");
            return;
        }

        CardData card = CardLibrary.GetCard(cardName);
        if (card == null)
        {
            Debug.LogError($"Card: {cardName} not found in library!");
            return;
        }
        
        FieldableCardInstance cardToPlay = CardFactory.CreateInstance(card, activePlayer, GetOpponent(activePlayer),board,turnCounter);

        if (board.PlaceCard(cardToPlay))
        {
            if (cardToPlay is IGameEventReceiver receiver)
            {
                receiver.HandleEvent(new GameEvent(GameEventType.OnPlayed, cardToPlay));
            }

           activePlayer.DiscardCard(1);

            actionTaken = true;
            //  StartCoroutine(DelayCombatResolution(2));
            CombatResolution();
            return;
        }

        Debug.Log("invalid play, try again");
    }

  private void CombatResolution()
    {
        eventDispatcher.CombatResolution();
        EndTurn();
    }


    private void EndTurn()
    {
        eventDispatcher.RoundEnd();
        StartTurn();
    }

    private void StartTurn()
    {
        turnCounter ++;
        activePlayer = GetOpponent(activePlayer);
        UIManager.Instance.SwitchPlayerTurn(activePlayer.playerSide);
        actionTaken = false;
        eventDispatcher.RoundStart(turnCounter);
    }

    public void OnSkipTurn()
    {
        CombatResolution();
    }

    Player GetOpponent(Player player)
    {
        return player.playerSide == PlayerSide.Left ? playerRight : playerLeft;
    }


    //TODO Temporary Testing methods
    private void SetUpTestEnvironment()
    {
        this.gameObject.AddComponent<WebSocketServerBehaviour>();
        //Create mock players and assign them to the server's connected players list
        PlayerData player1 = new PlayerData(1, "testLeft");
        PlayerData player2 = new PlayerData(2, "testRight");
        player1.resonances = new List<ResonanceType> { ResonanceType.Fire, ResonanceType.Death, ResonanceType.Spirit };
        player2.resonances = new List<ResonanceType>
            { ResonanceType.Wind, ResonanceType.Darkness, ResonanceType.Light };
        WebSocketServerBehaviour.Instance.ConnectedPlayers.Add(player1);
        WebSocketServerBehaviour.Instance.ConnectedPlayers.Add(player2);
        playerLeft.DrawCard(3);
        playerRight.DrawCard(3);
    }

    public void TestAddCardLeft()
    {
        // string cardName = $"TestCard{new Random().Next(1, 4)}";
        string cardName = $"TestCard1";
        Debug.Log("playing card: " + cardName);
        HandlePlayerPlayCard(cardName);
    }

    public void TestAddCardRight()
    {
        //   string cardName = $"TestCard{new Random().Next(4, 7)}";
        string cardName = $"TestCard5";
        Debug.Log("playing card: " + cardName);
        HandlePlayerPlayCard(cardName);
    }
}