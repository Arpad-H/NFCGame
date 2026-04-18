using System;
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
        WebSocketServerBehaviour.Instance.UpdateGameManagerReference(this);
      
    }

    private void Start()
    {
        UIManager.Instance.SwitchPlayerTurn(currentPlayersTurn);
    }

    public void HandlePlayerPlayCard(int cardId)
    {
        if (board.PlaceCard(currentPlayersTurn, cardId)) NextTurn();
        Debug.Log("invalid play, try again");
    }

    private void NextTurn()
    {
        currentPlayersTurn = currentPlayersTurn == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left;
        UIManager.Instance.SwitchPlayerTurn(currentPlayersTurn);
    }
}