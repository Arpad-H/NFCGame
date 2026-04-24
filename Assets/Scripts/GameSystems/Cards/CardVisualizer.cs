using GameSystems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardVisualizer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SpriteRenderer tokenImage;
    public TextMeshProUGUI HPText;
    public TextMeshProUGUI AttackText;
    
    private FieldableCardContext context;
    private PlayerSide side;

    public void Setup(FieldableCardContext fieldableCardContext, PlayerSide playerSide)
    {
       context = fieldableCardContext;
        side = side;
        tokenImage.sprite = fieldableCardContext.SourceCard.artwork;
        if (fieldableCardContext.SourceCard.cardType is MinionType)
        {
            HPText.text = ((MinionType)fieldableCardContext.SourceCard.cardType).health.ToString();
            AttackText.text = ((MinionType)fieldableCardContext.SourceCard.cardType).attack.ToString();
        }
       
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (context != null)
        {
            CardPreviewUI.Instance.Show(context, this.gameObject, side);
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
}