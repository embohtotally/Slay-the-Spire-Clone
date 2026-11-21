# Development Summary — 2026-11-20

[Project README](../README.md) · [Implementation To-Do List](TODO.md) · [Previous Summary (2026-11-19)](CHANGELOG_2026-11-19.md) · [Code Documentation Index](code/README.md)

Complete log of all combat responsiveness, chapter progression, pile viewer UI, procedural deck-shuffle VFX, card pacing, modular character Will architecture, Merchant Shop overhaul & editor baking, Combat Juice & Voicepack wiring, Zoltraak beam fix & shader overhaul, Run-Loop map rooms (Rest, Treasure, Events, Rewards, Victory Summary), and two-stage Celestial Party Setup (`Aura` & `Ember`) work completed on **2026-11-20** in `Slay-the-Spire-Clone` (Unity `6000.0.73f1`, URP `17.1.0`).

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
  - [9. Slay-the-Spire / Crazy-Dave Merchant Shop (Cards, Relics, Escalating Reset \& Removal)](#9-slay-the-spire--crazy-dave-merchant-shop-cards-relics-escalating-reset--removal)
  - [10. Combat Juice Pack, Living Procedural Combatants \& Voicepack Wiring](#10-combat-juice-pack-living-procedural-combatants--voicepack-wiring)
  - [11. Complete Run-Loop Map Rooms (Rest, Treasure, Events, Rewards \& Victory Summary)](#11-complete-run-loop-map-rooms-rest-treasure-events-rewards--victory-summary)
  - [12. Two-Stage Celestial Party Setup, Ultimates, Talents \& Animated Sprites (`Levels.unity`)](#12-two-stage-celestial-party-setup-ultimates-talents--animated-sprites-levelsunity)
  - [13. Zoltraak Beam Effect Fix \& Spellbeam Shader Visual Overhaul (`Game.unity`)](#13-zoltraak-beam-effect-fix--spellbeam-shader-visual-overhaul-gameunity)
  - [14. Editable In-Scene Merchant Shop Hierarchy \& Sprite Assets (`Merchant.unity`)](#14-editable-in-scene-merchant-shop-hierarchy--sprite-assets-merchantunity)
  - [15. Modular X-Cost Card System, Custom Inspector \& All-Effect Scaling](#15-modular-x-cost-card-system-custom-inspector--all-effect-scaling)
  - [16. Will System Renaming (`- Low Will` / `+ High Will`) \& Per-Card `1–3` RNG Will Gain](#16-will-system-renaming---low-will---high-will--per-card-13-rng-will-gain)
  - [17. Procedural Double-Blink Scene Transition, Map Node Activation \& Staggered Scene Entrances](#17-procedural-double-blink-scene-transition-map-node-activation--staggered-scene-entrances)
  - [18. File Change Inventory](#18-file-change-inventory)
    - [New Scripts](#new-scripts)
    - [New ScriptableObject \& Visual Assets](#new-scriptableobject--visual-assets)
    - [Modified Scenes, Prefabs \& Pools](#modified-scenes-prefabs--pools)
    - [Modified Scripts \& Shaders](#modified-scripts--shaders)
  - [19. Play-Mode Verification Summary](#19-play-mode-verification-summary)

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

## 9. Slay-the-Spire / Crazy-Dave Merchant Shop (Cards, Relics, Escalating Reset & Removal)

- **Design Goal:** Rebuild [`Merchant.unity`](../Assets/_Project/Scenes/Merchant.unity) to match the Figma shop mockup and *Crazy Dave* / *Slay the Spire* talking shopkeeper experience using existing project art assets.
- **Changes:**
  - [`MerchantShopUI.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs) *(New)*:
    - **Left Column:** `< MAP` return button (`Tombol.png`), live Gold counter pill with floating `-Xg` cost popups, 5-slot clickable `EQUIPPED RELICS` mini-strip, `DECK (N)` browser modal, escalating **`REMOVE`** card-removal service (`75g → 100g → 125g...`), top-center-left **`Item desc`** parchment inspector, animated talking **Vendor** (`HappySlime.png`, Inspector-swappable via `vendorSprite`) with idle breathing, talking hops, purchase jumps, unaffordable head-shakes, and reset spin-hops, and native **`Vendor Text`** typewriter dialogue (`Panel Box Text_0.png` + `Panel Box NAMA CHARACTER clip.png`).
    - **Right Column (`Panel.png` Stall + `Icon_Merchant.png` Banner):** `2 × 4` grid of **8 Card Offer Slots** (`C_Run Deck Card View.prefab`, auto-scaled to true visual bounds) with italic-gold prices and angled crimson **`SOLD`** stamp slam animations; bottom shelf with **3 Relic Offer Pedestals** right beside the circular **`RESET`** medallion.
    - **Escalating `RESET` Medallion (`10g → 20g → 30g...`):** Spins `-360°` with a golden pulse ring burst, updates escalating cost (`baseResetCost = 10`, `resetCostIncrement = 10`, optional `resetCostAcceleration`), flips old cards & relics out staggered, rolls fresh unshown stock (clearing `SOLD` stamps), and flips new wares in with a golden shimmer wave.
    - **5-Slot Relic Full Safety:** Integrates directly with `RelicSwapModalUI`—holds gold payment until swap confirmation and refunds cleanly if cancelled.
  - **Expanded Shop Pools & 6 Functional Combat Relics:**
    - Created 6 playable `RelicData` assets under `Assets/_Project/ScriptableObjects/Relics/` (`Relic_ScholarScroll`, `Relic_IronCharm`, `Relic_EmberLantern`, `Relic_MendingBrooch`, `Relic_StarlightAmulet`, `Relic_VigilantEye`) using existing project icons and real `RelicReactionEffect` triggers, bundled in [`Default Merchant Relic Pool.asset`](../Assets/_Project/ScriptableObjects/Relics/Default%20Merchant%20Relic%20Pool.asset).
    - Expanded [`Default Merchant Card Pool.asset`](../Assets/_Project/ScriptableObjects/Cards/Default%20Merchant%20Card%20Pool.asset) to 24 varied cards with tier-based pricing (`90g–150g`).

---

## 10. Combat Juice Pack, Living Procedural Combatants & Voicepack Wiring

- **Changes:**
  - [`CombatJuiceDirector.cs`](../Assets/_Project/Scripts/VFX/CombatJuiceDirector.cs) *(New, auto-attached by [`ActionSystem.cs`](../Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs))*:
    - **Hit-Stop & Camera Micro-Shake / Zoom Punch:** Impact-scaled freeze frames (`38ms–62ms`), directional camera recoil/shake (respecting `CombatJuiceDirector.ScreenShakeEnabled`), and micro-zoom punch on heavy hits, Shield Breaks, and enemy kills.
    - **Played-Card Comet Trails:** Spawns curved `UIProceduralRibbonTrail` + glowing orb projectiles from the hand zone straight to the target enemy (or onto the Hero for Buff/Shield cards) with radial impact bursts.
    - **Boss Intro & Will Threshold Alerts:** Dramatic Boss Encounter banner + camera rumble on Boss rooms, plus live visual callouts and subtle screen edge tint transitions on **High Will (`≥75%`)** and **Fear Breakdown (`<25%`)**.
    - **Combat Hotkeys:** `E` (End Turn), `A` (Draw Pile viewer), `D` (Discard Pile viewer).
  - **Living Procedural Combatants & Live HP-Bar Damage Preview:**
    - [`CombatantView.cs`](../Assets/_Project/Scripts/Views/CombatantView.cs), [`CombatantIdleState.cs`](../Assets/_Project/Scripts/StateMachine/CombatantIdleState.cs), [`CombatantAttackState.cs`](../Assets/_Project/Scripts/StateMachine/CombatantAttackState.cs), [`CombatantHurtState.cs`](../Assets/_Project/Scripts/StateMachine/CombatantHurtState.cs) *(also resolved `CS0414`)*, [`EnemyView.cs`](../Assets/_Project/Scripts/Views/EnemyView.cs), [`EnemyBoardView.cs`](../Assets/_Project/Scripts/Views/EnemyBoardView.cs), [`EnemySystem.cs`](../Assets/_Project/Scripts/Systems/EnemySystem.cs), [`CombatTargetingPresenter.cs`](../Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs):
      - Added staggered sinusoidal idle breathing/bobbing, directional attack wind-up & lunge, hurt knockback squash recoil, dramatic tilt-fade enemy defeat dissolution, and pulsing projected HP-bar damage loss shading while targeting enemies.
  - **Parabolic Color-Coded Combat Numbers:**
    - [`GameManager.cs`](../Assets/_Project/Scripts/General/GameManager.cs), [`DamageSystem.cs`](../Assets/_Project/Scripts/Systems/DamageSystem.cs), [`HealSystem.cs`](../Assets/_Project/Scripts/Systems/HealSystem.cs), [`ShieldSystem.cs`](../Assets/_Project/Scripts/Systems/ShieldSystem.cs): Upgraded floating combat text to parabolic fountain arcs with horizontal spread, bold drop-shadows, impact-scaled pop, and distinct styling for HP hits, `BLOCKED!`, `SHIELD BREAK!`, `+HP`, and `+BLOCK`.
  - **60 Character Voicepack Clips Wired:**
    - [`CharacterVoiceDirector.cs`](../Assets/_Project/Scripts/Audio/CharacterVoiceDirector.cs) *(New, auto-attached by `ActionSystem`)*: Automatically plays character-matched `SfxID.Aira_*` / `SfxID.Ember_*` voice clips (`attack`, `fire`, `ice`, `wind`, `heal`, `cure`, `curse`, `damaged`, `healed`) with anti-spam throttling.

---

## 11. Complete Run-Loop Map Rooms (Rest, Treasure, Events, Rewards & Victory Summary)

- **Campfire Rest Site ([`RestSiteUI.cs`](../Assets/_Project/Scripts/Map/Rest/RestSiteUI.cs) in [`Rest.unity`](../Assets/_Project/Scenes/Rest.unity)):**
  - Flickering campfire glow + rising procedural ember particles, top status bar (`HP / WILL / GOLD`), and 3 interactive campfire cards: **Rest** (`+30% Max HP`), **Calm Mind** (`+30 Will`), and **Smith** (scrollable Deck-Grid Card Upgrade picker with side-by-side **`Card → Card+`** confirmation preview).
- **Interactive Treasure Vault ([`TreasureRoomUI.cs`](../Assets/_Project/Scripts/Map/Treasure/TreasureRoomUI.cs) in [`Treasure.unity`](../Assets/_Project/Scenes/Treasure.unity)):**
  - Clickable rattling gilded chest with golden glow that bursts open to reveal claimable **Treasure Gold** (`55g–85g`) + a random unowned **Relic** from `Default Merchant Relic Pool` (with 5-slot `RelicSwapModalUI` support).
- **Data-Driven Branching Narrative Events ([`NarrativeEventData.cs`](../Assets/_Project/Scripts/Data/NarrativeEventData.cs), [`NarrativeEventUI.cs`](../Assets/_Project/Scripts/Map/Event/NarrativeEventUI.cs) in [`Event.unity`](../Assets/_Project/Scenes/Event.unity)):**
  - Created 6 chapter-themed `NarrativeEventData` assets under `Assets/_Project/ScriptableObjects/Events/` (*The Forgotten Toybox*, *The Whispering Mirror*, *Lantern of Warmth*, *Wandering Peddler's Wager*, *The Cracked Hourglass*, *Shrine of Quiet Resolve*) rendered inside a storybook `Panel.png` + `Panel Box Text_0.png` frame with requirement-locked choices, outcome previews, and stat/relic/card rewards.
- **Relic & Potion Drops + Victory/Defeat Run Summary:**
  - [`RunRewardController.cs`](../Assets/_Project/Scripts/Rewards/RunRewardController.cs) & [`RunRewards.unity`](../Assets/_Project/Scenes/RunRewards.unity): Added `RunRewardType.Relic` and `RunRewardType.Potion`, wired `Default Merchant Relic Pool`, and auto-appended Relic drops on Elite/Boss victories.
  - [`RunEndSummaryUI.cs`](../Assets/_Project/Scripts/Menus/RunEndSummaryUI.cs) in [`GameOver.unity`](../Assets/_Project/Scenes/GameOver.unity) & [`MapSystem.cs`](../Assets/_Project/Scripts/Map/MapSystem.cs): Added full Victory (`VICTORY — RUN COMPLETE!`) and Defeat Run Summary UI showing Hero, Chapter Reached, Deck Size, Collected Relics, Gold, Crystals Earned, and Play Again / Main Menu actions; routed Chapter 4 Boss completion directly into Victory mode.

---

## 12. Two-Stage Celestial Party Setup, Ultimates, Talents & Animated Sprites (`Levels.unity`)

- **Design Goal:** Replace the compact `CharacterSelectUI` box in [`Levels.unity`](../Assets/_Project/Scenes/Levels.unity) with a full two-stage Figma-faithful **Party Setup** experience supporting a 3-slot party, per-hero **Ultimates**, **Innate + Future Unlockable Talents**, and **real frame-by-frame animated standing character sprites**.
- **Changes:**
  - [`HeroData.cs`](../Assets/_Project/Scripts/Data/HeroData.cs):
    - Added `HeroUltimateDefinition` (`Title`, `UltimatePointCost`, `Description`, `Icon`) and `HeroTalentDefinition` (`Title`, `Description`, `Icon`, `IsInnate`, `IsUnlocked`, `UnlockRequirement`, plus optional combat modifiers `BonusOpeningDraw` and `BonusStartingShield`).
    - Added Party Setup framing offsets/scales (`tarotPortraitOffset/Scale`, `splashPortraitOffset/Scale`, `rosterIconOffset/Scale`) and frame-by-frame animation lists (`previewIdleFrames`, `previewActionFrames`, `previewIdleFps`, `previewActionFps`).
  - [`MatchSetupSystem.cs`](../Assets/_Project/Scripts/Systems/MatchSetupSystem.cs):
    - Automatically collects unlocked innate talent modifiers from `RunManager.SelectedParty` at combat start (`CollectUnlockedTalentBonuses`), applying **Aura's** *Lantern's Foresight* (`+1` opening draw) and **Ember's** *Steadfast Guard* (`+6` starting Shield).
  - [`PartySetupArtFactory.cs`](../Assets/_Project/Scripts/Levels/PartySetupArtFactory.cs) & [`PartySetupUI.cs`](../Assets/_Project/Scripts/Levels/PartySetupUI.cs) *(New)*:
    - **Levels.unity Launcher Dock:** Compact bottom-left 3-slot mini-portrait strip (`1`, `2`, `3`) + `PARTY SETUP` button synced live with `RunManager.SetSelectedParty` and `RunRelicManager`.
    - **Stage 1 (`PARTY SETUP` Overview):** Warm golden parchment backdrop, corner sunburst emblem, seamless terracotta wave accents, drifting golden motes, 3 top-right diamond slot-clear badges, and 3 tall ornate tarot cards (**Gold Sun / Crimson Star / Azure Moon**) with staggered deal-in bounce, idle float, hover lift/tilt/halo glow, and sparkle bursts.
    - **Stage 2 (Character Detail & Roster Selector):** Dimmed scrim over the 3 tarot cards, oversized left **Character Splash Portrait** with breathing aura, crisp white vertical divider line, serif Character Name, **Ultimate** (`+3 UP` badge), **Innate Talent**, interactive 4-node **Character Talents** strip (`INNATE` + locked `TALENT II / III / IV` future unlocks with click/hover inspection), `ASSIGN` / `SWAP` / `REMOVE` slot controls, bottom 5-icon square roster strip with bold **`1` / `2` / `3`** slot badges (plus 3 locked future companion teasers: *Lyra*, *Noctis*, *Seraphina*), and bottom-right **Live Animated Character Sprite Showcase** looping real idle PNGs and playing real attack/heal frame sequences with voice cues on selection or click.
  - Populated [`Hero_Aira.asset`](../Assets/_Project/ScriptableObjects/Heroes/Hero_Aira.asset) (**Aura** — Ultimate: *Astral Starfall*, Innate: *Lantern's Foresight*, 3 future talents, `Aira` idle/attack/heal frames) and [`Hero_Ember.asset`](../Assets/_Project/ScriptableObjects/Heroes/Hero_Ember.asset) (**Ember** — Ultimate: *Vanguard's Aegis*, Innate: *Steadfast Guard*, 3 future talents, `Amber Bunny` idle/attack frames).

---

## 13. Zoltraak Beam Effect Fix & Spellbeam Shader Visual Overhaul (`Game.unity`)

- **Root Causes Found:**
  - **Duplicate `BeamVfxSystem` Performer Detachment (No Damage / No Visuals):** [`Game.unity`](../Assets/_Project/Scenes/Game.unity) already contained a scene `BeamVfxSystem`, while `ActionSystem.Awake()` auto-added a second `BeamVfxSystem` before the scene instance ran `Awake()`. When the scene instance destroyed itself as a duplicate singleton, its `OnDisable()` called `ActionSystem.DetachPerformer<PlayBeamVfxGA>()`, silently dropping all `PlayBeamVfxGA` actions after spending Mana.
  - **Visual & Audio Defects:** All 14 `ParticleSystemRenderer`s on [`ZoltraakBeam.prefab`](../Assets/_Project/Prefabs/VFX/ZoltraakBeam.prefab) had null materials; all four `Spellbeam` shaders used pure additive blending with high HDR multipliers (plus a `7 × 10 = 70×` double intensity multiplication in `SpellbeamShockwave.shader`), turning the beam and rings into washed-out grey circles over the bright battlefield; `BeamCameraFx` was absent in `Game.unity`; and `Zoltraak_Config` SFX fields were `None`.
- **Changes:**
  - [`ActionSystem.cs`](../Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs): Added `EnsureCombatSystem<T>()` using `FindFirstObjectByType<T>(FindObjectsInactive.Include)` to prevent duplicate singleton creation when a system already exists in the scene.
  - [`BeamVfxSystem.cs`](../Assets/_Project/Scripts/VFX/BeamVfxSystem.cs): Guarded `OnEnable()` / `OnDisable()` on `Instance == this` so duplicate teardown never detaches `PlayBeamVfxGA`, and auto-attached `BeamCameraFx` to `Camera.main` when missing.
  - [`ZoltraakBeamController.cs`](../Assets/_Project/Scripts/VFX/ZoltraakBeamController.cs): Added `ResetVisualState()` on configure/cast start, fixed `rt.Order` / `rt.Proj` indexing, and added `EnsureParticleMaterial(...)` with a procedural soft radial URP particle material fallback.
  - [`SpellbeamBeamCore.shader`](../Assets/_Project/Shaders/SpellbeamBeamCore.shader), [`SpellbeamBeamSheath.shader`](../Assets/_Project/Shaders/SpellbeamBeamSheath.shader), [`SpellbeamMagicCircle.shader`](../Assets/_Project/Shaders/SpellbeamMagicCircle.shader), [`SpellbeamShockwave.shader`](../Assets/_Project/Shaders/SpellbeamShockwave.shader): Switched to crisp alpha-blended magenta-violet + white-hot core rendering and fixed the shockwave `70×` flash multiplication bug.
  - [`ZoltraakSetupAutomation.cs`](../Assets/_Project/Scripts/Editor/ZoltraakSetupAutomation.cs) & refreshed [`Zoltraak_Config.asset`](../Assets/_Project/ScriptableObjects/VFX/Zoltraak_Config.asset) / [`ZoltraakBeam.prefab`](../Assets/_Project/Prefabs/VFX/ZoltraakBeam.prefab): Tuned beam/circle radii, caster/target height offsets, persistent URP particle materials (`M_BeamParticleAdd`, `M_BeamParticleSmoke`), and `ChargeSfx` / `FireSfx` / `ImpactSfx`.

---

## 14. Editable In-Scene Merchant Shop Hierarchy & Sprite Assets (`Merchant.unity`)

- **Design Goal:** Make the procedural Merchant Shop hierarchy visible and directly editable in [`Merchant.unity`](../Assets/_Project/Scenes/Merchant.unity) during Edit Mode so designers can reposition, restyle, or swap sprites on any panel, card/relic slot, vendor visual, dialogue box, or button without losing changes at runtime.
- **Changes:**
  - [`MerchantShopUI.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs): Added `bakedShopRoot`, `BuildBakedHierarchyForEditor()`, `BindExistingShopUI(RectTransform)`, `PopulateEditorPreviewVisuals()`, and persistent Sprite fields (`solidSprite`, `circleSprite`, `circleRingSprite`, `softGlowSprite`, `stampBorderSprite`, `refreshIconSprite`). Play Mode binds directly to the baked scene hierarchy and preserves custom vendor base offsets during animations.
  - [`MerchantShopEditorBaker.cs`](../Assets/_Project/Scripts/Editor/MerchantShopEditorBaker.cs) *(New)*: Added **`Tools → Merchant → Bake Editable Merchant UI Into Scene`** to generate the 6 persistent Sprite PNGs under `Assets/_Project/Art/UI/Merchant/` and bake `Canvas/MerchantShopRoot` (with Edit-Mode preview cards, relics, prices, and inactive `MerchantDeckModal`) into [`Merchant.unity`](../Assets/_Project/Scenes/Merchant.unity).

---

## 15. Modular X-Cost Card System, Custom Inspector & All-Effect Scaling

- **Root Cause:** `Reward_Aura_Celestial_Nova.asset` stored `Mana: 0` with no X-cost keyword or multiplier pipeline, so it rendered `0` on the badge, consumed `0` Mana, and dealt a flat `18` damage regardless of available Mana.
- **Design Goal:** Display **`X`** everywhere (never `0` — both in-game and in the Unity Inspector), consume all remaining Mana atomically in FIFO order, and make X-scaling fully modular across **every** combat effect so designers can author future X-cost cards (Damage, Healing, Shield, Poison, Buffs, Debuffs, DoTs, Draw, Stun, Taunt, Fear/Will, Composite effects, and `X+N` upgrades) with zero C# changes.
- **Changes:**
  - **Data, Inspector & Cost Display (`X` Instead of `0`):**
    - [`CardData.cs`](../Assets/_Project/Scripts/Data/CardData.cs) & [`Card.cs`](../Assets/_Project/Scripts/Models/Card.cs): Added `CardKeywords.XCost` (`1 << 5`), `XBonus` (optional `+N` offset for future `X+1` upgraded cards), `IsXCost`, `CostLabel` (`"X"` when X-cost), and `GetEffectiveX(int manaSpent)`.
    - [`CardDataEditor.cs`](../Assets/_Project/Scripts/Editor/CardDataEditor.cs) *(New Custom Inspector)*: Adds an **`Is X-Cost`** toggle synced with `CardKeywords.XCost`; when enabled, replaces the numeric `Mana: 0` input with a bold **`X  (Consumes all remaining Mana)`** display, reveals `X Bonus`, and shows an authoring info box while preserving `[SerializeReference, SR]` effect dropdowns.
    - [`CardDisplayData.cs`](../Assets/_Project/Scripts/Views/CardDisplayData.cs) & [`CardView.cs`](../Assets/_Project/Scripts/Views/CardView.cs): Added `IsXCost` and `CostLabel`, ensuring hand cards, hover previews, deck/pile viewers, rewards, Rest Smith, and Merchant slots always render **`X`** (including across `SetDisplayedCost` hand refreshes).
    - [`RunDeckPanelController.cs`](../Assets/_Project/Scripts/UI/RunDeckPanelController.cs), [`CombatKeywordTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs), & [`MerchantShopUI.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs): Sorted X-cost cards separately from `0`-cost cards, added the `X-Cost` hover keyword tooltip, and rendered `Mana Cost: X (All Remaining)` in the Merchant inspector.
    - [`YamlCardImporter.cs`](../Assets/_Project/Scripts/Editor/YamlCardImporter.cs), [`CardsDatabase.yaml`](../Assets/_Project/Data/CardsDatabase.yaml), & [`Reward_Aura_Celestial_Nova.asset`](../Assets/_Project/ScriptableObjects/Cards/Generated/Aura/Reward_Aura_Celestial_Nova.asset): Added YAML support for `mana_cost: X`, `x_bonus`, `keywords`, `ApplyDoT`, and per-effect `x_scaling: Auto|Value|Duration|Both|None`; updated `Celestial Nova` to `CardKeywords.XCost` (`Deal 18 Damage x X (Spends all Mana)`).
  - **FIFO-Safe Mana Spending & Modular All-Effect Scaling:**
    - [`PlayCardGA.cs`](../Assets/_Project/Scripts/Game%20Actions/PlayCardGA.cs), [`PerformEffectGA.cs`](../Assets/_Project/Scripts/Game%20Actions/PerformEffectGA.cs), [`CardPlayService.cs`](../Assets/_Project/Scripts/Systems/CardPlayService.cs), [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs), & [`EffectSystem.cs`](../Assets/_Project/Scripts/Systems/EffectSystem.cs): Snapshots and reserves all `AvailableMana` on frame 0 so rapid-fire FIFO card chains consume the exact remaining Mana, skips play VFX/effects cleanly when `effectiveX <= 0`, and passes `XMultiplier = card.GetEffectiveX(spentMana)` into `Effect.GetGameAction(targets, caster, xMultiplier)`.
    - [`Effect.cs`](../Assets/_Project/Scripts/Models/Effect.cs): Added `XScalingAxis` (`Auto`, `Value`, `Duration`, `Both`, `None`) and virtual `GetGameAction(targets, caster, xMultiplier)` (returns `null` at `0`, sequential repeat fallback for custom future effects).
    - **All 22 Effect Subclasses Updated for Single-Action Scaled Execution:**
      - Direct numeric `× X`: [`DealDamageEffect.cs`](../Assets/_Project/Scripts/Effects/DealDamageEffect.cs), [`BeamAttackEffect.cs`](../Assets/_Project/Scripts/Effects/BeamAttackEffect.cs), [`ConditionalDamageEffect.cs`](../Assets/_Project/Scripts/Effects/ConditionalDamageEffect.cs), [`GainShieldEffect.cs`](../Assets/_Project/Scripts/Effects/GainShieldEffect.cs), [`HealEffect.cs`](../Assets/_Project/Scripts/Effects/HealEffect.cs), [`DrawEffect.cs`](../Assets/_Project/Scripts/Effects/DrawEffect.cs), [`DrawCardsEffect.cs`](../Assets/_Project/Scripts/Effects/DrawCardsEffect.cs), [`GainAPEffect.cs`](../Assets/_Project/Scripts/Effects/GainAPEffect.cs), [`AddStressEffect.cs`](../Assets/_Project/Scripts/Effects/AddStressEffect.cs), [`ReduceFearEffect.cs`](../Assets/_Project/Scripts/Effects/ReduceFearEffect.cs), [`RemoveDebuffEffect.cs`](../Assets/_Project/Scripts/Effects/RemoveDebuffEffect.cs), [`ApplyStunEffect.cs`](../Assets/_Project/Scripts/Effects/ApplyStunEffect.cs), [`ApplyTauntEffect.cs`](../Assets/_Project/Scripts/Effects/ApplyTauntEffect.cs), [`RedirectDamageEffect.cs`](../Assets/_Project/Scripts/Effects/RedirectDamageEffect.cs), [`CostReductionEffect.cs`](../Assets/_Project/Scripts/Effects/CostReductionEffect.cs), [`SummonEnemyEffect.cs`](../Assets/_Project/Scripts/Effects/SummonEnemyEffect.cs), [`CopyFearEffect.cs`](../Assets/_Project/Scripts/Effects/CopyFearEffect.cs).
      - Axis-configurable (`Auto` matches `BuffSystem` stacking rules; single Artifact-safe action): [`ApplyStatusEffect.cs`](../Assets/_Project/Scripts/Effects/ApplyStatusEffect.cs) (`Poison` → stacks, `Vulnerable`/`Weakness` → duration), [`ApplyBuffEffect.cs`](../Assets/_Project/Scripts/Effects/ApplyBuffEffect.cs) (`Strength`/`Dexterity`/`Poison`/`Thorns`/`Metallicize`/`PlatedArmor`/`Artifact` → value; `Weak`/`Vulnerable`/`Frail`/`Intangible`/`FearImmunity`/`Redirect` → duration), and [`ApplyDoTEffect.cs`](../Assets/_Project/Scripts/Effects/ApplyDoTEffect.cs).
      - Composite propagation: [`SimultaneousEffect.cs`](../Assets/_Project/Scripts/Effects/SimultaneousEffect.cs) and [`ScalingEffect.cs`](../Assets/_Project/Scripts/Effects/ScalingEffect.cs).
  - **Live Targeting Preview for Damage & Non-Damage X Effects:**
    - [`CombatTargetingPresenter.cs`](../Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs): Scales live damage/lethal reticles by `card.GetEffectiveX(AvailableMana)`, previews X-scaled non-damage effects (`+HP`, `+SHIELD`, `+POISON`, `VULNERABLE NT`, `WEAK NT`, `+STRENGTH`, `STUN NT`, `DOT`), and displays `0 EFFECT (0 MANA)` when aimed at `0` Mana.

---

## 16. Will System Renaming (`- Low Will` / `+ High Will`) & Per-Card `1–3` RNG Will Gain

- **Changes:**
  - **Per-Card RNG Will Gain ([`HeroSystem.cs`](../Assets/_Project/Scripts/Systems/HeroSystem.cs)):**
    - Subscribed a `POST` reaction on `PlayCardGA` with Inspector-configurable `grantWillOnCardPlay` (`true`), `minWillGainPerCard = 1`, and `maxWillGainPerCard = 3` (`UnityEngine.Random.Range(min, max + 1)`), plus `LastCardWillGain` and `CardWillGained` event.
    - Every card played now restores a random **`+1` to `+3` Will** to the party and spawns a cyan floating `"+N Will"` popup on the Hero.
  - **Unified Will System Naming (`- Low Will` & `+ High Will`, Replacing Combat "Fear"/"Stress" Labels):**
    - [`HeroWillProfile.cs`](../Assets/_Project/Scripts/Data/HeroWillProfile.cs), [`WillProfile_Aira.asset`](../Assets/_Project/ScriptableObjects/Heroes/WillProfile_Aira.asset), [`WillProfile_Ember.asset`](../Assets/_Project/ScriptableObjects/Heroes/WillProfile_Ember.asset), & [`Hero_Ember.asset`](../Assets/_Project/ScriptableObjects/Heroes/Hero_Ember.asset): Standardized state titles and descriptions to **`+ High Will`** and **`- Low Will`** (removing legacy `"Fear (Low Will)"` copy).
    - [`CombatStatusBadgeData.cs`](../Assets/_Project/Scripts/Models/CombatStatusBadgeData.cs), [`HeroView.cs`](../Assets/_Project/Scripts/Views/HeroView.cs), [`CombatJuiceDirector.cs`](../Assets/_Project/Scripts/VFX/CombatJuiceDirector.cs), [`CombatFeedbackBannerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatFeedbackBannerUI.cs), [`CombatKeywordTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs), [`RunStatusUI.cs`](../Assets/_Project/Scripts/UI/RunStatusUI.cs), & [`RunRewardController.cs`](../Assets/_Project/Scripts/Rewards/RunRewardController.cs): Standardized status badges (`+W` / **`+ High Will`**, `-W` / **`- Low Will`**), banners (**`+ HIGH WILL!`** / **`- LOW WILL!`**), floating callouts, toasts, keyword tooltips (documenting `+1 to +3 Will (RNG)`), reward copy (`Restore N Will`), and `RunStatusUI` (`Will: current/max`).
    - Updated all 7 legacy Fear card assets & [`CardsDatabase.yaml`](../Assets/_Project/Data/CardsDatabase.yaml) (`Comforting Words`, `Lantern of Hope`, `Memory Echo`, `Starfall Burst`, `Stand Behind Me`, `Last Lantern`, and `Will Shield`), and added `GainWill` / `CopyWill` aliases in [`YamlCardImporter.cs`](../Assets/_Project/Scripts/Editor/YamlCardImporter.cs) while keeping underlying serialized Unity fields/types intact.

---

## 17. Procedural Double-Blink Scene Transition, Map Node Activation & Staggered Scene Entrances

- **Design Goal:** Replace the old `Loading` scene transition with a procedural **double-blink eyelid transition** (Blink 1 flickers shut and drowsily re-opens on the current scene → Blink 2 sweeps shut and swaps scenes → eyelids open onto the new scene), add a juicy **per-node activation transition** on `Map.unity`, and stagger every destination scene's UI/actor entrance so elements cascade in cleanly instead of popping on screen simultaneously.
- **Changes:**
  - **Procedural Double-Blink Scene Transition ([`BlinkSceneTransition.cs`](../Assets/_Project/Scripts/General/Scenes/BlinkSceneTransition.cs) *(New)* & [`SceneLoader.cs`](../Assets/_Project/Scripts/General/Scenes/SceneLoader.cs)):**
    - Generates curved, softly feathered procedural upper/lower eyelid textures (`512 × 256` RGBA) on a persistent `DontDestroyOnLoad` `Screen Space - Overlay` Canvas (`sortingOrder = 32760`) with a drowsy vignette/blackout veil and input lock.
    - Choreographs `Blink1Closing` (`0.11s`) → `Blink1Opening` (`0.15s` to `72%`) → `Blink2Closing` (`0.17s` to `100%` blackout) → `LoadSceneAsync` while sealed → `OpeningNewScene` (`0.36s`), firing `SceneRevealing` / `SceneRevealed` and providing `BlinkSceneTransition.RunWhenRevealing(host, callback)`.
    - Replaced `SceneLoader.LoadScene(...)`'s legacy `Loading` scene detour with `BlinkSceneTransition` globally (covering Map ↔ Battle, Rewards → Map, Map ↔ Merchant/Rest/Treasure/Event, and menu transitions).
  - **Juicy Map Node Activation & Staggered Map Reveal ([`ProceduralMapRendererUI.cs`](../Assets/_Project/Scripts/UI/Map/ProceduralMapRendererUI.cs) & [`MapSystem.cs`](../Assets/_Project/Scripts/Map/MapSystem.cs)):**
    - Added `PlayNodeActivationTransition(MapNode, Action)` on every node click: locks input, plays elastic punch/wobble on the chosen node, bursts two expanding golden/accent shockwave rings + 8 radial sparkle motes, dims unselected nodes, hops the `HERE` pin onto the selected node, and subtly zooms/leans the parchment toward the node (`0.34s`) before triggering the double-blink transition.
    - Synchronized Map entrance (`PrepareHiddenForEntrance` + `BeginStaggeredMapEntrance`) with `BlinkSceneTransition.RunWhenRevealing`: parchment settles → chapter header drops in → legend bar fades in → dotted ink paths and nodes sweep in column-by-column Left → Right → `HERE` pin bounces onto the newly cleared node.
  - **Staggered Battle & Node-Room Entrances:**
    - [`CombatSceneEntranceDirector.cs`](../Assets/_Project/Scripts/VFX/CombatSceneEntranceDirector.cs) *(New, auto-attached by [`ActionSystem.cs`](../Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs))*, [`MatchSetupSystem.cs`](../Assets/_Project/Scripts/Systems/MatchSetupSystem.cs), [`EnemyView.cs`](../Assets/_Project/Scripts/Views/EnemyView.cs), & [`CombatantView.cs`](../Assets/_Project/Scripts/Views/CombatantView.cs): Holds actors/HUD off-stage behind closed lids, then reveals **Top Bar** (glide down) → **Hero** (left hop + squash bounce) → **Enemies** (staggered right swoop + landing bounce) → **Bottom HUD** (Mana, Draw, Discard, End Turn, Perks left-to-right pop) → **Enemy Intents** (staggered) → **Turn 1 Banner & Opening Hand Deal**.
    - Synchronized staggered entrances with `BlinkSceneTransition.RunWhenRevealing` in [`MerchantShopUI.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs), [`RestSiteUI.cs`](../Assets/_Project/Scripts/Map/Rest/RestSiteUI.cs), [`TreasureRoomUI.cs`](../Assets/_Project/Scripts/Map/Treasure/TreasureRoomUI.cs), [`NarrativeEventUI.cs`](../Assets/_Project/Scripts/Map/Event/NarrativeEventUI.cs), and added staggered reward row pop-ins in [`RunRewardController.cs`](../Assets/_Project/Scripts/Rewards/RunRewardController.cs).

---

## 18. File Change Inventory

### New Scripts
- [`Assets/_Project/Scripts/General/Scenes/BlinkSceneTransition.cs`](../Assets/_Project/Scripts/General/Scenes/BlinkSceneTransition.cs)
- [`Assets/_Project/Scripts/VFX/CombatSceneEntranceDirector.cs`](../Assets/_Project/Scripts/VFX/CombatSceneEntranceDirector.cs)
- [`Assets/_Project/Scripts/Editor/CardDataEditor.cs`](../Assets/_Project/Scripts/Editor/CardDataEditor.cs)
- [`Assets/_Project/Scripts/UI/Combat/DeckShuffleAnimatorUI.cs`](../Assets/_Project/Scripts/UI/Combat/DeckShuffleAnimatorUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/UIProceduralRibbonTrail.cs`](../Assets/_Project/Scripts/UI/Combat/UIProceduralRibbonTrail.cs)
- [`Assets/_Project/Scripts/Data/HeroWillProfile.cs`](../Assets/_Project/Scripts/Data/HeroWillProfile.cs)
- [`Assets/_Project/Scripts/Data/NarrativeEventData.cs`](../Assets/_Project/Scripts/Data/NarrativeEventData.cs)
- [`Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs)
- [`Assets/_Project/Scripts/Editor/MerchantShopEditorBaker.cs`](../Assets/_Project/Scripts/Editor/MerchantShopEditorBaker.cs)
- [`Assets/_Project/Scripts/Map/Rest/RestSiteUI.cs`](../Assets/_Project/Scripts/Map/Rest/RestSiteUI.cs)
- [`Assets/_Project/Scripts/Map/Treasure/TreasureRoomUI.cs`](../Assets/_Project/Scripts/Map/Treasure/TreasureRoomUI.cs)
- [`Assets/_Project/Scripts/Map/Event/NarrativeEventUI.cs`](../Assets/_Project/Scripts/Map/Event/NarrativeEventUI.cs)
- [`Assets/_Project/Scripts/VFX/CombatJuiceDirector.cs`](../Assets/_Project/Scripts/VFX/CombatJuiceDirector.cs)
- [`Assets/_Project/Scripts/Audio/CharacterVoiceDirector.cs`](../Assets/_Project/Scripts/Audio/CharacterVoiceDirector.cs)
- [`Assets/_Project/Scripts/Menus/RunEndSummaryUI.cs`](../Assets/_Project/Scripts/Menus/RunEndSummaryUI.cs)
- [`Assets/_Project/Scripts/Levels/CharacterSelectUI.cs`](../Assets/_Project/Scripts/Levels/CharacterSelectUI.cs)
- [`Assets/_Project/Scripts/Levels/PartySetupUI.cs`](../Assets/_Project/Scripts/Levels/PartySetupUI.cs)
- [`Assets/_Project/Scripts/Levels/PartySetupArtFactory.cs`](../Assets/_Project/Scripts/Levels/PartySetupArtFactory.cs)

### New ScriptableObject & Visual Assets
- **Relics & Pool (`Assets/_Project/ScriptableObjects/Relics/`):** `Default Merchant Relic Pool.asset`, `Relic_ScholarScroll.asset`, `Relic_IronCharm.asset`, `Relic_EmberLantern.asset`, `Relic_MendingBrooch.asset`, `Relic_StarlightAmulet.asset`, `Relic_VigilantEye.asset`
- **Narrative Events (`Assets/_Project/ScriptableObjects/Events/`):** `Event_ForgottenToybox.asset`, `Event_WhisperingMirror.asset`, `Event_LanternOfWarmth.asset`, `Event_PeddlersWager.asset`, `Event_CrackedHourglass.asset`, `Event_ShrineOfResolve.asset`
- **Playable Heroes & Will Profiles (`Assets/_Project/ScriptableObjects/Heroes/`):** `Hero_Aira.asset`, `Hero_Ember.asset`, `WillProfile_Aira.asset`, `WillProfile_Ember.asset`
- **Merchant Baked UI Sprites (`Assets/_Project/Art/UI/Merchant/`):** `Merchant_Solid.png`, `Merchant_Circle.png`, `Merchant_CircleRing.png`, `Merchant_SoftGlow.png`, `Merchant_StampBorder.png`, `Merchant_RefreshArrows.png`
- **Spellbeam Particle Materials (`Assets/_Project/Materials/Spellbeam/`):** `M_BeamParticleAdd.mat`, `M_BeamParticleSmoke.mat`

### Modified Scenes, Prefabs & Pools
- [`Assets/_Project/Scenes/Game.unity`](../Assets/_Project/Scenes/Game.unity) — Enabled `raycastTarget` on Discard/Draw Pile buttons; wired `Panel.png`, `panel_name_box.png`, and `Tombol.png` sprites onto `CombatPileViewerUI`.
- [`Assets/_Project/Scenes/Merchant.unity`](../Assets/_Project/Scenes/Merchant.unity) — Baked full editable `Canvas/MerchantShopRoot` hierarchy + preview slots + `RelicSwapModalUI` wired to `MerchantShopUI`.
- [`Assets/_Project/Scenes/Rest.unity`](../Assets/_Project/Scenes/Rest.unity) — Wired `RestSiteUI` campfire + interactive Smith upgrade picker.
- [`Assets/_Project/Scenes/Treasure.unity`](../Assets/_Project/Scenes/Treasure.unity) — Wired `TreasureRoomUI` interactive chest + relic showcase.
- [`Assets/_Project/Scenes/Event.unity`](../Assets/_Project/Scenes/Event.unity) — Wired `NarrativeEventUI` with all 6 handcrafted events.
- [`Assets/_Project/Scenes/RunRewards.unity`](../Assets/_Project/Scenes/RunRewards.unity) — Wired `Default Merchant Relic Pool` onto `RunRewardController` for Elite/Boss relic drops.
- [`Assets/_Project/Scenes/GameOver.unity`](../Assets/_Project/Scenes/GameOver.unity) — Wired `RunEndSummaryUI` (Victory & Defeat summary).
- [`Assets/_Project/Scenes/Levels.unity`](../Assets/_Project/Scenes/Levels.unity) — Wired two-stage `PartySetupUI` (`PartySetupController`) with `Aura` & `Ember` roster and launcher dock.
- [`Assets/_Project/Prefabs/VFX/ZoltraakBeam.prefab`](../Assets/_Project/Prefabs/VFX/ZoltraakBeam.prefab) & [`Assets/_Project/ScriptableObjects/VFX/Zoltraak_Config.asset`](../Assets/_Project/ScriptableObjects/VFX/Zoltraak_Config.asset) — Wired URP particle materials, tuned radii/anchors, and added Charge/Fire/Impact SFX.
- [`Assets/Resources/GameManager.prefab`](../Assets/Resources/GameManager.prefab) — Wired `defaultLevels` (`Level 1`–`Level 4`) onto `LevelProgressionManager`.
- [`Assets/_Project/ScriptableObjects/Cards/Default Merchant Card Pool.asset`](../Assets/_Project/ScriptableObjects/Cards/Default%20Merchant%20Card%20Pool.asset) — Expanded to 24 cards with tier-based prices (`90g–150g`).

### Modified Scripts & Shaders
- [`Assets/_Project/Scripts/VFX/BeamVfxSystem.cs`](../Assets/_Project/Scripts/VFX/BeamVfxSystem.cs)
- [`Assets/_Project/Scripts/VFX/ZoltraakBeamController.cs`](../Assets/_Project/Scripts/VFX/ZoltraakBeamController.cs)
- [`Assets/_Project/Scripts/Editor/ZoltraakSetupAutomation.cs`](../Assets/_Project/Scripts/Editor/ZoltraakSetupAutomation.cs)
- [`Assets/_Project/Shaders/SpellbeamBeamCore.shader`](../Assets/_Project/Shaders/SpellbeamBeamCore.shader)
- [`Assets/_Project/Shaders/SpellbeamBeamSheath.shader`](../Assets/_Project/Shaders/SpellbeamBeamSheath.shader)
- [`Assets/_Project/Shaders/SpellbeamMagicCircle.shader`](../Assets/_Project/Shaders/SpellbeamMagicCircle.shader)
- [`Assets/_Project/Shaders/SpellbeamShockwave.shader`](../Assets/_Project/Shaders/SpellbeamShockwave.shader)
- [`Assets/_Project/Scripts/General/GameManager.cs`](../Assets/_Project/Scripts/General/GameManager.cs)
- [`Assets/_Project/Scripts/Systems/Action System/ActionSystem.cs`](../Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs)
- [`Assets/_Project/Scripts/Systems/ManaSystem.cs`](../Assets/_Project/Scripts/Systems/ManaSystem.cs)
- [`Assets/_Project/Scripts/Systems/CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs)
- [`Assets/_Project/Scripts/Systems/CardPlayService.cs`](../Assets/_Project/Scripts/Systems/CardPlayService.cs)
- [`Assets/_Project/Scripts/Systems/DamageSystem.cs`](../Assets/_Project/Scripts/Systems/DamageSystem.cs)
- [`Assets/_Project/Scripts/Systems/ShieldSystem.cs`](../Assets/_Project/Scripts/Systems/ShieldSystem.cs)
- [`Assets/_Project/Scripts/Systems/HealSystem.cs`](../Assets/_Project/Scripts/Systems/HealSystem.cs)
- [`Assets/_Project/Scripts/Systems/HeroSystem.cs`](../Assets/_Project/Scripts/Systems/HeroSystem.cs)
- [`Assets/_Project/Scripts/Systems/EnemySystem.cs`](../Assets/_Project/Scripts/Systems/EnemySystem.cs)
- [`Assets/_Project/Scripts/Systems/MatchSetupSystem.cs`](../Assets/_Project/Scripts/Systems/MatchSetupSystem.cs)
- [`Assets/_Project/Scripts/StateMachine/CombatantIdleState.cs`](../Assets/_Project/Scripts/StateMachine/CombatantIdleState.cs)
- [`Assets/_Project/Scripts/StateMachine/CombatantAttackState.cs`](../Assets/_Project/Scripts/StateMachine/CombatantAttackState.cs)
- [`Assets/_Project/Scripts/StateMachine/CombatantHurtState.cs`](../Assets/_Project/Scripts/StateMachine/CombatantHurtState.cs)
- [`Assets/_Project/Scripts/Views/CombatantView.cs`](../Assets/_Project/Scripts/Views/CombatantView.cs)
- [`Assets/_Project/Scripts/Views/EnemyView.cs`](../Assets/_Project/Scripts/Views/EnemyView.cs)
- [`Assets/_Project/Scripts/Views/EnemyBoardView.cs`](../Assets/_Project/Scripts/Views/EnemyBoardView.cs)
- [`Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs`](../Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs)
- [`Assets/_Project/Scripts/Views/CombatHandController.cs`](../Assets/_Project/Scripts/Views/CombatHandController.cs)
- [`Assets/_Project/Scripts/Views/HandView.cs`](../Assets/_Project/Scripts/Views/HandView.cs)
- [`Assets/_Project/Scripts/Views/HeroView.cs`](../Assets/_Project/Scripts/Views/HeroView.cs)
- [`Assets/_Project/Scripts/Rewards/RunRewardController.cs`](../Assets/_Project/Scripts/Rewards/RunRewardController.cs)
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
- [`Assets/_Project/Scripts/Data/CardData.cs`](../Assets/_Project/Scripts/Data/CardData.cs)
- [`Assets/_Project/Scripts/Models/Card.cs`](../Assets/_Project/Scripts/Models/Card.cs)
- [`Assets/_Project/Scripts/Models/Effect.cs`](../Assets/_Project/Scripts/Models/Effect.cs)
- [`Assets/_Project/Scripts/Game Actions/PlayCardGA.cs`](../Assets/_Project/Scripts/Game%20Actions/PlayCardGA.cs)
- [`Assets/_Project/Scripts/Game Actions/PerformEffectGA.cs`](../Assets/_Project/Scripts/Game%20Actions/PerformEffectGA.cs)
- [`Assets/_Project/Scripts/Systems/EffectSystem.cs`](../Assets/_Project/Scripts/Systems/EffectSystem.cs)
- [`Assets/_Project/Scripts/Views/CardDisplayData.cs`](../Assets/_Project/Scripts/Views/CardDisplayData.cs)
- [`Assets/_Project/Scripts/Views/CardView.cs`](../Assets/_Project/Scripts/Views/CardView.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs)
- [`Assets/_Project/Scripts/UI/RunDeckPanelController.cs`](../Assets/_Project/Scripts/UI/RunDeckPanelController.cs)
- [`Assets/_Project/Scripts/Editor/YamlCardImporter.cs`](../Assets/_Project/Scripts/Editor/YamlCardImporter.cs)
- `Assets/_Project/Scripts/Effects/*.cs` (all 22 `Effect` subclasses updated with modular `GetGameAction(..., int xMultiplier)` scaling)
- [`Assets/_Project/Data/CardsDatabase.yaml`](../Assets/_Project/Data/CardsDatabase.yaml) & [`Assets/_Project/ScriptableObjects/Cards/Generated/Aura/Reward_Aura_Celestial_Nova.asset`](../Assets/_Project/ScriptableObjects/Cards/Generated/Aura/Reward_Aura_Celestial_Nova.asset)
- [`Assets/_Project/Scripts/Map/Manual/ManualMapController.cs`](../Assets/_Project/Scripts/Map/Manual/ManualMapController.cs)
- [`Assets/_Project/Scripts/Map/Manual/ManualMapLayoutLoader.cs`](../Assets/_Project/Scripts/Map/Manual/ManualMapLayoutLoader.cs)

---

## 19. Play-Mode Verification Summary

All features were verified live in Unity Play Mode with visual Game View captures and console checks (`0` compilation errors, `0` runtime errors, `0` new warnings):

| Feature Verified | Live Play-Mode Result |
| --- | --- |
| Stationary-cursor card re-hover | Playing `Rabbit Slash+` synchronously reduced Mana (`5 → 3`) and Hand (`5 → 4`) and immediately hovered the shifted neighbor card (`Zoltraak`) under the stationary cursor without moving out and back. |
| Same-frame FIFO burst + End Turn | Playing `Guard Stance` + `Arrow Rain` and clicking `End Turn` on the same frame queued all 3 actions in order (`Mana: 3 → 1`, `IsEnemyTurnQueuedOrActive = True`) and entered Turn 2 cleanly (`5/5` Mana, `5` drawn cards). |
| Hero Buff/Debuff hover tooltips | Stacking `Strength +3` (×2 → `×6`) and `Weak 2t` (×2 → `×4`) rendered 2 stacked icons under the hero portrait; real cursor screen-point hit-test opened the tooltip displaying `Strength ×6`, description, and `Stacks: 6 • 99 turns remaining`. |
| Automatic Chapter 1 → 2 → 3 → 4 progression | Defeating Chapter 1 Boss in `Game.unity` and continuing past `RunRewards` entered `CHAPTER 2: FEAR` directly on `Map.unity` with a fresh 15-node procedural map while preserving `Gold = 321`, `Crystals = 177`, `HP = 44/44`, and `Deck = 21`; verified automatic continuation into `CHAPTER 3: ANXIETY` and `CHAPTER 4: ANGER`, routing to Victory Summary on Chapter 4 completion. |
| Clickable Discard Pile viewer | Real `EventSystem` raycast hit `Discard Pile UI` (`RaycastTarget = True`, `TopHit = 'Discard Pile UI'`) and opened the framed `Panel.png` modal listing all 5 discarded cards with `Discard (5)` / `Draw (12)` / `Exhaust (0)` tabs. |
| Procedural deck reshuffle animation | Emptying Draw Pile (`0`) with `17` cards in Discard Pile and drawing `3` launched `14` spinning card-backs along sweeping Bézier arcs with `87` live ribbon trail samples, `21` afterimages, and `30` star sparkles, settling cleanly to `Draw = 14`, `Discard = 0`, `Hand = 8`, `IsAnimating = False`. |
| Card draw & discard pacing | Verified smooth `0.095s` draw stagger, `0.28s` glide-in scaling cleanly to `1.0`, and `0.06s` / `0.28s` end-turn discard cascade across Turn 1 → Turn 2 (`0` errors, `0` warnings). |
| Modular Character Will & dynamic portraits | Bound a 2-character party (`Ember` High Will `85/100` with custom `Radiant Resolve` +40% & `HighWillPortrait`; `Aira` Low Will `12/80` with custom `Shattered Focus` Fear & `LowWillPortrait`); top bar adapted cleanly to `2` active slots, swapped both portraits dynamically, rendered independent cyan/crimson Will bars & badges, and hovered Ember's custom `Radiant Resolve` tooltip (`0` errors, `0` warnings). |
| Merchant Shop (`Merchant.unity`) | Verified `2 × 4` (8 cards) + 3 relics beside circular `RESET (10g)`, live `Item desc` hover inspection, talking vendor (`Vendor Text`), buying 2 cards + 1 relic (`Gold: 500 → 170`, `Deck: 0 → 2`, `Relics: 0 → 1`, angled crimson `SOLD` stamps), escalating resets (`10g → 20g → 30g` with fresh stock & cleared `SOLD` stamps), low-gold guard (`5g` refused reset), and Deck Viewer + escalating Card Removal (`75g → 100g`, `Deck: 1 → 0`). |
| Combat Juice & Voicepack (`Game.unity`) | Verified `CombatJuiceDirector` & `CharacterVoiceDirector` auto-attached in `ActionSystem.Awake()`; played `Zoltraak` with procedural attack lunge, comet cast trail, hit-stop/camera impact hooks, parabolic popups, voice cue dispatch, and live HP-bar damage preview (`0` errors, `0` warnings). |
| Campfire Rest Site (`Rest.unity`) | Visually verified flickering campfire glow, floating embers, and 3 campfire cards (`REST`, `CALM MIND`, `SMITH`); verified `TryRestHeal()` restored HP (`24 → 40 / 50`) and updated narrative readout cleanly. |
| Interactive Treasure Vault (`Treasure.unity`) | Clicked chest to burst open `+61g` + *Ember Lantern* relic and claimed both (`Gold: 150 → 211`, `Relics: 0 → 1`) with `0` errors. |
| Branching Narrative Events (`Event.unity`) | Loaded *The Whispering Mirror* (3 choices with live outcome previews) and resolved Choice 0 (`-15 Will • Card: Knight's Resolve`) cleanly. |
| Victory Run Summary (`GameOver.unity`) | Verified `VICTORY — RUN COMPLETE!` banner, hero/chapter/deck/relic/gold/crystal summary parchment, and Play Again / Main Menu controls. |
| Party Setup (`Levels.unity`) | Verified launcher dock (`HP 114/114`, `Deck 16`), Stage 1 3-card tarot overview (Sun/Star/Moon frames, corner sunburst, 3 diamond badges), Stage 2 Character Detail (`Aura` & `Ember` splash art, vertical divider, Ultimate, Innate + locked Future Talents `II–IV`, 5 square roster icons with `1`/`2` badges, live frame-animated sprite), and live slot-swapping (`Ember` → Slot 3) syncing to `RunManager.SelectedParty` (`0` errors, `0` warnings). |
| Zoltraak Beam (`Game.unity`) | Cast `Zoltraak` against a 3-enemy encounter (`Mana: 5 → 3`, `ImpactsObserved = 3`, `-16` damage dealt to all 3 enemies: `200 → 184`, `120 → 104`, `35 → 19`); visually verified rotating magenta-violet magic circle, white-hot piercing beam + spiral sheath, starlight shockwaves, and soft radial impact particles (`0` errors, `0` warnings). |
| Editable Merchant (`Merchant.unity`) | Visually verified baked `MerchantShopRoot` in Edit Mode (full stall, preview cards, relics, vendor, dialogue, `10g` Reset wheel) and verified Play-Mode `TryBuyCardAt(0)`, `TryBuyRelicAt(0)`, escalating `TryResetShop()` (`10g → 20g`, `Gold: 500 → 230`), zero duplicate roots, and `0` console errors/warnings. |
| Modular X-Cost System (`Game.unity`) | Visually verified **`X`** cost badge on `Celestial Nova` in hand (never `0`) and custom `CardDataEditor` Inspector. Verified `Celestial Nova` (`18 × X` vs `-5` Def enemy): `X=5 → 85 dmg` (`5→0` Mana), `X=2 → 31 dmg` (`2→0` Mana), `X=0 → 0 dmg`, FIFO chain `1-cost + X=4 → 67 dmg` (`5→0` Mana), and `X-Cost` tooltip. Also verified modular runtime X-cost cards: **X-Heal** (`4 × 4 = +16 HP`), **X-Poison** (`3 × 4 = 12` stacks in 1 action + `+12 POISON` preview), **X-Buff Strength** (`2 × 3 = +6`), **X-Debuff Vulnerable** (`1T × 3 = 3T` + `VULNERABLE 3T` preview), **`X+1` Bonus** (`0 Mana → +5 HP`, `2 Mana → +15 HP`), and composite **`SimultaneousEffect`** (`+15 Shield & +3 Dexterity` at `X=3`), with `0` errors / `0` warnings. |
| Will System & `1–3` RNG Card Gain (`Game.unity`) | Verified 10 consecutive played cards each rolled RNG Will in `[1..3]` (`rolls=[2,3,3,3,1,3,3,2,2,3]`, `3` distinct values, Hero Will `30 → 55`, exact `+25` delta). Verified **`- Low Will`** (`Title='- Low Will'`, `Code='-W'`, no `"Fear"` text, Attack cards blocked), verified playing a card at `74` Will organically pushed Hero into **`+ High Will`** (`75/100`, `Title='+ High Will'`, `Code='+W'`), verified updated `Will` keyword tooltip & renamed card descriptions visually in Game View (`0` errors, `0` warnings). |
| Double-Blink Transitions & Staggered Entrances (`Map ↔ Game ↔ RunRewards`) | Verified live end-to-end loop (`Map → Node Activation → Double-Blink → Staggered Battle → RunRewards Result → Double-Blink → Map`): node activation played punch/shockwaves/sparkles/zoom before blinking; both transitions executed full `Blink1Closing → Blink1Opening → Blink2Closing → LoadingScene → OpeningNewScene` (staying on outgoing scene during Blink 1, swapping only on Blink 2); Battle staged Top Bar → Hero → Enemies → Bottom HUD → Intents → Turn 1 Hand cleanly (`EntranceDone=True`, `Enemies=2`, `Hand=5`); `RunRewards` staggered in and double-blinked back to `Map` (`17` nodes, `HERE` pin on cleared node `'0_0'`), `0` errors. |

