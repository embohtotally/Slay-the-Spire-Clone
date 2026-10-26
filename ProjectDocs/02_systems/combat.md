# Combat and Entity Systems

**Purpose:**
Manages the HP state of the hero and enemies, processes damage and status effects, handles the enemy turn cycle, and resolves deaths.

**Key Classes:**
- `DamageSystem`: Central authority on processing `DealDamageGA` actions.
- `EnemySystem`: Tracks active enemies on board and orchestrates enemy turns.
- `HeroSystem`: Provides easy singleton access to the active HeroView.
- `CombatantView` / `HeroView` / `EnemyView`: Visual representations handling health bars, damage popups, and state machine transitions.

**Public API / Behaviors:**
- `DamageSystem`:
  - Registers `DealDamageGA` performer. Deducts HP, triggers visual shakes, and queues `KillEnemyGA` or defeat states if HP <= 0.
- `EnemySystem`:
  - `Setup(List<EnemyData> data)`: Spawns enemies on `EnemyBoardView` slots.
  - Registers `EnemyTurnGA` performer: Iterates through active enemies, pushing `AttackHeroGA` (or their specific intents).
  - Registers `KillEnemyGA` performer: Removes enemy from board, triggers death animation. Checks for battle victory.

**Data Flow:**
1. A card or enemy intent queues a `DealDamageGA`.
2. `DamageSystem` performer resolves the damage value against the `CombatantView`.
3. If the combatant is an enemy and health drops to 0, `KillEnemyGA` is queued dynamically.
4. `KillEnemyGA` performer removes the enemy from `EnemySystem` lists.
5. If the list is empty, `EnemySystem` fires a `CombatWonGA` action to transition the game state.

**Dependencies:**
- `ActionSystem` (Damage, death, and turns are all actions).
- `GameController` (Notified on victory/defeat).
