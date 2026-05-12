using System.Collections.Generic;
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

    public void Show(FieldableCardInstance instance, GameObject cardObject, PlayerSide side)
    {
        artworkImage.sprite = instance.SourceCard.artwork;

        container.SetActive(true);

        Canvas.ForceUpdateCanvases();

        ShowKeywords(instance, side);

        RectTransform cardRect = cardObject.GetComponentInChildren<RectTransform>();

        Vector3[] corners = new Vector3[4];
        cardRect.GetWorldCorners(corners);

        Vector3 edge =
            (side == PlayerSide.Left)
                ? corners[2]
                : corners[0];

        float pivotX = (side == PlayerSide.Left) ? 0f : 1f;
        rectTransform.pivot = new Vector2(pivotX, 0.5f);

        float finalOffset = (side == PlayerSide.Left) ? padding : -padding;

        rectTransform.position = new Vector2(
            edge.x + finalOffset,
            (corners[0].y + corners[1].y) * 0.5f
        );
    }

    private void ShowKeywords(FieldableCardInstance instance,PlayerSide ownerSide)
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
        //TODO this is dirty hardcode
        if (instance.SourceCard.cardType is not MinionType) return;
        List<KeywordData> keywords = ((MinionType)instance.SourceCard.cardType).keywords;
        foreach (var kw in keywords)
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