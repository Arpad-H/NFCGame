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
    // computed. Minions use hpText/attackText; items hide the stat sockets and
    // instead show their supplied runes in this side's own suppliedActivatorRune
    // sockets; spells hide both stats and runes. The opposite layout is always
    // fully hidden.
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

        // Extra rune sockets that only items use, to show the two activator runes
        // the item supplies to the card it covers. Each is a full socket (empty
        // socket + glow + rune icon), mirroring rune1/rune2. Minions and spells
        // supply no runes, so these whole sockets are switched off for them.
        public GameObject suppliedActivatorRune1;
        public GameObject suppliedActivatorRune2;

        // Lights when a passive-field trigger fires. The passive has no rune, so
        // this glow's sprite is authored in the prefab; we only drive tint + alpha.
        public Image passiveGlow;

        // The stat icons drawn beside a minion's attack/HP numbers. Items and
        // spells hide the whole stat sockets, so these aren't driven there.
        public Image attackIcon;
        public Image hpIcon;

        public GameObject statusEffectContainer;
    }

    public TokenSideView leftSide;
    public TokenSideView rightSide;

    public RuneIconLibrary runeIcons;
    public GameObject statusEffectPrefab;

    [Header("Effect-trigger rune glow")]
    [Tooltip("Seconds the rune glow holds at full before fading, after its effect fires.")]
    public float glowHoldDuration = 1.1f;
    [Tooltip("Fade-in time as the glow lights up.")]
    public float glowFadeInDuration = 0.12f;
    [Tooltip("Fade-out time as the glow dies back down.")]
    public float glowFadeOutDuration = 0.45f;

    private readonly Dictionary<StatusEffectInstance, StatusEffectIcon> statusEffectMap = new();

    // Resonance colour the rune glows are tinted with; only alpha is animated.
    private Color glowColor = Color.white;

    // One running flash per glow image, so each glow pulses independently.
    private readonly Dictionary<Image, Coroutine> glowRoutines = new();

    private FieldableCardInstance instance;
    private PlayerSide side;

    // The layout activated for this token's side; all runtime updates target it.
    private TokenSideView view;

    private Vector3 baseScale;

    public void Setup(FieldableCardInstance fieldableCardInstance, PlayerSide playerSide)
    {
        instance = fieldableCardInstance;
        side = playerSide;

        // Activate the correctly-handed layout and hide the other outright.
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
            // Minions supply no activator runes, so switch those sockets off.
            HideSuppliedRuneSockets(view);
        }
        else if (fieldableCardInstance.SourceCard.cardType is ItemType itemType)
        {
            // Items carry no HP/attack, so hide the stat sockets; the two runes
            // the item supplies show in this side's own supplied-rune sockets,
            // while SetRuneIcons above still shows its effect-activating runes.
            HideStatSockets(view);
            ShowSuppliedRuneSockets(view, itemType);
        }
        else if (fieldableCardInstance.SourceCard.cardType is SpellType)
        {
            HideStatSockets(view);
            HideSuppliedRuneSockets(view);
            HideStatAndRuneSlots();
        }
    }

    // The active side of a token with no stats (items, spells) must not leave an
    // empty HP/attack socket showing. Hide both stat groups (icon + number) and
    // their shared "hpattacksockets" backing outright, not just the numbers.
    private void HideStatSockets(TokenSideView v)
    {
        if (v == null) return;
        if (v.hpText != null) v.hpText.transform.parent.gameObject.SetActive(false);
        if (v.attackText != null) v.attackText.transform.parent.gameObject.SetActive(false);
        Transform canvas = GetSideCanvas(v);
        Transform frame = canvas != null ? canvas.Find("hpattacksockets") : null;
        if (frame != null) frame.gameObject.SetActive(false);
    }

    // Fills the active side's two supplied-rune sockets with the item's supplied
    // activator runes (supplied[0] -> socket1, [1] -> socket2).
    private void ShowSuppliedRuneSockets(TokenSideView v, ItemType itemType)
    {
        var s0 = itemType.suppliedActivatorRunes.Length > 0 ? itemType.suppliedActivatorRunes[0] : GameSystems.Rune.None;
        var s1 = itemType.suppliedActivatorRunes.Length > 1 ? itemType.suppliedActivatorRunes[1] : GameSystems.Rune.None;
        FillSuppliedRuneSocket(v?.suppliedActivatorRune1, s0);
        FillSuppliedRuneSocket(v?.suppliedActivatorRune2, s1);
    }

    // Minions and spells supply no runes, so switch both sockets off entirely.
    private void HideSuppliedRuneSockets(TokenSideView v)
    {
        if (v?.suppliedActivatorRune1 != null) v.suppliedActivatorRune1.SetActive(false);
        if (v?.suppliedActivatorRune2 != null) v.suppliedActivatorRune2.SetActive(false);
    }

    // A supplied-rune socket mirrors the main rune sockets: an empty socket, a
    // glow and the rune icon. Show the rune icon over its empty socket, but keep
    // the glow off — that glow only ever marks an effect firing, which supplied
    // runes never do. An unsupplied slot leaves just its empty socket showing.
    private void FillSuppliedRuneSocket(GameObject socket, GameSystems.Rune rune)
    {
        if (socket == null) return;
        socket.SetActive(true);
        foreach (Transform child in socket.transform)
        {
            var img = child.GetComponent<Image>();
            if (img == null) continue;
            string n = child.name.ToLowerInvariant();
            if (n.Contains("glow"))
            {
                img.enabled = false;
            }
            else if (n.Contains("icon"))
            {
                if (runeIcons != null) img.sprite = runeIcons.GetIcon(rune);
                img.enabled = rune != GameSystems.Rune.None;
            }
            // The empty socket ("runeEmpty") is left as authored, staying visible.
        }
    }

    // The single Canvas child every side layout is built under.
    private static Transform GetSideCanvas(TokenSideView v)
    {
        if (v?.root == null) return null;
        Transform t = v.root.transform;
        return t.childCount > 0 ? t.GetChild(0) : null;
    }

    private void HideStatAndRuneSlots()
    {
        if (view.attackIcon != null) view.attackIcon.enabled = false;
        if (view.hpIcon != null) view.hpIcon.enabled = false;
        if (view.rune1 != null) view.rune1.enabled = false;
        if (view.rune2 != null) view.rune2.enabled = false;
        if (view.rune1Glow != null) view.rune1Glow.enabled = false;
        if (view.rune2Glow != null) view.rune2Glow.enabled = false;
        if (view.passiveGlow != null) view.passiveGlow.enabled = false;
    }

    private void SetRuneIcons(FieldableCardType cardType)
    {
        if (runeIcons == null) return;
        var r1 = cardType.effectActivatingRunes.Length > 0 ? cardType.effectActivatingRunes[0] : GameSystems.Rune.None;
        var r2 = cardType.effectActivatingRunes.Length > 1 ? cardType.effectActivatingRunes[1] : GameSystems.Rune.None;

        // A card always carries two effect slots (and often an activating rune for
        // each) even when a slot has no effect, so an assigned rune alone is not
        // enough to show anything. Rune N belongs to effect N: only show it when
        // that effect actually has text. This mirrors FieldableCardVisualizer's
        // gating so the token and the full card agree. When a rune is hidden its
        // inactive icon and glow turn off, leaving the empty rune socket visible.
        bool showRune1 = r1 != GameSystems.Rune.None && !string.IsNullOrWhiteSpace(cardType.effect1Description);
        bool showRune2 = r2 != GameSystems.Rune.None && !string.IsNullOrWhiteSpace(cardType.effect2Description);

        view.rune1.sprite = runeIcons.GetIcon(r1);
        view.rune1.enabled = showRune1;
        view.rune2.sprite = runeIcons.GetIcon(r2);
        view.rune2.enabled = showRune2;

        if (view.rune1Glow != null)
        {
            view.rune1Glow.sprite = runeIcons.GetGlowIcon(r1);
            view.rune1Glow.enabled = showRune1;
            view.rune1Glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        }
        if (view.rune2Glow != null)
        {
            view.rune2Glow.sprite = runeIcons.GetGlowIcon(r2);
            view.rune2Glow.enabled = showRune2;
            view.rune2Glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        }
        if (view.passiveGlow != null)
        {
            // No rune gate — the passive is always active. Only show its glow when
            // the card actually has a passive effect that can fire.
            bool hasPassive = cardType.PassiveEventTriggers is { Count: > 0 };
            view.passiveGlow.enabled = hasPassive;
            view.passiveGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
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

    // The board token no longer mirrors effect-field cover on the rune glow — the
    // glow now flashes only when an effect actually fires (see FlashEffectGlow).
    // Kept as a no-op so the portal's existing cover-update calls stay valid and
    // any future cover-related token visuals have a home.
    public void UpdateFieldCoverDisplay()
    {
    }

    // Tints both rune glows to the card's resonance colour, preserving their
    // current (hidden) alpha. Called by the portal right after Setup.
    public void SetResonanceGlowColor(Color color)
    {
        glowColor = color;
        TintGlow(view?.rune1Glow);
        TintGlow(view?.rune2Glow);
        TintGlow(view?.passiveGlow);
    }

    private void TintGlow(Image glow)
    {
        if (glow == null) return;
        glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glow.color.a);
    }

    // Lights the glow bound to the effect field that just fired, holds it, then
    // fades it back out. Effect1 -> rune1, Effect2 -> rune2, Passive -> passive
    // glow; combat/status fields have no glow on the token and are ignored.
    // Subscribed to the instance's OnEffectTriggered by the portal, so each real
    // effect trigger pulses its own glow in the card's resonance colour.
    public void FlashEffectGlow(EffectFieldPosition position)
    {
        Image glow = position switch
        {
            EffectFieldPosition.Effect1 => view?.rune1Glow,
            EffectFieldPosition.Effect2 => view?.rune2Glow,
            EffectFieldPosition.Passive => view?.passiveGlow,
            _ => null,
        };
        if (glow == null || !glow.enabled) return;

        if (glowRoutines.TryGetValue(glow, out Coroutine running) && running != null)
            StopCoroutine(running);
        glowRoutines[glow] = StartCoroutine(FlashGlowRoutine(glow));
    }

    private IEnumerator FlashGlowRoutine(Image glow)
    {
        yield return FadeGlowAlpha(glow, glow.color.a, 1f, glowFadeInDuration);
        if (glowHoldDuration > 0f) yield return new WaitForSeconds(glowHoldDuration);
        yield return FadeGlowAlpha(glow, glow.color.a, 0f, glowFadeOutDuration);
        glowRoutines[glow] = null;
    }

    private IEnumerator FadeGlowAlpha(Image glow, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, to);
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(from, to, elapsed / duration);
            glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, a);
            yield return null;
        }
        glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, to);
    }

    public void ApplyStatusEffect(StatusEffectInstance statusEffect)
    {
        if (view?.statusEffectContainer == null || statusEffectPrefab == null) return;

        // Internal-only statuses (e.g. OnAttackedApplyPlague) exist purely to drive
        // game logic and carry no icon. An unassigned icon means "don't surface it":
        // with nothing to draw, they must not spawn a blank chip in the container.
        if (statusEffect.Data == null || statusEffect.Data.icon == null) return;

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
