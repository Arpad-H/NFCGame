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

    [Header("Coin indicator (replaces the filled-image system when present)")]
    [Tooltip("Optional. When a CoinTurnIndicator exists in the scene the coin drives the " +
             "turn display and the legacy fill/active-image UI below is hidden. Scenes " +
             "without a coin (e.g. the tutorial) fall back to the old behaviour.")]
    [SerializeField] private CoinTurnIndicator coin;

    private void Awake()
    {
        if (coin == null) coin = FindAnyObjectByType<CoinTurnIndicator>();

        // The coin fully replaces the old fill bars / active-image swap — hide them.
        if (coin != null)
        {
            if (leftTurnIndicator != null) leftTurnIndicator.SetActive(false);
            if (rightTurnIndicator != null) rightTurnIndicator.SetActive(false);
        }
    }

    // Reflects the active player's remaining turn time. With a coin this drives the
    // wobble/instability; otherwise it falls back to the filled-image behaviour.
    public void UpdateTimer(PlayerSide side, float fill, bool low)
    {
        if (coin != null)
        {
            coin.SetTimeFraction(fill);
            return;
        }

        Image img = side == PlayerSide.Left ? leftTimerFill : rightTimerFill;
        if (img == null) return;
        img.fillAmount = fill;
        img.color = low ? Color.red : Color.white;
    }

    public void SwitchTurn(PlayerSide newPlayersTurn)
    {
        if (coin != null)
        {
            coin.SwitchTo(newPlayersTurn);
            return;
        }

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
