using System;
using GameSystems;
using TMPro;
using UnityEngine;
using System.Threading.Tasks;

public class Player : MonoBehaviour,IPlayerTargetable
{
    public int health;
    public int maxHealth;
    public PlayerSide playerSide;
    public TextMeshProUGUI healthText;
    public CardHand cardHand;
    public Action OnCardDrawn;
    public Action OnCardDiscarded;
    public Task TakeDamage(DamageEventData eventData)
    {
        health -= eventData.Amount;
        healthText.text = health.ToString();
        return Task.CompletedTask;
    }
    public void Heal(int amount)  
    {
        health += amount;
        if (health > maxHealth) health = maxHealth;
        healthText.text = health.ToString();
    }
    public async Task DrawCard(int amount)
    {
        cardHand.AddCard(amount);
        await Task.Delay(1000); //TODO replace with anim or player prompt
        OnCardDrawn?.Invoke();
    }  
    public async Task DiscardCard(int amount)
    {
        cardHand.DiscardCard(amount);
        await Task.Delay(1000); //TODO replace with anim or player prompt
        OnCardDiscarded?.Invoke();
    }
    public void CardPlayed()
    {
        cardHand.DiscardCard(1); //no discard since card is played, not discarded, but it removes the card from hand count
    }
}