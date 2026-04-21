using GameSystems;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    TurnIndicator turnIndicator;
    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        turnIndicator = FindAnyObjectByType<TurnIndicator>();
    }

     public void SwitchPlayerTurn(PlayerSide newSide)
    {
       turnIndicator.SwitchTurn(newSide);
    }

  
}
