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

    void Awake()
    {
        propBlock = new MaterialPropertyBlock();
    }

    public void SetResonanceType(ResonanceType type)
    {
        resonance = ResonanceLibrary.Instance.GetResonance(type);
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