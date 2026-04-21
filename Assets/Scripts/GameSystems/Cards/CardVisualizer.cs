using GameSystems;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardVisualizer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SpriteRenderer tokenImage;
    
    private CardData data;
    private PlayerSide side;

    public void Setup(CardData data, PlayerSide side)
    {
        this.data = data;
        this.side = side;
        tokenImage.sprite = data.artwork;
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