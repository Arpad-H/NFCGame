using System.Collections;
using System.Collections.Generic;
using GameSystems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// In-game representation of a fielded card (the "board token"). A stripped-down
// sibling of CardVisualizer: it shows only what a card needs while it sits on
// the board — artwork, the two activating runes (with their glow), and a
// minion's attack/HP values. Everything a player needs to read in full (name,
// passive/effect text, keywords) is surfaced on hover through CardPreviewUI,
// so none of that lives on the token itself.
//
// CardVisualizer is still the full-card view used by the library, the hover
// preview source, the card exporter, and the spell-cast animation; this class
// deliberately does not replace it.
public class BoardTokenVisualizer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image tokenImage;
    public TextMeshProUGUI HPText;
    public TextMeshProUGUI AttackText;

    public Image rune1;
    public Image rune1Glow;
    public Image rune2;
    public Image rune2Glow;
    public RuneIconLibrary runeIcons;

    // Items carry no attack/HP, so their stat slots instead show the runes the
    // item supplies to the card it covers.
    public Image attackIcon;
    public Image hpIcon;

    public RectTransform attackContainer;
    public RectTransform hpContainer;
    public RectTransform rune1Container;
    public RectTransform rune2Container;

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

        if (fieldableCardInstance.SourceCard.cardType is FieldableCardType fieldableCardType)
        {
            SetRuneIcons(fieldableCardType);
        }
        if (fieldableCardInstance.SourceCard.cardType is MinionType minionDef)
        {
            HPText.gameObject.SetActive(true);
            AttackText.gameObject.SetActive(true);
            HPText.text = minionDef.baseHealth.ToString();
            AttackText.text = minionDef.baseAttack.ToString();
        }
        else if (fieldableCardInstance.SourceCard.cardType is ItemType itemType)
        {
            HPText.gameObject.SetActive(false);
            AttackText.gameObject.SetActive(false);
            SetStatSlotRunes(itemType);
        }
        else if (fieldableCardInstance.SourceCard.cardType is SpellType)
        {
            HPText.gameObject.SetActive(false);
            AttackText.gameObject.SetActive(false);
            HideStatAndRuneSlots();
        }
        if (playerSide == PlayerSide.Right)
            MirrorStatRuneLayout();
    }

    private void SetStatSlotRunes(ItemType itemType)
    {
        if (runeIcons == null) return;
        var r0 = itemType.suppliedActivatorRunes.Length > 0 ? itemType.suppliedActivatorRunes[0] : GameSystems.Rune.None;
        var r1 = itemType.suppliedActivatorRunes.Length > 1 ? itemType.suppliedActivatorRunes[1] : GameSystems.Rune.None;

        if (attackIcon != null)
        {
            attackIcon.sprite = runeIcons.GetIcon(r0);
            attackIcon.enabled = r0 != GameSystems.Rune.None;
        }
        if (hpIcon != null)
        {
            hpIcon.sprite = runeIcons.GetIcon(r1);
            hpIcon.enabled = r1 != GameSystems.Rune.None;
        }
    }

    private void HideStatAndRuneSlots()
    {
        if (attackIcon != null) attackIcon.enabled = false;
        if (hpIcon != null) hpIcon.enabled = false;
        if (rune1 != null) rune1.enabled = false;
        if (rune2 != null) rune2.enabled = false;
        if (rune1Glow != null) rune1Glow.enabled = false;
        if (rune2Glow != null) rune2Glow.enabled = false;
    }

    // The prefab is authored for a left-side token; a right-side token is its
    // mirror image. The four stat/rune slots share a parent centred on the token
    // (anchored x = 0 is the centre line), so reflecting each slot's anchored x
    // flips the whole layout to the other side while keeping every icon+number
    // group intact and at its original height.
    //
    // (CardVisualizer instead swaps x between attack<->rune1 and hp<->rune2,
    // which only mirrors correctly on CardV2's symmetric layout; this token's
    // slots don't sit at mirror-image positions, so that swap stacks the stats
    // and scatters the runes.)
    private void MirrorStatRuneLayout()
    {
        ReflectAnchoredX(attackContainer);
        ReflectAnchoredX(hpContainer);
        ReflectAnchoredX(rune1Container);
        ReflectAnchoredX(rune2Container);
    }

    private static void ReflectAnchoredX(RectTransform rect)
    {
        if (rect == null) return;
        Vector2 p = rect.anchoredPosition;
        rect.anchoredPosition = new Vector2(-p.x, p.y);
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

        if (rune1Glow != null)
        {
            rune1Glow.sprite = runeIcons.GetGlowIcon(r1);
            rune1Glow.enabled = r1 != GameSystems.Rune.None;
            rune1Glow.color = new Color(1f, 1f, 1f, 0f);
        }
        if (rune2Glow != null)
        {
            rune2Glow.sprite = runeIcons.GetGlowIcon(r2);
            rune2Glow.enabled = r2 != GameSystems.Rune.None;
            rune2Glow.color = new Color(1f, 1f, 1f, 0f);
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

    public void UpdateStatsDisplay(int newHealth, int newAttack)
    {
        HPText.text = newHealth.ToString();
        AttackText.text = newAttack.ToString();
    }

    // Pulses the rune glow on/off as the card's effect fields cover/uncover.
    public void UpdateFieldCoverDisplay()
    {
        if (instance == null) return;
        SetGlowActive(rune1Glow, instance.IsFieldActive[1]);
        SetGlowActive(rune2Glow, instance.IsFieldActive[2]);
    }

    private void SetGlowActive(Image glowImage, bool active)
    {
        if (glowImage == null || !glowImage.enabled) return;
        StopCoroutine(nameof(FadeGlow));
        StartCoroutine(FadeGlow(glowImage, active ? 1f : 0f));
    }

    private IEnumerator FadeGlow(Image glowImage, float targetAlpha)
    {
        const float duration = 0.6f;
        float startAlpha = glowImage.color.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            glowImage.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
        glowImage.color = new Color(1f, 1f, 1f, targetAlpha);
    }

    public void ApplyStatusEffect(StatusEffectInstance statusEffect)
    {
        if (statusEffectContainer == null || statusEffectPrefab == null) return;

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
