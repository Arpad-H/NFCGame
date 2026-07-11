using System.Collections.Generic;

namespace Riftborn.Tutorial
{
    // The authored, ordered step list the director runs. Hard-coded in C# for
    // now (plan section 8); move to ScriptableObjects only if authoring hurts.
    //
    // This is the M1 walking-skeleton sequence — it exercises every advance
    // mode so the framework can be tested end to end. The real teaching script
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
                },
                new TutorialStep
                {
                    Id = "free-play",
                    Body = "Play any card (debug panel: PLAY_CARD). Playing a card ends your turn.",
                    Advance = StepAdvance.CardPlayed,
                    AllowAnyCard = true,
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
                },
            };
        }
    }
}
