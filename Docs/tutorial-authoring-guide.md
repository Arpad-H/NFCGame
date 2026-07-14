# Tutorial Authoring Guide (Riftborn)

How to customize the tutorial's **text**, **camera**, and **arrow/ring highlights**.
Everything is authored in C# — no scene editing needed for content changes.

---

## Where things live

| What you want to change | File |
|---|---|
| Step text, order, camera, highlights, advance conditions | `Assets/Scripts/Tutorial/TutorialSequence.cs` |
| Which cards the enemy plays each turn | `Assets/Scripts/Tutorial/ScriptedEnemyQueue.cs` (`initialPlays`) |
| Player/enemy resonances, portal HP, opening hand size | `Assets/Scripts/Tutorial/TutorialBootstrap.cs` |
| Ring/arrow look (color, size, pulse) | `Assets/Scripts/Tutorial/HighlightSystem.cs` (public fields) |
| Camera zoom framing (how tight a lane is) | `Assets/Scripts/Tutorial/TutorialCamera.cs` (public fields) |
| Prompt panel width/position | `Assets/Scripts/Tutorial/NotificationView.cs` |

The whole script is the `List<TutorialStep>` returned by `TutorialSequence.Build(...)`.
Add, remove, or reorder entries in that list — that is the tutorial.

After any change, verify it compiles (see **Testing** at the bottom); you do **not**
need to touch the scene or the `.meta` files.

---

## Anatomy of a step

Every beat is one `TutorialStep`. All fields are optional except an `Id`:

```csharp
new TutorialStep
{
    Id       = "play-doctor",              // short label (debug overlay + logs)
    Body     = "Scan the Plague Doctor…",  // the text shown in the prompt panel
    Advance  = StepAdvance.CardPlayed,      // what moves on to the next step
    ExpectedCard = "Plague Doctor",         // the only card allowed this step
    Camera   = CameraShot.SingleLane,       // camera framing on step enter
    CameraLane = 2,                          // which lane (0=top,1=mid,2=bottom)
    Highlight = HighlightTarget.Portal(you, 2), // ring+arrow on your lane-2 portal
    DimBackground = true,                    // dim everything except the highlight
    HoldSeconds = 6f,                        // only used when Advance = Hold
    OnEnter  = null,                         // optional callback on enter
    OnExit   = null,                         // optional callback on exit
},
```

At the top of `Build(...)` there are two helpers you'll use constantly:

```csharp
PlayerSide you = director.humanSide;   // the human's side (Left)
PlayerSide foe = ...;                   // the enemy's side (Right)
```

Use `you` / `foe` for highlights so the script stays correct if the human's side
ever changes.

---

## 1. Editing text

Set `Body`. It supports `\n` for line breaks and TMP rich-text tags
(`<b>`, `<color=#ffcc00>`, etc.). Keep it to ~3 lines — the panel grows to fit but
long text looks cramped.

```csharp
Body = "Spells never enter the board.\nThey resolve <b>instantly</b> from their lane.",
```

- An **empty** `Body` hides the prompt panel for that step (useful for a pure
  camera/hold beat).
- Info-only steps use `Advance = StepAdvance.Hold` with `HoldSeconds` sized to the
  reading length (short line ≈ 5s, three lines ≈ 10–14s).

---

## 2. Camera direction

`Camera` picks the framing the camera tweens to when the step is entered:

| `CameraShot` | Effect |
|---|---|
| `Keep` | Leave the camera where the previous step put it (default) |
| `FullBoard` | The authored scene pose — all 3 lanes |
| `SingleLane` | Zoom tight on one lane's two portals — set `CameraLane` (0/1/2) |

```csharp
Camera = CameraShot.SingleLane, CameraLane = 0,   // zoom to the top lane
Camera = CameraShot.FullBoard,                    // pull back to the whole board
```

Lanes are **0 = top, 1 = middle, 2 = bottom**, matching each side's resonance order
in `TutorialBootstrap` (player: `Death, Holy, Plague` → lanes 0,1,2).

**Tuning the zoom** (optional): on the `TutorialCamera` component (or its code
defaults) —
- `laneHalfWidth` / `laneHalfDepth` — how much world space a single-lane shot must
  show. Bigger = more zoomed out.
- `tweenSeconds` — how long the glide takes (default 0.9s).

The tween writes the camera transform absolutely each frame, so it composes fine
with the board's screen-shake.

---

## 3. Arrows & rings (highlights)

`Highlight` places a pulsing **ring** hugging a target plus a bobbing **arrow**
above it. Today the only target kind is a **portal**:

```csharp
Highlight = HighlightTarget.Portal(you, 2),   // your (human) lane-2 portal
Highlight = HighlightTarget.Portal(foe, 0),   // the enemy's lane-0 portal
```

- First arg is the **side** (`you` or `foe`), second is the **lane index** (0/1/2).
- Omit `Highlight` entirely (or leave it default) for no ring/arrow.
- `DimBackground = true` darkens everything except a hole around the highlight —
  use it to force attention on the very first "play here" beats, then drop it once
  the player knows the board.

The ring re-projects every frame, so it keeps hugging the portal through camera
tweens automatically. It resolves the target by matching a `Portal` whose
`ownerSide` and `laneIndex` equal what you passed — if you ever see
`"No portal to highlight for … lane …"` in the console, the side/lane pair doesn't
exist.

**Tuning the look** (optional): on the `HighlightSystem` component (or its code
defaults) —
- `worldRadius` — how wide the ring sits around the portal (default 2.2 world units).
- `ringColor` / `arrowColor` — tint.
- `pulseAmount` / `pulseSpeed` — ring breathing. `bobAmount` / `bobSpeed` — arrow
  bounce.
- `dimAlpha` — how dark `DimBackground` gets.

**Pointing at something other than a portal** (minion, HP number, history bar) is
not supported yet. To add it you'd extend `HighlightKind` in `TutorialStep.cs` and
teach `TutorialDirector.ResolveHighlightAnchor(...)` how to find that transform —
ask and I can wire a new anchor kind.

---

## 4. Advance conditions — and the rules that prevent lock-ups

`Advance` decides when the step ends:

| `StepAdvance` | Advances when… |
|---|---|
| `Hold` | `HoldSeconds` elapse (info steps) |
| `CardPlayed` | the player successfully plays this step's `ExpectedCard` |
| `CombatResolved` | the next combat (and its post-combat pause) finishes |
| `LaneWon` | a lane is decided and cleared |
| `GameOver` | the match ends |
| `Auto` | immediately after `OnEnter` (chain into the next step) |
| `Manual` | only the debug overlay's "Force advance" moves on |

**These ordering rules matter — breaking them soft-locks the tutorial:**

1. A `CombatResolved` / `LaneWon` / `GameOver` step must be the **current** step
   *before* that event fires. So **consecutive game events need consecutive event
   steps.** Never put a timed `Hold` between two events that happen back-to-back —
   the second event fires during the hold, is lost, and the player then can't
   advance.
2. A `Hold` is only safe **right before a `CardPlayed` step** — the engine is
   sitting idle waiting for the player, so no event slips past.
3. `LaneWon` fires **before** the same combat's `CombatResolved`. The step after a
   `LaneWon` step must be a `Hold` or `CardPlayed` (never `CombatResolved`), or it
   eats the trailing event.
4. The **match-ending** combat fires `GameOver` **instead of** `CombatResolved`, so
   the finishing stretch advances on `GameOver`.

Off-script protection is automatic: while a `CardPlayed` step is active, only its
`ExpectedCard` is accepted; anything else pops a red toast ("Not yet — play X") and
does **not** consume the turn. Use `AllowAnyCard = true` for a free-play/sandbox
step.

---

## 5. Keep cards, enemy, and resonances in sync

The match is choreographed. If you change **which** cards are played, update all of
these together (there's a turn-by-turn timeline comment at the top of
`TutorialSequence.cs`):

1. The step's **`ExpectedCard`** (exact `cardName`, case-insensitive).
2. The **"Assemble your hand"** step text (step 2) listing the physical cards.
3. **`ScriptedEnemyQueue.initialPlays`** if you change enemy plays (one card per
   enemy turn; empty slots = the enemy skips).
4. **`TutorialBootstrap`** resonances if a new card needs a resonance the player
   or enemy doesn't currently bring. Card → resonance:
   `Death, Holy, Plague` (player) and `Darkness, Psychic, Life` (enemy). A card can
   only be played into the portal matching its resonance (except in a Showdown).

Card names are the `cardName:` field inside `Assets/Adressables/Cards/**/*.asset`
(may differ from the file name — e.g. `Planthoe.asset`'s name is `Plantkeeper`).

Pacing levers in `TutorialBootstrap`: `portalHealth` (default 4 — lower resolves
lanes faster) and the forced resonances.

---

## 6. Testing loop (no phone needed)

1. Open `Assets/Scenes/TutorialScene.unity` and press Play. A dev debug panel shows
   in the top-right.
2. Use **"Play expected card"** to advance the current `CardPlayed` step; watch the
   board section for portal HP and turn owner. The enemy auto-plays its queue.
3. **"Force advance"**, **"Restart scene"**, and the camera/toast test buttons let
   you jump around. The **"Completed flag"** row resets `TutorialState` so you can
   re-test the first-run path.
4. To compile-check from the CLI without opening Unity, build `Assembly-CSharp.csproj`
   with MSBuild (exit 0 = clean).

The **Skip Tutorial** button (top-right, in Play) marks the tutorial complete and
returns to the menu at any time.
```
