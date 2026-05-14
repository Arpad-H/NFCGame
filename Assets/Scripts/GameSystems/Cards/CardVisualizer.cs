using System.Collections.Generic;
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

    public GameObject statusEffectContainer;
    public GameObject statusEffectPrefab;
    private Dictionary<StatusEffectInstance, StatusEffectIcon> statusEffectMap = new();
    
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

    public void UpdateStatsDisplay(int newHealth,int newAttack)
    {
        HPText.text = newHealth.ToString();
        AttackText.text = newAttack.ToString();
    }

    public void UpdateFieldCoverDisplay()
    {
            passive.color = instance.IsFieldActive[0] ? Color.green : Color.red;
            effect1.color = instance.IsFieldActive[1] ? Color.green : Color.red;
            effect2.color = instance.IsFieldActive[2] ? Color.green : Color.red;
    }

    public void ApplyStatusEffect(StatusEffectInstance statusEffect)
    {
        // Prevent duplicate icons for the same instance
        if (statusEffectMap.ContainsKey(statusEffect)) return;

        GameObject iconObj = Instantiate(statusEffectPrefab, statusEffectContainer.transform);
        StatusEffectIcon iconScript = iconObj.GetComponent<StatusEffectIcon>();
        
        if (iconScript != null)
        {
            iconScript.Setup(statusEffect.Data);
            statusEffectMap.Add(statusEffect, iconScript);
        }
    }

    public void RemoveStatusEffect(StatusEffectInstance statusEffect)
    { 
        if (statusEffectMap.TryGetValue(statusEffect, out StatusEffectIcon icon))
        {
            statusEffectMap.Remove(statusEffect);
            Destroy(icon.gameObject);
        }
    }
    
    public void ClearAllStatusEffects()
    {
        foreach (var icon in statusEffectMap.Values)
        {
            Destroy(icon.gameObject);
        }
        statusEffectMap.Clear();
    }
}