using System.Collections.Generic;
using GameSystems;
using UnityEngine;

// Hover preview: a large, static, read-only copy of the card under the cursor,
// shown beside the hovered card so the player can read its full rules.
//
// The card face is drawn by the SAME visualizers the real cards use
// (FieldableCardVisualizer for minions/items, SpellCardVisualizer for spells),
// driven through SetupForLibrary — the static path. That means:
//   * every effect reads as active (nothing is dimmed) and no runes glow — this
//     is a reference display, not a live board token;
//   * the stats shown are the card's DEFAULT values, not the hovered instance's
//     current HP/attack, so the player can judge a card's printed value (e.g.
//     how much a heal is worth) rather than its damaged state, which they can
//     already see on the board.
// Only the visualizer that matches the card type is shown; the other is hidden.
public class CardPreviewUI : MonoBehaviour
{
    public static CardPreviewUI Instance;

    [Header("Card face (static, side-aware)")]
    [Tooltip("Draws the minion/item layout. Lives on the CardMinion object.")]
    public FieldableCardVisualizer fieldableVisualizer;
    [Tooltip("Draws the spell layout. Lives on the CardSpell object.")]
    public SpellCardVisualizer spellVisualizer;

    [Header("Layout & Positioning")]
    public GameObject container;
    public RectTransform previewRect; // Drag the 'GameObject' from your hierarchy here
    public float padding = 20f;

    [Header("Keywords")]
    public GameObject keywordParentRight;
    public GameObject keywordParentLeft;
    public GameObject keywordPrefab;

    void Awake()
    {
        if (Instance == null) Instance = this;
        Hide();
    }

    public void Hide() => container.SetActive(false);

    public void Show(FieldableCardInstance instance, GameObject hoveredCardObject, PlayerSide side)
    {
        // 1. Draw the card face with the type-matching visualizer (static).
        PopulateCardFace(instance.SourceCard, side);

        // 2. Toggle Keyword Panels
        keywordParentLeft.SetActive(side == PlayerSide.Right); // Show left keywords if card is on the right
        keywordParentRight.SetActive(side == PlayerSide.Left);

        ShowKeywords(instance, side);
        // 3. Activate Container
        container.SetActive(true);

        // 4. Position the Preview
        PositionPreview(hoveredCardObject, side);
    }

    // Show only the visualizer matching the card type and populate it from the
    // card definition. SourceCard (not the live instance) is deliberate: the
    // preview shows default stats and every effect active, regardless of the
    // hovered card's current board state.
    private void PopulateCardFace(CardData card, PlayerSide side)
    {
        bool isSpell = card.cardType is SpellType;

        if (fieldableVisualizer != null) fieldableVisualizer.gameObject.SetActive(!isSpell);
        if (spellVisualizer != null) spellVisualizer.gameObject.SetActive(isSpell);

        if (isSpell)
        {
            if (spellVisualizer != null) spellVisualizer.SetupForLibrary(card, side);
        }
        else
        {
            if (fieldableVisualizer != null) fieldableVisualizer.SetupForLibrary(card, side);
        }
    }

    private void PositionPreview(GameObject hoveredCardObject, PlayerSide side)
    {
        RectTransform hoveredRect = hoveredCardObject.GetComponent<RectTransform>();
        if (hoveredRect == null) return;

        // Get corners of the card being hovered
        Vector3[] corners = new Vector3[4];
        hoveredRect.GetWorldCorners(corners);

        // Calculate vertical center
        float centerY = (corners[0].y + corners[1].y) * 0.5f;

        if (side == PlayerSide.Left)
        {
            // Hovered card is on Left -> Show preview to the RIGHT of the card
            previewRect.pivot = new Vector2(0f, 0.5f);
            previewRect.position = new Vector3(corners[2].x + padding, centerY, 0);
        }
        else
        {
            // Hovered card is on Right -> Show preview to the LEFT of the card
            previewRect.pivot = new Vector2(1f, 0.5f);
            previewRect.position = new Vector3(corners[0].x - padding, centerY, 0);
        }
    }


    private void ShowKeywords(FieldableCardInstance instance, PlayerSide ownerSide)
    {
        Transform keywordParent = ownerSide == PlayerSide.Left ? keywordParentRight.transform : keywordParentLeft.transform;
        if (ownerSide == PlayerSide.Left)
        {
            keywordParentLeft.gameObject.SetActive(false);
            keywordParentRight.gameObject.SetActive(true);
        }
        else
        {
            keywordParentRight.gameObject.SetActive(false);
            keywordParentLeft.gameObject.SetActive(true);
        }

        ClearKeywords();
        if (instance.SourceCard.cardType is not FieldableCardType fct) return;
        // Auto-detected from the card's description text — no manual keyword list to maintain.
        List<KeywordData> keywords = CardTextFormatter.GetKeywordsInCard(fct);
        foreach (var kw in keywords)
        {
            GameObject go = Instantiate(keywordPrefab, keywordParent);
            go.GetComponent<KeywordUIElement>().Setup(kw);
        }
    }

    private void ClearKeywords()
    {
        foreach (Transform child in keywordParentLeft.transform) Destroy(child.gameObject);
        foreach (Transform child in keywordParentRight.transform) Destroy(child.gameObject);
    }
}
