using UnityEngine;
using UnityEngine.UI;

public class CardDisplay : MonoBehaviour
{
    public CardScriptableObject cardData; // The data driving this UI
    
    [Header("UI References")]
    public Image artworkImage;

    public void Setup(CardScriptableObject data)
    {
        cardData = data;
        if (data.artwork != null) 
        {
            artworkImage.sprite = data.artwork;
        }
    }
}