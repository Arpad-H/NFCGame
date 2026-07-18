using System.Collections.Generic;

namespace Riftborn.Tutorial
{
    // The built-in teaching script (plan section 7, M6). This is now the
    // FALLBACK: the director prefers a TutorialSequenceAsset (inspector-authored)
    // and only calls Build() when no asset is wired or found in Resources. It is
    // also the seed the "Riftborn ▸ Tutorial ▸ Create Sequence Asset From Code"
    // menu item copies into a new asset, so editing here still matters.
    //
    // The whole match is choreographed against the fixed bootstrap setup
    // (player: Death/Holy/Plague on lanes 0/1/2, enemy: Darkness/Psychic/Life,
    // portals at 4 HP, player goes first) and the ScriptedEnemyQueue default
    // (Bruiser, Plantkeeper, Bruiser, Bruiser, Bruiser — all lane 2):
    //
    //   T1  player: Plague Doctor → lane 2 fronts, hits the open portal 4→2
    //   T2  enemy:  Bruiser        → lane 2, the clash grind begins
    //   T3  player: Rat            → stacks behind the Doctor
    //   T4  enemy:  Plantkeeper    → stacks behind the Bruiser
    //   T5  player: Beaked Mask    → attaches to Rat (last card), infects it
    //   T6  enemy:  Bruiser #2
    //   T7  player: Bloodletting   → infected Rat attacks from the back row
    //                                (the Mask's 3-round infection expires at
    //                                this turn's end — T7 is the last turn the
    //                                spell has a target, so keep it at T7)
    //   T8  enemy:  Bruiser #3
    //   T9  player: Temple Guard   → lane 0, open portal 4→2
    //   T10 enemy:  Bruiser #4     → Guard finishes lane 0: LaneWon
    //   T11 player: Pastafari Priest → lane 1, open portal 4→2
    //   T12 enemy:  queue empty, skips → Priest finishes lane 1: 2-0, GameOver
    //
    // Lane 2 is a deliberate stalemate (Bruiser out-heals the chip damage) so
    // the teaching lane never resolves; only the two undefended lanes decide
    // the match, and nothing scripted depends on lane-2 hit points.
    //
    // Step-ordering rules (the director advances the CURRENT step only):
    //  - A step that advances on CombatResolved/LaneWon/GameOver must already
    //    be current before that event can fire. Consecutive game events need
    //    consecutive event steps — never put a timed Hold between two events,
    //    or the second event fires while the Hold is up and is lost, and the
    //    validator then deadlocks the player out of the recovery play.
    //  - Holds are only safe right before a CardPlayed step: the engine sits
    //    waiting for the player, so no event can slip past.
    //  - LaneWon fires BEFORE the same combat's CombatResolved, so the step
    //    after a LaneWon step must tolerate that trailing event (Hold or
    //    CardPlayed, never CombatResolved).
    //  - The game-ending combat never fires CombatResolved (GameOver replaces
    //    it), so the final stretch advances on GameOver.
    public static class TutorialSequence
    {
        public static List<TutorialStep> Build()
        {
            return new List<TutorialStep>
            {
                new TutorialStep
                {
                    Id = "welcome",
                    Body = "Welcome to Riftborn!\n" +
                           "This is a real match against the Rift Warden — I call the shots, you play the cards.",
                    Advance = StepAdvance.Hold,
                    HoldSeconds = 6f,
                    Camera = CameraShot.FullBoard,
                },
                new TutorialStep
                {
                    Id = "assemble-hand",
                    Body = "Build your tutorial hand. Take these 6 cards from your deck and hold them:\n" +
                           "Plague Doctor, Rat, Beaked Mask, Bloodletting, Skeletal Temple Guard, Pastafari Priest.\n" +
                           "Set the rest of the deck aside.",
                    Advance = StepAdvance.Hold,
                    HoldSeconds = 18f,
                },
                new TutorialStep
                {
                    Id = "portals",
                    Body = "The three portals on your side are your gates — one per resonance you brought: " +
                           "Death, Holy and Plague.\n" +
                           "A card can only be played into the portal matching its resonance.",
                    Advance = StepAdvance.Hold,
                    HoldSeconds = 10f,
                    Camera = CameraShot.FullBoard,
                    Highlight = HighlightTarget.Portal(HighlightSide.You, 2),
                },

                // ── T1: first minion, automatic combat, portals are the target ──
                new TutorialStep
                {
                    Id = "play-doctor",
                    Body = "Let's open in your Plague lane.\n" +
                           "Scan the Plague Doctor now — it can only land here, in your Plague portal.",
                    Advance = StepAdvance.CardPlayed,
                    ExpectedCard = "Plague Doctor",
                    Camera = CameraShot.SingleLane,
                    CameraLane = 2,
                    Highlight = HighlightTarget.Portal(HighlightSide.You, 2),
                    DimBackground = true,
                },
                new TutorialStep
                {
                    Id = "auto-combat",
                    Body = "Combat runs by itself after every card — no aiming, no attack commands.\n" +
                           "No one defends this lane, so your Doctor strikes the enemy PORTAL itself: 4 → 2.\n" +
                           "Drain a portal to 0 and its whole lane is won.",
                    Advance = StepAdvance.CombatResolved,
                    Highlight = HighlightTarget.Portal(HighlightSide.Foe, 2),
                },

                // ── T2: enemy answers, first clash ──
                new TutorialStep
                {
                    Id = "enemy-clash",
                    Body = "Playing one card ended your turn — that's the rhythm: one card, then the fight.\n" +
                           "The Warden answers with a Bruiser in the same lane. Facing front minions CLASH: " +
                           "both strike at once, and both blows land.",
                    Advance = StepAdvance.CombatResolved,
                },

                // ── T3: stacking ──
                new TutorialStep
                {
                    Id = "stacking",
                    Body = "Your turn. Play the Rat into the SAME portal.\n" +
                           "Minions stack — the Rat lines up BEHIND the Doctor. Only the front minion fights; " +
                           "the ones behind wait, protected.",
                    Advance = StepAdvance.CardPlayed,
                    ExpectedCard = "Rat",
                    Highlight = HighlightTarget.Portal(HighlightSide.You, 2),
                    DimBackground = true,
                },
                new TutorialStep
                {
                    Id = "stack-watch",
                    Body = "See it: the Doctor keeps clashing while your Rat sits safe behind him.\n" +
                           "Their Bruiser heals 2 every round — you won't win this lane by grinding. We have other plans.",
                    Advance = StepAdvance.CombatResolved,
                },

                // ── T4: enemy stacks too ──
                new TutorialStep
                {
                    Id = "enemy-stacks",
                    Body = "The Warden piles into the same lane too.\n" +
                           "But a lane can only be won once — every card they sink here does nothing for " +
                           "their other two lanes. Remember that.",
                    Advance = StepAdvance.CombatResolved,
                },

                // ── T5: items & runes ──
                new TutorialStep
                {
                    Id = "item",
                    Body = "Items don't fight — they attach to the NEWEST minion in the portal and empower it.\n" +
                           "Play the Beaked Mask: it clips onto your Rat, its runes slot into the card beneath, " +
                           "and it INFECTS its holder. Watch the Rat's status mark — and trust the plan.",
                    Advance = StepAdvance.CardPlayed,
                    ExpectedCard = "Beaked Mask",
                    Highlight = HighlightTarget.Portal(HighlightSide.You, 2),
                    DimBackground = true,
                },
                new TutorialStep
                {
                    Id = "status-fx",
                    Body = "That mark on your Rat is a STATUS EFFECT — Infection. Stun, Sleep and Stealth work " +
                           "the same way: temporary conditions that fade after a few rounds.\n" +
                           "Infection from an ENEMY also ticks 1 damage each round start — from an ally it's " +
                           "just a mark. Yours is friendly… for now.",
                    Advance = StepAdvance.CombatResolved,
                },

                // ── T6: breather ──
                new TutorialStep
                {
                    Id = "enemy-again",
                    Body = "Another Bruiser joins their pile, and your Doctor keeps soaking every hit.\n" +
                           "Your Rat is still marked — one more turn is all that infection needs to pay off.",
                    Advance = StepAdvance.CombatResolved,
                },

                // ── T7: spells (the infection expires at this turn's end) ──
                new TutorialStep
                {
                    Id = "spell",
                    Body = "Spells never enter the board. They resolve INSTANTLY from their matching lane — " +
                           "you never pick a target.\n" +
                           "Cast Bloodletting: every infected ally attacks at once. And your Rat is infected…",
                    Advance = StepAdvance.CardPlayed,
                    ExpectedCard = "Bloodletting",
                    Highlight = HighlightTarget.Portal(HighlightSide.You, 2),
                    DimBackground = true,
                },
                new TutorialStep
                {
                    Id = "spell-watch",
                    Body = "There! Your Rat lunged from the BACK ROW — an extra blow before the normal clash " +
                           "even began.\n" +
                           "That's the Plague kit: mark your own, then cash the mark in.",
                    Advance = StepAdvance.CombatResolved,
                },

                // ── T8: pivot to the empty lanes ──
                new TutorialStep
                {
                    Id = "enemy-cap",
                    Body = "Their portal holds four cards now — five is the cap — and none of it matters.\n" +
                           "Look at the board: two enemy lanes stand completely EMPTY. Time to punish that.",
                    Advance = StepAdvance.CombatResolved,
                },

                // ── T9–T10: take the top lane ──
                new TutorialStep
                {
                    Id = "play-guard",
                    Body = "Your Skeletal Temple Guard is DEATH resonance — top lane, straight across from an " +
                           "UNDEFENDED portal.\nPlay it.",
                    Advance = StepAdvance.CardPlayed,
                    ExpectedCard = "Skeletal Temple Guard",
                    Camera = CameraShot.SingleLane,
                    CameraLane = 0,
                    Highlight = HighlightTarget.Portal(HighlightSide.You, 0),
                    DimBackground = true,
                },
                new TutorialStep
                {
                    Id = "guard-watch",
                    Body = "Nothing stands in the way, so the Guard hammers the portal itself: 4 → 2.\n" +
                           "One more unanswered swing takes the lane.",
                    Advance = StepAdvance.CombatResolved,
                    Highlight = HighlightTarget.Portal(HighlightSide.Foe, 0),
                },
                new TutorialStep
                {
                    Id = "finish-lane",
                    Body = "The Warden's turn — but they've abandoned this lane entirely.\n" +
                           "Your Guard swings again…",
                    Advance = StepAdvance.LaneWon,
                    Highlight = HighlightTarget.Portal(HighlightSide.Foe, 0),
                },
                new TutorialStep
                {
                    Id = "lane-rules",
                    Body = "TOP LANE WON! A destroyed portal closes its lane, and the cards there retire — " +
                           "put your Guard's card on your discard pile.\n" +
                           "Win 2 of 3 lanes to take the match. And if it ever stands 1–1, the last lane " +
                           "becomes a SHOWDOWN: any card, any resonance.",
                    Advance = StepAdvance.Hold,
                    HoldSeconds = 14f,
                    Camera = CameraShot.FullBoard,
                },

                // ── T11–T12: take the middle lane, win ──
                new TutorialStep
                {
                    Id = "play-priest",
                    Body = "One lane from victory. The Pastafari Priest is HOLY — middle lane, and their " +
                           "Psychic portal is just as empty.\nFinish this.",
                    Advance = StepAdvance.CardPlayed,
                    ExpectedCard = "Pastafari Priest",
                    Camera = CameraShot.SingleLane,
                    CameraLane = 1,
                    Highlight = HighlightTarget.Portal(HighlightSide.You, 1),
                    DimBackground = true,
                },
                new TutorialStep
                {
                    Id = "priest-watch",
                    Body = "4 → 2. The Warden gets one last turn — and nothing played in another lane can " +
                           "save this portal.",
                    Advance = StepAdvance.CombatResolved,
                    Highlight = HighlightTarget.Portal(HighlightSide.Foe, 1),
                },
                new TutorialStep
                {
                    Id = "victory",
                    Body = "Watch it crack…",
                    Advance = StepAdvance.GameOver,
                    Camera = CameraShot.FullBoard,
                    Highlight = HighlightTarget.Portal(HighlightSide.Foe, 1),
                },
                new TutorialStep
                {
                    Id = "complete",
                    Body = "VICTORY — two lanes to none.\n" +
                           "That's Riftborn: match resonance to portal, one card per turn, combat runs itself, " +
                           "only the front minion fights — and two lanes win the match.\n" +
                           "You're ready for a real opponent.",
                    Advance = StepAdvance.Hold,
                    HoldSeconds = 16f,
                    Camera = CameraShot.FullBoard,
                    // Persist completion on entry (survives quitting the outro),
                    // then return to the menu when the hold ends. The engine's
                    // turn loop already stopped at GameOver, so leaving the
                    // scene is the only exit.
                    Hooks = StepHooks.MarkCompleteOnEnter | StepHooks.ReturnToMenuOnExit,
                },
            };
        }
    }
}
