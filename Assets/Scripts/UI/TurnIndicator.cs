using GameSystems;
using UnityEngine;

public class TurnIndicator : MonoBehaviour
{
    public GameObject leftTurnIndicator;
    public GameObject rightTurnIndicator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
   public void SwitchTurn(PlayerSide newPlayersTurn)
    {
        if (newPlayersTurn == PlayerSide.Left)
        {
            leftTurnIndicator.SetActive(true);
            rightTurnIndicator.SetActive(false);
        }
        else
        {
            leftTurnIndicator.SetActive(false);
            rightTurnIndicator.SetActive(true);
        }
    }
}
