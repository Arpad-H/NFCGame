using System;
using System.Collections.Generic;
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

    // Blind (player-level, timed): while turns remain, every attack by this
    // player's minions has BlindMissPercent chance to whiff — including minions
    // played during the window. Applied by BlindPlayerEffect, rolled in the
    // combat/attack paths, ticked once per turn end by GameManager.EndTurn
    // (same cadence as status durations).
    public const int BlindMissPercent = 50;
    public int BlindTurnsRemaining { get; private set; }
    public bool IsBlinded => BlindTurnsRemaining > 0;

    public void ApplyBlind(int turns)
    {
        // Reapplying doesn't stack; the longer remaining duration wins.
        BlindTurnsRemaining = Math.Max(BlindTurnsRemaining, turns);
        Debug.Log($"{this} is BLINDED for {BlindTurnsRemaining} turn(s).");
    }

    public void TickBlind()
    {
        if (BlindTurnsRemaining > 0 && --BlindTurnsRemaining == 0)
        {
            Debug.Log($"{this} is no longer blinded.");
        }
    }

    // Cards this player has lost to a discard effect (e.g. DiscardLastPlacedEffect).
    // Stores card identities, so a future "recover from discard" makes a fresh
    // instance rather than resurrecting one carrying old runtime state.
    public readonly List<CardData> DiscardPile = new();

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

    // Takes one of this player's fielded cards off the board and back into the
    // hand. The hand is a physical-card count (the announcer has already told
    // the player to pick the card up), so the digital side only bumps it.
    // Mirrors Board.SendToDiscard's leave-the-field steps: effect-field cleanup
    // first, then removal — but the card's identity goes to the hand, not the pile.
    public async Task ReturnCardToHand(FieldableCardInstance card)
    {
        if (card == null) return;
        Portal portal = card.SourcePortal;

        // The card's own deactivation cleanup, while its fields are still active.
        await card.DetachCardFromThis();

        // A rune-supplying item activates the effect field of the card directly
        // beneath it; that neighbour must release those runes when the item goes.
        if (card is ItemInstance && portal != null)
        {
            FieldableCardInstance below = portal.GetCardDirectlyBelow(card);
            if (below != null) await below.DetachCardFromThis();
        }

        cardHand.AddCard(1);

        // Off the board without a death: RemoveCard also unregisters this card's
        // auras and cascades to anything stacked on top of it.
        portal?.RemoveCard(card);

        card.Board?.AuraRegistry.Reevaluate();
    }

    // Files a card into this player's discard pile. Called by Board.SendToDiscard
    // when one of this player's fielded cards is discarded from the board.
    public void AddToDiscardPile(CardData card)
    {
        if (card == null) return;
        DiscardPile.Add(card);
        Debug.Log($"{this} discarded {card.cardName} (pile now {DiscardPile.Count}).");
    }

    // MUST stay `override` — see CardInstance.ToString.
    public override string ToString()
    {
        return $"Player{playerId} [HP {health}/{maxHealth}, SHIELD {Shield}]";
    }
}