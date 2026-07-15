using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class LibraryManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject libraryWindow;
    public Transform gridContent;
    [Tooltip("Full-card prefab for minions and items (FieldableCardVisualizer).")]
    public GameObject fieldableCardPrefab;
    [Tooltip("Full-card prefab for spells (SpellCardVisualizer).")]
    public GameObject spellCardPrefab;
    public GameObject wrapperPrefab;

    [Header("Card focus")]
    [Tooltip("Handles the hover outline + blip, right-click focus-to-centre, and the " +
             "background blur. Assign the LibraryCardFocusController on the library window.")]
    public LibraryCardFocusController focusController;

    private List<(CardVisualizer visualizer, CardData data)> spawnedCards = new();
    private bool isInitialized = false;

    private void Start()
    {
       
    }

    private async Task InitializeLibrary()
    {
        await CardLibrary.Initialize();
    }
    public async void OpenLibrary()
    {
        libraryWindow.SetActive(true);

        if (!isInitialized)
        {
            await InitializeLibrary();
            List<CardData> cards = CardLibrary.GetCards();
            foreach (CardData data in cards)
            {
                GameObject wrapper = Instantiate(wrapperPrefab, gridContent);

                GameObject prefab = CardPrefabResolver.Resolve(data, fieldableCardPrefab, spellCardPrefab);
                GameObject newCard = Instantiate(prefab, wrapper.transform);

                newCard.transform.localPosition = Vector3.zero;

                float cardScale = 25f;
                Vector3 finalScale = new Vector3(cardScale, cardScale, cardScale);
                newCard.transform.localScale = finalScale;

                CardVisualizer display = newCard.GetComponent<CardVisualizer>();
                display.SetupForLibrary(data);

                LibraryCardInteraction interaction = newCard.AddComponent<LibraryCardInteraction>();
                interaction.Init(focusController);

                spawnedCards.Add((display, data));
            }
            isInitialized = true;
        }

       // FilterByCategory(0);
    }

    public void CloseLibrary()
    {
        libraryWindow.SetActive(false);
       // isInitialized = false;
    }

    public void FilterByCategory(int categoryIndex)
    {
        ResonanceType selectedCategory = (ResonanceType)categoryIndex;
        if (categoryIndex == -1) //all are active
        { 
            foreach (var (visualizer, data) in spawnedCards)
            {
                visualizer.transform.parent.gameObject.SetActive(true);
            }
            return;
        }
        foreach (var (visualizer, data) in spawnedCards)
        {
            visualizer.transform.parent.gameObject.SetActive(data.resonance == selectedCategory);
        }
    }
}