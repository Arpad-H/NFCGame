using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class LibraryManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject libraryWindow;
    public Transform gridContent;
    public GameObject cardPrefab;
    public GameObject wrapperPrefab;

    private List<CardVisualizer> spawnedCards = new List<CardVisualizer>();
    private bool isInitialized = false;

    private async void Start()
    {
        await CardLibrary.Initialize();
    }

    public void OpenLibrary()
    {
        libraryWindow.SetActive(true);

        if (!isInitialized)
        {
            foreach (CardData data in CardLibrary.GetCards())
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
            CardData originalData = CardLibrary.GetCard(card.Name.text);

            if (originalData != null)
            {
                bool matches = (originalData.resonance == selectedCategory);
                card.transform.parent.gameObject.SetActive(matches);
            }
        }
    }
}