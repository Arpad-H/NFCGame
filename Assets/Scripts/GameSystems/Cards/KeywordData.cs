using UnityEngine;

[CreateAssetMenu(fileName = "Keyword", menuName = "Cards/Keyword")]
public class KeywordData : ScriptableObject
{
    public string keywordName;
    [TextArea(3, 10)] public string description;
}