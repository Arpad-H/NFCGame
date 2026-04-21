using UnityEngine;
using UnityEngine.UI;
using TMPro; // Highly recommended over standard UI Text

public class CardVisualizer : MonoBehaviour
{
    [Header("UI References")]
    // public TextMeshProUGUI nameText;
    // public TextMeshProUGUI resonanceText;
    public SpriteRenderer artworkImage;

    public void Setup(CardData data)
    {
        // 1. Set the Text
        // nameText.text = data.cardName;
        // resonanceText.text = data.resonance.ToString();

        // 2. Set the Sprite
        artworkImage.sprite = data.artwork;
    }


}