# Project Conventions

Based on a scan of the existing codebase (`Assets/_Project/Scripts/`), the following conventions are strictly adhered to:

## Naming Conventions
- **MonoBehaviours (Visuals/UI):** Suffix with `View` (e.g., `CardView.cs`, `EnemyView.cs`, `CombatantView.cs`) or `UI` (e.g., `ManaUI.cs`, `PerksUI.cs`).
- **Data Containers (ScriptableObjects):** Suffix with `Data` (e.g., `CardData.cs`, `HeroData.cs`, `EnemyData.cs`).
- **Game Actions (Commands):** Suffix with `GA` (e.g., `DealDamageGA.cs`, `PlayCardGA.cs`, `DiscardAllCardsGA.cs`).
- **Systems/Singletons:** Suffix with `System` (e.g., `ActionSystem.cs`, `DamageSystem.cs`, `ManaSystem.cs`). Note: While they inherit from `Singleton<T>`, they use `System` instead of `Manager` for combat logic. `Manager` is used for global persistence (e.g. `RunManager`, `GameManager`).
- **Targeting Logic:** Suffix with `TM` (Target Mode) (e.g., `AllEnemiesTM.cs`, `RandomEnemyTM.cs`).
- **Variables and Fields:**
  - Standard C# CamelCase for local variables.
  - PascalCase for public properties.
  - Private serialized fields generally do not have an underscore prefix, but standard private fields do occasionally.

## Architectural and Code Style Conventions
- **Decoupling Logic from Display:** Logic classes (like `Card.cs`) do not inherit from MonoBehaviour. Only the classes responsible for rendering (`CardView.cs`) do. The view holds a reference to the logic model.
- **Event Handling:** Standard C# `event` / `delegate` patterns are avoided for core gameplay loops. Instead, all gameplay events subscribe to `ActionSystem.Instance.SubscribeReaction<T>`.
- **System Injection:** Most systems inherit from `Singleton<T>` and access each other globally via `SystemName.Instance.Method()`.
- **Animation Framework:** DOTween is universally used for UI animations, card movements, and combat impacts (`transform.DOShakePosition`).

## Folder Conventions
- `_Project/Scripts/Data/` - Core ScriptableObject schema definitions.
- `_Project/Scripts/Game Actions/` - Action commands dispatched via the Action System.
- `_Project/Scripts/Models/` - Non-MonoBehaviour logic definitions (Cards, Perks, Effects).
- `_Project/Scripts/Systems/` - Singleton controllers that dictate game rules.
- `_Project/Scripts/Views/` - MonoBehaviours defining the visual rendering layer.
- `_Project/Scripts/Target Modes/` - Auto-targeting algorithms.
