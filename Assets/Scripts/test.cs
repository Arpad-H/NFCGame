using UnityEngine;

public class HandTester : MonoBehaviour
{
    [SerializeField] private CardHand hand;
    [SerializeField] private int startCards = 5;

    private void Start()
    {
        for (int i = 0; i < startCards; i++)
        {
            hand.AddCard();
        }
    }

    private void Update()
    {
        // Press SPACE to add cards during play
        if (Input.GetKeyDown(KeyCode.Space))
        {
            hand.AddCard();
        }
    }
}