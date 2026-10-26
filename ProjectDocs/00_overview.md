# Project Overview

## Core Identity
**Project Name:** Slay-the-Spire-Clone
**Genre:** Roguelike Deckbuilder
**Core Loop:** The player traverses a branching procedural map containing various encounters (combat, events, merchants, rest sites). In combat, they draw a hand of cards, spend mana to play cards (dealing damage, gaining shield, applying effects), and end their turn to let enemies execute telegraphed intents. Upon winning, they receive rewards (new cards, relics, potions) to improve their deck before progressing to the next node.

## Technical Specifications
- **Unity Version:** 6000.0.73f1 (Unity 6)
- **Render Pipeline:** Universal Render Pipeline (URP) version 17.1.0
- **Target Platforms:** PC and Mobile (implied by `PC_Renderer` and `Mobile_Renderer` assets)

## High-Level Folder Structure Map (`Assets/_Project/`)

- `Animations/` - Unity Animation Clips and Controllers.
- `Art/` - Sprites, 3D models, textures, and other graphical assets.
- `Data/` - Core data scripts (primarily empty or reserved, most data scripts are in `Scripts/Data/`).
- `Fonts/` - SDF Fonts (Almendra, Fredoka) used by TextMeshPro.
- `Input/` - Input System configuration and action maps.
- `Materials/` - Materials configured for URP.
- `Prefabs/` - Reusable game objects organized by category (UI, Map, CardViews, etc.).
- `Scenes/` - Unity scenes (Loading, MainMenu, Game, Map, Event, Rest, Merchant, etc.).
- `ScriptableObjects/` - The actual ScriptableObject instances configuring the game (Cards, Enemies, Perks, Levels).
- `Scripts/` - C# source code, containing all systems, views, and data models.
- `Settings/` - URP profiles, Volume profiles, and general Unity settings.
- `Sounds/` - Audio clips (SFX, Music).

## Documentation Index

- [01 Architecture](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/ProjectDocs/01_architecture.md)
- [02 Systems - Action System](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/ProjectDocs/02_systems/action_system.md)
- [02 Systems - Cards](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/ProjectDocs/02_systems/cards.md)
- [02 Systems - Combat](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/ProjectDocs/02_systems/combat.md)
- [02 Systems - Beam VFX Subsystem](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/ProjectDocs/02_systems/beam_vfx_system.md)
- [02 Systems - Map & Progression](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/ProjectDocs/02_systems/map_progression.md)
- [03 Data Models](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/ProjectDocs/03_data_models.md)
- [04 Scenes and Flow](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/ProjectDocs/04_scenes_and_flow.md)
- [05 Conventions](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/ProjectDocs/05_conventions.md)
- [06 Open Questions](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/ProjectDocs/06_open_questions.md)
