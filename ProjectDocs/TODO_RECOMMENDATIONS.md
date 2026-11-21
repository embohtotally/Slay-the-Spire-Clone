# Plushie Odyssey — Implementation & Improvement Recommendations (2026-11-20)

[Project README](../README.md) · [Master Backlog (TODO.md)](TODO.md) · [2026-11-20 Changelog](CHANGELOG_2026-11-20.md) · [Code Documentation Index](code/README.md)

Focused roadmap of newly recommended features, combat mechanics, UX/readability improvements, and technical polish identified from comparing the current codebase (`44` cards, `7` enemies, `6` encounters, `6` events, `2` potions, `9` relics, `4` chapters) against **Slay the Spire** and **Witch's Apocalyptic Journey (`魔女的末日旅途`)**.

---

## 1. Slay the Spire — Combat Mechanics & Tactical Depth

- [ ] **Implement True Persistent `Power` Cards (Passive Combat Triggers)**
  - Currently `CardType.Power` cards discard like normal skills/attacks after resolving one-shot effects.
  - Remove played `Power` cards from the combat deck cycle for the rest of battle (never sent to Discard or Exhaust), display active Power badges on the combat HUD, and support persistent passive triggers (start/end of turn, on Attack/Buff played, on entering `+ High Will`).
- [ ] **Add Multi-Hit Attacks (`MultiHitDamageEffect`)**
  - Add a dedicated multi-hit effect hitting `N` times (or `X` times for Whirlwind-style X-cost cards) so `Strength`, `Vulnerable`, `Thorns`, and `PlatedArmor` interact properly on every individual hit, paired with staggered impact popups/shake.
- [ ] **Add Status & Curse Clutter Cards**
  - Author `Burn`, `Wound`, `Dazed`, `Void`, `Doubt`, and `Regret` cards injected by Elites, Bosses, and risky Narrative Events so `Exhaust`, `Cleanse`, `Draw`, and Merchant Card Removal gain stronger tactical tension.
- [ ] **Build Will-Archetype Synergy Cards & Relics**
  - **Will-Spender / Overdrive Cards:** High-impact attacks/buffs that intentionally consume `15–25` Will for burst payoff.
  - **Low-Will Redemption Cards:** Emergency cards playable even during `- Low Will` (below 25%) to break the Attack seal or convert low Will into Shield/Draw.
  - **Will-Threshold Relics:** Passive relics that trigger when crossing into `+ High Will` (`≥ 75%`) or recovering from `- Low Will`.
- [ ] **Add Turn-Scoped Cost Modifiers & Card-Combo Mechanics**
  - Separate single-turn / combat-only card cost reductions from run-wide `RunManager.CardCostModifiers`, and add Retain-scaling, Draw-scaling, and Combo/Finisher effects scaling with cards played this turn.
- [ ] **Add Mid-Combat Card Manipulation Modals**
  - **Scry:** Inspect top `N` cards of the Draw Pile and selectively send them to Discard.
  - **Discover (Choose 1 of N):** Present 3 temporary cards to add to hand for the current battle.
  - **In-Combat Hand Picker:** Choose a card in hand to Discard, Exhaust, or Upgrade (single card or full hand).
- [ ] **Expand Enemy AI Beyond Static Sequential Loops & Add Enemy Passive Badges**
  - Support conditional/weighted AI (Turn-1 openers, anti-repeat cooldowns, HP-threshold splits/phase shifts, reactive *Enrage* / *Time-Warp* triggers, *Ritual* scaling, and Status/Curse injection intents).
  - Display hoverable **Enemy Passive Trait Badges** (*Spiky*, *Mode Shift*, *Regenerate*, *Will-Eater Aura*) directly in `EnemyIntentHUD`.

---

## 2. Witch's Apocalyptic Journey (`魔女的末日旅途`) — Party, Ultimates & Crafting

- [ ] **Spawn Full Active Party on Battlefield & Route Per-Hero Card Ownership**
  - Spawn all slotted `RunManager.SelectedParty` avatars on the battlefield in [`HeroView.cs`](../Assets/_Project/Scripts/Views/HeroView.cs), tag character cards with `OwnerCharacterId` so each hero performs their own cast lunge and voice line (`CharacterVoiceDirector`), and finish [`AllyByCharacterTargetMode.cs`](../Assets/_Project/Scripts/Target%20Modes/AllyByCharacterTargetMode.cs) for ally-targeted heals, shields, and `Redirect`.
- [ ] **Wire Playable Hero Ultimates (`UltimatePointsView` + Cut-In Cast Flow)**
  - Place [`UltimatePointsView.cs`](../Assets/_Project/Scripts/UI/Combat/UltimatePointsView.cs) in `Game.unity`, charge **Ultimate Points (UP)** on card plays, `+ High Will` triggers, and hits taken, and trigger a **Splash Portrait Cut-In + Voice Line + Ultimate Action** (`Astral Starfall`, `Vanguard's Aegis`) when clicking a charged portrait (or pressing `Q`/`U`).
- [ ] **Wire Unlockable Character Talents (`II–IV`) & Campfire Companion Banter**
  - Connect persistent Hero Mastery / Bond XP (earned from runs, Events, and short visual-novel companion dialogues between active party members in `Rest.unity`) to unlock `HeroData.Talents` (`II–IV`) and execute their combat passives (*Unbreakable Will*, *Twin-Blade Momentum*, *Knight's Oath*, *Timeless Echo*).
- [ ] **"Blank Card / Super Card" Spell-Component Crafting Workshop**
  - Spend **Truth Crystals** (`Crystal` meta-currency) in a dedicated crafting modal to assemble custom cards by combining modular **Mana Cost (`1 / 2 / 3 / X`) + Base Effect + Modifiers (`+Poison`, `+Draw`, `+Will`, `Exhaust`, `Retain`, `AoE`)** on top of the existing `Effect` & `XScalingAxis` system.
- [ ] **Fate Cards & Daily Schedule Timeline Planner Mode**
  - Optional WAJ journey mode where players draw **Fate Cards** (encounters, rests, shops, events) and arrange them along a multi-slot **Schedule Timeline** with adjacency bonuses (e.g., *Rest before Elite grants +15 Will*; *Mystery after Merchant grants Shop Discount*).

---

## 3. Run Loop, Economy, Progression & Modes

- [ ] **Boss 3-Relic Choice Chest**
  - Replace single auto-dropped Boss relics with a **Pick 1 of 3 Boss Relics** overlay featuring high-impact risk/reward trade-offs (e.g., `+1 Max Mana, cannot Rest at Campfires`, or `+1 Max Mana, start combat with -15 Will`).
- [ ] **Merchant 50%-OFF Sale Tag, Potion Shelf & Shop Relics**
  - Flag 1 card per `Merchant.unity` stock roll with a visual **50%-OFF Sale Tag**, add a 2–3 slot **Potion Shelf** beside the Relic pedestals, and add relic-driven discount/restock hooks (*Membership Card*, *Courier*).
- [ ] **Expand Potions (`6–10` Types), Out-of-Combat Use, Hover Tooltips & Full-Belt Swap UI**
  - Expand beyond the current `2` potions (`Potion Draw`, `Potion Heal`) with Fire, Poison, Strength, Will-Restore, Shield, Energy, and Fairy-in-a-Bottle potions.
  - Support `PotionData.UsableOutsideCombat` across Map/Shop/Rest scenes, add hover tooltips on `PotionSlotView`, and prompt a Discard/Swap choice when [`RunPotionManager.cs`](../Assets/_Project/Scripts/Run/RunPotionManager.cs) is full (`3/3`).
- [ ] **Propagate Unified Top Status Bar Across All Non-Combat Scenes**
  - Bring the persistent top bar (`HP / Will / Gold / Crystals / 5-Slot Relics / Potions / Deck / Map`) to `Map.unity`, `Merchant.unity`, `Rest.unity`, `Treasure.unity`, `Event.unity`, and `RunRewards.unity`, and add **Floor counter** and **Ascension badge** indicators.
- [ ] **Upgrade `CardReward.unity` Choice Clarity**
  - Add tier rarity glow borders, an **`Owned: N` in Deck** badge, and hover `CombatKeywordTooltipUI` cards inside `CardRewardOptionView`.
- [ ] **Mid-Run Save / Continue, Ascension Tiers, Crystal Meta-Tree & Compendium**
  - Serialize active run state (seed, party, HP/Will, gold/crystals, deck, relics, potions, map graph, floor) for **Save & Quit / Continue** on `MainMenu.unity`.
  - Add **Persistent Crystal Meta-Upgrade Tree**, **Difficulty / Ascension tiers** on `Levels.unity`, **Custom Seed / Daily Run** modes, and a **Compendium** (Card Library, Relic Gallery, Potion List, Enemy Bestiary).

---

## 4. UX, Controls, Readability & Audio/Scene Hygiene

- [ ] **Dynamic Live-Adjusted Hand Card Numbers (`CardView`)**
  - Dynamically calculate and tint modified card values in hand (**green** when boosted by `Strength`, `Dexterity`, or `+ High Will`; **red** when reduced by `Weak` or `Frail`) and preview live `X`-scaled values (`18 × X = 90`).
- [ ] **End-Turn Smart Glow & Collapsible Combat Log**
  - Pulse/tint `EndTurnButtonUI` distinctly when `CardSystem.HasPlayableCards()` is `false` so players know no legal plays remain, and add a compact toggleable **Combat Log** drawer tracking damage, block, Will rolls, and status procs.
- [ ] **Full Keyboard & Gamepad Combat Controls**
  - Populate `Combat` and `Dialogue` maps in [`InputSystem_Actions.inputactions`](../Assets/_Project/InputSystem_Actions.inputactions) for keyboard card selection (`1–9`/`0`), Left/Right target cycling, `Space/Enter` cast, `X` Exhaust pile, `Q`/`U` Ultimate, and full gamepad navigation.
- [ ] **Fix Settings Startup Audio Sync & Add Gameplay Quality-of-Life Toggles**
  - Update [`SettingsController.LoadAudioWithoutNotify`](../Assets/_Project/Scripts/Menus/SettingsController.cs) so saved PlayerPrefs volumes push to `_mainMixer` immediately at startup (currently only updates slider positions).
  - Add **Fast Mode (`1.6×` animation speed)**, **Screen Shake** toggle (bound to `CombatJuiceDirector.ScreenShakeEnabled`), **Resolution / VSync / FPS Cap**, **Confirm End Turn with Unused Mana**, and **Reset Save Confirmation**.
- [ ] **Audio Layering & Eyelid-Blink Transition SFX**
  - Hook subtle blink/eyelid SFX cues into [`BlinkSceneTransition.cs`](../Assets/_Project/Scripts/General/Scenes/BlinkSceneTransition.cs) and support dynamic combat/low-HP/Boss music crossfading in `SceneAudioPlayer`.
- [ ] **Scene Cleanup & Automated Test Coverage**
  - Remove the legacy `'Arrows'` objects in `Assets/_Project/Scenes/Map.unity` that emit `182` `Missing Script` warnings on load, and retire obsolete `Loading.unity` from Build Settings now that `BlinkSceneTransition` handles all scene loads.
  - Add an automated Unity Test Framework suite (`Assets/_Project/Tests/`) covering X-cost mana & `XScalingAxis` math, Will System (`+ High Will`, `- Low Will`, `1–3` RNG rolls), Buff/Debuff stacking, `Artifact` negation, `Poison` ticks, `ProceduralMapGenerator` invariants, and Save/Load serialization.

