using TMPro;
using UnityEngine;

public class KeywordUIElement : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    public void Setup(KeywordData data)
    {
        titleText.text = data.keywordName;
        descriptionText.text = data.description;
    }
}