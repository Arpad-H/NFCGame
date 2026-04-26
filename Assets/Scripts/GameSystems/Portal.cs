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

    private List<(FieldableCardInstance context, CardVisualizer visual)> cardsInPortal
        = new List<(FieldableCardInstance, CardVisualizer)>();

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

    public void AddCard(FieldableCardInstance cardInstance)
    {
        CardVisualizer visual = Instantiate(tempCardPrefab, Vector3.zero, Quaternion.identity)
            .GetComponent<CardVisualizer>();

        visual.Setup(cardInstance, ownerSide);

        // Logic is already baked into the instance!
        if (cardInstance is MinionInstance minion)
        {
          //  minion.Definition = cardInstance.SourceCard.cardType as MinionType;
            minion.OnHealthChanged += visual.UpdateHealthDisplay;
            minion.OnDeath += () => RemoveCard(cardInstance);
        }

        cardsInPortal.Add((cardInstance, visual));
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

    public void RemoveCard(FieldableCardInstance cardInstance)
    {
        int index = cardsInPortal.FindIndex(c => c.context == cardInstance);
        if (index == -1) return;

        // destroy visual
        Destroy(cardsInPortal[index].visual.gameObject);

        // remove from list
        cardsInPortal.RemoveAt(index);

        // shift everything visually
        UpdateCardPositions();
    }

    public FieldableCardInstance GetCard(int index)
    {
        if (index < 0 || index >= cardsInPortal.Count) return null;
        return cardsInPortal[index].context;
    }
    
    public MinionInstance GetMinion(int n)
    {
        int count = 0;

        foreach (var entry in cardsInPortal)
        {
            if (entry.context.SourceCard.cardType is MinionType)
            {
                if (count == n)
                    return entry.context as MinionInstance;

                count++;
            }
        }

        return null; // not enough minions
    }

    public int GetMinionPosition(FieldableCardInstance fieldableCardInstance)
    {
        int count = 0;

        foreach (var entry in cardsInPortal)
        {
            if (entry.context.SourceCard.cardType is MinionType)
            {
                if (entry.context == fieldableCardInstance)
                    return count;

                count++;
            }
        }

        return -1; // not found or not a minion
    }
}