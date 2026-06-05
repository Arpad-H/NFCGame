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

    
    public TextMeshProUGUI PassiveText;
    public TextMeshProUGUI Effect1Text;
    public TextMeshProUGUI Effect2Text;
    
    public Image passive;
    public Image effect1;
    public Image effect2; //TODO temporary for debugging

    public GameObject statusEffectContainer;
    public GameObject statusEffectPrefab;
    private Dictionary<StatusEffectInstance, StatusEffectIcon> statusEffectMap = new();
    
    private FieldableCardInstance instance;
    private PlayerSide side;
    
    private Vector3 baseScale;

    public void Setup(FieldableCardInstance fieldableCardInstance, PlayerSide playerSide)
    {
        instance = fieldableCardInstance;
        side = playerSide;
        tokenImage.sprite = fieldableCardInstance.SourceCard.artwork;
        Name.text = fieldableCardInstance.SourceCard.cardName;
       
        if (fieldableCardInstance.SourceCard.cardType is FieldableCardType fieldableCardType)
        {
            PassiveText.text = fieldableCardType.passiveDescription;
            Effect1Text.text = fieldableCardType.effect1Description;
            Effect2Text.text = fieldableCardType.effect2Description;
        }
        if (fieldableCardInstance.SourceCard.cardType is MinionType minionDef)
        {
            HPText.text = minionDef.baseHealth.ToString();
            AttackText.text = minionDef.baseAttack.ToString();
        }
    }
    
    public void SetupForLibrary(CardData sourceCard)
    {
        instance = null; 
        tokenImage.sprite = sourceCard.artwork;
        Name.text = sourceCard.cardName;
       
        if (sourceCard.cardType is FieldableCardType fieldableCardType)
        {
            PassiveText.text = fieldableCardType.passiveDescription;
            Effect1Text.text = fieldableCardType.effect1Description;
            Effect2Text.text = fieldableCardType.effect2Description;
        }
        if (sourceCard.cardType is MinionType minionDef)
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
        
        if (baseScale != Vector3.zero) 
        {
            transform.localScale = baseScale * 1.4f; 
            
            Canvas overrideCanvas = transform.parent.gameObject.AddComponent<Canvas>();
            overrideCanvas.overrideSorting = true;
            overrideCanvas.sortingOrder = 100;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CardPreviewUI.Instance != null)
        {
            CardPreviewUI.Instance.Hide();
        }
        
        if (baseScale != Vector3.zero)
        {
            transform.localScale = baseScale;
            
            Canvas overrideCanvas = transform.parent.GetComponent<Canvas>();
            if (overrideCanvas != null)
            {
                Destroy(overrideCanvas);
            }
        }
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
    
    public void SetBaseScale(Vector3 scale)
    {
        baseScale = scale;
    }
}