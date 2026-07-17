using System;
using System.Collections.Generic;
using GameSystems;
using UnityEngine;

// ── History data model ────────────────────────────────────────────────────────
//
// A HistoryEntry describes ONE thing that happened in the match, in terms the UI
// can draw without reaching back into live game state: who acted, who it landed
// on, how much, and what kind of action it was. Game logic (combat, spells)
// reports these through GameHistory.Record(...); HistoryBarUI turns them into
// tiles. Nothing here touches Unity UI, so the model can be unit-tested and the
// bar rebuilt from Entries at any time.

// One actor on a tile — a card (minion / spell) or a player (face hits have no
// card). Display fields (Icon / Name / Side) are SNAPSHOTTED at record time so a
// tile never draws itself from mutating game state; the live CardInstance is
// kept only for click-to-inspect and is null for a player actor.
public readonly struct HistoryActor
{
    public readonly Sprite Icon;      // card artwork; null for a player
    public readonly string Name;
    public readonly PlayerSide Side;  // which side owns/initiated it
    public readonly bool IsPlayer;    // true = a face hit target (no card art)
    public readonly CardInstance Card; // live ref for inspect; null when IsPlayer

    private HistoryActor(Sprite icon, string name, PlayerSide side, bool isPlayer, CardInstance card)
    {
        Icon = icon;
        Name = name;
        Side = side;
        IsPlayer = isPlayer;
        Card = card;
    }

    // A minion, spell, or item instance.
    public static HistoryActor FromCard(CardInstance card)
    {
        CardData data = card != null ? card.SourceCard : null;
        PlayerSide side = card != null && card.Owner != null ? card.Owner.playerSide : default;
        return new HistoryActor(data != null ? data.artwork : null,
            data != null ? data.cardName : "?", side, false, card);
    }

    // The enemy hero on a face hit — no artwork, so the tile hides the image (or
    // the prefab can supply a default hero icon).
    public static HistoryActor FromPlayer(Player player)
    {
        return new HistoryActor(null, player != null ? $"Player {player.playerId}" : "?",
            player != null ? player.playerSide : default, true, null);
    }

    // A combat target, which may be a minion, the enemy player, or a portal.
    public static HistoryActor FromTarget(ITargetable target)
    {
        switch (target)
        {
            case MinionInstance minion: return FromCard(minion);
            case Player player: return FromPlayer(player);
            case Portal portal: return FromPortal(portal);
            default: return new HistoryActor(null, target != null ? target.ToString() : "?",
                default, false, target as CardInstance);
        }
    }

    // A portal target — no card, so its icon is its resonance's floor-rune (the
    // decal mask defined on the Resonance SO), and its name is that resonance's
    // identity.
    public static HistoryActor FromPortal(Portal portal)
    {
        Resonance res = portal != null ? portal.resonance : null;
        Sprite icon = res != null ? res.DecalSprite : null;
        string label = res != null && !string.IsNullOrEmpty(res.identity) ? res.identity
            : (portal != null ? portal.ToString() : "?");
        PlayerSide side = portal != null ? portal.ownerSide : default;
        return new HistoryActor(icon, label, side, false, null);
    }
}

// What kind of action a tile represents. Attack and Heal share the same duel
// layout (source → target + amount); Kind only drives the tint and, together
// with Lethal, the overlay icons (skull). Play is a card entering the game,
// with 0..N affected targets.
public enum HistoryKind
{
    Attack, // source dealt damage to its target(s)
    Heal,   // source healed its target(s)
    Play,   // a card was played (spell / minion / item)
}

public class HistoryEntry
{
    public readonly HistoryKind Kind;
    public readonly HistoryActor Source;

    // 0 = a lone action (a card played with no target), 1 = a duel
    // (source → target), N = a multi-target effect (rendered by the multi tile).
    public readonly IReadOnlyList<HistoryActor> Targets;

    public readonly int Amount;  // damage or healing; 0 where not applicable
    public readonly bool Lethal; // a single-target hit that killed its target (skull)

    public HistoryActor? Target => Targets.Count > 0 ? Targets[0] : (HistoryActor?)null;
    public bool IsMultiTarget => Targets.Count > 1;

    public HistoryEntry(HistoryKind kind, HistoryActor source, IReadOnlyList<HistoryActor> targets = null,
        int amount = 0, bool lethal = false)
    {
        Kind = kind;
        Source = source;
        Targets = targets ?? Array.Empty<HistoryActor>();
        Amount = amount;
        Lethal = lethal;
    }
}
