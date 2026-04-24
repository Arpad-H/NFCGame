using GameSystems;
using TMPro;
using UnityEngine;

public class Player : MonoBehaviour,ITargetable
{
    public int health;
    public int maxHealth;
    public PlayerSide playerSide;
    public TextMeshProUGUI healthText;
    
    public void TakeDamage(int amount)
    {
        health -= amount;
        healthText.text = health.ToString();
    }
    public void Heal(int amount)  
    {
        health += amount;
        if (health > maxHealth) health = maxHealth;
        healthText.text = health.ToString();
    }
}