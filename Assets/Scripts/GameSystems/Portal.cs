using System.Collections.Generic;
using GameSystems;
using TMPro;
using UnityEngine;

public class Portal : MonoBehaviour
{
    public PlayerSide ownerSide;
    public Resonance resonance;
    public GameObject LeftPortalVisual;
    public GameObject RightPortalVisual;
    private TextMeshProUGUI identityText;
    private SpriteRenderer laneSpriteRenderer;
    public Renderer portalRenderer;
    private MaterialPropertyBlock propBlock;
    private List<CardContext> cardsInPortal = new List<CardContext>();
    public ResonanceLibrary resonanceLibrary; //TODO move this
    public GameObject tempCardPrefab; //TODO move this
    public float cardSpacing = 1f;
    public float cardStartX = 2f;

    void OnValidate()
    {
        if (LeftPortalVisual == null || RightPortalVisual == null) return;
        SelectSide(ownerSide);
    }

    void SelectSide(PlayerSide newSide)
    {
        if (ownerSide == PlayerSide.Left)
        {
            RightPortalVisual.SetActive(true);
            LeftPortalVisual.SetActive(false);
            identityText = RightPortalVisual.GetComponentInChildren<TextMeshProUGUI>();
            laneSpriteRenderer = RightPortalVisual.GetComponentInChildren<SpriteRenderer>();
        }
        else
        {
            RightPortalVisual.SetActive(false);
            LeftPortalVisual.SetActive(true);
            identityText = LeftPortalVisual.GetComponentInChildren<TextMeshProUGUI>();
            laneSpriteRenderer = LeftPortalVisual.GetComponentInChildren<SpriteRenderer>();
        }
    }
    void Awake()
    {
        cardsInPortal.Clear();
        propBlock = new MaterialPropertyBlock();
        SelectSide(ownerSide);
    }

    public void SetResonanceType(ResonanceType type)
    {
        resonance = resonanceLibrary.GetResonance(type);
        identityText.text = resonance.identity;
        laneSpriteRenderer.sprite = resonance.sprite;
        ApplyColor(resonance.color);
    }

    private void ApplyColor(Color newColor)
    {
        if (portalRenderer == null) return;
        portalRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_Color", newColor);
        portalRenderer.SetPropertyBlock(propBlock);
    }

    public void AddCard(CardContext cardContext)
    {
        //calc position
        float sign = ownerSide == PlayerSide.Left ? -1 : 1;
        float x = (cardStartX + cardsInPortal.Count * cardSpacing) * sign;
        Vector3 cardPosition = new Vector3(x, transform.position.y, 0);
        
        //add card visualizer
        CardVisualizer cardVisualizer = Instantiate(tempCardPrefab, cardPosition, Quaternion.identity).GetComponent<CardVisualizer>();
        cardVisualizer.Setup(cardContext.SourceCard, ownerSide);
        cardsInPortal.Add(cardContext);
    }
    public int GetCardCount()
    {
        return cardsInPortal.Count;
    }
}