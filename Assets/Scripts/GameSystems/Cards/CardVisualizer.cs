using GameSystems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardVisualizer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SpriteRenderer tokenImage;
    public TextMeshProUGUI HPText;
    public TextMeshProUGUI AttackText;

    public Image crown; //TODO temporary for debugging
    public Image core;
    public Image root;

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
        if (PlayerSide.Left == side)
        {
            crown.color = instance.isFieldCovered[0] ? Color.red : Color.green;
            core.color = instance.isFieldCovered[1] ? Color.red : Color.green;
            root.color = instance.isFieldCovered[2] ? Color.red : Color.green;
        }
        else //flip bcs mirrored
        {
            crown.color = instance.isFieldCovered[2] ? Color.red : Color.green;
            core.color = instance.isFieldCovered[1] ? Color.red : Color.green;
            root.color = instance.isFieldCovered[0] ? Color.red : Color.green;
        }
    }
}