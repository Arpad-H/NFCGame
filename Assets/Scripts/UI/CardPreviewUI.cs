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
    
    public Transform keywordParentRight;
    public Transform keywordParentLeft;
    public GameObject keywordPrefab;

    void Awake() => Instance = this;

    public void Show(CardData data, GameObject cardObject, PlayerSide side)
    {
        artworkImage.sprite = data.artwork;
        container.SetActive(true);
        Canvas.ForceUpdateCanvases();
        
        ShowKeywords(data,side);
        
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

    private void ShowKeywords(CardData data,PlayerSide ownerSide)
    {
        Transform keywordParent = ownerSide == PlayerSide.Left ? keywordParentRight : keywordParentLeft;
        if (ownerSide == PlayerSide.Left)
        {
            
            keywordParentLeft.gameObject.SetActive(false);
            keywordParentRight.gameObject.SetActive(true);
        }
        else
        {
            keywordParentRight.gameObject.SetActive(false);
            keywordParentLeft.gameObject.SetActive(true);
        }
        
        ClearKeywords();
        foreach (var kw in data.keywords)
        {
            GameObject go = Instantiate(keywordPrefab, keywordParent);
            go.GetComponent<KeywordUIElement>().Setup(kw);
        }
    }

    private void ClearKeywords()
    {
        foreach (Transform child in keywordParentLeft) Destroy(child.gameObject);
        foreach (Transform child in keywordParentRight) Destroy(child.gameObject);
    }

    public void Hide() => container.SetActive(false);
}