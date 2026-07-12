using System.Collections.Generic;

namespace Riftborn.Tutorial
{
    // The authored, ordered step list the director runs. Hard-coded in C# for
    // now (plan section 8); move to ScriptableObjects only if authoring hurts.
    //
    // This is the M1 walking-skeleton sequence, now exercising the M3/M4
    // presentation layer (camera shots, highlight ring + dim, prompt panel) so
    // the whole pipeline is testable end to end. The real teaching script
    // (plan section 7) replaces it in M6.
    public static class TutorialSequence
    {
        public static List<TutorialStep> Build(TutorialDirector director)
        {
            return new List<TutorialStep>
            {
                new TutorialStep
                {
                    Id = "welcome",
                    Body = "Welcome to Riftborn! (Skeleton sequence — the real teaching content lands in M6.)",
                    Advance = StepAdvance.Hold,
                    HoldSeconds = 3f,
                    Camera = CameraShot.FullBoard,
                },
                new TutorialStep
                {
                    Id = "free-play",
                    Body = "Play any card (debug panel: PLAY_CARD). The arrow marks your lane-1 portal. Playing a card ends your turn.",
                    Advance = StepAdvance.CardPlayed,
                    AllowAnyCard = true,
                    Camera = CameraShot.SingleLane,
                    CameraLane = 0,
                    Highlight = HighlightTarget.Portal(director.humanSide, 0),
                    DimBackground = true,
                },
                new TutorialStep
                {
                    Id = "watch-combat",
                    Body = "Watch the fight — combat runs by itself after every play. No targeting, no attack orders.",
                    Advance = StepAdvance.CombatResolved,
                },
                new TutorialStep
                {
                    Id = "sandbox",
                    Body = "Skeleton finished — free play from here. The enemy plays its queued cards on its own turns.",
                    Advance = StepAdvance.Manual,
                    AllowAnyCard = true,
                    Camera = CameraShot.FullBoard,
                },
            };
        }
    }
}
