# Tutorial System — Implementation Plan (Riftborn)

A scripted, single-player tutorial built **on top of Riftborn's existing, fully-playable
engine** — not a greenfield build. It teaches a new player the real mechanics
(resonance portals, minion stacking, automatic combat, one-card-per-turn) by driving the
real game systems from the outside and narrating what happens.

Written to hand to a coding agent. Every milestone has concrete tasks and a testable
"definition of done." **Read section 1 first** — it is the ground truth about how this
engine actually works, and most of the original Hearthstone-style assumptions do not apply.

---

## 0. The core principle

Build the tutorial as a **scripted Director** that drives the real game from the outside via
the systems that already exist. It must **never** add `if (tutorial)` branches into gameplay
code. The only additions to gameplay code are a handful of **tutorial-agnostic seams**
(nullable delegates / plain C# events) listed in section 4.

The three load-bearing facts that shape everything:

1. **Input is already a WebSocket command.** The companion app sends `PLAY_CARD:<name>`; the
   engine plays it. There is nothing to "mock." The Director tests itself by sending WS
   messages or calling `GameManager.HandlePlayerPlayCard` directly.
2. **Combat is fully automatic and atomic.** Playing one card runs *place → resolve the
   entire board's combat → end turn* in a single awaited chain. The player never targets,
   taps, drags, or declares attacks. So the tutorial **gates at turn boundaries**, not at
   micro-actions, and *narrates* combat outcomes after they happen.
3. **The player's hand/deck lives on the companion app + physical cards, not in Unity.**
   Unity only tracks a hand *count* and learns a card's name when `PLAY_CARD` arrives. So
   there is no Unity-side deck to "rig" and no hand to "highlight." Determinism for the
   player comes from **a fixed physical tutorial hand the player is told to assemble**, plus
   the Director rejecting any off-script card. Only the **enemy** is fully scripted in-engine.

---

## 1. Ground truth — how this engine actually works

*(File references so the agent doesn't re-derive. Verify before extending.)*

- **WebSocket I/O** (`Assets/Scripts/WebSocketServer/WebSocketServer.cs`):
  - Inbound from app: `PLAY_CARD:<cardName>` → `GameManager.HandlePlayerPlayCard(cardName)`;
    `SELECT_ELEMENTS:<csv>` (resonance draft).
  - Outbound to app: `ACTION_PLAY_A_CARD`, `ACTION_WAIT`, `ACTION_DRAW_A_CARD`,
    `INITIATE_GAME_STATE`.
  - `HandlePlayerPlayCard(cardName)` acts for the **current `activePlayer`** — it does not
    validate which socket sent it (turn ownership is enforced only by the app receiving
    `ACTION_WAIT`). This is convenient: to make the scripted enemy play, just call it while
    `activePlayer` is the enemy.
- **Turn loop** (`Assets/Scripts/GameSystems/GameManager.cs`):
  `HandlePlayerPlayCard` → `PlaceCard`/`PlaySpell` → `CombatResolution()` (announces "Fight",
  resolves all lanes, waits `postCombatDelaySeconds`) → `EndTurn` (round-end effects) →
  `StartTurn` (swap `activePlayer`, draw 1, send `ACTION_PLAY_A_CARD`/`ACTION_WAIT`, start the
  60s timer). A `turnTimeLimit` timer auto-skips a player who doesn't act.
- **Board is 3 lanes, not 6** (`Assets/Scripts/GameSystems/Board.cs`): `lanes = new Lane[3]`;
  each `Lane` has a `LeftPortal` and a `RightPortal`. Each player owns **3 portals**, one per
  resonance they chose. There are **6 ResonanceTypes total** (`Darkness, Plague, Death,
  Psychic, Life, Holy` — `Resonance.cs`); a player picks 3.
- **Placement = stack into the resonance-matched portal** (`Board.PlaceCard`): the card goes
  into the owner's portal whose resonance equals the card's resonance (max
  `maxCardsPerPortal = 5`). Minions **stack**; only the **front** minion fights. **Items must
  be placed on top of a minion** (can't go in an empty portal). Items supply **activator
  runes** to neighbours (`ItemType.suppliedActivatorRunes` vs `effectActivatingRunes`) — a
  real mechanic worth a tutorial beat.
- **Automatic combat** (`Assets/Scripts/GameSystems/BoardCombat.cs`): lanes resolve
  top→mid→bottom. Two facing front minions **clash simultaneously** (both hits land even if
  one dies); otherwise the active side swings first; the target is the front enemy minion in
  that lane, **else the enemy portal in that lane** (the portal is the lane's "face" — see
  win model below). `Stun`/`Sleep` skip a minion's attack; `Stealth` makes it untargetable.
  **There is no summoning sickness** — a minion you just placed fights in that same turn. A
  **decided lane is skipped** (`if (lane.IsDecided) continue;`).
- **Win model — portal HP, best-of-3 lanes, showdown** (commit `051e018`, "switched to portal
  hp"). This *replaced* the old hero-HP model:
  - Each `Portal` implements `ITargetable` and has HP (`maxPortalHealth`, default **15**;
    `CurrentPortalHealth`; `IsDestroyed`). An undefended lane means attacking minions (and
    hero-targeting card effects) hit the **portal** directly; draining it to 0 loses that lane
    for its owner.
  - After each combat phase, `GameManager.HandlePostCombat()` calls
    `Board.ResolveDecidedLanes()` (awards each lane whose portal died to the other side via
    `Lane.WonBy`), announces it (`Announcer.AnnounceLaneWon`), and `Board.ClearLane()` (files
    both portals' cards to discard, no death). A decided lane no longer fights and rejects new
    plays.
  - **Win = 2 of 3 lanes** (`Board.CountLanesWon` ≥ 2) → `gameOver = true`,
    `Announcer.AnnounceVictory`, turn loop stops. The `gameOver` flag also guards
    `HandlePlayerPlayCard` / `OnSkipTurn` / `OnTurnTimeExpired`.
  - **Showdown**: at 1–1 with one lane left, `Board.EnterShowdown()` +
    `Announcer.AnnounceShowdown()`; while `IsShowdown`, `PlaceCard` accepts **any card
    regardless of resonance** into the player's own-side portal in the last contested lane.
  - The `Player` hero/`health` object still exists but is now only a fallback target for
    effects resolved off-lane — **portals are the real win target**, not the hero.
- **Spells** (`GameManager.PlaySpell`): not fielded. They play a cast animation and resolve
  their effect **automatically from the owner's matching-resonance lane** using predefined
  target logic. The player does not pick a target. `EnemyHeroTarget` / `OwnerHeroTarget` now
  resolve to the **enemy / own portal** in the card's lane (`ITargetLogic.cs`, same commit).
- **Card model**: `CardData` = `cardName`, `artwork`, `resonance`, `cardType`. `MinionType` =
  `baseHealth`, `baseAttack`. **There is no mana/cost anywhere.** Cards are resolved by name
  from `CardLibrary` (addressables).
- **Existing UI the tutorial reuses**: `Announcer` (awaitable center-screen banner —
  `AnnounceFight`, and now `AnnounceLaneWon` / `AnnounceShowdown` / `AnnounceVictory` —
  `Assets/Scripts/UI/Announcer.cs`), `GameHistory` (observable action log with `Added`/
  `Evicted` events, `Assets/Scripts/UI/History/GameHistory.cs`), `UIManager`, `Portal` /
  `BoardTokenVisualizer` (board tokens the tutorial points arrows at).
- **Existing onboarding is real and mostly done**: `MainMenu → GameModeSelection
  (Blind/Draft) → ConnectionMenu → QRCodeDisplay` generates per-player connect QRs (deep link
  `nfcgame://connect?ws=ws://<ip>:8080/Game?id=<n>&lobbyType=<type>`), waits for connect,
  takes resonance picks, counts down, loads `GameScene`, broadcasts `INITIATE_GAME_STATE`.
- **Test bootstrap already exists**: `GameManager.SetUpTestEnvironment()` fabricates two
  `PlayerData` with fixed resonances and draws 3 — the pattern the tutorial reuses to inject
  the scripted enemy.

---

## 2. Locked decisions

1. **Single human player + scripted enemy.** The enemy is simulated in-engine by the Director;
   it is never a connected app. The human still connects one app (they physically play NFC
   cards through it).
2. **All tutorial prompts render on the Unity board screen** — primarily **custom pop-ups**
   (so we can anchor **arrows** to board targets), with the `Announcer` banner for big beats.
   **Nothing is sent to the phone.**
3. **Off-script plays are rejected in-engine** with a pop-up / announcer message telling the
   player the correct card. The play is not consumed; it stays the player's turn.
4. **Hand management is the player's physical responsibility, even in the tutorial.** The
   tutorial opens by instructing the player to **pull the exact set of cards** it needs from
   their deck, and later steps narrate physical hand actions ("you played it — set that card
   on your discard pile"). Unity does not model the hand.
5. **Rigged everything.** Fixed player resonances, fixed physical tutorial hand, fixed scripted
   enemy queue tuned so the player reliably wins. No randomness.
6. **Gate at turn boundaries.** Info-only steps auto-advance after a readable hold (there is no
   board input device); action steps advance on the resulting game event.

---

## 3. What already exists vs. what to build

| Original plan component | Reality | Action |
|---|---|---|
| `ICardInputSource` / `MockCardInputSource` / `OnCardDetected` | Input is WS `PLAY_CARD` → `HandlePlayerPlayCard` | **Delete.** Use the WS seam. |
| `BoardModel` (6 lanes × slots) | `Board`/`Lane`/`Portal`, 3 lanes × 2 portals, stacks | **Reuse.** Don't rebuild. |
| `CardModel` (cost, canAttackThisTurn) | `CardData`/`*Instance`; no cost, no summoning sickness | **Reuse.** Drop cost & sickness. |
| `RiggedDeck` (fixed player draws) | No Unity deck/hand; app-side | **Cut for the player.** Fixed physical hand instead. Enemy queue only. |
| `TurnController` (+win condition) | `GameManager` turn loop **and** win condition exist (portal HP / 2-of-3 lanes / showdown, commit `051e018`) | **Reuse both.** Just add a game-end event hook (section 4). |
| `ScriptedEnemy` | Feasible by playing on the enemy's turn via existing loop | **Build** as a Director-driven queue. |
| `OnboardingFlow` (QR + stub connect) | Real QR/connect/draft flow already exists | **Adapt,** don't rebuild; skip the draft (force resonances). |
| `InputGate` (block board clicks) | Nothing is clickable on the board; input is WS | **Replace** with the off-script play rejector (section 4). |
| `TutorialDirector`/`Step`/`Sequence` | — | **Build** (backbone). |
| `NotificationView` (pop-up + arrow) | — | **Build** (primary prompt UI). |
| `HighlightSystem` (glow/arrow/dim) | — | **Build,** anchored to board tokens/portals. |
| `TutorialCamera` | Camera exists in `GameScene` | **Build** framing presets (1 lane / full board). |
| `TutorialState` (complete flag) | — | **Build.** |

---

## 4. Minimal engine seams to add (tutorial-agnostic)

These are plain hooks with **no tutorial knowledge**. In normal matches the delegates are
null / no subscribers, so behaviour is unchanged.

1. **Play validator + rejection callback** (in `GameManager.HandlePlayerPlayCard`, before it
   creates/places the card):
   - `public Func<string, bool> CardPlayValidator;` — if non-null and returns false, abort the
     play **without** consuming the turn or setting `actionTaken`.
   - `public Action<string> OnCardPlayRejected;` — invoked with the rejected name so the
     Director can pop the "wrong card" message. Also surface the existing placement failures
     (wrong resonance / portal full / item on empty portal — currently just `Debug.LogWarning`
     in `Board.PlaceCard`) through this same channel.
2. **Turn + phase events** (fire from the existing methods; the Director subscribes):
   - `event Action<Player> TurnStarted;` (end of `StartTurn`, after `activePlayer` is set).
   - `event Action<string> CardPlayedSuccessfully;` (after a successful place/spell).
   - `event Action CombatResolved;` (end of `CombatResolution`, after the post-combat delay).
   - *(Alternatively, `CardPlayedSuccessfully` can be replaced by observing
     `GameHistory.Added`; `TurnStarted` and the validator are the ones that must be added.)*
3. **Deterministic setup seams:**
   - `public PlayerSide? startingSideOverride;` consulted in `Awake` instead of the random
     pick (so the player always goes first).
   - `public bool turnTimerEnabled = true;` (or set `turnTimeLimit` very high) so the player
     can read a pop-up without the 60s auto-skip firing.
   - Tutorial pacing lever: `Portal.maxPortalHealth` is public — `TutorialBootstrap` can set
     it **low** (e.g. 3–5) so lanes resolve in a couple of hits instead of grinding through
     15 HP.
4. **Game-end event hook (small; the win *logic* already exists):**
   - The engine already ends the match (`GameManager.HandlePostCombat` → `gameOver = true` +
     `AnnounceVictory`) on a 2-of-3 lane win. Add `event Action<Player> GameOver;` (fire it
     where `gameOver` is set) and optionally `event Action<Lane> LaneWon;` so the Director can
     drive the Victory step and per-lane narration. No new win *logic* — just an observation
     seam.
   - The victory **banner** already exists (`AnnounceVictory`); a dedicated Victory *screen* is
     optional polish, not required.

---

## 5. Components to build

| Component | Responsibility |
|---|---|
| `TutorialDirector` | Runs the ordered steps; subscribes to the section-4 events; installs the play validator; drives the scripted enemy on its turns. |
| `TutorialStep` | One step: instruction/pop-up content, highlight target, expected card (or none), advance condition, `OnEnter`/`OnExit`. |
| `TutorialSequence` | The authored ordered step list (section 7 content). |
| `TutorialBootstrap` | Before `GameManager.Awake`: ensures a scripted enemy `PlayerData` (id 2) and forces both sides' 3 resonances; sets `startingSideOverride = player`; disables the turn timer; broadcasts `INITIATE_GAME_STATE`. |
| `ScriptedEnemyQueue` | Ordered list of enemy plays; on each enemy `TurnStarted`, dequeue and call `HandlePlayerPlayCard`. |
| `NotificationView` | Board-screen pop-up: body text, tail, and a **world-anchored arrow** pointing at a portal / token / health / etc. Primary prompt UI. |
| `HighlightSystem` | Glow outline + pulsing arrow on a board target; optional dim of the rest. Anchored to `Portal` / `BoardTokenVisualizer`. |
| `TutorialCamera` | Framing presets over the 3-lane board: `SingleLane(i)` (your portal + enemy portal of lane *i*) and `FullBoard`; smooth tween. |
| `OffScriptRejector` | Wires `CardPlayValidator`/`OnCardPlayRejected` to the current step's expected card and to a rejection pop-up. |
| `TutorialState` | Persist "tutorial complete"; skip/replay entry points. |

---

## 6. Milestones

Ordered to reach a testable walking skeleton fast, then layer content. Each is a
PR/checkpoint. (The win condition already exists — portal HP / 2-of-3 lanes / showdown — so
there is no prerequisite milestone; the tutorial only adds the game-end *event hook* in M2.)

### M0 — Scaffold & fast iteration
- Duplicate `GameScene` → **`TutorialScene`** (keeps portals, camera, `Announcer`,
  `GameHistory`, `UIManager`, players, biomes). Add the tutorial-only objects there.
- Boot path: a menu button **and** a play-mode debug entry that loads `TutorialScene` with the
  human already connected (or a stubbed single connection for pure-UI iteration).
- Debug overlay: "jump to step N", "force-advance", "restart", "simulate enemy card",
  "simulate PLAY_CARD:X" (so the agent can drive both sides without the app).
- **Done when:** Play drops into `TutorialScene` with the debug panel; the board is set up with
  fixed resonances and a scripted enemy present.

### M1 — Director framework (backbone)
- `TutorialStep`, `TutorialDirector`, `TutorialSequence`. Advance conditions support **game
  events** (section-4 hooks), **timed auto-advance** (info steps), and **manual debug next**.
- Throwaway 2-step sequence advancing on the debug next button.
- **Done when:** the throwaway sequence runs start→finish via the hooks/debug panel.
- *Upgrade path (note, don't build yet):* move steps to ScriptableObjects for no-recompile
  authoring.

### M2 — Engine seams + scripted enemy
- Add the section-4 seams (validator/rejection, `TurnStarted`/`CardPlayedSuccessfully`/
  `CombatResolved`, `startingSideOverride`, timer toggle).
- `TutorialBootstrap` + `ScriptedEnemyQueue`: player is Left and goes first; enemy is Right and
  plays its queued card automatically each enemy turn.
- **Done when:** with the debug "simulate PLAY_CARD" button you can play a player card, watch
  auto-combat, and see the enemy auto-play its scripted card next turn — no app, no tutorial UI.

### M3 — Tutorial prompt UI
- `NotificationView` (pop-up + world-anchored arrow) and `HighlightSystem` (glow/arrow on a
  `Portal`/token, optional dim). Wire `Announcer` for big beats.
- `OffScriptRejector`: playing a non-expected card pops "Not yet — play **X** into your **Y**
  portal" and does not consume the turn.
- **Done when:** a debug step can pop a message with an arrow on a specific portal, and an
  off-script `PLAY_CARD` is rejected with the correct message.

### M4 — Tutorial camera
- `TutorialCamera` presets `SingleLane(i)` and `FullBoard`; smooth tween on step enter.
- **Done when:** the Director can frame one lane, then zoom to the full 3-lane board, smoothly.

### M5 — Onboarding adaptation
- Reuse the existing QR/connect flow for the **one** human player. **Skip the resonance draft**
  — the tutorial forces its fixed 3 resonances in `TutorialBootstrap`.
- Add the opening **"pull these exact cards from your deck"** prompt (board pop-up listing the
  tutorial hand); auto-advance after a hold, then start turn 1.
- **Done when:** connecting one app leads into the tutorial with forced resonances and the
  "assemble your hand" prompt shown.

### M6 — Author the content (section 7)
- Implement the `TutorialSequence`; tune the fixed player hand and scripted enemy queue so each
  beat lines up and the player reliably wins.
- **Done when:** a fresh player runs the whole tutorial: connect → zoomed single-lane teaching
  → zoom-out → full-board finish → win → "complete."

### M7 — Polish & QA
- Skip/replay entry points; persist `TutorialState`. Handle quitting/re-entering mid-tutorial.
- Off-script/edge cases: wrong resonance, portal full, item on empty portal, playing during a
  pop-up. Timing, readability, art pass on glow/arrow/pop-up.
- **Done when:** skippable, replayable, robust to bad input and quitting.

---

## 7. The tutorial script (content)

Author as `TutorialSequence`. Card names below are **designer placeholders** — pick real cards
from `CardLibrary` that match the forced resonances and each teaching beat, and tune the enemy
queue so the player wins. (`Rat` is a known real card, per existing examples.) "Advance on" is
the condition to move to the next step. Info-only steps auto-advance after a hold.

**Setup (M5):** force player resonances (e.g. `Life, Holy, Death`), enemy resonances, and a
scripted enemy queue. Player is Left, goes first. Set `Portal.maxPortalHealth` **low** (e.g.
3–5) on the tutorial portals so lanes resolve in a couple of hits rather than grinding 15 HP.

1. **Welcome** — pop-up: "Welcome to Riftborn." *Advance:* hold.
2. **Assemble your hand** — pop-up lists the exact tutorial cards to pull from the deck:
   "Take **[Rat]**, **[Minion2]**, **[Spell1]** … from your deck and hold them." *Advance:*
   hold (long).
3. **Your portals** — camera → `FullBoard` briefly; pop-up + arrows: "You have 3 portals, one
   per resonance. A card only goes in the portal matching its resonance." *Advance:* hold.
4. **Zoom to a lane** — camera → `SingleLane(0)`. *Advance:* auto (enter hook).
5. **Play your first minion** — pop-up + arrow on the Life portal: "Play **[Rat]** — it drops
   into your **Life** portal." *Advance:* `CardPlayedSuccessfully(Rat)`.
   *(Off-script → rejection pop-up.)*
6. **Combat is automatic → portals are the target** — (auto-combat already resolved: Rat vs
   the empty enemy portal → **damages the enemy portal**) pop-up + arrow on the enemy portal's
   HP: "Combat happens on its own after you play — you never attack or target manually. With no
   defender, your minion hits the enemy **portal**. Drain a portal to 0 to **win that lane**."
   *Advance:* `CombatResolved`.
7. **One card per turn + hand upkeep** — pop-up: "Playing a card ends your turn. Set the card
   you just played on your discard pile as the rules say." *Advance:* hold.
8. **Enemy turn** — `ScriptedEnemyQueue` plays **[EnemyMinion]** into a facing lane; pop-up
   narrates it. *Advance:* enemy `CombatResolved`.
9. **Stacking & the front minion** — pop-up + arrow: "Play **[Minion2]** into the **same**
   portal. Minions stack — only the **front** one fights." *Advance:*
   `CardPlayedSuccessfully(Minion2)`.
10. **Clash** — (auto-combat: your front vs enemy front) pop-up: "Both front minions struck at
    once — that's a clash. Both blows land even if one dies." *Advance:* `CombatResolved`.
11. **Status effects** *(optional, only if a scripted card applies one)* — pop-up explains e.g.
    `Stun`/`Sleep` skips a minion's attack; `Stealth` can't be targeted. *Advance:* hold.
12. **Items & runes** *(optional advanced beat)* — pop-up + arrow: "Play **[Item]** onto your
    front minion — items sit on a minion and feed **runes** to neighbours to switch on their
    effects." *Advance:* `CardPlayedSuccessfully(Item)`.
13. **Spells resolve themselves** — pop-up + arrow: "Play **[Spell1]** — spells resolve
    automatically from their resonance lane; you don't pick a target." Rig the enemy board so
    the spell hits the intended minion. *Advance:* `CardPlayedSuccessfully(Spell1)` +
    `CombatResolved`.
14. **Win a lane** — script the fight so the player's minion drains the enemy portal in this
    lane to 0. On `LaneWon`, the engine announces it (`AnnounceLaneWon`) and clears the lane;
    pop-up: "You destroyed their portal — this lane is yours and closes." *Advance:* `LaneWon`.
15. **Zoom out** — camera → `FullBoard`: "Here's the whole board — 3 lanes, your 3 resonances
    vs theirs." *Advance:* auto.
16. **How you win the match** — pop-up: "Each lane is its own duel. **Win 2 of the 3 lanes** to
    win the match. If it's 1–1, the last lane becomes a **Showdown** — you can play **any**
    card there, ignoring resonance." *Advance:* hold.
17. **Finish the match** — enemy queue plays weak/no cards; the player is guided to take a
    second lane (or the showdown lane). The engine fires `AnnounceVictory` on the 2-of-3 win.
    *Advance:* `GameOver(player)`.
18. **Complete** — set `TutorialState.complete = true`; return to menu. (The victory *banner*
    already showed via `AnnounceVictory`; add a dedicated Victory screen only if you want more.)

*(Optional later: a short beat on the History bar — `GameHistory` already logs plays/attacks —
once you want to point at it.)*

---

## 8. Prototype shortcuts

- **No app needed for dev:** debug buttons fire `PLAY_CARD:X` for the player and dequeue the
  enemy, so the whole flow runs in the editor.
- **Reuse `SetUpTestEnvironment`'s pattern** for the fabricated enemy + fixed resonances.
- **Placeholder art:** grey-box pop-up/arrow/glow; art pass in M7.
- **Rig everything:** fixed resonances, fixed physical hand, fixed enemy queue.
- **Hard-code the sequence in C# first;** move to ScriptableObjects only if authoring hurts.

---

## 9. Open risks / to verify before/while building

- **Lanes clear when won.** Winning a lane fires `Board.ClearLane`, which discards *both*
  portals' cards (no death/deathrattle) and closes the lane to combat and new plays. The
  scripted sequence must expect boards to empty as lanes resolve, and must reach a **2-of-3**
  result — script the enemy so the player takes two lanes (or reaches and wins the showdown).
- **Showdown changes placement.** Once `IsShowdown` is set, the last lane accepts any card
  regardless of resonance. If a tutorial beat relies on resonance-matching, keep it *before*
  the game can reach 1–1, or teach showdown explicitly (step 16).
- **`GameManager.Awake` runs setup immediately** and reads `ConnectedPlayers` resonances —
  `TutorialBootstrap` must populate players/resonances **before** it (script execution order or
  an explicit init call). Verify ordering early.
- **The scripted enemy has no socket** — `SendToPlayer` to it is a harmless no-op, but confirm
  nothing else assumes the enemy app is present.
- **Info-step pacing:** with no board input device, info steps auto-advance on a timer. If
  playtests want manual pacing, the only self-contained option is a companion-app "continue"
  button (a small later addition) — but per the locked decision we stay board-only for now.
- **Placement-failure messaging:** route `Board.PlaceCard`'s existing warn-and-return-false
  cases (wrong resonance / portal full / item on empty minion / **lane already decided**)
  through `OnCardPlayRejected` so the player gets a real explanation, not a silent no-op.
