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
        side = playerSide;
        tokenImage.sprite = fieldableCardContext.SourceCard.artwork;
        if (fieldableCardContext.SourceCard.cardType is MinionType minionDef)
        {
            HPText.text = minionDef.baseHealth.ToString();
            AttackText.text = minionDef.baseAttack.ToString();
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