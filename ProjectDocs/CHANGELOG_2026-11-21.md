# Development Summary — 2026-11-21

[Project README](../README.md) · [Implementation To-Do List](TODO.md) · [Recommendations Backlog](TODO_RECOMMENDATIONS.md) · [Previous Summary (2026-11-20)](CHANGELOG_2026-11-20.md) · [Code Documentation Index](code/README.md)

Complete log of all gameplay expansion pillars implemented and wired to existing project assets on **2026-11-21** in `Slay-the-Spire-Clone` (Unity `6000.0.73f1`, URP `17.1.0`), covering True Persistent Power Cards, Live Dynamic Card Text + End-Turn Smart Glow + Collapsible Combat Log, Multi-Hit & Will Archetype & Status/Curse Cards, Mid-Combat Card Pickers, Conditional Enemy AI & Visible Enemy Passive Traits, Boss 3-Relic Choice Chest + Boss Trade-Offs + Merchant 50%-OFF Sale Tags, and Unity Console compilation fixes.

---

## Table of Contents

- [Development Summary — 2026-11-21](#development-summary--2026-11-21)
  - [Table of Contents](#table-of-contents)
  - [1. True Persistent Power Cards \& Active Power HUD](#1-true-persistent-power-cards--active-power-hud)
  - [2. Dynamic Live-Adjusted Hand Text, End-Turn Smart Glow \& Collapsible Combat Log](#2-dynamic-live-adjusted-hand-text-end-turn-smart-glow--collapsible-combat-log)
  - [3. Multi-Hit Attacks, Status/Curse Clutter, Will Archetypes \& Tactical Scaling](#3-multi-hit-attacks-statuscurse-clutter-will-archetypes--tactical-scaling)
  - [4. Mid-Combat Card Pickers (Scry, Discover, Discard/Exhaust \& Upgrade)](#4-mid-combat-card-pickers-scry-discover-discardexhaust--upgrade)
  - [5. Conditional Enemy AI \& Visible Enemy Passive Traits](#5-conditional-enemy-ai--visible-enemy-passive-traits)
  - [6. Boss 3-Relic Choice Chest, Boss Trade-Offs \& Merchant 50%-OFF Sale Tag](#6-boss-3-relic-choice-chest-boss-trade-offs--merchant-50-off-sale-tag)
  - [7. Console Compilation Fixes \& Existing Project Asset Wiring](#7-console-compilation-fixes--existing-project-asset-wiring)
  - [8. File Change Inventory](#8-file-change-inventory)
    - [New Scripts](#new-scripts)
    - [Modified Scripts](#modified-scripts)
    - [Updated ScriptableObjects \& Scenes](#updated-scriptableobjects--scenes)
  - [9. Edit-Mode \& Play-Mode Verification Summary](#9-edit-mode--play-mode-verification-summary)

---

## 1. True Persistent Power Cards & Active Power HUD

- **Design:** Playing a `CardType.Power` card removes it permanently from the combat deck cycle (entering neither Discard nor Exhaust), registers its passive rules for the rest of battle, displays hoverable Power badges on the hero HUD strip, and fires reactions on matching combat triggers.
- **Changes:**
  - [`CardData.cs`](../Assets/_Project/Scripts/Data/CardData.cs) & [`Card.cs`](../Assets/_Project/Scripts/Models/Card.cs): Added `PowerPassiveTrigger` (`TurnStart`, `TurnEnd`, `OnAttackCardPlayed`, `OnBuffCardPlayed`, `OnAnyCardPlayed`, `OnEnterHighWill`, `OnEnterLowWill`), `PowerPassiveDefinition`, `PersistPowerInCombat`, `PowerPassives`, `OwnerCharacterId`, and `Card.IsPersistentPower`.
  - [`PowerSystem.cs`](../Assets/_Project/Scripts/Systems/PowerSystem.cs) (auto-attached in [`ActionSystem.cs`](../Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs)): Registers played Powers, stacks duplicate passive IDs cleanly (`×N`), executes trigger effects (`TurnStart`, `TurnEnd`, `OnAttackCardPlayed`, `OnBuffCardPlayed`, `OnAnyCardPlayed`, `OnEnterHighWill`, `OnEnterLowWill`), clears on `CombatWonGA`, and exposes `CollectPowerBadges()`.
  - [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs): Skips adding `card.IsPersistentPower` cards to `discardPile` or `exhaustPile` in `LaunchPlayExitAnimation`, gives them an upward dissipation trajectory, and invokes `PowerSystem.Instance?.RegisterPowerCard(card)` inside `PlayCardPerformer`.
  - [`CombatStatusBadgeData.cs`](../Assets/_Project/Scripts/Models/CombatStatusBadgeData.cs) & [`CombatTopBarUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatTopBarUI.cs): Added `CreatePowerBadge(...)` and subscribed `CombatTopBarUI` to `PowerSystem.PowersChanged` so active Power badges appear with hover tooltips in the top bar.
- **Existing Assets Wired:**
  - [`Power_AegisRhythm.asset`](../Assets/_Project/ScriptableObjects/Cards/Expanded/Power_AegisRhythm.asset) (*"Whenever you play an Attack card, gain 3 Shield."*)
  - [`Power_AstralMastery.asset`](../Assets/_Project/ScriptableObjects/Cards/Expanded/Power_AstralMastery.asset) (*"At the start of each turn, gain +1 Strength for the rest of combat."*)

---

## 2. Dynamic Live-Adjusted Hand Text, End-Turn Smart Glow & Collapsible Combat Log

- **Dynamic Hand Card Numbers:**
  - [`CardDynamicDescriptionFormatter.cs`](../Assets/_Project/Scripts/Views/CardDynamicDescriptionFormatter.cs): Computes live damage, shield, combo scaling, in-combat upgrade bonuses, and `[X = N]` / `X times` previews against `BuffSystem` (`Strength`, `Dexterity`, `Weak`, `Frail`), `HeroView.IsHighWill`, and `ManaSystem.AvailableMana` — tinting boosted numbers green (`#59FF8A`) and weakened numbers red (`#FF5C5C`).
  - [`CardView.cs`](../Assets/_Project/Scripts/Views/CardView.cs) & [`CombatHandController.cs`](../Assets/_Project/Scripts/Views/CombatHandController.cs): Added `RefreshFromCard()`, `SetLiveDescription(...)`, cost reduction/increase color tinting (`SetDisplayedCost`), and automatic refreshes on `BuffSystem.StatusEffectsChanged`, `HeroView.StressStateChanged`, `CardSystem.CardsPlayedThisTurnChanged`, and `PowerSystem.PowersChanged`.
- **End-Turn Smart Glow:**
  - [`EndTurnButtonUI.cs`](../Assets/_Project/Scripts/UI/EndTurnButtonUI.cs): Pulses a `SmartGlowHalo` (reusing existing `Merchant_SoftGlow` sprite with a soft radial fallback) whenever `!CardSystem.Instance.HasPlayableCards()` during an unlocked player turn.
- **Collapsible Combat Log Drawer:**
  - [`CombatLogUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatLogUI.cs) (auto-attached in `ActionSystem.Awake()`): Right-edge collapsible drawer styled with existing `Panel.png` and `button1 .png`/`Tombol.png` sprites, logging turn boundaries, card plays (with X mana spent), damage/shield/heals, buffs/debuffs, Power triggers, enemy passives, Scry/Discover/Upgrade actions, and injected cards.
  - [`CombatKeywordTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs): Added hover keyword definitions for `Power`, `Scry`, `Discover`, `Upgrade`, `Status`, and `Curse`.

---

## 3. Multi-Hit Attacks, Status/Curse Clutter, Will Archetypes & Tactical Scaling

- **New Card `Effect` & `GameAction` Building Blocks:**
  - [`MultiHitDamageEffect.cs`](../Assets/_Project/Scripts/Effects/MultiHitDamageEffect.cs): Emits sequential `DealDamageGA` instances for `hitCount` (or `hitCount * xMultiplier` when `scaleHitsWithX` is enabled) so `Strength`, `Weak`, `Vulnerable`, `Thorns`/`Spiky`, and `ModeShift` apply to every individual hit; supported in live targeting/intent previews via [`CombatTargetingPresenter.cs`](../Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs) and [`EnemyIntentSnapshot.cs`](../Assets/_Project/Scripts/Models/EnemyIntentSnapshot.cs).
  - [`ComboDamageEffect.cs`](../Assets/_Project/Scripts/Effects/ComboDamageEffect.cs): Scales base damage with `CardsPlayedThisTurn`, `TurnsRetained`, `AttacksPlayedThisTurn`, or `HandSize` tracked on [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs) and `Card.TurnsRetained`.
  - [`SpendWillEffect.cs`](../Assets/_Project/Scripts/Effects/SpendWillEffect.cs) + [`ModifyStressGA.cs`](../Assets/_Project/Scripts/Game%20Actions/ModifyStressGA.cs) (`ignoreImmunity: true`) + [`HeroSystem.cs`](../Assets/_Project/Scripts/Systems/HeroSystem.cs): Enables Will-Spender burst cards that intentionally sacrifice Will even through Will Immunity.
  - [`CardPlayService.cs`](../Assets/_Project/Scripts/Systems/CardPlayService.cs) & [`CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs): Respects `CardKeywords.PlayableInLowWill` so redemption cards can still be played during `- Low Will`, while `CardKeywords.Unplayable`, `CardKeywords.Status`, and `CardKeywords.Curse` are enforced across hand playability checks and Discover pools.
  - [`HandSizeDamageEffect.cs`](../Assets/_Project/Scripts/Effects/HandSizeDamageEffect.cs), [`InjectCardEffect.cs`](../Assets/_Project/Scripts/Effects/InjectCardEffect.cs), [`ReduceHandCardCostEffect.cs`](../Assets/_Project/Scripts/Effects/ReduceHandCardCostEffect.cs), [`InjectCardGA.cs`](../Assets/_Project/Scripts/Game%20Actions/InjectCardGA.cs), and [`ReduceHandCardCostGA.cs`](../Assets/_Project/Scripts/Game%20Actions/ReduceHandCardCostGA.cs): Implements Status/Curse card injection, `OnDrawEffects` (`Status_Void` `-1 Mana`), `EndTurnInHandEffects` (`Status_Burn`, `Curse_Doubt`, `Curse_Regret`), and turn-scoped `TurnManaModifier` cost reductions reset at end of turn in `CardSystem`.
- **Existing Assets Wired:**
  - [`Attack_FlurryStrikes.asset`](../Assets/_Project/ScriptableObjects/Cards/Expanded/Attack_FlurryStrikes.asset), [`Attack_WhirlwindNova.asset`](../Assets/_Project/ScriptableObjects/Cards/Expanded/Attack_WhirlwindNova.asset), [`Attack_MomentumFinisher.asset`](../Assets/_Project/ScriptableObjects/Cards/Expanded/Attack_MomentumFinisher.asset), [`Will_OverdriveBurst.asset`](../Assets/_Project/ScriptableObjects/Cards/Expanded/Will_OverdriveBurst.asset), [`Will_DefiantSpark.asset`](../Assets/_Project/ScriptableObjects/Cards/Expanded/Will_DefiantSpark.asset)
  - [`Status_Burn.asset`](../Assets/_Project/ScriptableObjects/Cards/StatusAndCurses/Status_Burn.asset), [`Status_Wound.asset`](../Assets/_Project/ScriptableObjects/Cards/StatusAndCurses/Status_Wound.asset), [`Status_Dazed.asset`](../Assets/_Project/ScriptableObjects/Cards/StatusAndCurses/Status_Dazed.asset), [`Status_Void.asset`](../Assets/_Project/ScriptableObjects/Cards/StatusAndCurses/Status_Void.asset), [`Curse_Doubt.asset`](../Assets/_Project/ScriptableObjects/Cards/StatusAndCurses/Curse_Doubt.asset), [`Curse_Regret.asset`](../Assets/_Project/ScriptableObjects/Cards/StatusAndCurses/Curse_Regret.asset)

---

## 4. Mid-Combat Card Pickers (Scry, Discover, Discard/Exhaust & Upgrade)

- **Modal & Performers:**
  - [`CombatCardPickerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatCardPickerUI.cs) (auto-attached in `ActionSystem.Awake()`): Uses existing `CardViewCreator` cards and `Panel.png` / `button1 .png` UI chrome to present:
    - **Scry:** Inspect top `N` cards of the draw pile and toggle any subset to send to the discard pile.
    - **Discover:** Choose `1` of `3` candidate cards to add to hand (optionally costing `0` Mana this turn).
    - **Hand Choice:** Select cards in hand to **Discard**, **Exhaust**, or **Upgrade** for the remainder of combat (`Card.UpgradeInCombat()` granting `+3` potency via [`EffectSystem.cs`](../Assets/_Project/Scripts/Systems/EffectSystem.cs) & `-1` Mana cost or `+1 X`).
  - Added [`ScryGA.cs`](../Assets/_Project/Scripts/Game%20Actions/ScryGA.cs), [`DiscoverCardGA.cs`](../Assets/_Project/Scripts/Game%20Actions/DiscoverCardGA.cs), [`HandChoiceGA.cs`](../Assets/_Project/Scripts/Game%20Actions/HandChoiceGA.cs), [`ScryEffect.cs`](../Assets/_Project/Scripts/Effects/ScryEffect.cs), [`DiscoverCardEffect.cs`](../Assets/_Project/Scripts/Effects/DiscoverCardEffect.cs), [`HandChoiceEffect.cs`](../Assets/_Project/Scripts/Effects/HandChoiceEffect.cs), and blocking guards (`CombatCardPickerUI.IsAnyOpen`) across `CombatHandController`, `CardPlayService`, `EndTurnButtonUI`, `CombatTopBarUI`, and `EnemyIntentHUD`.
- **Existing Assets Wired:**
  - [`Utility_ForesightScry.asset`](../Assets/_Project/ScriptableObjects/Cards/Expanded/Utility_ForesightScry.asset) (also fixed `DrawEffect.amount` from `0` to `1` to match card text), [`Utility_ArcaneDiscovery.asset`](../Assets/_Project/ScriptableObjects/Cards/Expanded/Utility_ArcaneDiscovery.asset), and [`Utility_TacticalForge.asset`](../Assets/_Project/ScriptableObjects/Cards/Expanded/Utility_TacticalForge.asset).

---

## 5. Conditional Enemy AI & Visible Enemy Passive Traits

- **Conditional Weighted AI:**
  - [`EnemyIntent.cs`](../Assets/_Project/Scripts/Models/EnemyIntent.cs) & [`EnemyView.cs`](../Assets/_Project/Scripts/Views/EnemyView.cs): Added per-intent AI constraints (`Weight`, `TurnOneOpener`, `MinHpPercent`, `MaxHpPercent`, `CooldownTurns`, `MaxConsecutiveUses`) evaluated by `PickConditionalWeightedIndex()` when `AttackPattern == AIAttackPattern.ConditionalWeighted`.
- **Visible Enemy Passive Traits:**
  - [`EnemyData.cs`](../Assets/_Project/Scripts/Data/EnemyData.cs), [`EnemyPassiveSystem.cs`](../Assets/_Project/Scripts/Systems/EnemyPassiveSystem.cs) (auto-attached in `ActionSystem.Awake()`), [`CombatStatusBadgeData.cs`](../Assets/_Project/Scripts/Models/CombatStatusBadgeData.cs), and [`EnemyIntentHUD.cs`](../Assets/_Project/Scripts/UI/Combat/EnemyIntentHUD.cs):
    - Supports `Spiky` (Thorns damage reflection), `Regenerate` (end-of-turn heal), `Ritual` (end-of-turn `+Strength`), `Enrage` (`+Strength` when player plays Buff/Skill cards), `TimeWarp` (card-play counter ending player turn + `+Strength`), `ModeShift` (damage threshold granting Shield and scaling threshold), and `WillEaterAura` (start-of-turn Will drain).
    - Renders live hoverable passive badges directly above enemies in `EnemyIntentHUD` (`EnemyPassivesChanged` updates stacks such as `ModeShift` remaining damage or `TimeWarp` `X/Y` live).
- **Existing Enemy Assets Configured:**
  - [`Guard.asset`](../Assets/_Project/ScriptableObjects/Enemies/Guard.asset) (`Spiky 2`), [`King Lizard.asset`](../Assets/_Project/ScriptableObjects/Enemies/King%20Lizard.asset) (`ModeShift 28 / +16 Shield`, `Ritual 1`), [`The Last Paradise.asset`](../Assets/_Project/ScriptableObjects/Enemies/The%20Last%20Paradise.asset) (`ModeShift 28 / +16 Shield`, `Ritual 1`).

---

## 6. Boss 3-Relic Choice Chest, Boss Trade-Offs & Merchant 50%-OFF Sale Tag

- **Boss 3-Relic Choice Chest & Relic Trade-Offs:**
  - [`RelicData.cs`](../Assets/_Project/Scripts/Data/RelicData.cs): Added passive bonus & trade-off fields `BonusMaxMana`, `StartCombatWillDelta`, `DisableRestSiteHeal`, `MerchantDiscountPercent`, and `BonusOpeningDraw`.
  - [`RelicReactionDefinition.cs`](../Assets/_Project/Scripts/Relics/RelicReactionDefinition.cs) & [`RelicCombatTriggerSystem.cs`](../Assets/_Project/Scripts/Relics/RelicCombatTriggerSystem.cs): Added `EnteredHighWill` and `RecoveredFromLowWill` triggers driving [`Relic_WillResonator.asset`](../Assets/_Project/ScriptableObjects/Relics/Relic_WillResonator.asset).
  - [`BossRelicPickerUI.cs`](../Assets/_Project/Scripts/Rewards/BossRelicPickerUI.cs) & [`RunRewardController.cs`](../Assets/_Project/Scripts/Rewards/RunRewardController.cs): Automatically grants `RunRewardType.BossRelicChest` after `MapNodeType.Boss` encounters, presenting a 3-choice Boss Relic chest styled with existing `Panel.png`, `button1 .png`, and `Merchant_Circle.png` assets, highlighting `"Trade-off:"` text and integrating with `RelicSwapModalUI` if 5 slots are occupied. Wired `bossRelicChoices` onto [`RunRewards.unity`](../Assets/_Project/Scenes/RunRewards.unity).
  - [`MatchSetupSystem.cs`](../Assets/_Project/Scripts/Systems/MatchSetupSystem.cs) & [`RestSiteUI.cs`](../Assets/_Project/Scripts/Map/Rest/RestSiteUI.cs): Enforces `BonusMaxMana`, `StartCombatWillDelta`, `BonusOpeningDraw` at battle start and blocks Campfire Rest healing when `DisableRestSiteHeal` is active.
- **Merchant 50%-OFF Sale Tag & Relic Discounts:**
  - [`MerchantCardPool.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantCardPool.cs) & [`MerchantShopUI.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs): Tracks `OriginalPrice` and `IsOnSale`, applies equipped relic discounts (`MerchantDiscountPercent`), discounts `1` random card offer by `50%` per shop visit/reset, shows strikethrough original pricing in emerald green when affordable, and attaches a rotated red-and-gold `50% OFF` corner ribbon (`SaleRibbonTag`).
- **Existing Relic Assets Wired:**
  - [`BossRelic_AstralPrism.asset`](../Assets/_Project/ScriptableObjects/Relics/Boss/BossRelic_AstralPrism.asset) (*"+1 Opening Draw, 25% OFF Merchant wares"*)
  - [`BossRelic_CursedCrown.asset`](../Assets/_Project/ScriptableObjects/Relics/Boss/BossRelic_CursedCrown.asset) (*"+1 Max Mana/turn; Trade-off: cannot Rest (Heal) at Campfires"*)
  - [`BossRelic_SearingLantern.asset`](../Assets/_Project/ScriptableObjects/Relics/Boss/BossRelic_SearingLantern.asset) (*"+1 Max Mana/turn; Trade-off: start every combat with -15 Will"*)
  - [`Relic_WillResonator.asset`](../Assets/_Project/ScriptableObjects/Relics/Relic_WillResonator.asset)

---

## 7. Console Compilation Fixes & Existing Project Asset Wiring

- **Unity Console Compilation Fixes (prior to & alongside feature implementation):**
  - [`MapSystem.cs`](../Assets/_Project/Scripts/Map/MapSystem.cs#L254): Restored missing `private void ResolveMapTitleText()` method signature that caused 21 cascading C# syntax errors.
  - [`RunSaveSystem.cs`](../Assets/_Project/Scripts/Run/RunSaveSystem.cs): Updated `CaptureCurrentRunData()` and `TryRestoreSavedRun()` to align with `RunManager.cs` (`AdvanceToNextChapterMap` / `SelectMapNode`).
  - Removed duplicate/stale leftover symbols across `EnemyView.cs`, `RestSiteUI.cs`, `CardSystem.cs`, `CombatTargetingPresenter.cs`, and `MerchantShopUI.cs` so compilation succeeded with `0` errors.
- **Existing Art Sprites Assigned to All 20 Assets:**
  - **7 Ember-aligned Expanded Cards** → Ember portrait sprite (`guid: 28ea9aba4895f93488dbba35627d3515`, `fileID: 1597602930`).
  - **3 Aira-aligned Expanded Cards** → Aira portrait sprite (`guid: 56ce11f854e5896488c9b008fe7faaf5`, `fileID: 2054458977`).
  - **6 Status/Curse Cards** → Existing UI status & intent sprites (`Status_Burn.png`, `Status_Vulnerable.png`, `Intent_Stunned.png`, `Intent_WillDrain.png`, `Status_Weak.png`).
  - **4 Relics** → Existing status/intent/relic sprites (`Status_Burn.png`, `BossRelic_AstralPrism` / Starlight Amulet sprite `guid: 8a0f6752e06404547a37d7bfcf21dcb2`, `BossRelic_CursedCrown` / Iron Charm sprite `guid: dfc77bc2e5f47014d96bfad1582b6653`, `Intent_WillDrain.png`).

---

## 8. File Change Inventory

### New Scripts

- [`Assets/_Project/Scripts/Systems/PowerSystem.cs`](../Assets/_Project/Scripts/Systems/PowerSystem.cs)
- [`Assets/_Project/Scripts/Systems/EnemyPassiveSystem.cs`](../Assets/_Project/Scripts/Systems/EnemyPassiveSystem.cs)
- [`Assets/_Project/Scripts/Views/CardDynamicDescriptionFormatter.cs`](../Assets/_Project/Scripts/Views/CardDynamicDescriptionFormatter.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatLogUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatLogUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatCardPickerUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatCardPickerUI.cs)
- [`Assets/_Project/Scripts/Rewards/BossRelicPickerUI.cs`](../Assets/_Project/Scripts/Rewards/BossRelicPickerUI.cs)
- [`Assets/_Project/Scripts/Effects/MultiHitDamageEffect.cs`](../Assets/_Project/Scripts/Effects/MultiHitDamageEffect.cs)
- [`Assets/_Project/Scripts/Effects/ComboDamageEffect.cs`](../Assets/_Project/Scripts/Effects/ComboDamageEffect.cs)
- [`Assets/_Project/Scripts/Effects/SpendWillEffect.cs`](../Assets/_Project/Scripts/Effects/SpendWillEffect.cs)
- [`Assets/_Project/Scripts/Effects/HandSizeDamageEffect.cs`](../Assets/_Project/Scripts/Effects/HandSizeDamageEffect.cs)
- [`Assets/_Project/Scripts/Effects/ScryEffect.cs`](../Assets/_Project/Scripts/Effects/ScryEffect.cs)
- [`Assets/_Project/Scripts/Effects/DiscoverCardEffect.cs`](../Assets/_Project/Scripts/Effects/DiscoverCardEffect.cs)
- [`Assets/_Project/Scripts/Effects/HandChoiceEffect.cs`](../Assets/_Project/Scripts/Effects/HandChoiceEffect.cs)
- [`Assets/_Project/Scripts/Effects/InjectCardEffect.cs`](../Assets/_Project/Scripts/Effects/InjectCardEffect.cs)
- [`Assets/_Project/Scripts/Effects/ReduceHandCardCostEffect.cs`](../Assets/_Project/Scripts/Effects/ReduceHandCardCostEffect.cs)
- [`Assets/_Project/Scripts/Game Actions/ScryGA.cs`](../Assets/_Project/Scripts/Game%20Actions/ScryGA.cs)
- [`Assets/_Project/Scripts/Game Actions/DiscoverCardGA.cs`](../Assets/_Project/Scripts/Game%20Actions/DiscoverCardGA.cs)
- [`Assets/_Project/Scripts/Game Actions/HandChoiceGA.cs`](../Assets/_Project/Scripts/Game%20Actions/HandChoiceGA.cs)
- [`Assets/_Project/Scripts/Game Actions/InjectCardGA.cs`](../Assets/_Project/Scripts/Game%20Actions/InjectCardGA.cs)
- [`Assets/_Project/Scripts/Game Actions/ReduceHandCardCostGA.cs`](../Assets/_Project/Scripts/Game%20Actions/ReduceHandCardCostGA.cs)

### Modified Scripts

- [`Assets/_Project/Scripts/Data/CardData.cs`](../Assets/_Project/Scripts/Data/CardData.cs)
- [`Assets/_Project/Scripts/Data/EnemyData.cs`](../Assets/_Project/Scripts/Data/EnemyData.cs)
- [`Assets/_Project/Scripts/Data/RelicData.cs`](../Assets/_Project/Scripts/Data/RelicData.cs)
- [`Assets/_Project/Scripts/Models/Card.cs`](../Assets/_Project/Scripts/Models/Card.cs)
- [`Assets/_Project/Scripts/Models/CombatStatusBadgeData.cs`](../Assets/_Project/Scripts/Models/CombatStatusBadgeData.cs)
- [`Assets/_Project/Scripts/Models/EnemyIntent.cs`](../Assets/_Project/Scripts/Models/EnemyIntent.cs)
- [`Assets/_Project/Scripts/Models/EnemyIntentSnapshot.cs`](../Assets/_Project/Scripts/Models/EnemyIntentSnapshot.cs)
- [`Assets/_Project/Scripts/Game Actions/ModifyStressGA.cs`](../Assets/_Project/Scripts/Game%20Actions/ModifyStressGA.cs)
- [`Assets/_Project/Scripts/Game Actions/PerformEffectGA.cs`](../Assets/_Project/Scripts/Game%20Actions/PerformEffectGA.cs)
- [`Assets/_Project/Scripts/Systems/Action System/ActionSystem.cs`](../Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs)
- [`Assets/_Project/Scripts/Systems/CardPlayService.cs`](../Assets/_Project/Scripts/Systems/CardPlayService.cs)
- [`Assets/_Project/Scripts/Systems/CardSystem.cs`](../Assets/_Project/Scripts/Systems/CardSystem.cs)
- [`Assets/_Project/Scripts/Systems/EffectSystem.cs`](../Assets/_Project/Scripts/Systems/EffectSystem.cs)
- [`Assets/_Project/Scripts/Systems/HeroSystem.cs`](../Assets/_Project/Scripts/Systems/HeroSystem.cs)
- [`Assets/_Project/Scripts/Systems/MatchSetupSystem.cs`](../Assets/_Project/Scripts/Systems/MatchSetupSystem.cs)
- [`Assets/_Project/Scripts/Relics/RelicReactionDefinition.cs`](../Assets/_Project/Scripts/Relics/RelicReactionDefinition.cs)
- [`Assets/_Project/Scripts/Relics/RelicCombatTriggerSystem.cs`](../Assets/_Project/Scripts/Relics/RelicCombatTriggerSystem.cs)
- [`Assets/_Project/Scripts/Views/CardView.cs`](../Assets/_Project/Scripts/Views/CardView.cs)
- [`Assets/_Project/Scripts/Views/CombatHandController.cs`](../Assets/_Project/Scripts/Views/CombatHandController.cs)
- [`Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs`](../Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs)
- [`Assets/_Project/Scripts/Views/EnemyView.cs`](../Assets/_Project/Scripts/Views/EnemyView.cs)
- [`Assets/_Project/Scripts/UI/EndTurnButtonUI.cs`](../Assets/_Project/Scripts/UI/EndTurnButtonUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/CombatTopBarUI.cs`](../Assets/_Project/Scripts/UI/Combat/CombatTopBarUI.cs)
- [`Assets/_Project/Scripts/UI/Combat/EnemyIntentHUD.cs`](../Assets/_Project/Scripts/UI/Combat/EnemyIntentHUD.cs)
- [`Assets/_Project/Scripts/Map/MapSystem.cs`](../Assets/_Project/Scripts/Map/MapSystem.cs)
- [`Assets/_Project/Scripts/Map/Merchant/MerchantCardPool.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantCardPool.cs)
- [`Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs`](../Assets/_Project/Scripts/Map/Merchant/MerchantShopUI.cs)
- [`Assets/_Project/Scripts/Map/Rest/RestSiteUI.cs`](../Assets/_Project/Scripts/Map/Rest/RestSiteUI.cs)
- [`Assets/_Project/Scripts/Rewards/RunRewardController.cs`](../Assets/_Project/Scripts/Rewards/RunRewardController.cs)
- [`Assets/_Project/Scripts/Run/RunSaveSystem.cs`](../Assets/_Project/Scripts/Run/RunSaveSystem.cs)

### Updated ScriptableObjects & Scenes

- `Assets/_Project/ScriptableObjects/Cards/Expanded/*.asset` (10 assets — sprites wired + `Utility_ForesightScry.asset` draw count fixed)
- `Assets/_Project/ScriptableObjects/Cards/StatusAndCurses/*.asset` (6 assets — sprites wired)
- `Assets/_Project/ScriptableObjects/Relics/Boss/*.asset` & `Relic_WillResonator.asset` (4 assets — sprites wired)
- `Assets/_Project/Scenes/RunRewards.unity` (wired `bossRelicChoices` with all 3 Boss Relics on `RunRewardController`)

---

## 9. Edit-Mode & Play-Mode Verification Summary

- **Edit-Mode Deserialization & Mechanics Check (`verify_all_features.cs`):** **`55/55` passed, `0` failed** — verified all 16 Expanded/Status/Curse card assets, all 4 Boss/Will-Resonator relic assets, all 3 passive-enabled enemy assets, non-null sprite bindings, `Card.UpgradeInCombat()`, `CardDynamicDescriptionFormatter`, `MultiHitDamageEffect` scaling, and `MerchantOffer` discount + `50% OFF` calculations.
- **Live Play-Mode Verification (`Game.unity` — `verify_playmode_game.cs`):** **`17/17` passed, `0` failed** — verified `PowerSystem`, `EnemyPassiveSystem`, `CombatLogUI`, `CombatCardPickerUI` auto-creation, Persistent Power exclusion from Discard/Exhaust, HUD Power badge generation, green `#59FF8A` dynamic card text scaling on `+ High Will`, Combat Log open/close + entry capture, `EndTurnButtonUI` presence, visible Enemy Passive badge on `EnemyView`, and `BossRelicPickerUI` 3-choice chest modal.
- **Live Play-Mode Verification (`Merchant.unity` — `verify_playmode_merchant.cs`):** **`2/2` passed, `0` failed** — verified 1 card offer per shop visit receives the `50% OFF` sale flag and ribbon tag cleanly.
- **Unity Compiler & Console Status:** **`RECOMPILE_EXIT=0`**, **`compilationFailed: false`**, **`consoleErrors: 0`**, **`consoleWarnings: 0`** at completion.

