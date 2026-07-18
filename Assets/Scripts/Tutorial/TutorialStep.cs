using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftborn.Tutorial
{
    // How a step decides it is finished. Info steps hold for a readable beat
    // (the board screen has no input device); action steps advance on the game
    // event their instruction asks for (GameManager's outside-driver seams).
    public enum StepAdvance
    {
        Auto,           // advance immediately after OnEnter
        Hold,           // advance after HoldSeconds
        Manual,         // only the debug overlay's force-advance moves on
        CardPlayed,     // the player successfully played this step's expected card
        CombatResolved, // the next combat resolution (incl. post-combat delay) finished
        LaneWon,        // a lane was awarded and cleared
        GameOver        // the match ended
    }

    // Which framing the camera tweens to when a step is entered.
    public enum CameraShot
    {
        Keep,       // leave the camera wherever the previous step put it
        FullBoard,  // the authored scene pose showing all 3 lanes
        SingleLane, // tight on one lane's two portals; set CameraLane
        Custom,     // an explicit pose: CameraPosition + CameraOrthoSize
    }

    // What the highlight ring/arrow anchors to. Portal is a lane portal (by
    // relative side); Anchor is a named TutorialAnchor marker placed in the
    // scene, so a highlight can point at anything the author positions.
    public enum HighlightKind
    {
        None,
        Portal,
        Anchor,
    }

    // Which pieces of a highlight to draw. Value 0 = both, so a freshly added
    // list element shows a ring and an arrow (the historical default).
    public enum HighlightParts
    {
        RingAndArrow,
        RingOnly,
        ArrowOnly,
    }

    // Highlight side, relative to the human player. The director resolves it to
    // a concrete PlayerSide at runtime, so an authored asset stays correct even
    // if humanSide is flipped, and authoring reads as "your portal" / "the
    // enemy's portal" rather than a fixed Left/Right.
    public enum HighlightSide
    {
        You,
        Foe,
    }

    // Side effects the director fires when a step enters/exits. Serializable
    // stand-in for the old OnEnter/OnExit delegates (only the final step uses
    // them); a [Flags] mask renders as a compact dropdown in the Inspector.
    [Flags]
    public enum StepHooks
    {
        None = 0,
        MarkCompleteOnEnter = 1, // persist the tutorial as completed on entering
        ReturnToMenuOnExit = 2,  // load the main menu when this step exits
    }

    // One ring/arrow highlight. A zero-initialized value (Kind = None) draws
    // nothing; set Kind to Portal or Anchor and pick the target. Parts, ArrowClock
    // and WorldRadius all default (0) to "ring + arrow, arrow at the top, system
    // ring size", so a new list element behaves like the old single highlight.
    [Serializable]
    public struct HighlightTarget
    {
        public HighlightKind Kind;

        [Tooltip("Portal target: which side (You = the human's side).")]
        public HighlightSide Side;
        [Tooltip("Portal target: lane index (0 = top, 1 = middle, 2 = bottom).")]
        public int Lane;

        [Tooltip("Anchor target: id of the TutorialAnchor in the scene to point at (case-insensitive).")]
        public string AnchorId;

        [Tooltip("Draw a ring, an arrow, or both.")]
        public HighlightParts Parts;
        [Tooltip("Arrow position around the ring, as a clock hour: 12 (or 0) = top, 3 = right, 6 = bottom, 9 = left. Decimals allowed.")]
        public float ArrowClock;
        [Tooltip("Ring size in world units. 0 = use the HighlightSystem default (~2.2, good for a portal).")]
        public float WorldRadius;

        public static HighlightTarget Portal(HighlightSide side, int lane) =>
            new HighlightTarget { Kind = HighlightKind.Portal, Side = side, Lane = lane };

        public static HighlightTarget Anchor(string id) =>
            new HighlightTarget { Kind = HighlightKind.Anchor, AnchorId = id };
    }

    // What a dim zone's bright hole is anchored to. Portal / Anchor track a world
    // Transform (so the hole follows a moving or zoomed target, exactly like a
    // highlight); ScreenRect is a fixed rectangle in screen space, for UI regions
    // that aren't on the board.
    public enum DimZoneKind
    {
        Portal,
        Anchor,
        ScreenRect,
    }

    // One bright rectangle the dim leaves un-darkened, authored independently of
    // the ring/arrow highlights (the "custom zone" for the dim). Adding any zone to
    // a step turns the dim on for that step. Every zone — plus the highlights, when
    // DimBackground is on — merges into a SINGLE bounding hole: the shader-free
    // 4-rect dimmer can only cut one rectangle, so several zones give one hole
    // spanning all of them, not separate spotlights.
    [Serializable]
    public struct DimZone
    {
        public DimZoneKind Kind;

        [Tooltip("Portal target: which side (You = the human's side).")]
        public HighlightSide Side;
        [Tooltip("Portal target: lane index (0 = top, 1 = middle, 2 = bottom).")]
        public int Lane;

        [Tooltip("Anchor target: id of the TutorialAnchor to centre the hole on (case-insensitive).")]
        public string AnchorId;

        [Tooltip("Portal/Anchor: half-size of the bright hole in world units (X = camera right, Y = camera up). 0 on an axis = the HighlightSystem default (~2.2).")]
        public Vector2 WorldSize;

        [Tooltip("ScreenRect only: bright rectangle in normalised viewport coords — (x,y) = centre, (width,height) = size, all 0..1. E.g. centre (0.5,0.5) size (0.4,0.3).")]
        public Rect ScreenRect;

        public static DimZone Portal(HighlightSide side, int lane, Vector2 worldSize = default) =>
            new DimZone { Kind = DimZoneKind.Portal, Side = side, Lane = lane, WorldSize = worldSize };

        public static DimZone Anchor(string id, Vector2 worldSize = default) =>
            new DimZone { Kind = DimZoneKind.Anchor, AnchorId = id, WorldSize = worldSize };

        public static DimZone Screen(Rect normalizedRect) =>
            new DimZone { Kind = DimZoneKind.ScreenRect, ScreenRect = normalizedRect };
    }

    // One authored beat of the tutorial: what to tell the player, which card
    // (if any) they are allowed to play, what the presentation shows (camera
    // shot, highlight), and what event moves on to the next step.
    //
    // [Serializable] so a TutorialSequenceAsset can expose a reorderable list of
    // these in the Inspector. TutorialSequence.Build() is the built-in fallback
    // and the seed the "Create Sequence Asset From Code" menu item copies.
    [Serializable]
    public class TutorialStep
    {
        [Header("Content")]
        [Tooltip("Short label for logs and the debug overlay — not shown to the player.")]
        public string Id;

        [Tooltip("Instruction shown in the prompt panel. Supports line breaks and TMP rich text (<b>, <color=#ffcc00>). Leave empty to hide the panel for this beat.")]
        [TextArea(2, 6)]
        public string Body;

        [Header("Advance")]
        [Tooltip("What ends this step and moves to the next one.")]
        public StepAdvance Advance = StepAdvance.Hold;

        [Tooltip("Seconds to wait before auto-advancing. Only used when Advance = Hold.")]
        [Min(0f)]
        public float HoldSeconds = 4f;

        [Tooltip("The only card the player may play while this step is active. Empty = no play is legal here (unless Allow Any Card is on).")]
        public string ExpectedCard;

        [Tooltip("Sandbox switch: any player card passes the validator on this step.")]
        public bool AllowAnyCard;

        [Header("Presentation")]
        [Tooltip("Camera framing applied when the step is entered.")]
        public CameraShot Camera = CameraShot.Keep;

        [Tooltip("Lane the camera frames when Camera = SingleLane (0 = top, 1 = middle, 2 = bottom).")]
        public int CameraLane;

        [Tooltip("World position the camera tweens to when Camera = Custom. The camera keeps its current top-down rotation.")]
        public Vector3 CameraPosition;

        [Tooltip("Orthographic size (HALF the view height in world units) the camera zooms to when Camera = Custom. Smaller = more zoomed in. The shipped board camera sits at ~12.6.")]
        [Min(0.01f)]
        public float CameraOrthoSize = 12.62f;

        [Tooltip("Ring/arrow highlights shown this step. Add several to point at multiple things at once; leave empty for no highlight.")]
        public List<HighlightTarget> Highlights = new();

        // Legacy single highlight from pre-list assets. Hidden in the Inspector;
        // the director folds it in only when Highlights is empty, so older
        // authored assets keep working without a re-seed.
        [HideInInspector]
        public HighlightTarget Highlight;

        [Tooltip("Dim everything except a hole around the highlight(s) and any Dim Zones below. With no highlights and no zones there is nothing to leave bright, so nothing is dimmed.")]
        public bool DimBackground;

        [Tooltip("Bright areas the dim leaves un-darkened, placed independently of the ring/arrow. Adding any zone turns the dim on for this step. All zones (plus the highlights, when Dim Background is on) merge into ONE bounding hole.")]
        public List<DimZone> DimZones = new();

        [Header("Lifecycle")]
        [Tooltip("Side effects fired on enter/exit. Used by the final step (mark complete, return to menu).")]
        public StepHooks Hooks = StepHooks.None;
    }
}
