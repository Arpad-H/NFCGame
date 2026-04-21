using GameSystems;
using UnityEngine;
using UnityEngine.UI;

public class CardPreviewUI : MonoBehaviour
{
    public static CardPreviewUI Instance;

    public GameObject container;
    public Image artworkImage;
    public RectTransform rectTransform;
    public float padding = 10f;

    void Awake() => Instance = this;

    public void Show(CardData data, GameObject cardObject, PlayerSide side)
    {
        artworkImage.sprite = data.artwork;
        container.SetActive(true);
        Canvas.ForceUpdateCanvases();

        // Calculate card edges in screen space
        SpriteRenderer sr = cardObject.GetComponentInChildren<SpriteRenderer>();
        Bounds bounds = sr.bounds;
        
        float worldX = (side == PlayerSide.Left) ? bounds.max.x : bounds.min.x;
        Vector2 screenEdge = Camera.main.WorldToScreenPoint(new Vector3(worldX, bounds.center.y, 0));

        // Set pivot so UI grows away from the card
        float pivotX = (side == PlayerSide.Left) ? 0f : 1f;
        rectTransform.pivot = new Vector2(pivotX, 0.5f);

        // Apply position with padding
        float finalOffset = (side == PlayerSide.Left) ? padding : -padding;
        rectTransform.position = new Vector2(screenEdge.x + finalOffset, screenEdge.y);
    }

    public void Hide() => container.SetActive(false);
}