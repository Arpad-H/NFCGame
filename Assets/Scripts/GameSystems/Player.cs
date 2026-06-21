using System;
using GameSystems;
using TMPro;
using UnityEngine;
using System.Threading.Tasks;

public class Player : MonoBehaviour, IPlayerTargetable
{
    public int playerId;
    public int health;
    public int maxHealth;
    public PlayerSide playerSide;
    public TextMeshProUGUI healthText;
    public CardHand cardHand;
    public Action OnCardDrawn;
    public Action OnAboutToDrawCard;
    public Action OnCardDiscarded;

    // Temporary damage absorption, consumed before health (AddShieldEffect).
    public int Shield { get; set; }

    public Task TakeDamage(DamageEventData eventData)
    {
        int remaining = eventData.Amount;
        if (Shield > 0)
        {
            int absorbed = Math.Min(Shield, remaining);
            Shield -= absorbed;
            remaining -= absorbed;
        }

        AudioManager.Instance.PlayMinionClashSound();
        health -= remaining;
        healthText.text = health.ToString();
        return Task.CompletedTask;
    }

    public Task Heal(HealEventData healEventData)
    {
        health += healEventData.Amount;
        if (health > maxHealth) health = maxHealth;
        healthText.text = health.ToString();
        return Task.CompletedTask;
    }

    public Task ModifyStat(MinionStats stat, int amount)
    {
        switch (stat)
        {
            case MinionStats.Attack:
                Debug.LogWarning("Player does not have attack stat!");
                break;
            case MinionStats.Health:
                health += amount;
                if (health > maxHealth) health = maxHealth;
                healthText.text = health.ToString();
                break;
        }

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
        OnAboutToDrawCard?.Invoke();
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

    public void ReturnCardToHand(FieldableCardInstance fieldableCardInstance)
    {
        Debug.LogWarning("Returning card to hand is not implemented yet!");
        Debug.Log($"Card {fieldableCardInstance.SourceCard.cardName} should be returned to player {playerId}'s hand.");
    }

    public string ToString()
    {
        return $"Player {playerId}";
    }
}