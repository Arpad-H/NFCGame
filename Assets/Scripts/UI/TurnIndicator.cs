using GameSystems;
using UnityEngine;
using UnityEngine.UI;

public class TurnIndicator : MonoBehaviour
{
    public GameObject leftTurnIndicator;
    public GameObject rightTurnIndicator;

    [Header("Turn Timer (filled images)")]
    public Image leftTimerFill;
    public Image rightTimerFill;

    // Reflects the active player's remaining turn time on their indicator's
    // fill image. fill is 0..1; when low the fill turns red, otherwise white.
    public void UpdateTimer(PlayerSide side, float fill, bool low)
    {
        Image img = side == PlayerSide.Left ? leftTimerFill : rightTimerFill;
        if (img == null) return;
        img.fillAmount = fill;
        img.color = low ? Color.red : Color.white;
    }

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
