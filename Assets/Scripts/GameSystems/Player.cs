using System;
using GameSystems;
using TMPro;
using UnityEngine;

public class Player : MonoBehaviour,IPlayerTargetable
{
    public int health;
    public int maxHealth;
    public PlayerSide playerSide;
    public TextMeshProUGUI healthText;
    public CardHand cardHand;
    public Action OnCardDrawn;
    public Action OnCardDiscarded;
    public void TakeDamage(DamageEventData eventData)
    {
        health -= eventData.Amount;
        healthText.text = health.ToString();
    }
    public void Heal(int amount)  
    {
        health += amount;
        if (health > maxHealth) health = maxHealth;
        healthText.text = health.ToString();
    }
    public void DrawCard(int amount)
    {
        cardHand.AddCard(amount);
        OnCardDrawn?.Invoke();
    }  
    public void DiscardCard(int amount)
    {
        cardHand.DiscardCard(amount);
        OnCardDiscarded?.Invoke();
    }
    public void CardPlayed()
    {
        cardHand.DiscardCard(1); //no discard since card is played, not discarded, but it removes the card from hand count
    }
}