using System.Collections.Generic;
using UnityEngine;

public class LibraryManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject libraryWindow;
    public Transform gridContent;
    public GameObject cardPrefab;
    public GameObject wrapperPrefab;

    [Header("Your Cards")]
    public List<CardData> allCardsInGame;
    
    private List<CardVisualizer> spawnedCards = new List<CardVisualizer>();
    private bool isInitialized = false;

    public void OpenLibrary()
    {
        libraryWindow.SetActive(true);

        if (!isInitialized)
        {
            foreach (CardData data in allCardsInGame)
            {

                GameObject wrapper = Instantiate(wrapperPrefab, gridContent);
                

                GameObject newCard = Instantiate(cardPrefab, wrapper.transform);
                

                newCard.transform.localPosition = Vector3.zero;
    

                float cardScale = 25f; 
                Vector3 finalScale = new Vector3(cardScale, cardScale, cardScale);
                newCard.transform.localScale = finalScale;


                CardVisualizer display = newCard.GetComponent<CardVisualizer>();
                display.SetupForLibrary(data);
                
                display.SetBaseScale(finalScale);
                spawnedCards.Add(display);
            }
            isInitialized = true;
        }

        FilterByCategory(0); 
    }

    public void CloseLibrary()
    {
        libraryWindow.SetActive(false);
    }

    public void FilterByCategory(int categoryIndex)
    {
        ResonanceType selectedCategory = (ResonanceType)categoryIndex;

        foreach (CardVisualizer card in spawnedCards)
        {
            CardData originalData = allCardsInGame.Find(c => c.cardName == card.Name.text);
            
            if (originalData != null)
            {
                bool matches = (originalData.resonance == selectedCategory);
                
                card.transform.parent.gameObject.SetActive(matches);
            }
        }
    }
}