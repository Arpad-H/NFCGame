using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// Renders GameHistory as a horizontal bar of tiles, newest on the LEFT. Owns no
// game state: it mirrors GameHistory's Added / Evicted stream into tile
// instances and, when a tile is clicked, relays the entry for inspection.
//
// Put this on the bar's container (ideally a HorizontalLayoutGroup pinned to the
// top of the screen). New tiles are inserted at sibling index 0 so the layout
// group pushes older tiles to the right.
public class HistoryBarUI : MonoBehaviour
{
    [Header("Tile prefabs")]
    [Tooltip("Default tile: attack / kill / heal / single-target / card play.")]
    [SerializeField] private HistoryTile defaultTilePrefab;
    [Tooltip("Optional. Used when an entry affects more than one target. " +
             "Falls back to the default tile if left empty.")]
    [SerializeField] private HistoryTile multiTargetTilePrefab;

    [Header("Layout")]
    [Tooltip("Parent the tiles are spawned under (usually a HorizontalLayoutGroup). " +
             "Defaults to this transform.")]
    [SerializeField] private RectTransform tileContainer;

    [Header("Slot-in animation")]
    [SerializeField] private float slotInDuration = 0.25f;
    [SerializeField] private float slotOutDuration = 0.15f;

    // Tiles in the same newest-first order as GameHistory.Entries, plus a lookup
    // so an eviction finds its tile in O(1).
    private readonly List<HistoryTile> tiles = new();
    private readonly Dictionary<HistoryEntry, HistoryTile> tileByEntry = new();

    // Raised when a tile is clicked (click-to-inspect). The entry's Source /
    // Targets expose the live CardInstance refs a detail popup can use. Wire your
    // inspect/highlight logic to this from the inspector or in code.
    public event Action<HistoryEntry> TileClicked;

    private void Awake()
    {
        if (tileContainer == null) tileContainer = transform as RectTransform;
    }

    private bool subscribed;

    // Subscribe from both OnEnable and Start: OnEnable covers runtime toggling,
    // Start covers the first-frame race where our OnEnable runs before
    // GameHistory.Awake has set Instance (all Awakes precede all Starts).
    private void OnEnable() => TrySubscribe();
    private void Start() => TrySubscribe();

    private void TrySubscribe()
    {
        if (subscribed) return;
        GameHistory history = GameHistory.Instance;
        if (history == null) return;

        history.Added += HandleAdded;
        history.Evicted += HandleEvicted;
        subscribed = true;
        Rebuild(history); // pick up anything recorded before the bar subscribed
    }

    private void OnDisable()
    {
        if (!subscribed) return;
        GameHistory history = GameHistory.Instance;
        if (history != null)
        {
            history.Added -= HandleAdded;
            history.Evicted -= HandleEvicted;
        }
        subscribed = false;
    }

    // Rebuild from scratch (late enable / scene reload). Existing entries are
    // spawned oldest → newest so the final sibling order matches live adds.
    private void Rebuild(GameHistory history)
    {
        foreach (HistoryTile tile in tiles)
        {
            if (tile != null) Destroy(tile.gameObject);
        }
        tiles.Clear();
        tileByEntry.Clear();

        IReadOnlyList<HistoryEntry> entries = history.Entries;
        for (int i = entries.Count - 1; i >= 0; i--) SpawnFront(entries[i], animate: false);
    }

    private void HandleAdded(HistoryEntry entry) => SpawnFront(entry, animate: true);

    private void SpawnFront(HistoryEntry entry, bool animate)
    {
        HistoryTile prefab = entry.IsMultiTarget && multiTargetTilePrefab != null
            ? multiTargetTilePrefab
            : defaultTilePrefab;
        if (prefab == null)
        {
            Debug.LogWarning("HistoryBarUI: no tile prefab assigned.");
            return;
        }

        HistoryTile tile = Instantiate(prefab, tileContainer);
        tile.transform.SetSiblingIndex(0); // newest on the left
        tile.Bind(entry);
        tile.Clicked += HandleTileClicked;

        tiles.Insert(0, tile);
        tileByEntry[entry] = tile;

        if (animate)
        {
            tile.transform.localScale = Vector3.zero;
            tile.transform.DOScale(Vector3.one, slotInDuration).SetEase(Ease.OutBack);
        }
    }

    private void HandleEvicted(HistoryEntry entry)
    {
        if (!tileByEntry.TryGetValue(entry, out HistoryTile tile)) return;
        tileByEntry.Remove(entry);
        tiles.Remove(tile);
        if (tile == null) return;

        tile.Clicked -= HandleTileClicked;
        tile.transform.DOScale(Vector3.zero, slotOutDuration).SetEase(Ease.InBack)
            .OnComplete(() => { if (tile != null) Destroy(tile.gameObject); });
    }

    private void HandleTileClicked(HistoryTile tile)
    {
        if (tile != null && tile.Entry != null) TileClicked?.Invoke(tile.Entry);
    }
}
