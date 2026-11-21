# Plushie Odyssey — Implementation To-Do List

[Project README](../README.md) · [Code Documentation Index](code/README.md) · [Cards & Hand](code/cards-and-hand.md) · [Input](code/input.md) · [2026-11-20 Recommendations](TODO_RECOMMENDATIONS.md)

Master backlog comparing the current Unity codebase against **Slay the Spire**, **Witch's Apocalyptic Journey (魔女的末日旅途)**, and the project's **Miro** design board.

> **Scope note:** Every item below reflects code, scene, prefab, ScriptableObject, asset, and report state verified directly in the repository (`ProjectDocs/`, `Assets/_Project/`, `ProjectSettings/`, `Packages/`). New feature and improvement recommendations from the 2026-11-20 audit have been separated into [`TODO_RECOMMENDATIONS.md`](TODO_RECOMMENDATIONS.md).

---

## P0 — Critical Bugs, Refactor Wiring & Broken Systems

- [x] **Switch active combat scene from `old_Game.unity` to `Game.unity`**
  - Wired `Assets/_Project/Scenes/Game.unity` with all combat systems, views, HUD, and audio from `old_Game.unity` while preserving the refactored Canvas hand system, and updated [`ProjectSettings/EditorBuildSettings.asset`](../ProjectSettings/EditorBuildSettings.asset) to use `Assets/_Project/Scenes/Game.unity`.
- [x] **Finish `CombatHandController` refactor & Play Mode verification**
  - Updated [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs) for Canvas-aware draw/play/discard animations, added [`CombatTargetingPresenter.cs`](../Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs) to drive `ArrowView` from `CombatHandController`, fixed child `Wrapper` `Button` event interception and WorldSpace UI raycast blocking in [`CombatHandController.cs`](../Assets/_Project/Scripts/Views/CombatHandController.cs), wired `CardPlayService` / `CardTargetResolver` / `CombatHandController`, and verified hover, targeting, card play, End Turn, and Enemy Turn in Play Mode with zero console errors.
- [x] **Register `SimultaneousSystem` so `ScalingEffect` cards work**
  - Auto-attached [`SimultaneousSystem.cs`](../Assets/_Project/Scripts/Systems/SimultaneousSystem.cs) in [`ActionSystem.Awake()`](../Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs) alongside `DamageOverTimeSystem`, `BuffSystem`, `BeamVfxSystem`, and `FrierenShieldSystem`.
- [x] **Fix inverted pause time scale & wire pause menu into `Game.unity`**
  - Fixed [`GameManager.SetPaused(bool pause)`](../Assets/_Project/Scripts/General/GameManager.cs) to set `Time.timeScale = pause ? 0f : 1f;`.
  - Wired `PauseButton`, `PauseAnimateablePanel.prefab`, and `SettingsAnimateablePanel.prefab` (with `PanelTransitionTween` open/close listeners and `GameController.BackToMenu`) into `Game.unity`.
- [x] **Guard or remove the global save-wipe hotkey in `GameManager.Update()`**
  - Wrapped `KeyCode.Equals` (`PlayerPrefs.DeleteAll()`) in [`GameManager.cs`](../Assets/_Project/Scripts/General/GameManager.cs) behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- [x] **Fix `EffectSystem` hardcoded hero caster**
  - Added optional `Caster` to [`PerformEffectGA.cs`](../Assets/_Project/Scripts/Game%20Actions/PerformEffectGA.cs) and updated [`EffectSystem.cs`](../Assets/_Project/Scripts/Systems/EffectSystem.cs) to pass `performEffectGA.Caster` with fallback to `HeroSystem.Instance?.HeroView`.
- [ ] **Place orphaned combat HUDs & systems into `Game.unity`**
  - Wired into `Game.unity` / prefabs: `PotionUseSystem`, repaired `PotionBeltUI.prefab` (`RunPotionBeltUI`) + `PotionIconView.prefab` (`PotionSlotView`), `RelicListUI.prefab` (`RunRelicListUI`), and `CombatantStatusEffectsUI` on `Enemy View.prefab` (with placeholder icons cleared).
  - Still remaining for multi-hero / ultimate milestones:
    - `HeroStressBarView` / `HeroStressPanelUI` (for multi-hero party stress rows)
    - `UltimatePointsView` (when Ultimate charge/cast mechanics are implemented)

---

## P1 — Combat Mechanics, Buffs, Keywords & AI Gaps

### Status Effects, Buffs & DoT Fixes
- [x] **Implement no-op `BuffType` & `ConditionType` handlers**
  - Implemented `FearImmunity` (blocks stress gain in [`HeroSystem.cs`](../Assets/_Project/Scripts/Systems/HeroSystem.cs)), `Redirect` (attack redirection in [`BuffSystem.cs`](../Assets/_Project/Scripts/Systems/BuffSystem.cs)), StS-style stacking/ticking `Poison` in [`BuffSystem.cs`](../Assets/_Project/Scripts/Systems/BuffSystem.cs), and `ConditionType.TargetHasStatus` in [`ConditionalDamageEffect.cs`](../Assets/_Project/Scripts/Effects/ConditionalDamageEffect.cs).
- [x] **Fix `RemoveDebuffPerformer` missing `DamageOverTimeSystem` DoTs & DoT tick timing**
  - [`BuffSystem.RemoveDebuffPerformer`](../Assets/_Project/Scripts/Systems/BuffSystem.cs) now cleanses both `BuffSystem` debuffs and [`DamageOverTimeSystem`](../Assets/_Project/Scripts/Systems/DamageOverTimeSystem.cs) DoTs (`RemoveDoTsFor`).
  - Split DoT/Poison ticks so enemy DoTs tick at Enemy turn start (`EnemyTurnGA.PRE`) and hero DoTs tick at Player turn start (`EnemyTurnGA.POST`), with Poison decrementing by `1` per tick.
- [ ] **Clean up player turn-skip in `StunSystem` & extend `TauntSystem` for multi-hero**
  - `StunSystem` now honors `Artifact` on stun application; player turn-skip refactoring and multi-hero `HeroTM` targeting redirect remain for the multi-hero milestone.
- [x] **Add core *Slay the Spire* combat statuses**
  - Added and wired `Dexterity`, `Frail`, `Thorns`, `Metallicize`, `Artifact`, `Intangible`, `PlatedArmor`, and `Barricade` across [`BuffType.cs`](../Assets/_Project/Scripts/Models/BuffType.cs), [`BuffSystem.cs`](../Assets/_Project/Scripts/Systems/BuffSystem.cs), [`ShieldSystem.cs`](../Assets/_Project/Scripts/Systems/ShieldSystem.cs), and [`DamageSystem.cs`](../Assets/_Project/Scripts/Systems/DamageSystem.cs).

### Card Keywords & Mid-Combat Deck Mechanics
- [x] **Add Card Keywords, Exhaust Pile & Modular X-Cost Cards in `CardData` / `CardSystem`**
  - Added `[Flags] CardKeywords` (`Exhaust`, `Retain`, `Ethereal`, `Innate`, `Unplayable`, `XCost`) and `XBonus` to [`CardData.cs`](../Assets/_Project/Scripts/Data/CardData.cs) / [`Card.cs`](../Assets/_Project/Scripts/Models/Card.cs), custom [`CardDataEditor.cs`](../Assets/_Project/Scripts/Editor/CardDataEditor.cs) (`Is X-Cost` toggle showing `X` instead of `0`), **`X`** cost badge rendering across [`CardView.cs`](../Assets/_Project/Scripts/Views/CardView.cs) / [`MerchantShopUI.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs), FIFO-safe all-Mana spending in [`CardPlayService.cs`](../Assets/_Project/Scripts/Systems/CardPlayService.cs) / [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs), and modular `XScalingAxis` / `GetGameAction(..., int xMultiplier)` scaling across all 22 `Effect` subclasses (Damage, Heal, Shield, Poison/Status, Buffs/Debuffs, DoTs, Stun/Taunt, Draw, Fear/Will, and composite `SimultaneousEffect`/`ScalingEffect`) (persistent `Power` passive triggers remain for future cards).
- [x] **Enforce Max Hand Size (`10`) & empty-draw guards**
  - Enforced `MaxHandSize` (`10`) with `HandFullRejected` toast notification and null-draw guards in [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs).
- [x] **Add Max-Mana scaling API in `ManaSystem`**
  - Exposed `CurrentMana`, `MaxMana`, `ModifyMaxMana`, `SetMaxMana`, and `ManaChanged` in [`ManaSystem.cs`](../Assets/_Project/Scripts/Systems/ManaSystem.cs).
- [x] **Add Will System (`+ High Will` / `- Low Will`) & Per-Card `1–3` RNG Will Gain**
  - Replaced legacy combat Fear/Stress naming across [`HeroWillProfile.cs`](../Assets/_Project/Scripts/Data/HeroWillProfile.cs), [`HeroView.cs`](../Assets/_Project/Scripts/Views/HeroView.cs), [`CombatStatusBadgeData.cs`](../Assets/_Project/Scripts/Models/CombatStatusBadgeData.cs), [`CombatJuiceDirector.cs`](../Assets/_Project/Scripts/VFX/CombatJuiceDirector.cs), [`CombatKeywordTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs), `WillProfile_*.asset`, and card descriptions, and added per-card RNG `+1..+3` Will gain in [`HeroSystem.cs`](../Assets/_Project/Scripts/Systems/HeroSystem.cs).
- [ ] **Implement missing mid-combat card manipulation**
  - **Scry** (inspect & selectively discard top cards of draw pile)
  - **Discover / Choose 1 of N** card creation in combat
  - **Card Upgrade in combat** (single card or full hand)
  - **Status / Curse card injection** into draw/hand/discard piles during combat (by enemies and drawbacks)
  - *(Completed)* **Combat pile viewers**: clickable Draw / Discard / Exhaust pile modal browser via [`CombatPileViewerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatPileViewerUI.cs).

### Party, Heroes, Companions, Ultimates & Enemy AI
- [ ] **Implement multi-hero party & playable hero selection (Aura / Ember)**
  - *(Completed)* Built two-stage celestial **Party Setup** (`Levels.unity`) via [`PartySetupUI.cs`](../Assets/_Project/Scripts/Levels/PartySetupUI.cs) & [`PartySetupArtFactory.cs`](../Assets/_Project/Scripts/Levels/PartySetupArtFactory.cs) with 3 party slots, per-hero **Ultimates**, **Innate + Future Unlockable Talents** on [`HeroData.cs`](../Assets/_Project/Scripts/Data/HeroData.cs) (`Hero_Aira` / `Hero_Ember`), real frame-by-frame animated standing sprites, and innate combat bonuses in [`MatchSetupSystem.cs`](../Assets/_Project/Scripts/Systems/MatchSetupSystem.cs).
  - Remaining in combat: finish multi-member [`AllyByCharacterTargetMode.cs`](../Assets/_Project/Scripts/Target%20Modes/AllyByCharacterTargetMode.cs) and uncomment multi-hero world avatar spawning in [`HeroView.cs`](../Assets/_Project/Scripts/Views/HeroView.cs).
- [ ] **Implement Shared Basic Deck vs Character Starter/Rare Decks (Miro Frame 12)**
  - Separate a character-shared **Basic** card pool from per-character **5-card starter kits + 4 Rare cards** (and update [`YamlCardImporter.cs`](../Assets/_Project/Scripts/Editor/YamlCardImporter.cs), which currently sorts only into `Aura/` and `Ember/`).
- [ ] **Implement Ultimates, Companions/Pets & Archetypes**
  - Wire `UltimatePointsView` (currently only in `UIGameTest.unity`) to combat ultimate charge gain, ultimate cast abilities, **Companions/Pets**, and **Archetypes**.
- [ ] **Expand Enemy AI beyond static intent loops**
  - *(Completed)* Replaced plain-text intent with icon-based [`EnemyIntentHUD.cs`](../Assets/_Project/Scripts/UI/Combat/EnemyIntentHUD.cs) + live buff-adjusted damage/Will numbers and hover tooltips.
  - Remaining: conditional & phase-driven AI (HP-threshold splits, multi-phase bosses, turn-based cycles, reactive triggers when cards are played, Enrage/Artifact/Ritual passives, and Stun/Taunt/Heal/Status-injection intents).
- [ ] **Retire or replace legacy `Perk` condition stubs**
  - [`OnEnemyAttackCondition.cs`](../Assets/_Project/Scripts/Perk%20Condtions/OnEnemyAttackCondition.cs) returns `true` unconditionally and is the sole condition under `Perk Condtions/`; finish consolidating passive triggers under `RelicCombatTriggerSystem`.

---

## P2 — Run Loop, Map, Economy, Potions & Meta-Progression

### Shop, Rest, Rewards & Potions
- [x] **Complete Relic & Potion drops in `RunRewardController`**
  - Added `RunRewardType.Relic` and `RunRewardType.Potion` to [`RunRewardController.cs`](../Assets/_Project/Scripts/Rewards/RunRewardController.cs) with automatic Elite/Boss Relic drops and `RelicSwapModalUI` integration in `RunRewards.unity` (Boss 3-Relic picker remains optional future polish).
- [x] **Build Slay-the-Spire Merchant Shop with Relics, Escalating Reset & Interactive Card Removal (`Merchant.unity`)**
  - Built [`MerchantShopUI.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs) with talking Vendor + native `Vendor Text` dialogue, `Item desc` hover inspector, 2×4 (8 cards) + 3 Relic pedestals beside an escalating circular `RESET` (`10g → 20g → 30g...`) with procedural flip/shimmer choreography, Deck Viewer, and escalating Deck-Grid Card Removal (`75g → 100g...`) (Potion shelf / Courier discounts remain optional future polish).
- [x] **Make `Rest.unity` player-driven with interactive Smith Upgrade Picker**
  - Built [`RestSiteUI.cs`](../Assets/_Project/Scripts/Map/Rest/RestSiteUI.cs) in `Rest.unity` with flickering campfire embers, **Rest** (`+30% HP`), **Calm Mind** (`+30 Will`), and interactive **Smith** Deck-Grid Picker + side-by-side `Card → Card+` preview modal.
- [ ] **Implement out-of-combat Potions & Full-Belt Swap UI**
  - Support `PotionData.UsableOutsideCombat` on Map/Shop/Rest scenes, combat-end / lethal-prevention triggers, and a Discard/Swap prompt when [`RunPotionManager.cs`](../Assets/_Project/Scripts/Run/RunPotionManager.cs) is at `Capacity` (`3`) instead of silently returning `false`.

### Map, Continuous Acts, Meta-Currency & Modes
- [x] **Build Slay-the-Spire Procedural Left-to-Right Map (`Map.unity`) & In-Combat Current-Map Peek Overlay (`Game.unity`)**
  - Upgraded [`ProceduralMapGenerator.cs`](../Assets/_Project/Scripts/Map/ProceduralMapGenerator.cs) & [`MapGeneratorConfig.cs`](../Assets/_Project/Scripts/Map/MapGeneratorConfig.cs) (`Default Map Generator Config.asset`) to procedurally generate **6–8 column** Left-to-Right branching maps with organic position jitter, non-crossing dotted paths, and balanced StS node rules (no consecutive Elite/Rest/Shop/Treasure, no early Elite/Rest, guaranteed Start Enemies on Left, Rest/Treasure pre-Boss, Boss on Right, and sibling diversity).
  - Built [`ProceduralMapRendererUI.cs`](../Assets/_Project/Scripts/UI/Map/ProceduralMapRendererUI.cs) using existing project Map art assets (`scroll.png`, `Pin Map.png`, `Tombol.png`, `Icon_*.png`) with Left→Right staggered procedural pop-up animations, curved inked dotted trails, hover tooltips, and a Legend bar.
  - Wired [`MapSystem.cs`](../Assets/_Project/Scripts/Map/MapSystem.cs) into `Map.unity` with full scene routing (`Game`, `Merchant`, `Rest`, `Event`, `Treasure`, Boss level completion) and wired [`CombatMapOverlayUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatMapOverlayUI.cs) to `CombatTopBar/MapButton` (and `M` hotkey) in `Game.unity` showing the live current run map + `"HERE"` pin marker.
- [x] **Bridge `LevelDefinition` stages into continuous Multi-Act Runs + Victory Summary Flow**
  - Wired automatic direct Chapter `1 → 2 → 3 → 4` progression after Boss victories across [`LevelProgressionManager.cs`](../Assets/_Project/Scripts/Levels/LevelProgressionManager.cs), [`RunManager.cs`](../Assets/_Project/Scripts/Run/RunManager.cs), and [`MapSystem.cs`](../Assets/_Project/Scripts/Map/MapSystem.cs), routing Chapter 4 Boss completion into the new Victory / Defeat Run Summary UI ([`RunEndSummaryUI.cs`](../Assets/_Project/Scripts/Menus/RunEndSummaryUI.cs) in `GameOver.unity`).
- [ ] **Implement `Crystal` Meta-Currency (Miro Frame 1 Spec)**
  - Miro Frame 1's HUD wireframe includes both `Gold` and `Crystal`; [`RunManager.cs`](../Assets/_Project/Scripts/Run/RunManager.cs) tracks `Gold` & `Crystals`, and `RunEndSummaryUI` awards run Crystals—still need persistent meta-wallet saving and a meta-progression upgrade shop.
- [ ] **Add Mid-Run Save / Resume, Seeded Runs, Modes & Compendium**
  - Serialize active run state (seed, deck, HP/Stress, gold/crystals, relics, potions, map progress, floor) for Save & Quit / Continue.
  - Add **Run Seeds** (custom seed input + deterministic RNG), **Ascension / Difficulty** modifiers, **Daily / Custom Modes**, **Compendium / Card Library** viewer, **Run History / Lifetime Stats**, **Unlockable Pools**, and **Achievements**.

### Content Volume (Current vs Target)
- [ ] **Scale ScriptableObject content beyond prototype counts**
  - Cards: currently `31` YAML entries in [`CardsDatabase.yaml`](../Assets/_Project/Data/CardsDatabase.yaml) (`44` Card `.asset` files total; `24` in Merchant Pool)
  - Heroes: `3` (`Default Hero.asset`, `Hero_Aira.asset`, `Hero_Ember.asset` + `WillProfile_Aira`, `WillProfile_Ember`)
  - Relics: `8` (`Relic 1`, `Relic 2`, plus 6 functional combat shop/treasure relics in `Default Merchant Relic Pool.asset`)
  - Narrative Events: `6` (`Assets/_Project/ScriptableObjects/Events/`)
  - Potions: `2` (`Potion Draw.asset`, `Potion Heal.asset`)
  - Enemies: `7` (`Guard`, `King Lizard`, `Lonely Tiger`, `Red Slime`, `Slime`, `The Last Paradise`, `Wilted Rhino`)
  - Encounters: `6`
  - Levels: `4`

---

## P3 — Input, Settings, Localization & UI Feedback

- [ ] **Build `Combat` and `Dialogue` Input Action Maps & Gamepad/Keyboard Controls**
  - Per [`ProjectDocs/code/input.md`](code/input.md), [`InputSystem_Actions.inputactions`](../Assets/_Project/InputSystem_Actions.inputactions) only contains template `Player` and `UI` maps. Add `Combat` and `Dialogue` maps, keyboard hotkeys (card `1–9`, End Turn, view Draw/Discard/Deck), and full gamepad navigation.
- [ ] **Expand `SettingsAnimateablePanel.prefab` & `SettingsController`**
  - Currently limited to `MasterVolume`, `MusicVolume`, and `SFXVolume` sliders (and `LoadAudioWithoutNotify` does not push saved mixer floats on startup). Add Resolution / Display Mode / VSync / FPS Cap, Fast Mode toggle, Screen Shake toggle, Card Confirmation, Language selector, Keybind Rebinding, and Reset Save.
- [ ] **Add Live Damage / Shield / Lethal Previews & Keyword Tooltips**
  - *(Completed)* Live Target Damage/Lethal & Non-Damage X-Cost Reticles ([`CombatTargetingPresenter.cs`](../Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs)), projected HP-bar damage shading ([`CombatantView.cs`](../Assets/_Project/Scripts/Views/CombatantView.cs)), Card Keyword Tooltips ([`CombatKeywordTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs)), Hero & Enemy Buff/Debuff Hover Tooltips ([`CombatStatusTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatStatusTooltipUI.cs)), and Enemy Intent Hover Tooltips ([`EnemyIntentHUD.cs`](../Assets/_Project/Scripts/UI/Combat/EnemyIntentHUD.cs)).
  - Remaining: live buff-adjusted card descriptions on hand cards and hover Keyword Tooltips on Relics/Potions outside combat.
- [ ] **Install & Wire Localization Framework (Miro Frame 9 Spec)**
  - [`Packages/manifest.json`](../Packages/manifest.json) has no localization package (`com.unity.localization`). Add localization infrastructure and import `en`, `zh-Hant`, `ja`, and `ko` string tables.

---

## P4 — Audio, VFX & Art Integration Backlog

- [x] **Add Procedural Double-Blink Scene Transitions, Map Node Activation & Staggered Scene Entrances**
  - Replaced legacy `Loading.unity` transitions with curved procedural double-blink eyelids ([`BlinkSceneTransition.cs`](../Assets/_Project/Scripts/General/Scenes/BlinkSceneTransition.cs)), added juicy Map node click activation choreography (`ProceduralMapRendererUI.PlayNodeActivationTransition`), and added staggered scene entrances across `Game.unity` ([`CombatSceneEntranceDirector.cs`](../Assets/_Project/Scripts/VFX/CombatSceneEntranceDirector.cs)), `Map.unity`, `RunRewards.unity`, `Merchant.unity`, `Rest.unity`, `Treasure.unity`, and `Event.unity`.
- [x] **Wire the 60 unreferenced Character Voicepack WAVs**
  - Connected `Aira` and `Ember` voice cues (`SfxID.Aira_*` / `SfxID.Ember_*`) to combat card plays, damage taken, and healing via [`CharacterVoiceDirector.cs`](../Assets/_Project/Scripts/Audio/CharacterVoiceDirector.cs) (auto-attached in `ActionSystem.Awake()`).
- [x] **Add Core Combat Procedural Juice Pack**
  - Added [`CombatJuiceDirector.cs`](../Assets/_Project/Scripts/VFX/CombatJuiceDirector.cs) (hit-stop, impact-scaled camera shake & zoom punch, played-card comet ribbon trails, Boss Entrance banners, `+ High Will` / `- Low Will` callouts, `E`/`A`/`D` combat hotkeys), procedural combatant idle breathing / directional attack lunges / hurt recoil / enemy death dissolve, parabolic color-coded floating combat numbers, and live HP-bar projected damage preview shading.
- [ ] **Fill empty global Audio/VFX director slots & triage unused pools**
  - On `GameManager.prefab` -> `GameActionAudioDirector`, populate empty `sfxName` entries on `playCardSfx`, `drawCardsBatchSfx`, `dealDamageSfx`, `healSfx`, and `gainShieldSfx`; assign `playCardVfx` on `GameActionVfxDirector`.
  - Review the `267` sidelined audio clips moved to `NotUsed/` ([`AudioSetupReport.md`](../Assets/_Project/Sounds/AudioSetupReport.md)) and `50` unused CFXR/Hit VFX prefabs ([`VfxAssignmentReport.md`](../Assets/_Project/Scripts/VFX/VfxAssignmentReport.md)).
- [ ] **Implement remaining feedback VFX cues from [`VfxHookReport.md`](../Assets/_Project/Scripts/VFX/VfxHookReport.md)**
  - Card hover glow/pulse, pickup/drag trail, invalid release flash, draw shimmer, discard dissolve, reshuffle swirl, map node select/activation & completed-path glow, treasure/gold/relic/potion claim sparkles, HP-threshold tension, and boss intro cues.
- [ ] **Replace placeholder `Temp/` sprites, wire unused art & add combat animations**
  - Named enemies (`The Last Paradise`, `Lonely Tiger`, `Guard`, `King Lizard`, `Wilted Rhino`) still reference sprites in `Assets/_Project/Art/Temp/` (`Boss Eldritch God Shaccad'Yoggoth 1.png`, `Cerberus Ptolemaios(1).png`, `Darkness Lord Knight 2.png`, `Dragon Black.png`, `Earth Lion.png`), and `Card View.prefab` still references `Temp/sprite_test.jpg`.
  - Unreferenced assets needing integration or cleanup (`0` refs): `Temp/Darkness Knight Magellan.png`, `Cnaracter/Desain_character Ember 4.png`, `Background/Taman Berbunga/background.png`, and `BOX TEXT UI/CONTOH !.png`.
  - Upgrade static combat `SpriteRenderer` visuals to animated character/enemy rigs and Stress-reactive expression portraits.

---

## Missing UI & Screens Backlog (Project + Slay the Spire + Witch's Apocalyptic Journey)

Scene/prefab audit comparing current Unity UI (`Assets/_Project/Scenes/` & `Assets/_Project/Prefabs/UI/`) against **Miro** specs, **Slay the Spire**, and **Witch's Apocalyptic Journey (魔女的末日旅途)**.

### A. Cross-Scene Persistent Top Bar (Miro Frame 1 & Frame 3)
- [ ] **Unify the persistent Top Status Bar across all run scenes**
  - *(Completed in `Game.unity`)* Built the Figma-layout **Combat Top Bar** ([`CombatTopBarUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatTopBarUI.cs)) with relocated Party HP Bar, **3 Character Portrait Cards with Will sliders** (`>=75%` High Will `+25%` ATK/DEF, `<25%` Low Will Fear breakdown) & per-character Buff/Debuff grids, **Top-Middle 5-Slot Relic Bar** ([`TopBarRelicSlotsUI.cs`](../Assets/_Project/Scripts/UI/Relics/TopBarRelicSlotsUI.cs)) with 5-relic capacity (`RunRelicManager`), hover tooltips, drag-and-drop slot reordering, and automatic **Full-Slots Drag-and-Drop Equip/Unequip/Swap Modal** ([`RelicSwapModalUI.cs`](../Assets/_Project/Scripts/UI/Relics/RelicSwapModalUI.cs)), **Crystal** + **Gold** readouts, relocated **PotionBeltUI**, **Map**, and **Menu** buttons.
  - Remaining for non-combat scenes (`Map.unity`, `Merchant.unity`, `Rest.unity`, `CardReward.unity`, `RunRewards.unity`, `GameTutorial.unity`): propagate this unified Top Bar & `RunDeckContainer` across all run scenes and add **Floor counter** / **Ascension badge** / per-relic stack counters.

### B. Combat UI (`Game.unity` vs `old_Game.unity` + StS / WAJ)
- [x] **Build out the `Game.unity` combat HUD (`GameCanvas`)**
  - Wired `ManaUI`, `EndTurnButtonUI`, Draw/Discard pile HUDs, `PerksUI`, `PotionBeltUI`, `TopBarRelicSlotsUI` + `RelicSwapModalUI`, `HeroView`, `EnemyBoardView`, `CombatTopBarUI`, `EnemyIntentHUD`, `PauseButton`, `PauseAnimateablePanel`, `SettingsAnimateablePanel`, `RunDeckContainer`, and `ArrowView` + StS/WAJ target reticle indicators (`CombatTargetingPresenter`) into `Game.unity`.
- [x] **Add In-Combat Pile Viewers** (Miro Frame 3: *"showing the discard piles"*)
  - Implemented clickable **Draw Pile Modal** (sorted order indicator), **Discard Pile Modal**, and **Exhaust Pile Modal** via [`CombatPileViewerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatPileViewerUI.cs), wired to `Deck Size UI` and `Discard Pile UI`.
- [ ] **Add Enemy & Companion Combat Overlays**
  - *(Completed)* Built icon-based **Enemy Intent HUD** ([`EnemyIntentHUD.cs`](../Assets/_Project/Scripts/UI/Combat/EnemyIntentHUD.cs) + [`EnemyIntentSnapshot.cs`](../Assets/_Project/Scripts/Models/EnemyIntentSnapshot.cs) + 10 replaceable template icon PNGs in `Assets/_Project/Art/UI/Intents/`) with color-coded top action labels, live buff-adjusted numbers, cursor hover description tooltips, Player-Turn deferred reveals (enemies no longer roll new intents mid-Enemy-Turn), and procedural pop-in/idle/execute animations.
  - *(Completed)* Attached `CombatantStatusEffectsUI` to `Enemy View.prefab` and removed placeholder status icons.
  - Remaining: place multi-hero `HeroStressPanelUI`, `HeroStressBarView`, `UltimatePointsView`, **Breakdown / Fear State banner**, and **Companion / Summon Turn Intent & HP frames**.
- [ ] **Add Live Combat Math, Tooltips & Turn Prompts**
  - *(Completed)* Card hover **Keyword Tooltips** ([`CombatKeywordTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs)), **Enemy Intent Hover Tooltips** ([`EnemyIntentHUD.cs`](../Assets/_Project/Scripts/UI/Combat/EnemyIntentHUD.cs)), **Hero & Enemy Stacked Buff/Debuff Icon Hover Tooltips** ([`CombatStatusTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatStatusTooltipUI.cs)), **Relic Hover Tooltips** ([`TopBarRelicSlotsUI.cs`](../Assets/_Project/Scripts/UI/Relics/TopBarRelicSlotsUI.cs)), **Live Target Damage / Lethal & Taunt Preview Reticles** ([`CombatTargetingPresenter.cs`](../Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs)), **Turn Phase Banner** (`PLAYER TURN` / `ENEMY TURN`), and **Play-Rejection / Hand-Full Toast** ([`CombatFeedbackBannerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatFeedbackBannerUI.cs)).
  - Remaining: tooltips on Potions and **Mid-Combat Card Picker Modals** (Scry, Discover, Discard/Exhaust/Upgrade pickers).

### C. Existing Map & Non-Combat Scene UI Gaps
- [ ] **`MainMenu.unity`**: Only has `Play`, `Settings`, `Credits`, `Exit`. Add **Continue / Resume Run**, **Game Modes** (Standard, Daily, Custom Seed/Modifiers), **Compendium** (Card Library, Relic Gallery, Potion List, Enemy Bestiary), **Run History & Lifetime Stats**, **Achievements**, Language switcher, and **Quit Confirmation** popup.
- [ ] **`Levels.unity`**: *(Completed)* Added two-stage Celestial **Party Setup** (`Aura` & `Ember`, 3-slot tarot cards, Ultimates, Innate + Future Unlockable Talents, 5-icon roster strip, animated chibi sprites) via [`PartySetupUI.cs`](../Assets/_Project/Scripts/Levels/PartySetupUI.cs). Remaining: **Difficulty selector (`Difficulty : Easy`)**, **Level Reward preview panel**, and **Ascension / Seed** controls.
- [ ] **`Map.unity`**: *(Completed)* Built procedural Left-to-Right map with Legend bar, node activation transition, and staggered return reveal via [`ProceduralMapRendererUI.cs`](../Assets/_Project/Scripts/UI/Map/ProceduralMapRendererUI.cs). Remaining: **Act Boss icon/preview** at top of scroll map, clickable **RunDeckContainer**, and **PotionBelt / RelicList** HUD.
- [x] **`Merchant.unity`**: Built [`MerchantShopUI.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs) + Edit-Mode scene baker [`MerchantShopEditorBaker.cs`](../Assets/_Project/Scripts/Editor/MerchantShopEditorBaker.cs) with 8 card offers (`2×4`), 3 Relic pedestals beside an escalating circular `RESET` (`10g → 20g → 30g...`), talking vendor + `Vendor Text` dialogue, `Item desc` hover inspector, Deck Viewer, and escalating Deck-Grid Card Removal (`75g → 100g...`) (Potion shelf remains optional).
- [x] **`Rest.unity`**: Built [`RestSiteUI.cs`](../Assets/_Project/Scripts/Map/Rest/RestSiteUI.cs) with flickering campfire embers, **Rest**, **Calm Mind**, and interactive **Smith** Deck-Grid Upgrade Picker + `Card → Card+` side-by-side preview modal.
- [x] **`Treasure.unity`**: Built [`TreasureRoomUI.cs`](../Assets/_Project/Scripts/Map/Treasure/TreasureRoomUI.cs) with interactive rattling chest burst animation, Gold claim, and random Relic showcase with 5-slot `RelicSwapModalUI` integration.
- [x] **`Event.unity`**: Built data-driven [`NarrativeEventData.cs`](../Assets/_Project/Scripts/Data/NarrativeEventData.cs) + [`NarrativeEventUI.cs`](../Assets/_Project/Scripts/Map/Event/NarrativeEventUI.cs) with 6 handcrafted chapter-themed events, requirement-locked choices, outcome previews, and result feedback.
- [ ] **`RunRewards.unity` & `CardReward.unity`**: Added Relic & Potion reward types + automatic Elite/Boss Relic drops to `RunRewardController`. Still remaining: Boss 3-Relic Picker overlay, Full-Potion-Belt Swap modal, and Rarity/Owned-Count badges on `CardReward.unity`.
- [x] **`GameOver.unity` & Victory Summary Screen**: Built [`RunEndSummaryUI.cs`](../Assets/_Project/Scripts/Menus/RunEndSummaryUI.cs) supporting both **Victory (`VICTORY — RUN COMPLETE!`)** and **Game Over** with Hero, Chapters Reached, Deck Size, Relics, Gold, Crystals Earned, and Play Again / Main Menu controls.
- [ ] **`DialogueTutorial.unity` & Settings Modal**: Add Miro Frame 1's **Intro Cutscene UI**, plus Dialogue **History Log**, **Auto / Text Speed**, and **Branching Choice** buttons to `Dialogue (1).prefab` (`NPCInteraction`). Expand `SettingsAnimateablePanel.prefab` beyond 3 audio sliders to include **Display/Resolution/VSync**, **Fast Mode**, **Screen Shake**, **Confirmations**, **Language (`en`/`zh-Hant`/`ja`/`ko`)**, **Keybinds**, and **Reset Save confirmation**.

### D. Witch's Apocalyptic Journey (`魔女的末日旅途`) Signature UIs
- [ ] **Fate Cards & Daily Schedule Timeline Planner UI**
  - WAJ lets players draw **Fate Cards** (encounters, rests, shops, events) and manually slot them along a **Daily Schedule / Journey Timeline** rather than walking a purely fixed node graph. No Fate Card hand, Timeline slot bar, or Route-Arrangement UI exists yet.
- [ ] **"Super Card" / Blank Card Crafting Workshop UI**
  - WAJ features a Blank Card customization system where players spend **Truth Crystals** (`Crystal` meta-currency in Miro Frame 1) to fuse/replace spell components and create custom cards. No Card Crafting / Component Fusion modal exists yet.
- [ ] **Camp Routine, Blessings & Talent / Meta-Upgrade Tree UI (Miro Frame 1 `Crystal` & Frame 8 `Skills and Upgrades`)**
  - Add a **Meta-Progression / Crystal Upgrade Tree** screen from the hub/menu, in-run **Blessing Selection UI** (distinct from Cards and Relics), and WAJ-style **Character Routine / Schedule & Companion Bond** panels during Camp/Rest phases.

