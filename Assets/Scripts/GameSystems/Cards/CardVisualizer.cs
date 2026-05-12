using GameSystems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardVisualizer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image tokenImage;
    public TextMeshProUGUI HPText;
    public TextMeshProUGUI AttackText;
    public TextMeshProUGUI Name;

    public Image passive;
    public Image effect1;
    public Image effect2; //TODO temporary for debugging

    private FieldableCardInstance instance;
    private PlayerSide side;

    public void Setup(FieldableCardInstance fieldableCardInstance, PlayerSide playerSide)
    {
        instance = fieldableCardInstance;
        side = playerSide;
        tokenImage.sprite = fieldableCardInstance.SourceCard.artwork;
        if (fieldableCardInstance.SourceCard.cardType is MinionType minionDef)
        {
            HPText.text = minionDef.baseHealth.ToString();
            AttackText.text = minionDef.baseAttack.ToString();
            Name.text = fieldableCardInstance.SourceCard.cardName;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (instance != null)
        {
            CardPreviewUI.Instance.Show(instance, this.gameObject, side);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CardPreviewUI.Instance.Hide();
    }

    public void UpdateHealthDisplay(int newHealth)
    {
        HPText.text = newHealth.ToString();
    }

    public void UpdateFieldCoverDisplay()
    {
            passive.color = instance.isFieldActive[0] ? Color.green : Color.red;
            effect1.color = instance.isFieldActive[1] ? Color.green : Color.red;
            effect2.color = instance.isFieldActive[2] ? Color.green : Color.red;
    }
}