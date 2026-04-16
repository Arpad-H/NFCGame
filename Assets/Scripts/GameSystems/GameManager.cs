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
            board = new  Board();
            board.SetUpBoard();
            currentPlayersTurn = new Random().Next(0, 2) == 0 ? PlayerSide.Left : PlayerSide.Right;
        }

        public void PlaceCard(int cardId)
        {
            board.PlaceCard(currentPlayersTurn,cardId);
        }
        
    }
