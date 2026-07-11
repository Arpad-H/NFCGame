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
//
// The runes and the stat block live on the card EDGE, so their artwork and
// position depend on which side of the board the card belongs to AND on whether
// the card is a minion (two effect runes + HP/Attack) or an item (two effect
// runes + two supplied runes — four runes, no stats). Rather than nudging one
// set of objects around and swapping stats for runes (which left the half-hexagon
// art misaligned and pushed the stats too close to the edge), the prefab holds
// four complete, pre-aligned layouts — minion/item × left/right — and we simply
// activate the matching one. Each layout draws its runes with the matching
// half-hexagon variant: left layouts use v1, right layouts use v2.
public class FieldableCardVisualizer : CardVisualizer
{
    // Shared base for one (card type × side) layout: the container to toggle plus
    // the two effect-activating runes (with glow) that both minions and items show
    // on the card edge. Only the active layout's container is shown.
    [System.Serializable]
    public class CardLayout
    {
        [Tooltip("Root object toggled on when this layout is the active one.")]
        public GameObject container;

        [Tooltip("The two effect-activating runes on the card edge. They use the " +
                 "matching half-hexagon variant (left = v1, right = v2) and glow " +
                 "when their field activates.")]
        public Image rune1;
        public Image rune1Glow;
        public Image rune2;
        public Image rune2Glow;
    }

    // Minions add the HP/Attack values to the two effect runes.
    [System.Serializable]
    public class MinionLayout : CardLayout
    {
        public TextMeshProUGUI HPText;
        public TextMeshProUGUI AttackText;
    }

    // Items add two supplied activator runes (the runes this item hands to its
    // neighbours) to the two effect runes — four runes total, no stats. Supplied
    // runes use the same half-hexagon variant as the side but never glow.
    [System.Serializable]
    public class ItemLayout : CardLayout
    {
        public Image suppliedRune1;
        public Image suppliedRune2;
    }

    [Header("Layouts (toggled by card type + PlayerSide)")]
    [Tooltip("Minion on the LEFT side. Runes use the v1 (left half) art.")]
    public MinionLayout minionLeftLayout;
    [Tooltip("Minion on the RIGHT side. Runes use the v2 (right half) art.")]
    public MinionLayout minionRightLayout;
    [Tooltip("Item on the LEFT side. Runes use the v1 (left half) art.")]
    public ItemLayout itemLeftLayout;
    [Tooltip("Item on the RIGHT side. Runes use the v2 (right half) art.")]
    public ItemLayout itemRightLayout;

    [Header("Shared effect column (not flipped)")]
    public TextMeshProUGUI PassiveText;
    public TextMeshProUGUI Effect1Text;
    public TextMeshProUGUI Effect2Text;

    public Image passive;
    public Image effect1;
    public Image effect2;

    public RuneIconLibrary runeIcons;

    public GameObject statusEffectContainer;
    public GameObject statusEffectPrefab;
    private Dictionary<StatusEffectInstance, StatusEffectIcon> statusEffectMap = new();

    [Header("Effect text state")]
    public Color activeEffectTextColor = Color.white;
    public Color inactiveEffectTextColor = new Color(0.55f, 0.55f, 0.6f, 0.65f);
    public bool boldWhenActive = true;

    // The layout currently shown; picked from card type + `side` in ResolveActiveLayout.
    // A MinionLayout or ItemLayout depending on the card.
    private CardLayout active;

    // Activating runes backing the effect icons, so they can swap between
    // inactive/glowing sprites the same way rune1/rune2 do.
    private GameSystems.Rune effect1Rune = GameSystems.Rune.None;
    private GameSystems.Rune effect2Rune = GameSystems.Rune.None;
    private readonly Dictionary<Image, Coroutine> effectIconRoutines = new();
    private readonly Dictionary<TextMeshProUGUI, Coroutine> effectTextRoutines = new();

    protected override void PopulateFromInstance(FieldableCardInstance fieldableCardInstance)
    {
        CardData sourceCard = fieldableCardInstance.SourceCard;
        ResolveActiveLayout(sourceCard.cardType is ItemType);

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
            SetStatValues(minionDef.baseHealth, minionDef.baseAttack);
        }
        else if (sourceCard.cardType is ItemType itemType)
        {
            SetSuppliedRunes(itemType);
        }
    }

    protected override void PopulateFromLibrary(CardData sourceCard)
    {
        ResolveActiveLayout(sourceCard.cardType is ItemType);

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
            SetStatValues(minionDef.baseHealth, minionDef.baseAttack);
        }
        else if (sourceCard.cardType is ItemType itemType)
        {
            SetSuppliedRunes(itemType);
        }
    }

    // Show the layout matching this card's type + side and hide the other three.
    // Every side-dependent read afterwards goes through `active`, so all the
    // rune/stat wiring lives in one place. Library / exporter have no side and
    // default to the left (v1) layout.
    private void ResolveActiveLayout(bool isItem)
    {
        bool onRight = side == PlayerSide.Right;
        active = isItem
            ? (onRight ? itemRightLayout : itemLeftLayout)
            : (onRight ? minionRightLayout : minionLeftLayout);

        SetContainerActive(minionLeftLayout, active == minionLeftLayout);
        SetContainerActive(minionRightLayout, active == minionRightLayout);
        SetContainerActive(itemLeftLayout, active == itemLeftLayout);
        SetContainerActive(itemRightLayout, active == itemRightLayout);
    }

    private static void SetContainerActive(CardLayout layout, bool on)
    {
        if (layout != null && layout.container != null)
            layout.container.SetActive(on);
    }

    private void SetStatValues(int hp, int atk)
    {
        if (!(active is MinionLayout m)) return;
        if (m.HPText != null) m.HPText.text = hp.ToString();
        if (m.AttackText != null) m.AttackText.text = atk.ToString();
    }

    // Items show two supplied activator runes alongside their two effect runes.
    private void SetSuppliedRunes(ItemType itemType)
    {
        if (runeIcons == null || !(active is ItemLayout it)) return;
        var r0 = itemType.suppliedActivatorRunes.Length > 0 ? itemType.suppliedActivatorRunes[0] : GameSystems.Rune.None;
        var r1 = itemType.suppliedActivatorRunes.Length > 1 ? itemType.suppliedActivatorRunes[1] : GameSystems.Rune.None;

        if (it.suppliedRune1 != null)
        {
            it.suppliedRune1.sprite = runeIcons.GetIcon(r0, side);
            it.suppliedRune1.enabled = r0 != GameSystems.Rune.None;
        }
        if (it.suppliedRune2 != null)
        {
            it.suppliedRune2.sprite = runeIcons.GetIcon(r1, side);
            it.suppliedRune2.enabled = r1 != GameSystems.Rune.None;
        }
    }

    private void SetRuneIcons(FieldableCardType cardType, bool differentiateInactive)
    {
        if (runeIcons == null || active == null) return;
        var r1 = cardType.effectActivatingRunes.Length > 0 ? cardType.effectActivatingRunes[0] : GameSystems.Rune.None;
        var r2 = cardType.effectActivatingRunes.Length > 1 ? cardType.effectActivatingRunes[1] : GameSystems.Rune.None;

        // Edge runes pick the half-hexagon variant that matches the active side.
        if (active.rune1 != null)
        {
            active.rune1.sprite = runeIcons.GetIcon(r1, side);
            active.rune1.enabled = r1 != GameSystems.Rune.None;
        }
        if (active.rune2 != null)
        {
            active.rune2.sprite = runeIcons.GetIcon(r2, side);
            active.rune2.enabled = r2 != GameSystems.Rune.None;
        }

        if (active.rune1Glow != null)
        {
            active.rune1Glow.sprite = runeIcons.GetGlowIcon(r1, side);
            active.rune1Glow.enabled = r1 != GameSystems.Rune.None;
            active.rune1Glow.color = new Color(1f, 1f, 1f, 0f);
        }
        if (active.rune2Glow != null)
        {
            active.rune2Glow.sprite = runeIcons.GetGlowIcon(r2, side);
            active.rune2Glow.enabled = r2 != GameSystems.Rune.None;
            active.rune2Glow.color = new Color(1f, 1f, 1f, 0f);
        }

        // The effect icons mirror the runes: they start on the inactive sprite
        // and glow up to the glowing sprite when the matching field activates.
        // They live in the centered text column and use the full-hexagon art.
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
        SetStatValues(newHealth, newAttack);
    }

    public void UpdateFieldCoverDisplay()
    {
        if (active == null) return;
        SetEffectIconActive(effect1, effect1Rune, instance.IsFieldActive[1]);
        SetEffectIconActive(effect2, effect2Rune, instance.IsFieldActive[2]);

        if (effect1Rune != GameSystems.Rune.None) SetEffectTextActive(Effect1Text, instance.IsFieldActive[1]);
        if (effect2Rune != GameSystems.Rune.None) SetEffectTextActive(Effect2Text, instance.IsFieldActive[2]);

        SetGlowActive(active.rune1Glow, instance.IsFieldActive[1]);
        SetGlowActive(active.rune2Glow, instance.IsFieldActive[2]);
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
