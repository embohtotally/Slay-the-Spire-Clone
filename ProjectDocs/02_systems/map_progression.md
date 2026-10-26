# Map and Progression

**Purpose:**
Handles the rogue-like meta-structure of the game, procedural map generation (or manual layout), node selection, and preserving player state across different scenes.

**Key Classes:**
- `MapSystem` / `MapGraph` / `MapNode`: Generates and manages the directed graph of the run's map.
- `ProceduralMapGenerator`: Uses `MapGeneratorConfig` to populate map layers with specific encounter types (Combat, Elite, Rest, Merchant).
- `RunManager`: Persistent Singleton carrying the player's deck, perks, gold, and health between the map scene and game scenes.
- `LevelProgressionManager`: Manages the overall levels / act structure.

**Public API (RunManager):**
- `InitializeRun(HeroData)`: Called at the start of a new run to clone base stats.
- `AddCard(CardData)` / `RemoveCard(CardData)`: Modifies the master deck.
- `AddRelic(RelicData)`: Modifies player's persistent relics.
- Data references provided to scenes on load (via `MatchSetupSystem` reading from `RunManager`).

**Data Flow:**
1. `MapSystem` constructs the visual layout. Player clicks an accessible `MapNodeView`.
2. Map scene saves current location, then loads the relevant scene (e.g. `Game` for combat, `Merchant` for shop).
3. In the new scene, setup systems query `RunManager.Instance.CurrentDeck` and `RunManager.Instance.CurrentHealth`.
4. Upon scene completion, rewards are gathered and added back to `RunManager`.
5. Scene transitions back to `Map`.

**Dependencies:**
- Configured by `MapGeneratorConfig`.
- Relies on Unity's SceneManagement (`SceneLoader`) to transition.
