using System.Collections.Generic;
using GameSystems;
using JetBrains.Annotations;
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
    private List<(FieldableCardContext context, CardVisualizer visual)> cardsInPortal 
        = new List<(FieldableCardContext, CardVisualizer)>();
    public ResonanceLibrary resonanceLibrary; //TODO move this
    public GameObject tempCardPrefab; //TODO move this
    public float cardSpacing = 1f;
    public float cardStartX = 2f;
    public int laneIndex; // 0 = top, 1 = middle, 2 = bottom
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

    public void AddCard(FieldableCardContext cardContext)
    {
        CardVisualizer visual = Instantiate(tempCardPrefab, Vector3.zero, Quaternion.identity)
            .GetComponent<CardVisualizer>();

        visual.Setup(cardContext, ownerSide);
        if (cardContext.SourceCard.cardType is MinionType minion) //TODO revisit type casting here
        {
            // Tell the visualizer: "When this minion's health changes, run your Update function"
            minion.OnHealthChanged += visual.UpdateHealthDisplay;
            cardContext.SetTarget(minion);
        }
      
        cardsInPortal.Add((cardContext, visual));

        UpdateCardPositions();
    }
    private void UpdateCardPositions()
    {
        float sign = ownerSide == PlayerSide.Left ? -1 : 1;

        for (int i = 0; i < cardsInPortal.Count; i++)
        {
            float x = (cardStartX + i * cardSpacing) * sign;
            Vector3 targetPos = new Vector3(x, transform.position.y, 0);

            cardsInPortal[i].visual.transform.position = targetPos;
        }
    }
    public int GetCardCount()
    {
        return cardsInPortal.Count;
    }

    public void RemoveCard(FieldableCardContext cardContext)
    {
        int index = cardsInPortal.FindIndex(c => c.context == cardContext);
        if (index == -1) return;

        // destroy visual
        Destroy(cardsInPortal[index].visual.gameObject);

        // remove from list
        cardsInPortal.RemoveAt(index);

        // shift everything visually
        UpdateCardPositions();
    }
    public FieldableCardContext GetCard(int index)
    {
        if (index < 0 || index >= cardsInPortal.Count) return null;
        return cardsInPortal[index].context;
    }
    
  
    public FieldableCardContext GetMinion(int n)
    {
        int count = 0;

        foreach (var entry in cardsInPortal)
        {
            if (entry.context.SourceCard.cardType is MinionType)
            {
                if (count == n)
                    return entry.context;

                count++;
            }
        }

        return null; // not enough minions
    }
}