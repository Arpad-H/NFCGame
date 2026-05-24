using System.Collections.Generic;
using UnityEngine;

public class LibraryManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject libraryWindow;
    public Transform gridContent;
    public GameObject cardPrefab;

    [Header("Data")]
    public List<CardScriptableObject> allCardsInGame;
    
    private List<CardDisplay> spawnedCards = new List<CardDisplay>();
    private bool isInitialized = false;

    public void OpenLibrary()
    {
        libraryWindow.SetActive(true);

        if (!isInitialized)
        {
            InitializeLibrary();
        }
        
        FilterByCategory(0);
    }

    public void CloseLibrary()
    {
        libraryWindow.SetActive(false);
    }

    private void InitializeLibrary()
    {
        foreach (CardScriptableObject cardData in allCardsInGame)
        {
            GameObject newCard = Instantiate(cardPrefab, gridContent);
            CardDisplay display = newCard.GetComponent<CardDisplay>();
            
            display.Setup(cardData);
            spawnedCards.Add(display);
        }
        isInitialized = true;
    }
    
    public void FilterByCategory(int categoryIndex)
    {
        CardCategory selectedCategory = (CardCategory)categoryIndex;

        foreach (CardDisplay card in spawnedCards)
        {
            bool matches = card.cardData.category == selectedCategory;
            card.gameObject.SetActive(matches);
        }
    }
}