# Development Summary — 2026-11-19

[Project README](../README.md) · [Implementation To-Do List](TODO.md) · [Code Documentation Index](code/README.md)

Complete log of all engineering, scene wiring, combat mechanics, UI/UX, audio, and documentation work completed on **2026-11-19** in `Slay-the-Spire-Clone` (Unity `6000.0.73f1`, URP `17.1.0`).

---

## Table of Contents

1. [Documentation & Architecture Planning](#1-documentation--architecture-planning)
2. [Playable `Game.unity` Migration & P0 Bug Fixes](#2-playable-gameunity-migration--p0-bug-fixes)
3. [P1 Combat Mechanics, Status Effects, Keywords & Live Intents](#3-p1-combat-mechanics-status-effects-keywords--live-intents)
4. [Enemy Target Selection Indicator & Modern Card Hand Feel + SFX](#4-enemy-target-selection-indicator--modern-card-hand-feel--sfx)
5. [Figma Combat Top Bar, 3 Character Portraits & Will System](#5-figma-combat-top-bar-3-character-portraits--will-system)
6. [Top-Middle 5-Slot Relic Bar & Full-Slots Drag-and-Drop Swap Modal](#6-top-middle-5-slot-relic-bar--full-slots-drag-and-drop-swap-modal)
7. [Enemy Intent Icon Templates, Hover Tooltips, Turn Timing & Procedural Animations](#7-enemy-intent-icon-templates-hover-tooltips-turn-timing--procedural-animations)
8. [File Change Inventory](#8-file-change-inventory)
9. [Verification Summary](#9-verification-summary)

---

## 1. Documentation & Architecture Planning

- Created and continuously maintained [`ProjectDocs/TODO.md`](TODO.md):
  - Prioritized backlog (`P0`–`P4`) grounded directly in the Unity codebase (`Assets/_Project/`, `ProjectSettings/`, `Packages/`).
  - Added a **Missing UI & Screens Backlog** comparing the current scenes and prefabs against *Slay the Spire*, *Witch's Apocalyptic Journey (魔女的末日旅途)*, and the project's Miro design board.
- Added a **Core Progression Loop Flowchart** on the project's Miro board mapping the full player journey from Main Menu → Run Setup → Map & Node Encounters (Combat, Elite, Event, Merchant, Rest, Treasure) → Act Boss → Victory / Defeat → Meta-Progression.

---

## 2. Playable `Game.unity` Migration & P0 Bug Fixes

- **Wired `Game.unity` into a fully playable combat scene** ([`Assets/_Project/Scenes/Game.unity`](../Assets/_Project/Scenes/Game.unity)):
  - Migrated all combat systems, views, HUD elements, audio directors, and pause/settings modals (`PauseAnimateablePanel`, `SettingsAnimateablePanel`) from `old_Game.unity` while preserving the refactored Canvas `HandView` / `CombatHandController` card architecture.
  - Switched active build scene from `old_Game.unity` to `Game.unity` in [`ProjectSettings/EditorBuildSettings.asset`](../ProjectSettings/EditorBuildSettings.asset).
- **Auto-registered missing combat systems** in [`ActionSystem.cs`](../Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs):
  - Ensured `SimultaneousSystem`, `DamageOverTimeSystem`, `BuffSystem`, `BeamVfxSystem`, and `FrierenShieldSystem` attach automatically on startup so `ScalingEffect` and multi-effect cards work reliably.
- **Fixed critical runtime bugs**:
  - [`GameManager.cs`](../Assets/_Project/Scripts/General/GameManager.cs): Fixed inverted pause time scale (`Time.timeScale = pause ? 0f : 1f`) and guarded the `KeyCode.Equals` save-wipe hotkey behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
  - [`PerformEffectGA.cs`](../Assets/_Project/Scripts/Game%20Actions/PerformEffectGA.cs) & [`EffectSystem.cs`](../Assets/_Project/Scripts/Systems/EffectSystem.cs): Replaced hardcoded hero caster with optional `Caster` (falling back to `HeroSystem.Instance?.HeroView`).
  - Repaired [`PotionBeltUI.prefab`](../Assets/_Project/Prefabs/UI/RelicPotion/PotionBeltUI.prefab) and [`PotionIconView.prefab`](../Assets/_Project/Prefabs/UI/RelicPotion/PotionIconView.prefab), and attached `CombatantStatusEffectsUI` onto [`Enemy View.prefab`](../Assets/_Project/Prefabs/Enemy%20View.prefab) with placeholder icons cleared.

---

## 3. P1 Combat Mechanics, Status Effects, Keywords & Live Intents

- **Expanded Status Effects & Buffs** across [`BuffType.cs`](../Assets/_Project/Scripts/Models/BuffType.cs), [`BuffData.cs`](../Assets/_Project/Scripts/Models/BuffData.cs), [`BuffSystem.cs`](../Assets/_Project/Scripts/Systems/BuffSystem.cs), [`DamageOverTimeSystem.cs`](../Assets/_Project/Scripts/Systems/DamageOverTimeSystem.cs), [`DamageSystem.cs`](../Assets/_Project/Scripts/Systems/DamageSystem.cs), [`ShieldSystem.cs`](../Assets/_Project/Scripts/Systems/ShieldSystem.cs), [`HeroSystem.cs`](../Assets/_Project/Scripts/Systems/HeroSystem.cs), [`StunSystem.cs`](../Assets/_Project/Scripts/Systems/StunSystem.cs), and [`ConditionalDamageEffect.cs`](../Assets/_Project/Scripts/Effects/ConditionalDamageEffect.cs):
  - Implemented previously no-op handlers: `FearImmunity`, `Redirect`, stacking/ticking `Poison`, and `ConditionType.TargetHasStatus`.
  - Added core *Slay the Spire* statuses: `Dexterity`, `Frail`, `Thorns`, `Metallicize`, `Artifact` (blocks negative buffs & stuns), `Intangible`, `PlatedArmor`, and `Barricade`.
  - Fixed `RemoveDebuffPerformer` to cleanse both `BuffSystem` debuffs and `DamageOverTimeSystem` DoTs (`RemoveDoTsFor`), and split DoT/Poison tick timing (`EnemyTurnGA.PRE` for enemies, `EnemyTurnGA.POST` for heroes).
- **Card Keywords, Exhaust Pile, Hand Cap & Max-Mana API**:
  - Added `[Flags] CardKeywords` (`Exhaust`, `Retain`, `Ethereal`, `Innate`, `Unplayable`) in [`CardData.cs`](../Assets/_Project/Scripts/Data/CardData.cs) and [`Card.cs`](../Assets/_Project/Scripts/Models/Card.cs).
  - Implemented `ExhaustPile`, `MaxHandSize = 10` enforcement (`HandFullRejected` event), and empty-draw guards in [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs) & [`CardPlayService.cs`](../Assets/_Project/Scripts/Systems/CardPlayService.cs).
  - Exposed `CurrentMana`, `MaxMana`, `ModifyMaxMana`, `SetMaxMana`, and `ManaChanged` in [`ManaSystem.cs`](../Assets/_Project/Scripts/Systems/ManaSystem.cs).
- **Live Enemy Intents & Combat Feedback UIs**:
  - Upgraded [`EnemyView.cs`](../Assets/_Project/Scripts/Views/EnemyView.cs) to display live buff-adjusted color-coded intents (`[ATK N]`, `[DEF +N]`, `[WILL -N]`, `[HEAL +N]`, `[BUFF]`, `[DEBUFF]`, `[STUNNED]`).
  - Added [`CombatFeedbackBannerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatFeedbackBannerUI.cs) for turn-phase banners (`PLAYER TURN` / `ENEMY TURN`) and rejection/hand-full toasts.
  - Added [`CombatPileViewerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatPileViewerUI.cs) for clickable Draw, Discard, and Exhaust pile browser modals.
  - Added [`CombatKeywordTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs) with word-boundary (`\b`) keyword detection on card hover.

---

## 4. Enemy Target Selection Indicator & Modern Card Hand Feel + SFX

- **StS / WAJ Enemy Target Selection Indicator** ([`CombatTargetingPresenter.cs`](../Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs), [`ArrowView.cs`](../Assets/_Project/Scripts/Views/ArrowView.cs), [`CardTargetResolver.cs`](../Assets/_Project/Scripts/Systems/CardTargetResolver.cs)):
  - Added pulsing corner target reticles around hovered/valid enemies, enemy highlight tint/scale feedback, AoE multi-target highlights, and live damage / **LETHAL** / **TAUNT** preview badges.
  - Fixed `ArrowView` origin anchor to spawn cleanly from the card's visual `Wrapper` top edge.
- **Modern Card Hover Pop-Up, Correlated Neighbor Spread & Existing SFX** ([`CardVisualFeedback.cs`](../Assets/_Project/Scripts/Views/CardVisualFeedback.cs), [`HandView.cs`](../Assets/_Project/Scripts/Views/HandView.cs), [`CombatHandController.cs`](../Assets/_Project/Scripts/Views/CombatHandController.cs), [`Card View.prefab`](../Assets/_Project/Prefabs/CardViews/Card%20View.prefab)):
  - Disabled ghost/duplicate hover preview (`_useHoverPreview = false`) so hovered cards lift, scale up smoothly in place, straighten, and render on top without doubling.
  - Added an unmoving `HoverHitbox` to prevent pointer-enter/exit jitter and implemented correlated neighbor spread in `HandView` so adjacent cards smoothly slide outward around the hovered card.
  - Fixed child `Wrapper/Button` raycast interception and properly reset `CardPointerInput` state on `EndInteraction`.
  - Wired existing Tune SFX cues: `SfxID.hover` (card hover), `SfxID.card_pickup` (card grab), `SfxID.card_target_start` (single-target drag start), `SfxID.node_hover` (enemy target lock-on), and `SfxID.card_release_invalid` (invalid release/cancel).

---

## 5. Figma Combat Top Bar, 3 Character Portraits & Will System

- **Figma-Layout Combat Top Bar** ([`CombatTopBarUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatTopBarUI.cs), [`Game.unity`](../Assets/_Project/Scenes/Game.unity)):
  - Relocated the shared **Party HP Bar** (with Shield & Low-Will penalty overlays) to top-left and added `CombatantView.HealthChanged` in [`CombatantView.cs`](../Assets/_Project/Scripts/Views/CombatantView.cs).
  - Placed **3 Character Portrait Cards** directly beneath the HP bar (`Ember`, `Aura`, and a 3rd party slot) with individual **Will sliders**, state indicators, and per-character **Buff / Debuff icon grids**.
  - Added **Crystal** currency tracking in [`RunManager.cs`](../Assets/_Project/Scripts/Run/RunManager.cs) alongside Gold, plus Map scroll and Menu buttons on the top-right.
- **Will System (`Fear` → `Will`)** ([`RunHeroStressState.cs`](../Assets/_Project/Scripts/Run/RunHeroStressState.cs), [`HeroView.cs`](../Assets/_Project/Scripts/Views/HeroView.cs), [`BuffSystem.cs`](../Assets/_Project/Scripts/Systems/BuffSystem.cs), [`HeroSystem.cs`](../Assets/_Project/Scripts/Systems/HeroSystem.cs), [`EnemyView.cs`](../Assets/_Project/Scripts/Views/EnemyView.cs)):
  - Refactored the meter to **Will** (`0%–100%`, starting at `50%`) while retaining legacy Stress/Fear method aliases so all existing cards and enemy intents remain compatible (enemy fear attacks drain Will).
  - **High Will (`>= 75%`)**: Grants a positive combat bonus (**+25% outgoing attack damage** and **+25% Shield gain**), cyan portrait border glow, bright blue Will fill, and a `WL+` buff indicator.
  - **Low Will (`< 25%`)**: Triggers the Fear breakdown effect (effective Max HP penalty bar), crimson Will fill, red portrait border, and `FEAR` debuff badge.

---

## 6. Top-Middle 5-Slot Relic Bar & Full-Slots Drag-and-Drop Swap Modal

- **5-Relic Max Capacity & Swap State** ([`RunRelicManager.cs`](../Assets/_Project/Scripts/Run/RunRelicManager.cs)):
  - Enforced a fixed **5-slot** equipped cap (`MaxEquippedRelics = 5`, index-preserved `EquippedSlots`) while keeping `CurrentRelics` and `GetRelicsCopy()` backwards-compatible with `RelicCombatTriggerSystem`.
  - When `AddRelic()` is called while all 5 slots are full, it stores `PendingRelic` and raises `RelicSlotsFullRequested` / `AnyRelicSlotsFullRequested`.
  - Added `GetRelicAt`, `EquipAtSlot`, `UnequipAtSlot`, `SwapSlots`, atomic `ApplySlotLoadout` (safely supports reordering `Unique` relics), `SetPendingRelic`, and `ClearPendingRelic`.
- **Top-Middle 5-Slot Relic Template Bar** ([`TopBarRelicSlotsUI.cs`](../Assets/_Project/Scripts/UI/Relics/TopBarRelicSlotsUI.cs), [`Game.unity`](../Assets/_Project/Scenes/Game.unity)):
  - Placed the **5 light-grey template squares** dead-center inside `GameCanvas/CombatTopBar/CenterTray` directly over the sun medallion (matching the Figma mockup).
  - Displays each relic's sprite automatically (or a rarity-bordered monogram badge when `RelicData.Image` is not yet assigned).
  - Supports **hover tooltips**, **direct drag-and-drop reordering** across the 5 slots, and **clicking any slot** to open the management modal.
  - Relocated `PotionBeltUI` between the Relic slots and Crystal readout with `showEmptyCapacitySlots = false` and removed the redundant legacy `CombatTopBar/RelicListUI`.
- **Full-Slots Drag-and-Drop Equip / Unequip / Swap Modal** ([`RelicSwapModalUI.cs`](../Assets/_Project/Scripts/UI/Relics/RelicSwapModalUI.cs)):
  - Auto-opens whenever a new relic is acquired while all 5 slots are occupied.
  - Lets players stage changes freely before committing:
    - Drag any equipped relic → `UNEQUIP ZONE` to open a slot, then drag `NEW RELIC` → the open slot.
    - Drag `NEW RELIC` directly onto an occupied slot to swap cleanly.
    - Drag between equipped slots to reorder.
  - Includes live hover details, drag ghost visual, Tune SFX (`card_pickup`, `relic_claim`, `card_discard`, `ui_cancel`), and **Confirm Loadout** / **Skip New Relic** (`Esc`) buttons.

---

## 7. Enemy Intent Icon Templates, Hover Tooltips, Turn Timing & Procedural Animations

- **10 Replaceable Template Icon Sprites** (`Assets/_Project/Art/UI/Intents/`):
  - Generated crisp white-on-transparent `128×128` Sprite templates (`Intent_Attack`, `Intent_AttackDebuff`, `Intent_Defend`, `Intent_Buff`, `Intent_Debuff`, `Intent_WillDrain`, `Intent_Heal`, `Intent_Summon`, `Intent_Stunned`, `Intent_Unknown`) wired to serialized slots on `EnemyIntentHUD` so custom art can be dropped in anytime.
- **Structured Intent Classification & Color-Coded Top Labels** ([`EnemyIntentSnapshot.cs`](../Assets/_Project/Scripts/Models/EnemyIntentSnapshot.cs)):
  - Classifies all enemy effects (including `SimultaneousEffect`, `ApplyDoTEffect`, `SummonEnemyEffect`, `ApplyBuffEffect`, `AddStressEffect`, `GainShieldEffect`, `HealEffect`, `ApplyStunEffect`, `ApplyTauntEffect`, and live buff-adjusted attacks) into color-coded action categories with top labels (`ATTACK`, `ATTACK + WILL`, `DEFEND`, `BUFF`, `AFFLICT`, `DEBUFF`, `WILL DRAIN`, `HEAL`, `SUMMON`, `STUNNED`), bold values, optional secondary chips (`-5 WILL`), and rich natural-language descriptions.
- **Icon HUD, Procedural Animations & Hover Description Tooltip** ([`EnemyIntentHUD.cs`](../Assets/_Project/Scripts/UI/Combat/EnemyIntentHUD.cs), [`Game.unity`](../Assets/_Project/Scenes/Game.unity)):
  - Displays each enemy's color-coded top action pill, framed template icon, bold number readout, and secondary chip cleanly above the enemy's sprite bounds (clamped safely below `CombatTopBar`).
  - Procedural DOTween animations: squash-and-stretch upward pop-in with radial burst flash (staggered left-to-right across enemies), category-aware alive idle float/pulse/sway, value punch on live buff updates, and upward execute-consume burst when an enemy acts.
  - Non-blocking cursor hover detection displays an action-colored tooltip panel detailing what the enemy intends to do (automatically suppressed during card drags/targeting or open modals).
- **Deferred Player-Turn Intent Reveal** ([`EnemySystem.cs`](../Assets/_Project/Scripts/Systems/EnemySystem.cs), [`EnemyView.cs`](../Assets/_Project/Scripts/Views/EnemyView.cs)):
  - Removed immediate `PickNextIntent()` from `ExecuteIntentPerformer`; enemies now consume and hide their intent upon acting (including stunned skips and mid-turn summons) and wait until `EnemyTurnGA.POST` (Player Turn start) to pick and pop in their next turn's intents.

---

## 8. File Change Inventory

### New Scripts
- [`Assets/_Project/Scripts/Models/EnemyIntentSnapshot.cs`](../Assets/_Project/Scripts/Models/EnemyIntentSnapshot.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatFeedbackBannerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatFeedbackBannerUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatPileViewerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatPileViewerUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatTopBarUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatTopBarUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/EnemyIntentHUD.cs`](../Assets/_Project/Scripts/UI/Combat/EnemyIntentHUD.cs)
- [`Assets/_Project/Scripts/UI/Relics/TopBarRelicSlotsUI.cs`](../Assets/_Project/Scripts/UI/Relics/TopBarRelicSlotsUI.cs)
- [`Assets/_Project/Scripts/UI/Relics/RelicSwapModalUI.cs`](../Assets/_Project/Scripts/UI/Relics/RelicSwapModalUI.cs)
- [`Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs`](../Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs)

### New Art Templates
- `Assets/_Project/Art/UI/Intents/Intent_*.png` (10 replaceable Sprite templates)

### Modified Scenes, Prefabs & Settings
- [`Assets/_Project/Scenes/Game.unity`](../Assets/_Project/Scenes/Game.unity)
- [`Assets/_Project/Prefabs/CardViews/Card View.prefab`](../Assets/_Project/Prefabs/CardViews/Card%20View.prefab)
- [`Assets/_Project/Prefabs/Enemy View.prefab`](../Assets/_Project/Prefabs/Enemy%20View.prefab)
- [`Assets/_Project/Prefabs/UI/RelicPotion/PotionBeltUI.prefab`](../Assets/_Project/Prefabs/UI/RelicPotion/PotionBeltUI.prefab)
- [`Assets/_Project/Prefabs/UI/RelicPotion/PotionIconView.prefab`](../Assets/_Project/Prefabs/UI/RelicPotion/PotionIconView.prefab)
- [`ProjectSettings/EditorBuildSettings.asset`](../ProjectSettings/EditorBuildSettings.asset)

### Modified Scripts & Docs
- **Run & Core**: `RunHeroStressState.cs`, `RunManager.cs`, `RunRelicManager.cs`, `GameManager.cs`, `MouseUtils.cs`
- **Systems**: `ActionSystem.cs`, `BuffSystem.cs`, `CardPlayService.cs`, `CardSystem.cs`, `CardTargetResolver.cs`, `DamageOverTimeSystem.cs`, `DamageSystem.cs`, `EffectSystem.cs`, `HeroSystem.cs`, `ManaSystem.cs`, `ShieldSystem.cs`, `StunSystem.cs`
- **Data, Models, Effects & Actions**: `CardData.cs`, `Card.cs`, `BuffData.cs`, `BuffType.cs`, `DealDamageGA.cs`, `PerformEffectGA.cs`, `AddStressEffect.cs`, `ApplyBuffEffect.cs`, `ApplyStatusEffect.cs`, `BeamAttackEffect.cs`, `ConditionalDamageEffect.cs`, `DealDamageEffect.cs`, `GainShieldEffect.cs`, `HealEffect.cs`
- **Views & UI**: `ArrowView.cs`, `CardVisualFeedback.cs`, `CombatHandController.cs`, `CombatantView.cs`, `EnemyView.cs`, `HandView.cs`, `HeroView.cs`, `CombatantStatusEffectsUI.cs`
- **Docs**: [`ProjectDocs/TODO.md`](TODO.md), [`ProjectDocs/CHANGELOG_2026-11-19.md`](CHANGELOG_2026-11-19.md)

---

## 9. Verification Summary

Every milestone above was compiled (`0` errors) and verified live in Unity Play Mode inside `Assets/_Project/Scenes/Game.unity` with **`0` console errors and `0` runtime warnings**, including visual inspection of captured `1920×1080` Game View frames:
- Full combat turn loop (draw, hover pop-up & neighbor spread, single-target reticle + lethal preview, AoE targeting, card play, pile modals, End Turn, enemy intent execution).
- Will system transitions across **Neutral (50%)**, **High Will (85%, `+25%` ATK/DEF + `WL+`)**, and **Low Will (15%, Fear HP cap penalty + `FEAR`)**.
- Top-Middle 5-slot Relic bar (empty Figma template squares → 5/5 equipped relics → 6th relic auto-opening `RelicSwapModalUI` → uGUI drag-and-drop unequip, equip, swap, Confirm Loadout, and direct Top Bar slot reordering).
- Enemy Intent Icon HUD across Turn 1 (`ATTACK + WILL`), cursor hover tooltips (`Attack & Will Drain` and `Affliction`), mid-Enemy-Turn consumed/hidden state (confirming no premature intent roll), and Turn 2 Player-Turn staggered pop-in (`DEFEND +3`, `AFFLICT 8/t`, `BUFF +5`).

