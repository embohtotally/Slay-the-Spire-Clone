# Open Questions & Flags

During the codebase audit, the following items were flagged for review or clarification:

## 1. Temporary/Hacky Workarounds Noted in Code
- **`TutorialManager.cs` (Lines 130 & 148):** Contains comments noting: `"Temporarily remove listener so ForceEndDialogue doesn't trigger OnDialogueFinished"`. This suggests a potential race condition or tightly coupled event flow in the dialogue system that might need a more robust, long-term fix to avoid missing listeners if an error occurs.
- **`CFXR_MaterialInspector.cs` (Line 58):** Third-party asset mentions a "hackish" implementation. Safe to ignore unless modifying VFX shaders, but good to be aware of.

## 2. Unclarified Architectural Decisions
- **Event Sourcing vs. Standard Events:** While combat relies entirely on `ActionSystem` PRE/POST reactions, the `UI` and external menu systems still seem to use UnityEvents or standard C# actions. Is it intentional to keep the `ActionSystem` purely restricted to the `Game.unity` combat scene, or should it be expanded to handle out-of-combat events (like buying items in the `Merchant` scene)?
- **Data Duplication:** `RelicData` seems to wrap `PerkData`. Is there a planned divergence where Relics will have non-Perk attributes (like durability/charges), or could these two ScriptableObject definitions be merged to simplify the project structure?

## 3. Potential Parsing Limitations
- **Spline and Map Node Connections:** The parsing identified that `HandView` uses Unity Splines for card placement and the map uses Graph generation. Deeply inspecting the mathematically generated procedural paths (`ProceduralMapGenerator.cs`) requires runtime execution to see the true results of the `MapGeneratorConfig`, which couldn't be fully statically verified from the scripts alone.
- **Third-Party Package Noise:** Several files exist for `SREditor`, `NaughtyAttributes`, `KinoBloom`, and `DOTween`. The audit intentionally excluded deep-dives into these packages to focus on `_Project` logic. Let me know if you need any plugins audited specifically.
