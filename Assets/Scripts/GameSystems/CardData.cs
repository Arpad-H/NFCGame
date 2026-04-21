using UnityEngine;
using UnityEngine.UI;

public class CardData
{
    public string cardName;
    public ResonanceType resonance;
    public Sprite artwork;

    public CardData(string name, ResonanceType resonance, Sprite sprite)
    {
        this.cardName = name;
        this.resonance = resonance;
        this.artwork = sprite;
    }
}