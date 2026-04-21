using System.Collections.Generic;
using GameSystems;
using TMPro;
using UnityEngine;

public class Portal : MonoBehaviour
{
    public PlayerSide ownerSide;
    public Resonance resonance;
    public TextMeshProUGUI identityText;
    public Renderer portalRenderer;
    private MaterialPropertyBlock propBlock;
    private List<CardData> cardsInPortal = new List<CardData>();
    public ResonanceLibrary resonanceLibrary; //TODO move this
    public GameObject tempCardPrefab; //TODO move this
    public float cardSpacing = 1f;
    public float cardStartX = 2f;

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
    }

    public void SetResonanceType(ResonanceType type)
    {
        resonance = resonanceLibrary.GetResonance(type);
        identityText.text = resonance.identity;
        ApplyColor(resonance.color);
    }

    private void ApplyColor(Color newColor)
    {
        if (portalRenderer == null) return;
        portalRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_Color", newColor);
        portalRenderer.SetPropertyBlock(propBlock);
    }

    public void AddCard(CardData card)
    {
        //calc position
        float sign = ownerSide == PlayerSide.Left ? -1 : 1;
        float x = (cardStartX + cardsInPortal.Count * cardSpacing) * sign;
        Vector3 cardPosition = new Vector3(x, transform.position.y, 0);
        
        //add card visualizer
        CardVisualizer cardVisualizer = Instantiate(tempCardPrefab, cardPosition, Quaternion.identity).GetComponent<CardVisualizer>();
        cardVisualizer.Setup(card, ownerSide);
        cardsInPortal.Add(card);
    }
}