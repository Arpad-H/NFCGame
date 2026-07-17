using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One target inside a history tile's DamageTargetContainer: the target's
// portrait plus an amount badge (icon + number). HistoryTile spawns one of
// these per affected target, so single- and multi-target entries share the
// exact same path — the vertical container just grows with the target count.
public class DamageTargetView : MonoBehaviour
{
    [Tooltip("Target portrait. Hidden when the target has no artwork (e.g. a player face hit).")]
    [SerializeField] private Image portrait;
    [Tooltip("Icon + number badge, toggled together. Hidden when this target took no amount (a miss).")]
    [SerializeField] private GameObject amountGroup;
    [Tooltip("Damage / healing number for this target.")]
    [SerializeField] private TextMeshProUGUI amountText;

    // Populate this view from a target and the amount it took. amount <= 0
    // (e.g. a miss) hides the badge so only the portrait shows.
    public void Set(HistoryActor actor, int amount)
    {
        if (portrait != null)
        {
            portrait.sprite = actor.Icon;
            portrait.enabled = actor.Icon != null; // players have no art
        }

        bool showAmount = amount > 0;
        if (amountText != null) amountText.text = amount.ToString();
        if (amountGroup != null) amountGroup.SetActive(showAmount);
        else if (amountText != null) amountText.gameObject.SetActive(showAmount);
    }
}
