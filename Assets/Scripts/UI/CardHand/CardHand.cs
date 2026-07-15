using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CardHand : MonoBehaviour
{
    [Header("Setup")] [SerializeField] private GameObject cardPrefab;

    private List<CardView> cards = new List<CardView>();
    public int cardCount = 0;
    [Header("Layout")] [SerializeField] private float spacing = 120f;
    [SerializeField] private float curveHeight = 25f;
    [SerializeField] private float maxRotation = 15f;

    [Header("Animation")] [SerializeField] private float moveSpeed = 10f;

    //TODO temp just numbers
    public TextMeshProUGUI cardText;

    public void AddCard(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            GameObject obj = Instantiate(cardPrefab, transform);
            CardView card = obj.GetComponent<CardView>();
            cards.Add(card);
        }

        cardCount = cards.Count;
        UpdateLayout();
    }

    public void DiscardCard(int amount)
    {
        for (int i = 0; i < amount && cards.Count > 0; i++)
        {
            // Remove the most recently added card from the fan.
            CardView card = cards[cards.Count - 1];
            cards.RemoveAt(cards.Count - 1);
            cardTargets.Remove(card);
            if (card != null) Destroy(card.gameObject);
        }

        cardCount = cards.Count;
        UpdateLayout();
    }

    private void Update()
    {
        AnimateCards();
    }

    private void UpdateLayout()
    {
        //TOODO temp just numbers
        cardText.text = $"{cards.Count}";
        //return;


        if (cards.Count == 0) return;

        float mid = (cards.Count - 1) / 2f;

        for (int i = 0; i < cards.Count; i++)
        {
            CardView card = cards[i];
        
            float offset = i - mid;
        
            // Target position
            float x = offset * spacing;
            float y = -Mathf.Pow(offset, 2) * curveHeight;
        
            Vector2 targetPos = new Vector2(x, y);
        
            // Target rotation
            float rotation = -offset * maxRotation;
            Quaternion targetRot = Quaternion.Euler(0, 0, rotation);
        
            // Store targets in component
            card.Rect.SetSiblingIndex(i);
        
            cardTargets[card] = (targetPos, targetRot);
        }
    }

    // --- Internal animation system ---
    private Dictionary<CardView, (Vector2 pos, Quaternion rot)> cardTargets
        = new Dictionary<CardView, (Vector2, Quaternion)>();

    private void AnimateCards()
    {
        foreach (var card in cards)
        {
            if (!cardTargets.ContainsKey(card)) continue;
    
            var target = cardTargets[card];
    
            // Smooth movement
            card.Rect.anchoredPosition = Vector2.Lerp(
                card.Rect.anchoredPosition,
                target.pos,
                Time.deltaTime * moveSpeed
            );
    
            // Smooth rotation
            card.Rect.localRotation = Quaternion.Lerp(
                card.Rect.localRotation,
                target.rot,
                Time.deltaTime * moveSpeed
            );
        }
    }
}