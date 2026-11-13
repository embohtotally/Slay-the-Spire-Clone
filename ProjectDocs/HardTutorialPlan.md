# Hard Tutorial Implementation Plan

This plan details how we will create a step-by-step "Hard Tutorial" that restricts the player's actions until they learn how to play, using `NPCInteraction` for dialogue delivery.

## Proposed Changes

### 1. `TutorialManager.cs` (New Script)
We will create a `TutorialManager` script that acts as a singleton `Instance` in the tutorial scene. It will:
- Track the current tutorial `Phase` (e.g., `PlayCard`, `EndTurn`).
- Hold a reference to an `NPCInteraction` component to trigger dialogue boxes for instructions.
- Provide `CanInteractWithCard(Card)` and `CanEndTurn()` methods that return `true` or `false` based on the current phase.

### 2. Lock Card Dragging
We will modify the card interaction logic so that during the tutorial, you can only drag the specific card you are supposed to learn about.
- Update `CardView.cs` logic to block dragging if `TutorialManager` says it isn't allowed yet.

### 3. Lock End Turn Button
We will prevent the player from ending their turn prematurely.
- Update `EndTurnButtonUI.cs` to block clicking if `TutorialManager` says the player must do something else first.

### 4. Force specific enemies for Tutorial
Normally, the map node dictates the enemies. In the tutorial scene, we want to force a specific dummy enemy.
- Add a new inspector toggle: `[SerializeField] private bool forceInspectorEnemies = false;` to `MatchSetupSystem.cs`.
- If checked, it will ignore the map's random enemy generator and strictly spawn the dummy enemies you set in the Inspector.

## The Planned Sequence
1. **Dialogue**: "Welcome to combat! Drag an Attack card to the enemy." -> Only Attacking is allowed.
2. **Dialogue**: "Great! Now end your turn." -> Only clicking End Turn is allowed.
3. **Dialogue**: "You're ready!" -> Unlocks everything completely and the tutorial finishes.
