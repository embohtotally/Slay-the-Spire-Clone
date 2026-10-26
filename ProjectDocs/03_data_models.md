# Data Models

The project utilizes ScriptableObjects extensively to define game content. These classes exist primarily as data containers.

## Game Entities

| Class | Type | Purpose | Key Fields | Referenced By |
|-------|------|---------|------------|---------------|
| `HeroData` | ScriptableObject | Defines the starting state for a player class. | `MaxHealth`, `StartingMana`, `Deck` (List of CardData) | `RunManager`, `MatchSetupSystem` |
| `EnemyData` | ScriptableObject | Defines an enemy archetype. | `EnemyName`, `MaxHealth`, `Prefab`, `IntentList` | `EnemySystem`, `EncounterData` |
| `CardData` | ScriptableObject | Defines a playable card. | `CardName`, `ManaCost`, `Description`, `CardArt`, `ManualTargetEffect`, `AutoTargetEffects` | `CardSystem`, `RunManager`, `CardViewCreator` |
| `PerkData` | ScriptableObject | Defines a passive relic/buff. | `PerkName`, `Icon`, `Condition` | `PerkSystem`, `RunManager` |
| `RelicData` | ScriptableObject | Defines persistent relics (often wraps PerkData). | `RelicName`, `PerkData` | `RunRelicManager` |
| `PotionData` | ScriptableObject | Defines single-use potions. | `PotionName`, `Effect` | `RunPotionManager` |

## Map & Run Configuration

| Class | Type | Purpose | Key Fields | Referenced By |
|-------|------|---------|------------|---------------|
| `EncounterData` | ScriptableObject | Defines a specific combat node (group of enemies). | `Enemies` (List of EnemyData), `IsElite` | `MapGenerator`, `MatchSetupSystem` |
| `LevelDefinition` | ScriptableObject | Defines an Act/Level of the game. | `LevelName`, `MapConfig`, `PossibleEncounters`, `BossEncounter` | `LevelProgressionManager` |
| `MapGeneratorConfig` | ScriptableObject | Tuning parameters for procedural map generation. | `GridWidth`, `GridHeight`, `PathCount`, `NodeProbabilities` | `ProceduralMapGenerator` |

## Logic Definitions

- `Effect`: Abstract base class specifying `GetGameAction(targets, caster)`. Subclassed into `DealDamageEffect`, `DrawCardsEffect`, etc. Included in Cards and Potions.
- `TargetMode`: Abstract base class specifying how a card auto-finds targets without manual player drag. Subclassed into `RandomEnemyTM`, `AllEnemiesTM`, `NoTM`. Included in `CardData`.
- `PerkCondition`: Abstract class defining when a perk triggers (e.g., `OnEnemyAttackCondition`). Includes `ReactionTiming` (PRE/POST) to specify exactly when in the action resolution it occurs.
