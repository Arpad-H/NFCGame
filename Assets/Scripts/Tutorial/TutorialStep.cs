using System;
using GameSystems;

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
    }

    // What the highlight ring/arrow anchors to. Portal is the only kind the
    // script needs so far; extend it (front minion, history bar, ...) as the
    // M6 content asks for more.
    public enum HighlightKind
    {
        None,
        Portal,
    }

    public struct HighlightTarget
    {
        public HighlightKind Kind;
        public PlayerSide Side;
        public int Lane;

        public static HighlightTarget Portal(PlayerSide side, int lane) =>
            new HighlightTarget { Kind = HighlightKind.Portal, Side = side, Lane = lane };
    }

    // One authored beat of the tutorial: what to tell the player, which card
    // (if any) they are allowed to play, what the presentation shows (camera
    // shot, highlight), and what event moves on to the next step.
    public class TutorialStep
    {
        public string Id;
        public string Body;
        public StepAdvance Advance = StepAdvance.Hold;
        public float HoldSeconds = 4f;

        // The only card the player may play while this step is active. null
        // means no player play is legal here (unless AllowAnyCard is set).
        public string ExpectedCard;

        // Sandbox switch: any player card passes the validator on this step.
        public bool AllowAnyCard;

        // Presentation (M3/M4), applied by the director on step enter. Body is
        // shown in the NotificationView prompt panel; an empty Body hides it.
        public CameraShot Camera = CameraShot.Keep;
        public int CameraLane;            // used when Camera == SingleLane
        public HighlightTarget Highlight; // default Kind == None → no highlight
        public bool DimBackground;        // dim everything except the highlight

        public Action OnEnter;
        public Action OnExit;
    }
}
