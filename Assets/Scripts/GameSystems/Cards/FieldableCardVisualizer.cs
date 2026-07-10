using System.Collections;
using System.Collections.Generic;
using GameSystems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Full card view for fieldable cards — minions and items. Carries the stats,
// the two activating runes (with glow), and the passive/effect1/effect2 text
// and icons. Spells use SpellCardVisualizer instead; this class is never given
// a spell.
public class FieldableCardVisualizer : CardVisualizer
{
    public TextMeshProUGUI HPText;
    public TextMeshProUGUI AttackText;

    public TextMeshProUGUI PassiveText;
    public TextMeshProUGUI Effect1Text;
    public TextMeshProUGUI Effect2Text;

    public Image rune1;
    public Image rune1Glow;
    public Image rune2;
    public Image rune2Glow;
    public RuneIconLibrary runeIcons;

    public Image attackIcon;
    public Image hpIcon;

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

    [Header("Effect text state")]
    public Color activeEffectTextColor = Color.white;
    public Color inactiveEffectTextColor = new Color(0.55f, 0.55f, 0.6f, 0.65f);
    public bool boldWhenActive = true;

    // Activating runes backing the effect icons, so they can swap between
    // inactive/glowing sprites the same way rune1/rune2 do.
    private GameSystems.Rune effect1Rune = GameSystems.Rune.None;
    private GameSystems.Rune effect2Rune = GameSystems.Rune.None;
    private readonly Dictionary<Image, Coroutine> effectIconRoutines = new();
    private readonly Dictionary<TextMeshProUGUI, Coroutine> effectTextRoutines = new();

    protected override void PopulateFromInstance(FieldableCardInstance fieldableCardInstance)
    {
        CardData sourceCard = fieldableCardInstance.SourceCard;

        if (sourceCard.cardType is FieldableCardType fieldableCardType)
        {
            PassiveText.text = CardTextFormatter.Format(fieldableCardType.passiveDescription);
            Effect1Text.text = CardTextFormatter.Format(fieldableCardType.effect1Description);
            Effect2Text.text = CardTextFormatter.Format(fieldableCardType.effect2Description);
            // On the board an effect stays dimmed until its field activates.
            SetRuneIcons(fieldableCardType, differentiateInactive: true);
        }
        if (sourceCard.cardType is MinionType minionDef)
        {
            HPText.gameObject.SetActive(true);
            AttackText.gameObject.SetActive(true);
            HPText.text = minionDef.baseHealth.ToString();
            AttackText.text = minionDef.baseAttack.ToString();
        }
        else if (sourceCard.cardType is ItemType itemType)
        {
            HPText.gameObject.SetActive(false);
            AttackText.gameObject.SetActive(false);
            SetStatSlotRunes(itemType);
        }
        if (side == PlayerSide.Right)
            SwapStatRunePositions();
    }

    protected override void PopulateFromLibrary(CardData sourceCard)
    {
        if (sourceCard.cardType is FieldableCardType fieldableCardType)
        {
            PassiveText.text = CardTextFormatter.Format(fieldableCardType.passiveDescription);
            Effect1Text.text = CardTextFormatter.Format(fieldableCardType.effect1Description);
            Effect2Text.text = CardTextFormatter.Format(fieldableCardType.effect2Description);
            // Library / exporter have no game state, so show every effect as active
            // (undimmed) to keep the text easy to read.
            SetRuneIcons(fieldableCardType, differentiateInactive: false);
        }
        if (sourceCard.cardType is MinionType minionDef)
        {
            HPText.gameObject.SetActive(true);
            AttackText.gameObject.SetActive(true);
            HPText.text = minionDef.baseHealth.ToString();
            AttackText.text = minionDef.baseAttack.ToString();
        }
        else if (sourceCard.cardType is ItemType itemType)
        {
            HPText.gameObject.SetActive(false);
            AttackText.gameObject.SetActive(false);
            SetStatSlotRunes(itemType);
        }
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

    private void SetRuneIcons(FieldableCardType cardType, bool differentiateInactive)
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

        // The effect icons mirror the runes: they start on the inactive sprite
        // and glow up to the glowing sprite when the matching field activates.
        effect1Rune = r1;
        effect2Rune = r2;
        SetupEffectIcon(effect1, r1);
        SetupEffectIcon(effect2, r2);

        // On the board, text starts dimmed alongside the inactive rune sprite and
        // an effect with no activating rune is always on, so it keeps the active
        // colour. When we're not differentiating (library / exporter) every effect
        // shows as active regardless of its activating rune.
        SetEffectTextActive(Effect1Text, !differentiateInactive || r1 == GameSystems.Rune.None, true);
        SetEffectTextActive(Effect2Text, !differentiateInactive || r2 == GameSystems.Rune.None, true);
        SetEffectTextActive(PassiveText, true, true);

        if (passive != null) passive.enabled = false; // passive has no activating rune
    }

    private void SetupEffectIcon(Image icon, GameSystems.Rune rune)
    {
        if (icon == null) return;
        if (rune == GameSystems.Rune.None)
        {
            icon.enabled = false;
            return;
        }
        icon.enabled = true;
        icon.sprite = runeIcons.GetIcon(rune);
        icon.color = Color.white;
    }

    public void UpdateStatsDisplay(int newHealth, int newAttack)
    {
        HPText.text = newHealth.ToString();
        AttackText.text = newAttack.ToString();
    }

    public void UpdateFieldCoverDisplay()
    {
        SetEffectIconActive(effect1, effect1Rune, instance.IsFieldActive[1]);
        SetEffectIconActive(effect2, effect2Rune, instance.IsFieldActive[2]);

        if (effect1Rune != GameSystems.Rune.None) SetEffectTextActive(Effect1Text, instance.IsFieldActive[1]);
        if (effect2Rune != GameSystems.Rune.None) SetEffectTextActive(Effect2Text, instance.IsFieldActive[2]);

        SetGlowActive(rune1Glow, instance.IsFieldActive[1]);
        SetGlowActive(rune2Glow, instance.IsFieldActive[2]);
    }

    private void SetEffectTextActive(TextMeshProUGUI text, bool active, bool instant = false)
    {
        if (text == null) return;

        if (boldWhenActive)
        {
            text.fontStyle = active
                ? text.fontStyle | FontStyles.Bold
                : text.fontStyle & ~FontStyles.Bold;
        }

        Color target = active ? activeEffectTextColor : inactiveEffectTextColor;

        if (effectTextRoutines.TryGetValue(text, out Coroutine running) && running != null)
            StopCoroutine(running);

        if (instant || !isActiveAndEnabled)
        {
            text.color = target;
            effectTextRoutines[text] = null;
            return;
        }
        effectTextRoutines[text] = StartCoroutine(FadeTextColor(text, target));
    }

    private IEnumerator FadeTextColor(TextMeshProUGUI text, Color target)
    {
        const float duration = 0.25f;
        Color from = text.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            text.color = Color.Lerp(from, target, elapsed / duration);
            yield return null;
        }
        text.color = target;
    }

    private void SetEffectIconActive(Image icon, GameSystems.Rune rune, bool active)
    {
        if (icon == null || !icon.enabled || rune == GameSystems.Rune.None) return;
        Sprite target = active ? runeIcons.GetGlowIcon(rune) : runeIcons.GetIcon(rune);
        if (target == null) return;

        if (effectIconRoutines.TryGetValue(icon, out Coroutine running) && running != null)
            StopCoroutine(running);
        effectIconRoutines[icon] = StartCoroutine(SwapEffectIcon(icon, target));
    }

    private IEnumerator SwapEffectIcon(Image icon, Sprite target)
    {
        const float fade = 0.2f;
        if (icon.sprite != target)
        {
            yield return FadeImageAlpha(icon, icon.color.a, 0f, fade);
            icon.sprite = target;
            yield return FadeImageAlpha(icon, 0f, 1f, fade);
        }
        else
        {
            yield return FadeImageAlpha(icon, icon.color.a, 1f, fade);
        }
    }

    private IEnumerator FadeImageAlpha(Image img, float from, float to, float duration)
    {
        Color c = img.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(from, to, elapsed / duration);
            img.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }
        img.color = new Color(c.r, c.g, c.b, to);
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
