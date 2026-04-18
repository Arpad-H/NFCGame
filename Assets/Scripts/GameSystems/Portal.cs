using System.Collections.Generic;
using GameSystems;
using TMPro;
using UnityEngine;

public class Portal : MonoBehaviour
{
    public PlayerSide ownerSide;
    Resonance resonance;
    public TextMeshProUGUI identityText;
    public Renderer portalRenderer;
    private MaterialPropertyBlock propBlock;
    private List<CardData> cardsInPortal = new List<CardData>();
    public ResonanceLibrary resonanceLibrary; //TODO move this
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
        cardsInPortal.Add(card);
    }
}