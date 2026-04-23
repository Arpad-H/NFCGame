using GameSystems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardVisualizer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SpriteRenderer tokenImage;
    public TextMeshProUGUI HPText;
    public TextMeshProUGUI AttackText;
    
    private CardData data;
    private PlayerSide side;

    public void Setup(CardData data, PlayerSide side)
    {
        this.data = data;
        this.side = side;
        tokenImage.sprite = data.artwork;
        if (data.cardType is MinionType)
        {
            HPText.text = ((MinionType)data.cardType).health.ToString();
            AttackText.text = ((MinionType)data.cardType).attack.ToString();
        }
       
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (data != null)
        {
            CardPreviewUI.Instance.Show(data, this.gameObject, side);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CardPreviewUI.Instance.Hide();
    }
}