# Action System

**Purpose:** 
The core heartbeat of the gameplay logic. It guarantees that game actions execute sequentially, allowing visual animations (via Coroutines) to resolve before processing subsequent mechanics or triggers.

**Key Classes:**
- `ActionSystem`: Singleton executing the action loop.
- `GameAction`: Abstract base class for all actions (e.g. `DrawCardsGA`, `DealDamageGA`).
- `ReactionTiming`: Enum defining `PRE` (before performer) and `POST` (after performer) phases.

**Public API (ActionSystem):**
- `AttachPerformer<T>(Func<T, IEnumerator> performer)`: Registers a coroutine to handle a specific `GameAction`.
- `DetachPerformer<T>()`: Removes the performer for a type.
- `SubscribeReaction<T>(Action<T> reaction, ReactionTiming timing)`: Binds a callback to execute before/after a specific action type.
- `Perform(GameAction action, Action OnPerformFinished = null)`: Starts the execution flow of an action.
- `AddReaction(GameAction gameAction)`: Nests an action into the active phase dynamically.

**Data Flow:**
1. Action passed to `Perform()`.
2. Triggers `preSubs` subscribers -> Evaluates dynamically added pre-reactions.
3. Executes registered `Performer` Coroutine -> Evaluates dynamically added execution reactions.
4. Triggers `postSubs` subscribers -> Evaluates dynamically added post-reactions.

**Dependencies:**
Used heavily by almost every other system in combat (`CardSystem`, `DamageSystem`, `EnemySystem`).
