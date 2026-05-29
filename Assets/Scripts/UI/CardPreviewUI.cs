using System.Collections.Generic;
using GameSystems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardPreviewUI : MonoBehaviour
{
  public static CardPreviewUI Instance;

    [Header("UI References")]
    public Image tokenImage;
    public TextMeshProUGUI HPText;
    public TextMeshProUGUI AttackText;
    public TextMeshProUGUI NameText;
    public TextMeshProUGUI PassiveText;
    public TextMeshProUGUI Effect1Text;
    public TextMeshProUGUI Effect2Text;

    [Header("Layout & Positioning")]
    public GameObject container;
    public RectTransform previewRect; // Drag the 'GameObject' from your hierarchy here
    public float padding = 20f;
    
    [Header("Keywords")]
    public GameObject keywordParentRight;
    public GameObject keywordParentLeft;
    public GameObject keywordPrefab; 
    void Awake()
    {
        if (Instance == null) Instance = this;
        Hide();
    }

    public void Hide() => container.SetActive(false);

    public void Show(FieldableCardInstance instance, GameObject hoveredCardObject, PlayerSide side)
    {
        // 1. Populate Data
        PopulateCardData(instance);

        // 2. Toggle Keyword Panels
        keywordParentLeft.SetActive(side == PlayerSide.Right); // Show left keywords if card is on the right
        keywordParentRight.SetActive(side == PlayerSide.Left);

        ShowKeywords(instance,side);
        // 3. Activate Container
        container.SetActive(true);

        // 4. Position the Preview
        PositionPreview(hoveredCardObject, side);
    }

    private void PopulateCardData(FieldableCardInstance instance)
    {
        tokenImage.sprite = instance.SourceCard.artwork;
        NameText.text = instance.SourceCard.cardName;

        if (instance.SourceCard.cardType is FieldableCardType fct)
        {
            PassiveText.text = fct.passiveDescription;
            Effect1Text.text = fct.effect1Description;
            Effect2Text.text = fct.effect2Description;
        }

        if (instance.SourceCard.cardType is MinionType minion)
        {
            HPText.text = minion.baseHealth.ToString();
            AttackText.text = minion.baseAttack.ToString();
        }
    }

    private void PositionPreview(GameObject hoveredCardObject, PlayerSide side)
    {
        RectTransform hoveredRect = hoveredCardObject.GetComponent<RectTransform>();
        if (hoveredRect == null) return;

        // Get corners of the card being hovered
        Vector3[] corners = new Vector3[4];
        hoveredRect.GetWorldCorners(corners);

        // Calculate vertical center
        float centerY = (corners[0].y + corners[1].y) * 0.5f;

        if (side == PlayerSide.Left)
        {
            // Hovered card is on Left -> Show preview to the RIGHT of the card
            previewRect.pivot = new Vector2(0f, 0.5f);
            previewRect.position = new Vector3(corners[2].x + padding, centerY, 0);
        }
        else
        {
            // Hovered card is on Right -> Show preview to the LEFT of the card
            previewRect.pivot = new Vector2(1f, 0.5f);
            previewRect.position = new Vector3(corners[0].x - padding, centerY, 0);
        }
    }


    private void ShowKeywords(FieldableCardInstance instance,PlayerSide ownerSide)
    {
        Transform keywordParent = ownerSide == PlayerSide.Left ? keywordParentRight.transform : keywordParentLeft.transform;
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
        if (instance.SourceCard.cardType is not FieldableCardType) return;
        List<KeywordData> keywords = ((MinionType)instance.SourceCard.cardType).keywords;
        foreach (var kw in keywords)
        {
            GameObject go = Instantiate(keywordPrefab, keywordParent);
            go.GetComponent<KeywordUIElement>().Setup(kw);
        }
    }

    private void ClearKeywords()
    {
        foreach (Transform child in keywordParentLeft.transform) Destroy(child.gameObject);
        foreach (Transform child in keywordParentRight.transform) Destroy(child.gameObject);
    }
}