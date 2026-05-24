using UnityEngine;

public enum CardCategory { Psychic = 0, Life = 1, Darkness = 2, Plague = 3, Death = 4, Holy = 5 }

[CreateAssetMenu(fileName = "New Card", menuName = "Card Game/Card Data")]
public class CardScriptableObject : ScriptableObject
{
    public string cardName;
    public CardCategory category;
    public Sprite artwork;
    public int life;

    public int damage;
    public string description1;
    public string description2;
    public string description3;

}