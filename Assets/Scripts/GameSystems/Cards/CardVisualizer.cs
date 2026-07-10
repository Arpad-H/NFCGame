using GameSystems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Full, readable card view — name, artwork and resonance theme. Used as a
// static display by the library grid, the card exporter and the spell-cast
// animation; it is never live-updated (the board uses BoardTokenVisualizer for
// stats/status/field changes).
//
// Abstract base: the surface every full card shares lives here, and a subclass
// fills in the type-specific body:
//   FieldableCardVisualizer — minions and items (stats, runes, effect fields)
//   SpellCardVisualizer      — spells (just the spell description)
// Pick the matching prefab for a card with CardPrefabResolver.
public abstract class CardVisualizer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image tokenImage;
    public TextMeshProUGUI Name;

    [Header("Resonance theme")]
    [Tooltip("CardThemeApplier on the card base image. Recolored per the card's resonance.")]
    public CardThemeApplier cardTheme;
    [Tooltip("Maps a ResonanceType to its Resonance asset (which carries the CardTheme).")]
    public ResonanceLibrary resonanceLibrary;

    // Set on the board / spell-cast path; null for the library and exporter,
    // which have no game state. Gates the hover preview and lets subclasses read
    // board state (e.g. field activation).
    protected FieldableCardInstance instance;
    protected PlayerSide side;

    private Vector3 baseScale;

    // Board / spell-cast entry: a live card instance.
    public void Setup(FieldableCardInstance fieldableCardInstance, PlayerSide playerSide)
    {
        instance = fieldableCardInstance;
        side = playerSide;
        ApplyCommon(fieldableCardInstance.SourceCard);
        PopulateFromInstance(fieldableCardInstance);
    }

    // Library / exporter entry: the card definition, no game state.
    public void SetupForLibrary(CardData sourceCard)
    {
        instance = null;
        ApplyCommon(sourceCard);
        PopulateFromLibrary(sourceCard);
    }

    // Name, artwork and resonance theme are shown the same way for every type.
    private void ApplyCommon(CardData sourceCard)
    {
        ApplyResonanceTheme(sourceCard.resonance);
        tokenImage.sprite = sourceCard.artwork;
        Name.text = sourceCard.cardName;
    }

    // Fill the type-specific body from a live instance. Board state is available
    // via `instance` / `side` (e.g. an effect stays dimmed until its field
    // activates).
    protected abstract void PopulateFromInstance(FieldableCardInstance fieldableCardInstance);

    // Fill the type-specific body with no game state — the library and exporter
    // show every effect as active (undimmed) to keep the text easy to read.
    protected abstract void PopulateFromLibrary(CardData sourceCard);

    // Resolve the card's resonance to its Resonance asset and hand that asset's
    // CardTheme to the base recolor. No library or theme assigned -> leave the base
    // material's default colors untouched.
    private void ApplyResonanceTheme(ResonanceType resonance)
    {
        if (cardTheme == null || resonanceLibrary == null) return;
        Resonance res = resonanceLibrary.GetResonance(resonance);
        if (res == null || res.theme == null) return;
        cardTheme.SetTheme(res.theme);
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

    public void SetBaseScale(Vector3 scale)
    {
        baseScale = scale;
    }
}
