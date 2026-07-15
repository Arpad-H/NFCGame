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
//
// A token on the right half of the board is the mirror image of one on the
// left. Rather than reflecting positions at runtime, the prefab carries two
// pre-authored, fully mirrored layouts (leftSide / rightSide). Setup activates
// the one matching the token's side and drives only that view; everything after
// (stat updates, rune glow, status effects) targets the same active view.
public class BoardTokenVisualizer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // One complete set of token graphics. The prefab has two of these, laid out
    // as mirror images, so the correct-handed layout is picked instead of being
    // computed. Minions use hpText/attackText; items reuse the attackIcon/hpIcon
    // slots to show their supplied runes; spells hide both.
    [System.Serializable]
    public class TokenSideView
    {
        public GameObject root;

        public Image tokenImage;
        public TextMeshProUGUI hpText;
        public TextMeshProUGUI attackText;

        public Image rune1;
        public Image rune1Glow;
        public Image rune2;
        public Image rune2Glow;

        // Items carry no attack/HP, so their stat slots instead show the runes
        // the item supplies to the card it covers.
        public Image attackIcon;
        public Image hpIcon;

        public GameObject statusEffectContainer;
    }

    public TokenSideView leftSide;
    public TokenSideView rightSide;

    public RuneIconLibrary runeIcons;
    public GameObject statusEffectPrefab;

    private readonly Dictionary<StatusEffectInstance, StatusEffectIcon> statusEffectMap = new();

    private FieldableCardInstance instance;
    private PlayerSide side;

    // The layout activated for this token's side; all runtime updates target it.
    private TokenSideView view;

    private Vector3 baseScale;

    public void Setup(FieldableCardInstance fieldableCardInstance, PlayerSide playerSide)
    {
        instance = fieldableCardInstance;
        side = playerSide;

        // Activate the correctly-handed layout and hide the other.
        bool isRight = playerSide == PlayerSide.Right;
        view = isRight ? rightSide : leftSide;
        if (leftSide?.root != null) leftSide.root.SetActive(!isRight);
        if (rightSide?.root != null) rightSide.root.SetActive(isRight);

        view.tokenImage.sprite = fieldableCardInstance.SourceCard.artwork;

        if (fieldableCardInstance.SourceCard.cardType is FieldableCardType fieldableCardType)
        {
            SetRuneIcons(fieldableCardType);
        }
        if (fieldableCardInstance.SourceCard.cardType is MinionType minionDef)
        {
            view.hpText.gameObject.SetActive(true);
            view.attackText.gameObject.SetActive(true);
            view.hpText.text = minionDef.baseHealth.ToString();
            view.attackText.text = minionDef.baseAttack.ToString();
        }
        else if (fieldableCardInstance.SourceCard.cardType is ItemType itemType)
        {
            view.hpText.gameObject.SetActive(false);
            view.attackText.gameObject.SetActive(false);
            SetStatSlotRunes(itemType);
        }
        else if (fieldableCardInstance.SourceCard.cardType is SpellType)
        {
            view.hpText.gameObject.SetActive(false);
            view.attackText.gameObject.SetActive(false);
            HideStatAndRuneSlots();
        }
    }

    private void SetStatSlotRunes(ItemType itemType)
    {
        if (runeIcons == null) return;
        var r0 = itemType.suppliedActivatorRunes.Length > 0 ? itemType.suppliedActivatorRunes[0] : GameSystems.Rune.None;
        var r1 = itemType.suppliedActivatorRunes.Length > 1 ? itemType.suppliedActivatorRunes[1] : GameSystems.Rune.None;

        if (view.attackIcon != null)
        {
            view.attackIcon.sprite = runeIcons.GetIcon(r0);
            view.attackIcon.enabled = r0 != GameSystems.Rune.None;
        }
        if (view.hpIcon != null)
        {
            view.hpIcon.sprite = runeIcons.GetIcon(r1);
            view.hpIcon.enabled = r1 != GameSystems.Rune.None;
        }
    }

    private void HideStatAndRuneSlots()
    {
        if (view.attackIcon != null) view.attackIcon.enabled = false;
        if (view.hpIcon != null) view.hpIcon.enabled = false;
        if (view.rune1 != null) view.rune1.enabled = false;
        if (view.rune2 != null) view.rune2.enabled = false;
        if (view.rune1Glow != null) view.rune1Glow.enabled = false;
        if (view.rune2Glow != null) view.rune2Glow.enabled = false;
    }

    private void SetRuneIcons(FieldableCardType cardType)
    {
        if (runeIcons == null) return;
        var r1 = cardType.effectActivatingRunes.Length > 0 ? cardType.effectActivatingRunes[0] : GameSystems.Rune.None;
        var r2 = cardType.effectActivatingRunes.Length > 1 ? cardType.effectActivatingRunes[1] : GameSystems.Rune.None;

        view.rune1.sprite = runeIcons.GetIcon(r1);
        view.rune1.enabled = r1 != GameSystems.Rune.None;
        view.rune2.sprite = runeIcons.GetIcon(r2);
        view.rune2.enabled = r2 != GameSystems.Rune.None;

        if (view.rune1Glow != null)
        {
            view.rune1Glow.sprite = runeIcons.GetGlowIcon(r1);
            view.rune1Glow.enabled = r1 != GameSystems.Rune.None;
            view.rune1Glow.color = new Color(1f, 1f, 1f, 0f);
        }
        if (view.rune2Glow != null)
        {
            view.rune2Glow.sprite = runeIcons.GetGlowIcon(r2);
            view.rune2Glow.enabled = r2 != GameSystems.Rune.None;
            view.rune2Glow.color = new Color(1f, 1f, 1f, 0f);
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
        view.hpText.text = newHealth.ToString();
        view.attackText.text = newAttack.ToString();
    }

    // Pulses the rune glow on/off as the card's effect fields cover/uncover.
    public void UpdateFieldCoverDisplay()
    {
        if (instance == null) return;
        SetGlowActive(view.rune1Glow, instance.IsFieldActive[1]);
        SetGlowActive(view.rune2Glow, instance.IsFieldActive[2]);
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
        if (view?.statusEffectContainer == null || statusEffectPrefab == null) return;

        // Prevent duplicate icons for the same instance
        if (statusEffectMap.ContainsKey(statusEffect)) return;

        GameObject iconObj = Instantiate(statusEffectPrefab, view.statusEffectContainer.transform);
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
