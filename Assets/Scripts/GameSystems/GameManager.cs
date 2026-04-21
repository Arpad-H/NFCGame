using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;
using Random = System.Random;


public class GameManager : MonoBehaviour
{
    Board board;
    PlayerSide currentPlayersTurn;

    private void Awake()
    {
        board = new Board();
        board.SetUpBoard();
        currentPlayersTurn = new Random().Next(0, 2) == 0 ? PlayerSide.Left : PlayerSide.Right;
        if (WebSocketServerBehaviour.Instance ==
            null)
        {
            SetUpTestEnvironment();
        }
        WebSocketServerBehaviour.Instance.UpdateGameManagerReference(this);
      
    }

   

    private void Start()
    {
        UIManager.Instance.SwitchPlayerTurn(currentPlayersTurn);
    }

    public void HandlePlayerPlayCard(int cardId)
    {
        if (board.PlaceCard(currentPlayersTurn, cardId))
        {
            NextTurn();
            return;
        }
        Debug.Log("invalid play, try again");
    }

    private void NextTurn()
    {
        currentPlayersTurn = currentPlayersTurn == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left;
        UIManager.Instance.SwitchPlayerTurn(currentPlayersTurn);
    }
    
    
    //TODO Temporary Testing methods
    private void SetUpTestEnvironment()
    {
        this.gameObject.AddComponent<WebSocketServerBehaviour>();
        //Create mock players and assign them to the server's connected players list
        PlayerData player1 = new PlayerData( 1,  "testLeft");
        PlayerData player2 = new PlayerData( 2,  "testRight");
        player1.resonances = new List<ResonanceType> { ResonanceType.Fire, ResonanceType.Death, ResonanceType.Spirit };
        player2.resonances = new List<ResonanceType> { ResonanceType.Wind, ResonanceType.Darkness, ResonanceType.Light };
        WebSocketServerBehaviour.Instance.ConnectedPlayers.Add(player1);
        WebSocketServerBehaviour.Instance.ConnectedPlayers.Add(player2);
    }
    public void TestAddCardLeft()
    {
       
        int cardId = new Random().Next(1, 4);
        Debug.Log(cardId);
        HandlePlayerPlayCard(cardId);
    }

    public void TestAddCardRight()
    {
        int cardId = new Random().Next(4, 7);
        Debug.Log(cardId);
        HandlePlayerPlayCard(cardId);
    }
    
}