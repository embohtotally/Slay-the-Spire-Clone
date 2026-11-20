# Development Summary — 2026-11-20

[Project README](../README.md) · [Implementation To-Do List](TODO.md) · [Previous Summary (2026-11-19)](CHANGELOG_2026-11-19.md) · [Code Documentation Index](code/README.md)

Complete log of all combat responsiveness, chapter progression, pile viewer UI, procedural deck-shuffle VFX, card pacing, and modular character Will architecture work completed on **2026-11-20** in `Slay-the-Spire-Clone` (Unity `6000.0.73f1`, URP `17.1.0`).

---

## Table of Contents

- [Development Summary — 2026-11-20](#development-summary--2026-11-20)
  - [Table of Contents](#table-of-contents)
  - [1. Responsive Card Re-Hover Under Stationary Cursor](#1-responsive-card-re-hover-under-stationary-cursor)
  - [2. Non-Blocking FIFO Combat Queue (Immediate Card Play \& End Turn)](#2-non-blocking-fifo-combat-queue-immediate-card-play--end-turn)
  - [3. Player-Side Buff/Debuff Hover Tooltips (with Stack Counts)](#3-player-side-buffdebuff-hover-tooltips-with-stack-counts)
  - [4. Automatic Seamless Chapter Continuation After Boss Victory](#4-automatic-seamless-chapter-continuation-after-boss-victory)
  - [5. Clickable Discard \& Draw Pile Card List Viewer (Existing Project Assets)](#5-clickable-discard--draw-pile-card-list-viewer-existing-project-assets)
  - [6. Procedural Slay-the-Spire Deck Reshuffle Animation with Glowing Trails](#6-procedural-slay-the-spire-deck-reshuffle-animation-with-glowing-trails)
  - [7. Tuned Card Draw \& End-Turn Discard Pacing](#7-tuned-card-draw--end-turn-discard-pacing)
  - [8. Modular Character Will System \& Dynamic Portraits (Character-Select Ready)](#8-modular-character-will-system--dynamic-portraits-character-select-ready)
  - [9. File Change Inventory](#9-file-change-inventory)
    - [New Scripts](#new-scripts)
    - [Modified Scenes \& Prefabs](#modified-scenes--prefabs)
    - [Modified Scripts](#modified-scripts)
  - [10. Play-Mode Verification Summary](#10-play-mode-verification-summary)

---

## 1. Responsive Card Re-Hover Under Stationary Cursor

- **Root Cause:** `CombatHandController` cleared hover and dropped `OnPointerEnter` events while `ActionSystem.IsPerforming` was `true`. Once combat became idle again, Unity's `EventSystem` would not re-emit `OnPointerEnter` for a hand card that had shifted underneath a stationary cursor until the player moved out and back in or clicked.
- **Changes ([`CombatHandController.cs`](../Assets/_Project/Scripts/Views/CombatHandController.cs)):**
  - Decoupled `CanHoverCards` and `CanReceiveInput` from `_playService.IsBusy`; cards now remain hoverable and playable throughout Player Turn and only lock when Enemy Turn is queued/active or a modal (`CombatMapOverlayUI`, `CombatPileViewerUI`, `RelicSwapModalUI`) is open.
  - Added continuous cursor reconciliation (`SyncHoverUnderPointer()`) in `LateUpdate()`, selecting the top-most hand card under the cursor (checking both the unmoving `HoverHitbox` and the lifted `Wrapper` visual on the currently hovered card so moving upward onto the elevated card never flickers off).

---

## 2. Non-Blocking FIFO Combat Queue (Immediate Card Play & End Turn)

- **Root Cause:** `ActionSystem.Perform` ignored incoming actions whenever `IsPerforming` was `true`, while `CardSystem`, `DamageSystem`, `ShieldSystem`, `HealSystem`, `HeroSystem`, and `ManaSystem` stalled on per-action coroutine waits (`0.4s–0.75s`).
- **Changes:**
  - [`ActionSystem.cs`](../Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs): Added a strict FIFO `pendingQueue` (`Enqueue`), `IsEnemyTurnQueuedOrActive` (guards against duplicate `EnemyTurnGA` submissions and locks player cards once End Turn is queued), and `TurnStateChanged`.
  - [`ManaSystem.cs`](../Assets/_Project/Scripts/Systems/ManaSystem.cs): Added synchronous `ReserveMana(int)` / `ReleaseReservedMana(int)` and `AvailableMana = currentMana - reservedMana` so rapid-fire card plays immediately update the Mana UI and cannot overspend mana.
  - [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs) & [`HandView.cs`](../Assets/_Project/Scripts/Views/HandView.cs): Added synchronous `ReserveCardPlay(Card)` (detaches the card from hand on frame 0, disables its raycasts immediately, and launches a fire-and-forget DOTween exit sequence) and non-blocking `AddCardImmediate(CardView)` draw fan updates.
  - [`CardPlayService.cs`](../Assets/_Project/Scripts/Systems/CardPlayService.cs) & [`EndTurnButtonUI.cs`](../Assets/_Project/Scripts/UI/EndTurnButtonUI.cs): Added `IsTurnLocked`, immediate reservation + FIFO `Enqueue(new PlayCardGA(...))`, and non-dropping `Enqueue(new EnemyTurnGA())` on End Turn clicks.
  - [`DamageSystem.cs`](../Assets/_Project/Scripts/Systems/DamageSystem.cs), [`ShieldSystem.cs`](../Assets/_Project/Scripts/Systems/ShieldSystem.cs), [`HealSystem.cs`](../Assets/_Project/Scripts/Systems/HealSystem.cs), [`HeroSystem.cs`](../Assets/_Project/Scripts/Systems/HeroSystem.cs): Removed blocking per-target coroutine stalls (`yield break;`) so combat effects resolve immediately in FIFO order while VFX, shakes, bar tweens, and floating text animate in parallel.

---

## 3. Player-Side Buff/Debuff Hover Tooltips (with Stack Counts)

- **Root Cause:** `CombatTopBar/PartyWillPanel` in `Game.unity` keeps `CanvasGroup.blocksRaycasts = false` so high card drags are never obstructed, which prevented Unity's `GraphicRaycaster` from delivering `OnPointerEnter` events to the hero portrait status icons.
- **Changes:**
  - [`CombatTopBarUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatTopBarUI.cs): Added non-blocking screen-point cursor hit-testing (`UpdateStatusCellHover()` in `LateUpdate()`) across all active hero portrait status icons with `+5px` hover padding (automatically suppressed while dragging cards or when modals are open).
  - [`CombatStatusTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatStatusTooltipUI.cs): Updated tooltip formatting so hovered status icons display the buff/debuff title with `×N` stack count, description, and a `Stacks: N` duration footer.

---

## 4. Automatic Seamless Chapter Continuation After Boss Victory

- **Root Cause:** Defeating an Act/Chapter Boss previously called `LevelProgressionManager.CompleteCurrentLevel()` and redirected the player back to `Levels.unity`, forcing them to re-select the next chapter manually. Additionally, transitioning chapters did not carry forward full campaign state cleanly across procedural maps.
- **Changes:**
  - [`LevelProgressionManager.cs`](../Assets/_Project/Scripts/Levels/LevelProgressionManager.cs) & [`GameManager.prefab`](../Assets/Resources/GameManager.prefab):
    - Added `CompleteCurrentLevelAndAdvance(...)`, `EnsureActiveLevel(...)`, `HasNextLevel`, `NextLevel`, and wired `defaultLevels` (`Level 1: Loneliness`, `Level 2: Fear`, `Level 3: Anxiety`, `Level 4: Anger`) onto `GameManager.prefab` so chapter progression works even when launching directly into `Map.unity` or `Game.unity`.
    - Expanded `CampaignRunState` to preserve `Crystals`, `Potions`, and `Relics` alongside HP, Will/Stress, Gold, and Deck.
  - [`RunManager.cs`](../Assets/_Project/Scripts/Run/RunManager.cs): Added `AdvanceToNextChapterMap(...)`, `SetChapterInfo(...)`, `CurrentChapterNumber`, `CurrentChapterName`, and `IsCurrentNodeBoss` to transition smoothly to a fresh chapter map while retaining all run state.
  - [`MapSystem.cs`](../Assets/_Project/Scripts/Map/MapSystem.cs): Replaced the redirect to `Levels.unity` on `graph.IsBossCleared()` with automatic direct entry into the next chapter (`Chapter 1 → 2 → 3 → 4`), generating a fresh procedural map, updating the scroll header (`CHAPTER N: NAME • START (LEFT) → BOSS (RIGHT)`), and displaying a non-blocking animated chapter entrance banner. Only returns to `Levels.unity` after defeating the final chapter's boss.
  - [`LevelDefinition.cs`](../Assets/_Project/Scripts/Levels/LevelDefinition.cs), [`CombatMapOverlayUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatMapOverlayUI.cs), [`ManualMapController.cs`](../Assets/_Project/Scripts/Map/Manual/ManualMapController.cs), [`ManualMapLayoutLoader.cs`](../Assets/_Project/Scripts/Map/Manual/ManualMapLayoutLoader.cs): Added optional per-chapter `MapGeneratorConfigOverride` and `EncounterPoolOverride` slots on `LevelDefinition`, displayed the active chapter name on the in-combat Map peek overlay, and mirrored auto-advancement in the legacy manual-map flow.

---

## 5. Clickable Discard & Draw Pile Card List Viewer (Existing Project Assets)

- **Root Cause:** Both `Discard Pile UI` and `Draw Pile UI` in `Game.unity` had `m_RaycastTarget: 0` on their `Image` components, causing clicks on the pile icons to pass straight through without invoking `CombatPileViewerUI`.
- **Changes:**
  - [`Game.unity`](../Assets/_Project/Scenes/Game.unity): Enabled `m_RaycastTarget: 1` on both `Discard Pile UI` and `Draw Pile UI` and wired existing project art sprites onto `CombatPileViewerUI`.
  - [`CombatPileViewerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatPileViewerUI.cs):
    - Restyled the modal using existing project assets: [`Panel.png`](../Assets/_Project/Art/Map/Panel.png) (framed panel), [`panel_name_box.png`](../Assets/_Project/Art/Menus/panel_name_box.png) (ornate title ribbon), [`Tombol.png`](../Assets/_Project/Art/Map/Tombol.png) (pile tabs & close button), and [`C_Run Deck Card View.prefab`](../Assets/_Project/Prefabs/CardViews/C_Run%20Deck%20Card%20View.prefab) in a symmetrical 5-column scrollable grid with a vertical scrollbar.
    - Displays live counts on the **`Discard (N)`**, **`Draw (N)`**, and **`Exhaust (N)`** tabs (most recently discarded shown first), `OutBack` pop-in + staggered card reveal animation, runtime `raycastTarget` verification, `1.10×` pile-button hover juice, and `IsAnyOpen` modal input locking wired into `CombatHandController` and `CombatTopBarUI`.

---

## 6. Procedural Slay-the-Spire Deck Reshuffle Animation with Glowing Trails

- **Changes:**
  - [`DeckShuffleAnimatorUI.cs`](../Assets/_Project/Scripts/UI/Combat/DeckShuffleAnimatorUI.cs):
    - Auto-creates a non-blocking `DeckShuffleVfxLayer` on the combat Canvas whenever the draw pile reaches `0` and reshuffles from the discard pile (with an optional `_cardBackSprite` Inspector override).
    - Spawns a sweeping staggered fan (`6–14` cards) of procedural celestial card-backs soaring along high, varied quadratic Bézier arcs from the Discard Pile (bottom-right) into the Draw Pile (bottom-left) with spinning rotation and pseudo-3D flip shimmer.
    - Emits translucent golden motion-blur card afterimages, 4-point twinkling star sparkles, Discard Pile launch recoil, Draw Pile elastic bounce punches, expanding golden impact shockwave rings, and live ticking pile counters during flight.
  - [`UIProceduralRibbonTrail.cs`](../Assets/_Project/Scripts/UI/Combat/UIProceduralRibbonTrail.cs):
    - Native Canvas `MaskableGraphic` ribbon trail tailored for `Screen Space - Overlay` Canvases using a procedural feathered glow texture and a dual-layer pass (soft contrast outer glow + luminous golden-white tapered core).
  - [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs): Replaced instant `RefillDeck()` with `RefillDeckWithAnimation()` inside `DrawCardsPerformer` so the reshuffle wave visibly sweeps across the battlefield and slams into the Draw Pile before the newly drawn cards deal out.

---

## 7. Tuned Card Draw & End-Turn Discard Pacing

- **Changes:**
  - [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs): Exposed Inspector-tunable animation timing fields while preserving non-blocking FIFO gameplay:
    - `drawStaggerDelay = 0.095f` (up from `0.045s`)
    - `discardStaggerDelay = 0.06f` (up from `0.025s`)
    - `discardFlyDuration = 0.28f` (removed the hard `0.14s` cap; added `Ease.InOutCubic` and subtle `-24°` swoop rotation + brief end-turn cascade breath before the enemy turn swings)
    - Removed the hard `0.12s` cap on played-card fly-out animations (`LaunchPlayExitAnimation`) so `doTweenMoveDuration` / `doTweenScaleDuration` are respected.
  - [`HandView.cs`](../Assets/_Project/Scripts/Views/HandView.cs) & [`CombatHandController.cs`](../Assets/_Project/Scripts/Views/CombatHandController.cs): Added `drawCardFlyDuration = 0.28f` with smooth scale-in (`0.45 → 1.0`) from the Draw Pile, and ensured grabbing a card mid-draw immediately kills the draw scale tween and locks full `1.0` scale.

---

## 8. Modular Character Will System & Dynamic Portraits (Character-Select Ready)

- **Design Goal:** Decouple per-character Will rules and HUD portraits from hardcoded scene slots so future Character Selection screens can pass any chosen party roster (`1, 2, 3, 4+` heroes) and automatically vary portraits, Will bars, thresholds, colors, and tooltips.
- **Changes:**
  - [`HeroWillProfile.cs`](../Assets/_Project/Scripts/Data/HeroWillProfile.cs) *(New ScriptableObject: `Create → Data → Hero Will Profile`)*:
    - Configures per-character `MaxWill`, `StartingWill`, `HighWillThreshold`, `LowWillThreshold`, `HighWillBonusMultiplier`, `LowWillMaxHealthPenaltyFraction`, custom High/Low Will titles & descriptions (`{threshold}`, `{bonus}`, `{penalty}` placeholders), and optional custom Will slider/border color palettes.
  - [`HeroData.cs`](../Assets/_Project/Scripts/Data/HeroData.cs):
    - Added `CharacterId`, `DisplayName`, `Portrait`, optional expression overrides (`HighWillPortrait`, `LowWillPortrait`), and `WillProfile`, plus `GetPortraitForWill(isHighWill, isLowWill)` (safely preserves scene fallback sprites when a legacy `HeroData` has no HUD portrait assigned).
  - [`RunHeroStressState.cs`](../Assets/_Project/Scripts/Run/RunHeroStressState.cs), [`HeroView.cs`](../Assets/_Project/Scripts/Views/HeroView.cs), & [`CombatStatusBadgeData.cs`](../Assets/_Project/Scripts/Models/CombatStatusBadgeData.cs):
    - Each party member tracks its own modular Will pool, thresholds, bonus multiplier, Fear HP penalty fraction, portrait expression, and custom Will tooltip copy.
    - Added `HeroView.GetWillState(int)`, `AddWillToHero(int, int)`, and `DrainWillFromHero(int, int)`.
  - [`RunManager.cs`](../Assets/_Project/Scripts/Run/RunManager.cs), [`MatchSetupSystem.cs`](../Assets/_Project/Scripts/Systems/MatchSetupSystem.cs), & [`RunPlayerStateInitializer.cs`](../Assets/_Project/Scripts/Run/RunPlayerStateInitializer.cs):
    - Added `RunManager.SelectedParty`, `SetSelectedParty(...)`, `ClearSelectedParty()`, and `SelectedPartyChanged`. `MatchSetupSystem` and `RunPlayerStateInitializer` automatically consume `RunManager.SelectedParty` when populated (falling back cleanly to Inspector `heroTeam` for standalone testing).
  - [`CombatTopBarUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatTopBarUI.cs):
    - Dynamically binds every character's portrait (including High/Low Will expression swaps), independent Will slider, custom Will color palette, and per-slot Will status badge (`RefreshStatusGrids` evaluates each hero's own Will state independently).
    - Automatically matches active slot count to multi-hero party size (hiding unused slots and dynamically cloning extra slots if `party.Count > 3`).
- **How to Add a Selectable Character Later:**
  1. Create a `HeroWillProfile` asset (`Create → Data → Hero Will Profile`) or leave empty for standard `100 / 50 / 75% / 25%` rules.
  2. Create a `HeroData` asset (`Create → Data → Hero`), assign `DisplayName`, `Health`, `Deck`, `Portrait` (plus optional `HighWillPortrait` / `LowWillPortrait`), and `WillProfile`.
  3. From your future Character Selection UI, call `RunManager.Instance.SetSelectedParty(selectedHeroes)` before loading `Map` / `Game` — HP, Deck, Perks, Portraits, Will meters, and Will status tooltips update automatically.

---

## 9. File Change Inventory

### New Scripts
- [`Assets/_Project/Scripts/UI/Combat/DeckShuffleAnimatorUI.cs`](../Assets/_Project/Scripts/UI/Combat/DeckShuffleAnimatorUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/UIProceduralRibbonTrail.cs`](../Assets/_Project/Scripts/UI/Combat/UIProceduralRibbonTrail.cs)
- [`Assets/_Project/Scripts/Data/HeroWillProfile.cs`](../Assets/_Project/Scripts/Data/HeroWillProfile.cs)

### Modified Scenes & Prefabs
- [`Assets/_Project/Scenes/Game.unity`](../Assets/_Project/Scenes/Game.unity) — Enabled `raycastTarget` on Discard/Draw Pile buttons; wired `Panel.png`, `panel_name_box.png`, and `Tombol.png` sprites onto `CombatPileViewerUI`.
- [`Assets/Resources/GameManager.prefab`](../Assets/Resources/GameManager.prefab) — Wired `defaultLevels` (`Level 1`–`Level 4`) onto `LevelProgressionManager`.

### Modified Scripts
- [`Assets/_Project/Scripts/Systems/Action System/ActionSystem.cs`](../Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs)
- [`Assets/_Project/Scripts/Systems/ManaSystem.cs`](../Assets/_Project/Scripts/Systems/ManaSystem.cs)
- [`Assets/_Project/Scripts/Systems/CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs)
- [`Assets/_Project/Scripts/Systems/CardPlayService.cs`](../Assets/_Project/Scripts/Systems/CardPlayService.cs)
- [`Assets/_Project/Scripts/Systems/DamageSystem.cs`](../Assets/_Project/Scripts/Systems/DamageSystem.cs)
- [`Assets/_Project/Scripts/Systems/ShieldSystem.cs`](../Assets/_Project/Scripts/Systems/ShieldSystem.cs)
- [`Assets/_Project/Scripts/Systems/HealSystem.cs`](../Assets/_Project/Scripts/Systems/HealSystem.cs)
- [`Assets/_Project/Scripts/Systems/HeroSystem.cs`](../Assets/_Project/Scripts/Systems/HeroSystem.cs)
- [`Assets/_Project/Scripts/Systems/MatchSetupSystem.cs`](../Assets/_Project/Scripts/Systems/MatchSetupSystem.cs)
- [`Assets/_Project/Scripts/Views/CombatHandController.cs`](../Assets/_Project/Scripts/Views/CombatHandController.cs)
- [`Assets/_Project/Scripts/Views/HandView.cs`](../Assets/_Project/Scripts/Views/HandView.cs)
- [`Assets/_Project/Scripts/Views/HeroView.cs`](../Assets/_Project/Scripts/Views/HeroView.cs)
- [`Assets/_Project/Scripts/UI/EndTurnButtonUI.cs`](../Assets/_Project/Scripts/UI/EndTurnButtonUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatTopBarUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatTopBarUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatStatusTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatStatusTooltipUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatPileViewerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatPileViewerUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatMapOverlayUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatMapOverlayUI.cs)
- [`Assets/_Project/Scripts/Models/CombatStatusBadgeData.cs`](../Assets/_Project/Scripts/Models/CombatStatusBadgeData.cs)
- [`Assets/_Project/Scripts/Data/HeroData.cs`](../Assets/_Project/Scripts/Data/HeroData.cs)
- [`Assets/_Project/Scripts/Run/RunManager.cs`](../Assets/_Project/Scripts/Run/RunManager.cs)
- [`Assets/_Project/Scripts/Run/RunHeroStressState.cs`](../Assets/_Project/Scripts/Run/RunHeroStressState.cs)
- [`Assets/_Project/Scripts/Run/RunPlayerStateInitializer.cs`](../Assets/_Project/Scripts/Run/RunPlayerStateInitializer.cs)
- [`Assets/_Project/Scripts/Levels/LevelDefinition.cs`](../Assets/_Project/Scripts/Levels/LevelDefinition.cs)
- [`Assets/_Project/Scripts/Levels/LevelProgressionManager.cs`](../Assets/_Project/Scripts/Levels/LevelProgressionManager.cs)
- [`Assets/_Project/Scripts/Map/MapSystem.cs`](../Assets/_Project/Scripts/Map/MapSystem.cs)
- [`Assets/_Project/Scripts/Map/Manual/ManualMapController.cs`](../Assets/_Project/Scripts/Map/Manual/ManualMapController.cs)
- [`Assets/_Project/Scripts/Map/Manual/ManualMapLayoutLoader.cs`](../Assets/_Project/Scripts/Map/Manual/ManualMapLayoutLoader.cs)

---

## 10. Play-Mode Verification Summary

All features were verified live in Unity Play Mode with visual Game View captures and console checks (`0` compilation errors, `0` runtime errors):

| Feature Verified | Live Play-Mode Result |
| --- | --- |
| Stationary-cursor card re-hover | Playing `Rabbit Slash+` synchronously reduced Mana (`5 → 3`) and Hand (`5 → 4`) and immediately hovered the shifted neighbor card (`Zoltraak`) under the stationary cursor without moving out and back. |
| Same-frame FIFO burst + End Turn | Playing `Guard Stance` + `Arrow Rain` and clicking `End Turn` on the same frame queued all 3 actions in order (`Mana: 3 → 1`, `IsEnemyTurnQueuedOrActive = True`) and entered Turn 2 cleanly (`5/5` Mana, `5` drawn cards). |
| Hero Buff/Debuff hover tooltips | Stacking `Strength +3` (×2 → `×6`) and `Weak 2t` (×2 → `×4`) rendered 2 stacked icons under the hero portrait; real cursor screen-point hit-test opened the tooltip displaying `Strength ×6`, description, and `Stacks: 6 • 99 turns remaining`. |
| Automatic Chapter 1 → 2 → 3 → 4 progression | Defeating Chapter 1 Boss in `Game.unity` and continuing past `RunRewards` entered `CHAPTER 2: FEAR` directly on `Map.unity` with a fresh 15-node procedural map while preserving `Gold = 321`, `Crystals = 177`, `HP = 44/44`, and `Deck = 21`; verified automatic continuation into `CHAPTER 3: ANXIETY` and `CHAPTER 4: ANGER`, returning to `Levels.unity` only after clearing Chapter 4. |
| Clickable Discard Pile viewer | Real `EventSystem` raycast hit `Discard Pile UI` (`RaycastTarget = True`, `TopHit = 'Discard Pile UI'`) and opened the framed `Panel.png` modal listing all 5 discarded cards with `Discard (5)` / `Draw (12)` / `Exhaust (0)` tabs. |
| Procedural deck reshuffle animation | Emptying Draw Pile (`0`) with `17` cards in Discard Pile and drawing `3` launched `14` spinning card-backs along sweeping Bézier arcs with `87` live ribbon trail samples, `21` afterimages, and `30` star sparkles, settling cleanly to `Draw = 14`, `Discard = 0`, `Hand = 8`, `IsAnimating = False`. |
| Card draw & discard pacing | Verified smooth `0.095s` draw stagger, `0.28s` glide-in scaling cleanly to `1.0`, and `0.06s` / `0.28s` end-turn discard cascade across Turn 1 → Turn 2 (`0` errors, `0` warnings). |
| Modular Character Will & dynamic portraits | Bound a 2-character party (`Ember` High Will `85/100` with custom `Radiant Resolve` +40% & `HighWillPortrait`; `Aira` Low Will `12/80` with custom `Shattered Focus` Fear & `LowWillPortrait`); top bar adapted cleanly to `2` active slots, swapped both portraits dynamically, rendered independent cyan/crimson Will bars & badges, and hovered Ember's custom `Radiant Resolve` tooltip (`0` errors, `0` warnings). |

