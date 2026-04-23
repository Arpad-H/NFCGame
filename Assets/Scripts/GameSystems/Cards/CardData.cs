using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



[CreateAssetMenu(fileName = "New Card", menuName = "Cards/Card")]
public class CardData : ScriptableObject
{
    [Header("Visuals")]
    public string cardName;
    public Sprite artwork;
    public ResonanceType resonance;

    [Header("Keywords")]
    public List<KeywordData> keywords = new List<KeywordData>();

    [Header("Logic")]
    [SerializeReference] 
    public List<ICardEffect> effects = new List<ICardEffect>();
}