using System;
using System.Collections.Generic;
using UnityEngine;

// The match's action history: a bounded, newest-first list of HistoryEntry and
// the single source of truth for the history bar. Game logic reports semantic
// actions through the static Record(...) helper; the view (HistoryBarUI)
// observes Added / Evicted to build and trim the on-screen tiles.
//
// Keeping the model here — separate from the view — means combat and spell code
// never touch UI (they only Record), and the bar can be rebuilt from Entries at
// any time (late enable, scene reload). Capacity ("the past X events") lives on
// the model, so there is exactly one place that decides what falls off the end.
//
// Drop this on a GameObject in the gameplay scene (like DamageNumberSpawner /
// Announcer). It does not need to survive scene loads.
public class GameHistory : MonoBehaviour
{
    public static GameHistory Instance { get; private set; }

    [Tooltip("How many of the most recent entries to keep — the 'past X events'.")]
    [SerializeField] private int capacity = 8;

    private readonly List<HistoryEntry> entries = new(); // index 0 = newest

    // Fired after an entry is pushed to the front of the history.
    public event Action<HistoryEntry> Added;
    // Fired when an entry falls off the end because capacity was exceeded (or on
    // Clear). The view destroys the matching tile.
    public event Action<HistoryEntry> Evicted;

    public IReadOnlyList<HistoryEntry> Entries => entries; // newest-first
    public int Capacity => capacity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Safe entry point for game logic. No-ops if the service isn't in the scene,
    // so combat / spells never NRE just because a given scene runs without the
    // bar. This is the ONLY method gameplay code should call.
    public static void Record(HistoryEntry entry)
    {
        if (entry == null || Instance == null) return;
        Instance.Add(entry);
    }

    private void Add(HistoryEntry entry)
    {
        entries.Insert(0, entry);
        Added?.Invoke(entry);

        // Trim from the oldest end. capacity <= 0 is treated as unbounded.
        while (capacity > 0 && entries.Count > capacity)
        {
            HistoryEntry evicted = entries[entries.Count - 1];
            entries.RemoveAt(entries.Count - 1);
            Evicted?.Invoke(evicted);
        }
    }

    // Wipes the history (e.g. on a new match). Evicts from the oldest end so the
    // view can animate tiles out in a natural order.
    public void Clear()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            HistoryEntry evicted = entries[i];
            entries.RemoveAt(i);
            Evicted?.Invoke(evicted);
        }
    }
}
