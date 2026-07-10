using GameSystems;
using TMPro;
using UnityEngine;

// Full card view for spells. A spell carries far less than a minion or item —
// no stats, activating runes or effect fields — just its rules text. Name,
// artwork and resonance theme come from the CardVisualizer base.
public class SpellCardVisualizer : CardVisualizer
{
    [Tooltip("Shows SpellType.SpellDescription — the spell's rules text.")]
    public TextMeshProUGUI DescriptionText;

    protected override void PopulateFromInstance(FieldableCardInstance fieldableCardInstance)
        => PopulateSpell(fieldableCardInstance.SourceCard);

    protected override void PopulateFromLibrary(CardData sourceCard)
        => PopulateSpell(sourceCard);

    private void PopulateSpell(CardData sourceCard)
    {
        if (DescriptionText == null) return;
        if (sourceCard.cardType is SpellType spell)
            DescriptionText.text = CardTextFormatter.Format(spell.SpellDescription);
    }
}
