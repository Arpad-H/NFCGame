# Tutorial Authoring Guide (Riftborn)

How to customize the tutorial's **text**, **camera**, and **arrow/ring highlights**.

There are two ways to author steps, and they use the **same data model**:

- **In the Inspector (recommended)** — edit a `TutorialSequence` ScriptableObject as a
  reorderable list of steps. No code, no scene surgery. See *Author in the Inspector* below.
- **In code** — edit the built-in list in `Assets/Scripts/Tutorial/TutorialSequence.cs`.
  This is the **fallback** the director uses when no asset is found, and the **seed** the
  *Create Sequence Asset From Code* menu copies into a new asset. Editing it still matters.

---

## Where things live

| What you want to change | Where |
|---|---|
| Step text, order, camera, highlights, advance conditions | **Inspector:** the `TutorialSequence` asset · **Code fallback:** `Assets/Scripts/Tutorial/TutorialSequence.cs` |
| Which cards the enemy plays each turn | `Assets/Scripts/Tutorial/ScriptedEnemyQueue.cs` (`initialPlays`) |
| Player/enemy resonances, portal HP, opening hand size | `TutorialBootstrap` component on the `Tutorial` GameObject (Inspector), or its code defaults |
| Ring/arrow look (color, size, pulse, dim alpha) | `HighlightSystem` component on the `Tutorial` GameObject (Inspector) — already exposed |
| Camera zoom framing (how tight a lane is) | `TutorialCamera` component on the `Tutorial` GameObject (Inspector) |
| Prompt panel width/position, toast duration | `NotificationView` component on the `Tutorial` GameObject (Inspector) |

Note the **look and pacing knobs are already in the Inspector** — they are public fields on
the components sitting on the `Tutorial` GameObject in `TutorialScene.unity`. Only the
per-step *content* moved to the new asset.

---

## Author in the Inspector (recommended)

The tutorial is a `TutorialSequenceAsset` — an ordered, reorderable list of `TutorialStep`s.

**First-time setup (one click):**

1. Menu **Riftborn ▸ Tutorial ▸ Create Sequence Asset From Code**. This writes
   `Assets/Resources/TutorialSequence.asset`, pre-filled with all the built-in steps, and
   selects it.
2. Because it lives in a `Resources` folder, the `TutorialDirector` loads it **automatically**
   — no wiring needed. (You can instead drag it onto the director's *Sequence Asset* field if
   you keep the asset elsewhere.)
3. Press Play. The tutorial runs from the asset. Editing a step in the Inspector and pressing
   Play again shows the change immediately.

> You can also make a blank one via **Assets ▸ Create ▸ Riftborn ▸ Tutorial Sequence**, but the
> menu item above is the fast path since it copies the existing 12-turn choreography.

**Editing:** select the asset, expand a step, and set its fields (all described below). Use the
list's **＋ / － / drag handles** to add, delete, and reorder steps. The reorder rules in
section 4 still apply — reordering event steps carelessly can soft-lock the tutorial.

**How the director picks a source (in order):** a *Sequence Asset* wired on the director →
else `Resources/TutorialSequence` → else the built-in `TutorialSequence.Build()`. An empty
asset falls through to the code sequence, so a half-made asset never yields a blank tutorial.

---

## Anatomy of a step

Every beat is one `TutorialStep`. The Inspector shows these exact fields (grouped
Content / Advance / Presentation / Lifecycle); in code they are the same fields, and all
are optional except an `Id`:

| Field | Group | What it does |
|---|---|---|
| `Id` | Content | Short label for logs + the debug overlay (not shown to the player) |
| `Body` | Content | Instruction text in the prompt panel (empty hides the panel) |
| `Advance` | Advance | What ends the step (see section 4) |
| `HoldSeconds` | Advance | Seconds to wait — only used when `Advance = Hold` |
| `ExpectedCard` | Advance | The only card allowed this step (empty = none) |
| `AllowAnyCard` | Advance | Free-play/sandbox: accept any card |
| `Camera` / `CameraLane` | Presentation | Camera framing on enter (section 2) |
| `Highlights` | Presentation | Zero or more ring/arrow highlights (section 3) |
| `DimBackground` | Presentation | Dim everything except the highlight(s) |
| `Hooks` | Lifecycle | Side effects on enter/exit — used by the final step only |

The same step in code (the fallback list in `TutorialSequence.cs`):

```csharp
new TutorialStep
{
    Id       = "play-doctor",                        // short label (debug overlay + logs)
    Body     = "Scan the Plague Doctor…",            // the text shown in the prompt panel
    Advance  = StepAdvance.CardPlayed,                // what moves on to the next step
    ExpectedCard = "Plague Doctor",                   // the only card allowed this step
    Camera   = CameraShot.SingleLane,                 // camera framing on step enter
    CameraLane = 2,                                   // which lane (0=top,1=mid,2=bottom)
    Highlights = new() { HighlightTarget.Portal(HighlightSide.You, 2) }, // ring+arrow on YOUR lane-2 portal
    DimBackground = true,                             // dim everything except the highlight(s)
    HoldSeconds = 6f,                                 // only used when Advance = Hold
},
```

### Sides are relative: You / Foe

Highlights take a **relative** side — `HighlightSide.You` (the human) or `HighlightSide.Foe`
(the scripted enemy) — which the director resolves to the concrete board side at runtime. So
a step stays correct no matter which side the human plays (`humanSide` on `TutorialDirector`),
and it reads naturally: "highlight *your* portal" vs "*the enemy's* portal." In the Inspector,
each entry in the `Highlights` list is a foldout: set **Kind** = `Portal`, **Side** = `You`/`Foe`,
**Lane** = 0/1/2. Leave the list empty for no highlight.

### Lifecycle hooks

`Hooks` is a flags field (a dropdown in the Inspector) for step side effects. Only the final
step uses it — `MarkCompleteOnEnter` (persist the tutorial as seen) plus `ReturnToMenuOnExit`
(leave the scene when the outro's hold ends). Leave it `None` for every other step.

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

Each step has a **`Highlights` list** — zero, one, or several. Every entry is a pulsing
**ring** hugging a target and/or a bobbing **arrow** parked at a clock position around it.
In the Inspector, expand `Highlights`, press **＋**, and set the entry's fields; add more
entries to point at several things at once.

Each highlight entry:

| Field | What it does |
|---|---|
| `Kind` | `None` (draws nothing), `Portal`, or `Anchor` |
| `Side` / `Lane` | **Portal** target: side (`You`/`Foe`, relative to the human) + lane 0/1/2 |
| `AnchorId` | **Anchor** target: the id of a `TutorialAnchor` in the scene (see below) |
| `Parts` | `RingAndArrow` (default), `RingOnly`, or `ArrowOnly` |
| `ArrowClock` | where the arrow sits around the ring: **12** (or 0) = top, **3** = right, **6** = bottom, **9** = left. Decimals allowed; the arrow rotates to point inward |
| `WorldRadius` | ring size in world units. **0** = the `HighlightSystem` default (~2.2, sized for a portal). Set it larger/smaller for bigger/smaller targets |

In code (the seed list) the same two shorthands exist:

```csharp
Highlights = new()
{
    HighlightTarget.Portal(HighlightSide.You, 2),           // ring+arrow, your lane-2 portal
    HighlightTarget.Anchor("coin"),                          // ring+arrow, the TutorialAnchor "coin"
},
```
…and set `Parts` / `ArrowClock` / `WorldRadius` with an object initializer when you need them:
```csharp
new HighlightTarget { Kind = HighlightKind.Anchor, AnchorId = "hand", Parts = HighlightParts.ArrowOnly, ArrowClock = 6f },
```

### Pointing at anything: `TutorialAnchor`

`Portal` is the shortcut for a lane gate. To point a highlight at **anything else** — the turn
coin, the hand-count, a specific board spot — use an **anchor marker**:

1. In the scene, create an empty GameObject where you want the ring/arrow to sit (or select an
   existing object like the coin), and add the **`TutorialAnchor`** component to it.
2. Give it an **id** (e.g. `coin`, `hand`, `portal-center`). Ids are case-insensitive; keep them unique.
3. In the step's highlight, set **Kind** = `Anchor` and **Anchor Id** = that id.

The highlight follows the marker's transform every frame — **move or parent the marker in the
scene to reposition the highlight**. (Steps live in an asset, and Unity assets can't hold direct
scene-object links, so the id is the bridge. If you mistype it you'll see
`"No TutorialAnchor with id '…' in the scene."` in the console.)

### Dim

`DimBackground = true` darkens everything except a hole around the highlight(s) — good for the
first "play here" beats. With several highlights it cuts **one** hole spanning all of them (a
true multi-hole cutout isn't possible with the plain-Image approach). A highlight with `Parts`
showing nothing but the step dimmed still punches a hole — i.e. a pure **spotlight**.

The ring re-projects every frame, so highlights keep hugging their targets through camera tweens
automatically.

**Tuning the look** (optional, on the `HighlightSystem` component or its code defaults):
- `worldRadius` — default ring size when a highlight leaves `WorldRadius` at 0.
- `ringColor` / `arrowColor` — tint (global; shared by every highlight).
- `pulseAmount` / `pulseSpeed` — ring breathing. `bobAmount` / `bobSpeed` — arrow bounce.
- `minCanvasRadius` / `maxCanvasRadius` — clamp the on-screen ring size at extreme zooms.
- `dimAlpha` — how dark `DimBackground` gets.

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
| `Auto` | immediately after the step is entered (chain into the next step) |
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
