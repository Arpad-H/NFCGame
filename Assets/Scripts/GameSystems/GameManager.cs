using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;
using Random = System.Random;


public class GameManager : MonoBehaviour
{
    Board board;
    PlayerSide activePlayer;
    public int maxCardsPerPortal = 5;
    
    private async void Awake()
    {
        await CardLibrary.Initialize();
        Debug.Log("CardLibrary ready.");
        board = new Board();
        board.SetUpBoard(maxCardsPerPortal);
        activePlayer = new Random().Next(0, 2) == 0 ? PlayerSide.Left : PlayerSide.Right;
        if (WebSocketServerBehaviour.Instance ==
            null)
        {
            SetUpTestEnvironment();
        }

        WebSocketServerBehaviour.Instance.UpdateGameManagerReference(this);
    }


    private void Start()
    {
        UIManager.Instance.SwitchPlayerTurn(activePlayer);
    }

    public void HandlePlayerPlayCard(string cardName)
    {
        CardData card = CardLibrary.GetCard(cardName);
        if (card == null)
        {
            Debug.LogError($"Card: {cardName} not found in library!");
            return;
        }

        CardContext context = new CardContext().SetBoard(board)
            .SetSourceCard(card)
            .SetOwner(activePlayer)
            .SetOpponent(activePlayer);

        if (board.PlaceCard(activePlayer, context))
        {
            NextTurn();
            return;
        }

        Debug.Log("invalid play, try again");
    }

    PlayerSide GetOpponent(PlayerSide activePlayer)
    {
        return activePlayer == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left;
    }

    private void NextTurn()
    {
        activePlayer = activePlayer == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left;
        UIManager.Instance.SwitchPlayerTurn(activePlayer);
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
    }

    public void TestAddCardLeft()
    {
        string cardName = $"TestCard{new Random().Next(1, 4)}";
        Debug.Log("playing card: " + cardName);
        HandlePlayerPlayCard(cardName);
    }

    public void TestAddCardRight()
    {
        string cardName = $"TestCard{new Random().Next(4, 7)}";
        Debug.Log("playing card: " + cardName);
        HandlePlayerPlayCard(cardName);
    }
}