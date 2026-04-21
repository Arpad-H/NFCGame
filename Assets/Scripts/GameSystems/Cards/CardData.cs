using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardData
{
    public string cardName;
    public ResonanceType resonance;
    public Sprite artwork;
    public List<KeywordData> keywords = new List<KeywordData>();

    public CardData(string name, ResonanceType resonance, Sprite sprite, List<KeywordData> keywords = null)
    {
        this.cardName = name;
        this.resonance = resonance;
        this.artwork = sprite;
        this.keywords = keywords ?? new List<KeywordData>();
    }
}