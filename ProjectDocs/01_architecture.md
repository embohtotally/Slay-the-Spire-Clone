# Architectural Overview

The project utilizes a highly decoupled, data-driven, and event-driven architecture heavily based on the Model-View-System (MVS) paradigm and a custom Command-Performer pattern (the **Action System**).

## Architectural Patterns

1. **Model-View-System (MVS):**
   - **Models**: Pure data classes or structs containing no MonoBehaviours (`Card`, `Perk`, `Effect`, `TargetMode`).
   - **Views**: MonoBehaviours responsible exclusively for rendering visuals, animations (DOTween), and accepting user inputs (`CardView`, `CombatantView`, `HeroView`).
   - **Systems**: Singletons orchestrating state, rules, and game actions (`CardSystem`, `EnemySystem`, `ActionSystem`).

2. **Command-Performer (Action System):**
   - Core gameplay mechanics are executed as discrete `GameAction` commands.
   - Systems map `Performers` (Coroutine delegates) to execute specific actions over time (e.g., animating a fireball and dealing damage).
   - This provides absolute control over the sequence of visual events and avoids race conditions in card games.

3. **Data-Driven Configuration:**
   - Content relies heavily on ScriptableObjects. Designers configure characters (`HeroData`, `EnemyData`), abilities (`CardData`), buffs (`PerkData`), and levels (`LevelDefinition`) entirely in the Unity Editor without touching code.

4. **Service Locator / Singletons:**
   - Systems are globally accessible singletons extending from `Singleton<T>`.

## Managers, Systems, and Services

| System Class | Responsibility |
|--------------|----------------|
| `ActionSystem` | Orchestrates the sequential execution flow of all gameplay logic (`GameAction` commands) via Coroutines. |
| `CardSystem` | Manages deck, hand, discard pile, and drawing/playing mechanics. |
| `EnemySystem` | Orchestrates enemy spawning, attack intents, and enemy turn sequence. |
| `DamageSystem` | Listens to `DealDamageGA`, updates HP, and triggers kill actions. |
| `ManaSystem` | Tracks and controls player mana, managing refill triggers at turn start. |
| `HeroSystem` | Holder of the `HeroView` reference and initial player setup. |
| `PerkSystem` | Manages active perks (relics/buffs), their lifecycles, and trigger conditions. |
| `EffectSystem` | Maps `PerformEffectGA` (generic effect triggers from cards/perks) into specific resulting game actions (damage, draw, etc). |
| `DamageOverTimeSystem` | Handles DOT effects like poison or burn. |
| `BuffSystem` | Tracks active temporary modifiers (Strength, Weakness, etc.). |
| `MatchSetupSystem` | Initializes the battle scene state across all other systems. |
| `ManualTargetingSystem` | Manages raycasts for player-targeted cards and visualizes the targeting arrow. |
| `GameController` | High-level battle state management (Victory/Defeat). |
| `Interactions` | Global input gatekeeper; blocks player actions while the `ActionSystem` is busy performing. |
| `RunManager` | Persists run progression data (current deck, health, relics) across different scenes. |

## The Event System (ActionSystem Reactions)

Events are not handled via standard C# `event` keywords but through the `ActionSystem`'s reaction pipeline.
When a `GameAction` executes, it triggers two subscriber queues:
- **PRE Phase:** Executes subscribed logic before the action's visual `Performer` runs.
- **POST Phase:** Executes subscribed logic after the `Performer` finishes.

**Example Flow:**
1. Action `EnemyTurnGA` is pushed.
2. `CardSystem` subscribes to PRE `EnemyTurnGA`: Triggers hand discard.
3. `EnemyTurnGA` Performer runs: Enemies execute their intents.
4. `ManaSystem` subscribes to POST `EnemyTurnGA`: Refills mana.
5. `CardSystem` subscribes to POST `EnemyTurnGA`: Draws a new hand.

## State Machines

The project utilizes a dedicated State Machine pattern (`StateMachine/`) primarily for combatant animations and behavioral states.

- **`StateMachine` (Class):** Generic state runner handling `ChangeState`.
- **`CombatantState`:** Base class for all states involving a `CombatantView`.
- **Active States:**
  - `CombatantIdleState`
  - `CombatantAttackState`
  - `CombatantBuffState`
  - `CombatantHealState`
  - `CombatantHurtState`
  - `CombatantDeadState`

*Transition Triggers:* States are largely driven by the perform logic inside specific `GameActions` (e.g., `AttackHeroGA` performer tells the enemy state machine to switch to `CombatantAttackState`).
