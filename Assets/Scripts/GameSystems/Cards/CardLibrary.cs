using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public static class CardLibrary
{
    private static Dictionary<string, CardData> _lookup = new();
    private static bool _initialized = false;

    public static async Task Initialize()
    {
        if (_initialized) return;

        var handle = Addressables.LoadAssetsAsync<CardData>("CardData", null);
        var cards = await handle.Task;

        foreach (var card in cards)
        {
            _lookup.Add(card.cardName, card);
        }

        _initialized = true;
    }

    public static CardData GetCard(string id)
    {
        if (!_initialized)
            Debug.LogError("CardLibrary not initialized!");

        return _lookup.TryGetValue(id, out var card) ? card : null;
    }
}