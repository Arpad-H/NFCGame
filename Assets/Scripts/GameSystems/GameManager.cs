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
    PlayerSide activePlayer;
    public Player playerLeft; //TODO temp player representation
    public Player playerRight;
    public int maxCardsPerPortal = 5;
    public bool shufflePortals = false;
    private bool actionTaken = false;
    private BoardEventDispatcher eventDispatcher;

    private async void Awake()
    {
        await CardLibrary.Initialize();
        Debug.Log("CardLibrary ready.");

        board = new Board();
        board.shufflePortals = shufflePortals;
        board.SetUpBoard(maxCardsPerPortal);
        eventDispatcher = new BoardEventDispatcher(board);
        activePlayer = new Random().Next(0, 2) == 0 ? PlayerSide.Left : PlayerSide.Right;
        UIManager.Instance.SwitchPlayerTurn(activePlayer);
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

        FieldableCardContext context = new FieldableCardContext().SetBoard(board)
            .SetOwner(activePlayer == PlayerSide.Left ? playerLeft : playerRight)
            .SetOpponent(GetOpponent(activePlayer))
            .SetSourceCard(card);

        if (board.PlaceCard(context))
        {
            if (context.cardInstance is IGameEventReceiver receiver)
            {
                receiver.HandleEvent(new GameEvent(GameEventType.OnPlayed, context));
            }

            if (activePlayer == PlayerSide.Left) playerLeft.DiscardCard(1);
            else playerRight.DiscardCard(1);

            actionTaken = true;
            //  StartCoroutine(DelayCombatResolution(2));
            EndTurn();
            return;
        }

        Debug.Log("invalid play, try again");
    }

    // IEnumerator DelayCombatResolution(int i)
    // {
    //     yield return new WaitForSeconds(i);
    //     ResolveCombat();
    // }


    private void EndTurn()
    {
        eventDispatcher.RoundEnd();
        StartTurn();
    }

    private void StartTurn()
    {
        activePlayer = activePlayer == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left;
        UIManager.Instance.SwitchPlayerTurn(activePlayer);
        actionTaken = false;
        eventDispatcher.RoundStart();
    }

    public void OnSkipTurn()
    {
        EndTurn();
    }

    Player GetOpponent(PlayerSide player)
    {
        return player == PlayerSide.Left ? playerRight : playerLeft;
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