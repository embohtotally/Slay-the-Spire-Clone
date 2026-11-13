# Namespaces

[Code index](README.md)

All project scripts use `PlushieOdyssey.<group>`. Third-party code keeps its original namespace.

| Group | Use |
| --- | --- |
| `Core` | GameManager, bootstrap, singleton, logging |
| `Core.Scenes`, `Core.Extensions`, `Core.Events`, `Core.StateMachines` | Shared scene loading and utilities |
| `Audio` | Tune, audio cues, generated audio IDs |
| `Inputs` | Input reader, contexts, UI bridge |
| `Tweens` | BaseTween and UI effects |
| `Data` | Card, hero, enemy, relic, potion, encounter assets |
| `Combat.Actions`, `Combat.Systems`, `Combat.Models` | Action processing, combat logic, runtime models |
| `Combat.Effects`, `Combat.Targeting`, `Combat.Conditions` | Effect and targeting implementations |
| `Combat.Views`, `Combat.States` | Combat visuals, creators, animation states |
| `Combat` | Shared combat contracts, e.g. IHaveCaster |
| `Run`, `Relics`, `Rewards` | Run progression and reward/relic logic |
| `Map` | Map generation, data, navigation |
| `Map.Manual`, `Map.Merchant`, `Map.Rest`, `Map.Events`, `Map.Treasure` | Map-specific features |
| `UI`, `UI.Menus`, `UI.Events`, `UI.Potions`, `UI.Relics`, `UI.StatusEffects` | Screen controls and UI views |
| `Levels`, `Dialogue`, `Tutorials`, `Vfx` | Feature-specific code |
| `Editor.Tools`, `Editor.Audio` | Editor-only tooling; keep files in an `Editor` folder |

## New scripts

- Choose by responsibility, not by who wrote the script. Import only the groups you need.
- Use `Inputs`, not `Input`, to avoid hiding `UnityEngine.Input`.
- Keep reusable code in its shared group; add a subgroup only when useful.
- Example: a settings controller uses `namespace PlushieOdyssey.UI.Menus` and imports `PlushieOdyssey.Inputs` / `PlushieOdyssey.Tweens` as needed.

## Migration

Script paths, `.meta` GUIDs, and assembly names are unchanged. `MovedFrom` is kept only on managed-reference classes (effects, target modes, conditions, and relic reactions) to preserve saved asset data.
The audio enum generator also writes the new namespace. Verify card effects and Inspector callbacks after Unity recompiles.
