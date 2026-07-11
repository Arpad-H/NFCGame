using System;

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

    // One authored beat of the tutorial: what to tell the player, which card
    // (if any) they are allowed to play, and what event moves on to the next
    // step. Rendered by the debug overlay for now; NotificationView (M3) will
    // take over the player-facing presentation.
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

        public Action OnEnter;
        public Action OnExit;
    }
}
