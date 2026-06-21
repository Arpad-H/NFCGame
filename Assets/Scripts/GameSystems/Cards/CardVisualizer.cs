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
    
  
    public Image rune1;
    public Image rune2;
    public RuneIconLibrary runeIcons;

    public RectTransform attackContainer;
    public RectTransform hpContainer;
    public RectTransform rune1Container;
    public RectTransform rune2Container;
    
    public Image passive;
    public Image effect1;
    public Image effect2; 

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
            SetRuneIcons(fieldableCardType);
        }
        if (fieldableCardInstance.SourceCard.cardType is MinionType minionDef)
        {
            HPText.text = minionDef.baseHealth.ToString();
            AttackText.text = minionDef.baseAttack.ToString();
        }
        if (playerSide == PlayerSide.Right)
            SwapStatRunePositions();
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
            SetRuneIcons(fieldableCardType);
        }
        if (sourceCard.cardType is MinionType minionDef)
        {
            HPText.text = minionDef.baseHealth.ToString();
            AttackText.text = minionDef.baseAttack.ToString();
        }
    }

    private void SwapStatRunePositions()
    {
        if (attackContainer != null && rune1Container != null)
        {
            float tmp = attackContainer.anchoredPosition.x;
            attackContainer.anchoredPosition = new Vector2(rune1Container.anchoredPosition.x, attackContainer.anchoredPosition.y);
            rune1Container.anchoredPosition = new Vector2(tmp, rune1Container.anchoredPosition.y);
        }
        if (hpContainer != null && rune2Container != null)
        {
            float tmp = hpContainer.anchoredPosition.x;
            hpContainer.anchoredPosition = new Vector2(rune2Container.anchoredPosition.x, hpContainer.anchoredPosition.y);
            rune2Container.anchoredPosition = new Vector2(tmp, rune2Container.anchoredPosition.y);
        }
    }

    private void SetRuneIcons(FieldableCardType cardType)
    {
        if (runeIcons == null) return;
        var r1 = cardType.effectActivatingRunes.Length > 0 ? cardType.effectActivatingRunes[0] : GameSystems.Rune.None;
        var r2 = cardType.effectActivatingRunes.Length > 1 ? cardType.effectActivatingRunes[1] : GameSystems.Rune.None;
        rune1.sprite = runeIcons.GetIcon(r1);
        rune1.enabled = r1 != GameSystems.Rune.None;
        rune2.sprite = runeIcons.GetIcon(r2);
        rune2.enabled = r2 != GameSystems.Rune.None;
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