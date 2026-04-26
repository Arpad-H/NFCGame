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
    }  
    public void DiscardCard(int amount)
    {
        cardHand.DiscardCard(amount);
    }
}