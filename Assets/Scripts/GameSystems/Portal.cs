using System.Collections.Generic;
using System.Threading.Tasks;
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

    public CardVisualizer GetVisualizer(FieldableCardInstance instance)
    {
        var match = cardsInPortal.Find(x => x.context == instance);
        return match.visual;
    }

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
        if (!resonance)
        {
            Debug.LogError("Resonance not found: " + type);
            return;
        }
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

    public async Task AddCard(FieldableCardInstance cardInstance)
    {
        CardVisualizer visual = Instantiate(tempCardPrefab, Vector3.zero, Quaternion.Euler(90,0,0))
            .GetComponent<CardVisualizer>();

        visual.Setup(cardInstance, ownerSide);

        if (cardInstance is MinionInstance minion)
        {
            minion.OnStatsChanged += visual.UpdateStatsDisplay;
            // Resolve the portal at death time: lane shifts can move the card
            // to another portal after this subscription was made.
            minion.OnDeath += () => cardInstance.SourcePortal?.RemoveCard(cardInstance);
            minion.OnStatusEffectAdded += visual.ApplyStatusEffect;
            minion.OnStatusEffectRemoved += visual.RemoveStatusEffect;
        }
        FieldableCardInstance currentLastCardInPortal = cardsInPortal.Count > 0 ? cardsInPortal[^1].context : null;
        if (currentLastCardInPortal !=null && cardInstance is ItemInstance item)
        {
            
            await currentLastCardInPortal.AttachCardToThis(item
                .GetSuppliedRunes()); //only items and spells activate effect activating runes
            if (currentLastCardInPortal is MinionInstance minionInstance) item.ItemHolder = minionInstance;
            else if (currentLastCardInPortal is ItemInstance itemInstance) item.ItemHolder = itemInstance.ItemHolder; 
            //update visual of current last card in portal to reflect that it is now covered by another card, if there is one.
            var lastCardVisualizer = cardsInPortal[^1].visual;
            lastCardVisualizer.UpdateFieldCoverDisplay();
        }
        visual.UpdateFieldCoverDisplay();
        cardsInPortal.Add((cardInstance, visual));
        UpdateCardPositions();
        
        
    }

    private void UpdateCardPositions()
    {
        float sign = ownerSide == PlayerSide.Left ? -1 : 1;

        for (int i = 0; i < cardsInPortal.Count; i++)
        {
            float x = (cardStartX + i * cardSpacing) * sign;
            Vector3 targetPos = new Vector3(x,0 , transform.position.z);

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

        // Any continuous effects granted by this card end when it leaves the
        // field (covers items removed when their holder dies — no death batch).
        cardInstance.Board?.AuraRegistry.UnregisterAllFrom(cardInstance);

        // destroy visual
        Destroy(cardsInPortal[index].visual.gameObject);

        // remove from list
        cardsInPortal.RemoveAt(index);

        if (index < cardsInPortal.Count)
        {
            var nextCard = cardsInPortal[index];
            nextCard.context.DetachCardFromThis();
            nextCard.visual.UpdateFieldCoverDisplay();

            if (nextCard.context is ItemInstance)
            {
                RemoveCard(nextCard.context); //recursivly removes spells or items that depend on a minion to be present
            }
        }
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
    public List<MinionInstance> GetAllMinionsInPortal()
    {
        List<MinionInstance> minions = new List<MinionInstance>();

        foreach (var entry in cardsInPortal)
        {
            if (entry.context.SourceCard.cardType is MinionType) minions.Add(entry.context as MinionInstance);
        }

        return minions;
    }

    public List<FieldableCardInstance> GetAllCardsInPortal()
    {
        List<FieldableCardInstance> cards = new List<FieldableCardInstance>();
        foreach (var entry in cardsInPortal) cards.Add(entry.context);
        return cards;
    }

    // First minion that can be picked by default attack targeting — stealthed
    // units are skipped (Stealth = untargetable, not damage-immune).
    public MinionInstance GetFirstTargetableMinion()
    {
        foreach (var entry in cardsInPortal)
        {
            if (entry.context is MinionInstance minion && !minion.HasStatusEffect(StatusEffectType.Stealth))
                return minion;
        }

        return null;
    }

    // Moves a minion to the front (combat position) or back of this portal.
    // The minion's attached items/spells directly follow it in the stack and
    // move with it as one block, so holder relationships stay intact.
    public void MoveMinion(MinionInstance minion, bool toFront)
    {
        int index = cardsInPortal.FindIndex(c => c.context == minion);
        if (index == -1) return;

        int blockEnd = index + 1;
        while (blockEnd < cardsInPortal.Count && cardsInPortal[blockEnd].context is not MinionInstance)
            blockEnd++;

        var block = cardsInPortal.GetRange(index, blockEnd - index);
        cardsInPortal.RemoveRange(index, blockEnd - index);

        if (toFront) cardsInPortal.InsertRange(0, block);
        else cardsInPortal.AddRange(block);

        UpdateCardPositions();
    }

    // Removes and returns the full card stack without destroying visuals or
    // detaching anything — used by Board.ShiftLanes to move stacks between
    // portals. Pair with ReceiveCards on the destination portal.
    public List<(FieldableCardInstance context, CardVisualizer visual)> TakeAllCards()
    {
        var taken = new List<(FieldableCardInstance, CardVisualizer)>(cardsInPortal);
        cardsInPortal.Clear();
        return taken;
    }

    public void ReceiveCards(List<(FieldableCardInstance context, CardVisualizer visual)> cards, Lane lane)
    {
        foreach (var entry in cards)
        {
            entry.context.SetSourcePortal(this).SetTargetLane(lane);
            cardsInPortal.Add(entry);
        }

        UpdateCardPositions();
    }
}