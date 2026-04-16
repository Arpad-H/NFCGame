using UnityEngine;
using UnityEngine.UI;

public class CardView : MonoBehaviour
{
    [SerializeField] private Image artwork;

    public RectTransform Rect { get; private set; }

    private void Awake()
    {
        Rect = GetComponent<RectTransform>();
    }

    public void SetSprite(Sprite sprite)
    {
        if (artwork != null)
            artwork.sprite = sprite;
    }
}