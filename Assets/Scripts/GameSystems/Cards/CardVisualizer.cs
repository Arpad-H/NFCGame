using GameSystems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardVisualizer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SpriteRenderer tokenImage;
    public TextMeshProUGUI HPText;
    public TextMeshProUGUI AttackText;
    
    private FieldableCardInstance _instance;
    private PlayerSide side;

    public void Setup(FieldableCardInstance fieldableCardInstance, PlayerSide playerSide)
    {
       _instance = fieldableCardInstance;
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
        if (_instance != null)
        {
            CardPreviewUI.Instance.Show(_instance, this.gameObject, side);
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