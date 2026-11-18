# Plushie Odyssey — Implementation To-Do List

[Project README](../README.md) · [Code Documentation Index](code/README.md) · [Cards & Hand](code/cards-and-hand.md) · [Input](code/input.md)

Master backlog comparing the current Unity codebase against **Slay the Spire**, **Witch's Apocalyptic Journey (魔女的末日旅途)**, and the project's **Miro** design board.

> **Scope note:** Every item below reflects code, scene, prefab, ScriptableObject, asset, and report state verified directly in the repository (`ProjectDocs/`, `Assets/_Project/`, `ProjectSettings/`, `Packages/`).

---

## P0 — Critical Bugs, Refactor Wiring & Broken Systems

- [ ] **Switch active combat scene from `old_Game.unity` to `Game.unity`**
  - [`ProjectSettings/EditorBuildSettings.asset`](../ProjectSettings/EditorBuildSettings.asset) currently includes `Assets/_Project/Scenes/Old/old_Game.unity` and omits `Assets/_Project/Scenes/Game.unity`.
  - Complete migration from the `6595`-line `old_Game.unity` to the refactored `2022`-line `Game.unity`, then update Build Settings.
- [ ] **Finish `CombatHandController` refactor & Play Mode verification**
  - Per [`ProjectDocs/code/cards-and-hand.md`](code/cards-and-hand.md), migrate legacy draw/discard presentation, card audio/VFX hooks, and targeting-arrow rendering (`ArrowView` / `ManualTargetingSystem`) to `CombatHandController`, finish scene/prefab wiring, and verify in Play Mode.
- [ ] **Register `SimultaneousSystem` so `ScalingEffect` cards work**
  - [`ScalingEffect.cs`](../Assets/_Project/Scripts/Effects/ScalingEffect.cs) returns `new SequentialGameAction(actions)`, whose performer is only attached by [`SimultaneousSystem.cs`](../Assets/_Project/Scripts/Systems/SimultaneousSystem.cs).
  - [`ActionSystem.Awake()`](../Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs) auto-attaches `DamageOverTimeSystem`, `BuffSystem`, `BeamVfxSystem`, and `FrierenShieldSystem`—**not** `SimultaneousSystem`—and `SimultaneousSystem` is currently placed in `0` scenes or prefabs (breaking scaling cards like `Reward_Aura_Mana_Bloom.asset`).
- [ ] **Fix inverted pause time scale & wire pause menu into `Game.unity`**
  - [`GameManager.SetPaused(bool pause)`](../Assets/_Project/Scripts/General/GameManager.cs) currently sets `Time.timeScale = 1f` when `pause == true` and `0f` when `false`.
  - `PauseAnimateablePanel.prefab` is only placed in `old_Game.unity` and `GameTutorial.unity`, not `Game.unity`.
- [ ] **Guard or remove the global save-wipe hotkey in `GameManager.Update()`**
  - [`GameManager.cs`](../Assets/_Project/Scripts/General/GameManager.cs) calls `PlayerPrefs.DeleteAll()` unconditionally whenever `KeyCode.Equals` (`=`) is pressed, with no `#if UNITY_EDITOR` or debug-build guard.
- [ ] **Fix `EffectSystem` hardcoded hero caster**
  - [`EffectSystem.cs`](../Assets/_Project/Scripts/Systems/EffectSystem.cs) unconditionally passes `HeroSystem.Instance.HeroView` into `GetGameAction`, which prevents multi-hero allies, companions, or non-hero sources from acting as distinct casters through `PerformEffectGA`.
- [ ] **Place orphaned combat HUDs & systems into `Game.unity`**
  - Component scan shows the following exist in scripts but are absent from active combat scenes:
    - `PotionUseSystem` -> `[]` (no scenes/prefabs)
    - `RunPotionBeltUI` -> `[]`
    - `HeroStressBarView` -> `[]`
    - `HeroStressPanelUI` -> `[]`
    - `UltimatePointsView` -> `[UIGameTest.unity]` only
    - `RunRelicListUI` -> `[RelicListUI.prefab]` only
    - `CombatantStatusEffectsUI` -> `[Hero View.prefab]` only (missing from Enemy views/prefabs)

---

## P1 — Combat Mechanics, Buffs, Keywords & AI Gaps

### Status Effects, Buffs & DoT Fixes
- [ ] **Implement no-op `BuffType` & `ConditionType` handlers**
  - [`BuffType.cs`](../Assets/_Project/Scripts/Models/BuffType.cs) defines `FearImmunity`, `Redirect`, and `Poison`, but [`BuffSystem.cs`](../Assets/_Project/Scripts/Systems/BuffSystem.cs) only applies modifiers for `Strength`, `Weak`, `DamageReductionPercentage`, `Vulnerable`, and `DamageReduction` (`Poison` is only mapped in `IsDebuff` / `ApplyStatusPerformer` with no damage-tick effect inside `BuffSystem`).
  - [`ConditionalDamageEffect.cs`](../Assets/_Project/Scripts/Effects/ConditionalDamageEffect.cs) has an empty `case ConditionType.TargetHasStatus: break;` no-op.
- [ ] **Fix `RemoveDebuffPerformer` missing `DamageOverTimeSystem` DoTs & DoT tick timing**
  - `BuffSystem.RemoveDebuffPerformer` only clears entries in `BuffSystem.activeBuffs`, leaving DoTs stored in `DamageOverTimeSystem` untouched.
  - Refactor `DamageOverTimeSystem` (currently ticking both Hero and Enemy DoTs together on `EnemyTurnGA.PRE` via duration) so *Slay the Spire*-style Poison decrements cleanly by `1` on each afflicted combatant's own turn and cleanses properly.
- [ ] **Clean up player turn-skip in `StunSystem` & extend `TauntSystem` for multi-hero**
  - [`StunSystem.cs`](../Assets/_Project/Scripts/Systems/StunSystem.cs) skips a stunned player turn inside `EnemyTurnPostReaction` by immediately queueing another `EnemyTurnGA()`, bypassing normal turn-start/turn-end cleanup.
  - [`TauntSystem.cs`](../Assets/_Project/Scripts/Systems/TauntSystem.cs) only enforces player targeting onto taunted enemies; add enemy-targeting redirect (`HeroTM` / multi-hero support) when heroes or companions apply `Redirect` / `Taunt`.
- [ ] **Add core *Slay the Spire* combat statuses**
  - `Dexterity` / `Frail` (Block/Shield scaling & reduction)
  - `Thorns` / `Metallicize` / `Artifact` (debuff negate) / `Intangible` / `Plated Armor` / `Barricade` (retain shield)

### Card Keywords & Mid-Combat Deck Mechanics
- [ ] **Add Card Keywords & Exhaust Pile in `CardData` / `CardSystem`**
  - [`CardData.cs`](../Assets/_Project/Scripts/Data/CardData.cs) defines `CardType` (`Attack`, `Buff`, `Power`), but lacks keyword flags and runtime handling for `Exhaust`, `Retain`, `Ethereal`, `Innate`, `Unplayable`, `X-Cost`, and persistent `Power` passive triggers.
  - [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs) only tracks `drawPile`, `discardPile`, and `hand`; add an `exhaustPile` and exhaust-recovery actions.
- [ ] **Enforce Max Hand Size (`10`) & empty-draw guards**
  - `HandView.maxHandSize` (`10`) is currently used only for fan-spline layout spacing; add StS-style `"Hand is full"` draw blocking/overflow-to-discard handling (and guard empty draws cleanly where [`ListExtensions.Draw<T>()`](../Assets/_Project/Scripts/Extensions/ListExtensions.cs) returns `default`/`null`).
- [ ] **Add Max-Mana scaling API in `ManaSystem`**
  - [`ManaSystem.cs`](../Assets/_Project/Scripts/Systems/ManaSystem.cs) hardcodes `maxMana = 3` and clamps `ModifyMana` to `[0, maxMana]`; expose max-mana modifiers for Energy Boss Relics (`+1 Max Mana/AP`) and multi-turn mana buffs.
- [ ] **Implement missing mid-combat card manipulation**
  - **Scry** (inspect & selectively discard top cards of draw pile)
  - **Discover / Choose 1 of N** card creation in combat
  - **Card Upgrade in combat** (single card or full hand)
  - **Status / Curse card injection** into draw/hand/discard piles during combat (by enemies and drawbacks)
  - **Combat pile viewers**: in-combat Draw Pile, Discard Pile, and Exhaust Pile modal browsers (currently only run-deck panels exist: `RunDeckPanelController` / `RunDeckCardView`).

### Party, Heroes, Companions, Ultimates & Enemy AI
- [ ] **Implement multi-hero party & playable hero selection (Aura / Ember)**
  - Only `1` hero asset exists ([`Default Hero.asset`](../Assets/_Project/ScriptableObjects/Heroes/Default%20Hero.asset), `Health: 44`), while [`HeroView.cs`](../Assets/_Project/Scripts/Views/HeroView.cs) has multi-hero `heroTeam` spawning logic commented out.
  - Finish multi-member [`AllyByCharacterTargetMode.cs`](../Assets/_Project/Scripts/Target%20Modes/AllyByCharacterTargetMode.cs) (currently still targets the single shared `HeroView`), plus separate `HeroData` assets and playable selection for **Aura** and **Ember**.
- [ ] **Implement Shared Basic Deck vs Character Starter/Rare Decks (Miro Frame 12)**
  - Separate a character-shared **Basic** card pool from per-character **5-card starter kits + 4 Rare cards** (and update [`YamlCardImporter.cs`](../Assets/_Project/Scripts/Editor/YamlCardImporter.cs), which currently sorts only into `Aura/` and `Ember/`).
- [ ] **Implement Ultimates, Companions/Pets & Archetypes**
  - Wire `UltimatePointsView` (currently only in `UIGameTest.unity`) to combat ultimate charge gain, ultimate cast abilities, **Companions/Pets**, and **Archetypes**.
- [ ] **Expand Enemy AI beyond static intent loops**
  - Replace plain-text `NextIntent.IntentName` on [`EnemyView.cs`](../Assets/_Project/Scripts/Views/EnemyView.cs) with StS intent icons (Attack / Multi-hit / Defend / Buff / Debuff / Unknown) and live damage numbers.
  - Add conditional & phase-driven AI (HP-threshold splits, multi-phase bosses, turn-based cycles, reactive triggers when cards are played, Enrage/Artifact/Ritual passives, and Stun/Taunt/Heal/Status-injection intents).
- [ ] **Retire or replace legacy `Perk` condition stubs**
  - [`OnEnemyAttackCondition.cs`](../Assets/_Project/Scripts/Perk%20Condtions/OnEnemyAttackCondition.cs) returns `true` unconditionally and is the sole condition under `Perk Condtions/`; finish consolidating passive triggers under `RelicCombatTriggerSystem`.

---

## P2 — Run Loop, Map, Economy, Potions & Meta-Progression

### Shop, Rest, Rewards & Potions
- [ ] **Complete Reward drops in `RunRewardController`**
  - [`RunRewardController.cs`](../Assets/_Project/Scripts/Rewards/RunRewardController.cs) currently only supports `RunRewardType.Gold` and `RunRewardType.CardReward`; add Relic drops (Elite/Treasure/Boss), Potion drops, and Boss Relic selection.
- [ ] **Expand `MerchantController` & fix interactive Card Removal UI**
  - [`MerchantController.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantController.cs) sells cards and upgrades via `MerchantCardPool`; add **Relic** shelves, **Potion** shelves, StS-style discount/Courier effects, and a deck-grid picker UI for [`MerchantCardRemovalController.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantCardRemovalController.cs) (which currently relies on a single `selectedCard` reference).
- [ ] **Make `RestSiteController` player-driven**
  - [`RestSiteController.cs`](../Assets/_Project/Scripts/Map/Rest/RestSiteController.cs) currently uses a single hardcoded serialized pair (`upgradeSourceCard` -> `upgradeTargetCard`); replace with a deck-browser UI so players choose which card to upgrade (`Smith`), plus optional relics/camp actions (Lift / Toke / Dig / Recall).
- [ ] **Implement out-of-combat Potions & Full-Belt Swap UI**
  - Support `PotionData.UsableOutsideCombat` on Map/Shop/Rest scenes, combat-end / lethal-prevention triggers, and a Discard/Swap prompt when [`RunPotionManager.cs`](../Assets/_Project/Scripts/Run/RunPotionManager.cs) is at `Capacity` (`3`) instead of silently returning `false`.

### Map, Continuous Acts, Meta-Currency & Modes
- [ ] **Replace `MapSystem` non-combat placeholder & populate Manual Map Layouts**
  - [`MapSystem.cs`](../Assets/_Project/Scripts/Map/MapSystem.cs) still logs a placeholder message on procedural non-combat nodes instead of loading Event/Merchant/Rest/Treasure scenes cleanly in that flow.
  - Under `Assets/_Project/Prefabs/Map/ManualLayouts/`, only `FlowTestLayout.prefab` assigns custom `encounter` overrides (`2`); `LonelinessLayout.prefab`, `FearLayout.prefab`, `AnxietyLayout.prefab`, `AngerLayout.prefab`, and `DefaultLayout.prefab` all have `0` assigned encounter overrides (`AngerLayout.prefab` overrides all `40` `nodeType` values to `1`).
- [ ] **Bridge `LevelDefinition` stages into continuous Multi-Act Runs + Victory Flow**
  - Currently `Level 1` (`loneliness`), `Level 2` (`fear`), `Level 3` (`anxiety`), and `Level 4` (`anger`) unlock sequentially in `PlayerPrefs` (`SlayClone.HighestUnlockedLevelIndex`) and return to Levels on completion. Add continuous Act 1→3→Final Boss runs (or act-transition healing/boss chests/true-ending keys) plus a dedicated **Victory** / Run Summary screen.
- [ ] **Implement `Crystal` Meta-Currency (Miro Frame 1 Spec)**
  - Miro Frame 1's HUD wireframe includes both `Gold` and `Crystal`; [`RunManager.cs`](../Assets/_Project/Scripts/Run/RunManager.cs) only tracks `Gold` (`startingGold: 150` in `GameManager.prefab`). Add Crystal earning, persistence, and a meta-progression shop.
- [ ] **Add Mid-Run Save / Resume, Seeded Runs, Modes & Compendium**
  - Serialize active run state (seed, deck, HP/Stress, gold/crystals, relics, potions, map progress, floor) for Save & Quit / Continue.
  - Add **Run Seeds** (custom seed input + deterministic RNG), **Ascension / Difficulty** modifiers, **Daily / Custom Modes**, **Compendium / Card Library** viewer, **Run History / Lifetime Stats**, **Unlockable Pools**, and **Achievements**.

### Content Volume (Current vs Target)
- [ ] **Scale ScriptableObject content beyond prototype counts**
  - Cards: currently `31` YAML entries in [`CardsDatabase.yaml`](../Assets/_Project/Data/CardsDatabase.yaml) (`44` Card `.asset` files total)
  - Heroes: `1` (`Default Hero.asset`)
  - Relics: `2` (`Relic 1.asset`, `Relic 2.asset`)
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
  - Dynamically calculate and display buff/debuff-adjusted damage on hovered/dragged cards and enemy intents, plus projected HP/Shield loss on `CombatantView` sliders.
  - Implement hover **Keyword Tooltips** (`*Tooltip*` script search currently returns `0` files) for buffs, debuffs, Stress, Relics, Potions, and Card Keywords.
- [ ] **Install & Wire Localization Framework (Miro Frame 9 Spec)**
  - [`Packages/manifest.json`](../Packages/manifest.json) has no localization package (`com.unity.localization`). Add localization infrastructure and import `en`, `zh-Hant`, `ja`, and `ko` string tables.

---

## P4 — Audio, VFX & Art Integration Backlog

- [ ] **Wire the 60 unreferenced Character Voicepack WAVs**
  - All `60` clips under `Assets/_Project/Sounds/Sfx/Voicepack/` (`30` `Aira/` + `30` `Ember/`: `attack1-3`, `damaged1-3`, `healed1-3`, `fire`, `ice`, `wind`, `heal`, `cure`, `curse`) currently have `0` references across all `.unity`, `.prefab`, and `.asset` files.
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
- [ ] **Unify the persistent Top Status Bar (`RunStatusUI` + `PotionBeltUI` + `RelicListUI`) across all run scenes**
  - Currently `PotionBeltUI.prefab` and `RelicListUI.prefab` appear in `0` scenes, `RunStatusUI.prefab` is missing from `Game.unity`, `old_Game.unity`, `GameTutorial.unity`, `CardReward.unity`, and `RunRewards.unity`, and `RunDeckContainer.prefab` is missing from `Map.unity`, `Merchant.unity`, `RunRewards.unity`, and `Game.unity`.
  - Add missing Top Bar HUD elements specified in StS / WAJ / Miro Frame 1:
    - Hero Name, Class/Title, Level/Act, **Floor counter**, and **Ascension/Difficulty badge**
    - Dual currency readout: **Gold** + **Crystal** (`gambar crystal`, Miro Frame 1)
    - Interactive **Potion Belt (`PotionBeltUI`)** with Use / Discard / Info actions
    - Interactive **Relics Row (`RelicListUI`)** with hover tooltips and per-relic stack/cooldown counters
    - Always-accessible **Deck Viewer**, **Peek Map**, and **Settings / Pause** icon buttons

### B. Combat UI (`Game.unity` vs `old_Game.unity` + StS / WAJ)
- [ ] **Build out the `Game.unity` combat HUD (`GameCanvas`)**
  - `Game.unity` currently only has card-hand sandbox objects (`HandView`, `Ghosts`, `DragLayer`, `CardPlayService`, `CardViewAdder`) and lacks `ManaUI`, `EndTurnButtonUI`, Draw/Discard pile HUDs, `HeroView`, `EnemyBoardView`, `StressSliderUI`, `PauseButton`, `PauseAnimateablePanel`, and `ArrowView`.
- [ ] **Add In-Combat Pile Viewers** (Miro Frame 3: *"showing the discard piles"*)
  - Clickable **Draw Pile Modal** (sorted order indicator), **Discard Pile Modal**, and **Exhaust Pile Modal** (currently only `RunDeckContainer` exists for the full run deck).
- [ ] **Add Enemy & Companion Combat Overlays**
  - Replace plain-text `Attack Text` (`"ATK: 99"` / `NextIntent.IntentName`) on `Enemy View.prefab` with **Intent Icons** (Attack, Multi-Hit `NxM`, Shield, Buff, Debuff, Stunned, Unknown) and intent tooltips.
  - Attach `CombatantStatusEffectsUI` to **Enemy prefabs** (currently only present on `Hero View.prefab`).
  - Place `HeroStressPanelUI`, `HeroStressBarView`, and `UltimatePointsView` (currently test-only in `UIGameTest.unity`) into the combat HUD, plus a **Breakdown / Fear State banner** and locked-card overlay when stress hits max.
  - Add **Companion / Summon / Multi-Hero Turn Intent & HP frames** (Miro Frame 3: *"Even companion shows what they're gonna do on their next turn"*).
- [ ] **Add Live Combat Math, Tooltips & Turn Prompts**
  - Floating **Keyword & Effect Tooltips** on cards, relics, potions, statuses, and enemy intents (`*Tooltip*` scripts currently count `0`).
  - **Dynamic Damage / Shield / Lethal Preview** (recalculating `Weak`/`Vulnerable`/`Strength` numbers directly on hovered cards, enemy intents, and target HP bars).
  - **Turn Phase Banner** (`"Player Turn"` / `"Enemy Turn"`), **Out-of-Mana / Invalid Target toast** (Miro Frame 3), and **Mid-Combat Card Picker Modals** (Scry, Discover/Choose 1 of N, Discard/Exhaust/Upgrade card selection).

### C. Existing Map & Non-Combat Scene UI Gaps
- [ ] **`MainMenu.unity`**: Only has `Play`, `Settings`, `Credits`, `Exit`. Add **Continue / Resume Run**, **Game Modes** (Standard, Daily, Custom Seed/Modifiers), **Compendium** (Card Library, Relic Gallery, Potion List, Enemy Bestiary), **Run History & Lifetime Stats**, **Achievements**, Language switcher, and **Quit Confirmation** popup.
- [ ] **`Levels.unity`**: Only shows `Level 1`–`Level 4` (`Title`, `Status`, `Description`, `Enter`, `Cancel`). Add Miro Frame 1's **Difficulty selector (`Difficulty : Easy`)**, **Level Reward preview panel**, **Character / Starter Deck selection** (`Aura` vs `Ember` + Shared Basic Deck), and **Ascension / Seed** controls.
- [ ] **`Map.unity`**: Add the **Map Legend panel** sketched in Miro Frame 1 (`Elite Boss`, `Enemy`, `Rest`, `Merchant`, `Event`), **Act Boss icon/preview** at top of scroll map, clickable **RunDeckContainer**, and **PotionBelt / RelicList** HUD.
- [ ] **`Merchant.unity`**: Currently only has card `OfferPanel`, `MerchantCardRemovalController`, `RunStatusUI`, and `Back`. Add **Relic Shop shelf**, **Potion Shop shelf**, **Deck-Grid Card Removal modal** (currently relies on a single `selectedCard` field), **Shopkeeper speech/greeting bubble**, affordability/purchased feedback, and **Deck Viewer** button.
- [ ] **`Rest.unity`**: Currently only has `Go Rest`, `Reduce Stress`, `Leave`, `RunDeckContainer`, and `RunStatusUI`. Add a **Smith / Upgrade Card button** (missing in `Rest.unity`), an interactive **Deck-Grid Upgrade Picker + `Card -> Card+` side-by-side preview modal**, and relic/camp action buttons (Lift, Toke, Dig, Recall).
- [ ] **`Treasure.unity`**: Currently only has `Claim 50 Gold` and `Leave`. Add an interactive **Chest Open animation/state**, **Relic / Card Reward Showcase card**, Mimic/Curse dilemma UI, and **True-Ending Key** claim slot.
- [ ] **`Event.unity`**: Currently only has `Gain 50 Gold` and `Leave` buttons. Add **Event Title banner**, **Narrative Story text box**, **Event Illustration frame**, **Outcome preview & requirement-lock tooltips** on choices, and result summary feedback.
- [ ] **`RunRewards.unity` & `CardReward.unity`**: `RunRewards.unity` only shows `Rewards`, `Card Reward`, and `Continue`—add **Gold / Relic / Potion claim rows**, **Boss 3-Relic Picker overlay**, and **Full-Potion-Belt Swap/Discard modal**. On `CardReward.unity`, add **Rarity banners**, **Owned Duplicate count badges**, and **Keyword Tooltips**.
- [ ] **`GameOver.unity` & Missing Victory Screen**: Currently only shows `"Game Over"`, `"Your run has ended."`, and `"Main Menu"`. Add **Victory screen**, **Run Summary & Score breakdown**, **Final Deck & Relics recap**, **Seed display/copy**, **Crystal meta-currency earned bar**, and **Play Again / Quick Retry**.
- [ ] **`DialogueTutorial.unity` & Settings Modal**: Add Miro Frame 1's **Intro Cutscene UI**, plus Dialogue **History Log**, **Auto / Text Speed**, and **Branching Choice** buttons to `Dialogue (1).prefab` (`NPCInteraction`). Expand `SettingsAnimateablePanel.prefab` beyond 3 audio sliders to include **Display/Resolution/VSync**, **Fast Mode**, **Screen Shake**, **Confirmations**, **Language (`en`/`zh-Hant`/`ja`/`ko`)**, **Keybinds**, and **Reset Save confirmation**.

### D. Witch's Apocalyptic Journey (`魔女的末日旅途`) Signature UIs
- [ ] **Fate Cards & Daily Schedule Timeline Planner UI**
  - WAJ lets players draw **Fate Cards** (encounters, rests, shops, events) and manually slot them along a **Daily Schedule / Journey Timeline** rather than walking a purely fixed node graph. No Fate Card hand, Timeline slot bar, or Route-Arrangement UI exists yet.
- [ ] **"Super Card" / Blank Card Crafting Workshop UI**
  - WAJ features a Blank Card customization system where players spend **Truth Crystals** (`Crystal` meta-currency in Miro Frame 1) to fuse/replace spell components and create custom cards. No Card Crafting / Component Fusion modal exists yet.
- [ ] **Camp Routine, Blessings & Talent / Meta-Upgrade Tree UI (Miro Frame 1 `Crystal` & Frame 8 `Skills and Upgrades`)**
  - Add a **Meta-Progression / Crystal Upgrade Tree** screen from the hub/menu, in-run **Blessing Selection UI** (distinct from Cards and Relics), and WAJ-style **Character Routine / Schedule & Companion Bond** panels during Camp/Rest phases.

