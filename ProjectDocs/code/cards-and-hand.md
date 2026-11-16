# Cards and hand

[Code index](README.md) · [Views](../../Assets/_Project/Scripts/Views/) · [Systems](../../Assets/_Project/Scripts/Systems/)

Canvas-only presentation. Local references/events; no singleton lookup in the view, input, feedback, hover preview, or hand controller. `CardPlayService` bridges to the existing gameplay backend, which still contains globals.

## Component scope

| Component | Scope / location | Owns |
| --- | --- | --- |
| `CardView` | Per card prefab, including preview cards | Text, artwork, frame, displayed cost, blocked/selected overlays. |
| `CardPointerInput` | Same root as an interactive hand card's CardView | Pointer events and pointer-press tracking. |
| `CardVisualFeedback` | Optional component on the same card root | Hover/press scale on an inner visual child. |
| `CardFanLayout` | Per hand, on a UI container | Fan pose calculation and selected-object curve gizmo. |
| `HandView` | Per hand, usually beside CardFanLayout | Registered views, layout animation, sibling order. |
| `CombatHandController` | Per hand rig/prefab | Input subscriptions, selection, drag/target state, cancellation, play requests. |
| `CardViewHoverSystem` | Local preview owner, outside the preview card's hierarchy | One enlarged display-only card; **not a singleton**. |
| `CardPlayService` | Per battle, on a scene services object | Membership/cost/rule checks and action submission. |
| `CardTargetResolver` | Per battle, on a scene services object | Camera raycast and combatant validity/taunt checks. |
| `CardViewAdder` | Optional debug component on the hand rig | Random presentation cards and owned-card cleanup; not gameplay injection. |

None creates persistent objects. Hand-internal references can live in a prefab; scene gameplay references are assigned on the instance.

`CardData` is an authored asset; `Card` is a runtime copy; `CardDisplayData` is a value snapshot. Neither runtime type is a component.

```text
CardRoot [CardView, CardPointerInput, optional CardVisualFeedback]
└── VisualRoot [Images, TMP, optional state overlays]

HandRig [CombatHandController]
├── HandRoot [HandView, CardFanLayout]
├── PlayRegion [RectTransform]
├── DragLayer [RectTransform, CanvasGroup]
└── PreviewLayer [RectTransform]
    └── PreviewCard [CardView, CanvasGroup]
```

Keep hand/drag/preview under the same Canvas setup. Later siblings normally render above earlier ones. The hover owner stays outside PreviewCard; no Layout Group should also position hand cards.

## Reference fields

**R** = required for the component's feature; **O** = optional; **C** = conditionally required.

| Component | Field | Contract |
| --- | --- | --- |
| CardView | `titleText`, `descriptionText`, `manaText`, `typeText` | C: TMP UI labels; null omits that label. |
| | `cardImage` | C: artwork Image. |
| | `frameImage`, `basicFrameSprite`, `rareFrameSprite`, `specialFrameSprite` | C: rarity-frame rendering; missing sprite hides the frame. |
| | `_blockedOverlay`, `_selectedOverlay` | O: child GameObjects, never CardRoot. |
| CardVisualFeedback | `_visualRoot` | R: inner RectTransform, not the moving/scaling card root. |
| HandView | `_fanLayout` | R: layout whose object receives card views; null searches the same GameObject only. |
| CombatHandController | `_handView` | R: hand whose runtime registrations are observed. |
| | `_canvas` | R for complete UI interaction: pointer conversion; assign World Camera for non-Overlay mode. |
| | `_dragLayer` | C: automatic-target dragging; must have CanvasGroup. Raycast blocking is temporarily disabled while dragging. |
| | `_playRegion` | C: release-confirmation rectangle for automatic-target cards; no Image required. |
| | `_hoverPreview` | O: enlarged preview; null leaves local hover feedback available. |
| | `_playService` | C: actual gameplay; null permits isolated presentation but cannot execute a card. |
| | `_inputRouter` | O for pointer isolation; required for routed Back/context gating. Use the existing active router and [UIInputBridge](input.md). |
| CardViewHoverSystem | `cardViewHover` | R: separate Canvas CardView, parented under the visible preview bounds. |
| | `_inputGroup` | R: CanvasGroup on PreviewCard or its preview-only ancestor; not shared with the hand. |
| | `_canvas` | R: render Canvas; World Camera required outside Overlay mode. |
| CardPlayService | `_cardSystem`, `_actionSystem`, `_manaSystem`, `_heroView` | R: active, initialized gameplay components; service does not initialize them. |
| | `_targetResolver` | C: manual-target cards. |
| | `_tutorial` | O: enforce restrictions from an active TutorialManager. |
| CardTargetResolver | `_camera`, `_targetLayers` | R for pointer resolution: world camera and mask containing target **3D colliders**. |
| | `_enemySystem` | C: enemy validity/taunt checks; active with its board initialized. |

CardView references are explicit, not inferred from child order. Hand controller discovers CardPointerInput and optional feedback on each registered card's same root.
Pointer delivery requires EventSystem, Canvas GraphicRaycaster, and a raycastable card graphic.

## Settings

| Component | Fields / meaning |
| --- | --- |
| CardPointerInput | `_interactable`: pointer reporting; `SetInteractable(false)` cancels pointer state. |
| CardVisualFeedback | `_hoverScale = 1.08`, `_pressedScale = 0.97`: base-scale multipliers; `_duration = 0.12` seconds (`0` = instant); `_ease`. |
| CardFanLayout | `_width = 1000`: maximum center-to-center span, clamped to container width; `_arcHeight = 80`: downward edge offset, both in Canvas units; `_maxCardAngle = 15` degrees. `_showCurve` / `_curveColor`: selected Scene-view gizmo. |
| HandView | `maxHandSize = 10`: spacing reference count, **not capacity**. `doTweenUpdatePositionDuration = 0.15`: layout seconds. `cards`: read-only runtime list. |
| CardViewHoverSystem | `_offset = (140, 180)`: pointer offset; `_padding = (12, 12)`: parent-bound margins, in local Canvas units. |
| CardTargetResolver | `_rayDistance = 100`: maximum ray distance in world units. |

HandRoot needs non-zero width. Smaller hands occupy a centered portion of the fan; larger hands compress into its width.
Feedback owns only VisualRoot scale; the hand/controller own outer movement and ordering.

## Binding and ownership

```csharp
// Display only; no gameplay state is created or changed.
view.Setup(cardData);
view.SetDisplayedCost(2);
preview.Show(view.DisplayData, screenPosition, view.IsBlocked);
```

```csharp
// Runtime presentation; runtimeCard must already belong to CardSystem's hand.
view.Setup(runtimeCard);
yield return handView.AddCard(view);
```

- CardSystem owns gameplay membership; HandView owns view registration. Adding a child or calling `HandView.AddCard` does not add a gameplay Card.
- `Setup(CardData)` / `Display(CardDisplayData)` do not bind a playable runtime copy.
- HandView's **Preview Child Cards** button arranges direct child CardViews in Edit Mode, including inactive children. Supports Undo; does not populate the runtime list.
- CardView only displays states. Feedback methods do not subscribe to input themselves; the controller supplies those calls.
- Costs/states refresh on registration, action busy changes, and hero status notifications. External changes can call `CardPlayService.RefreshState()` or `CombatHandController.RefreshCardStates()`.

## Interaction contract

```text
CardPointerInput → CombatHandController → CardPlayService → gameplay backend
                         ├── HandView → CardFanLayout
                         ├── CardVisualFeedback
                         └── CardViewHoverSystem → preview CardView
```

- `ManualTargetEffect != null`: select/target, release over a valid combatant; otherwise restore.
- No manual effect: drag to Play Region to confirm; effects determine targets automatically.
- `CardTargetType` restricts Enemy/Hero/Any. `CardType` (Attack/Buff/Power) does not select the interaction mode.
- Play Region is a confirmation rectangle, not a rule checker. Drag Layer is a temporary rendering/input container, not gameplay storage.
- A release blocked by unrelated uGUI is rejected. Permitted UI hits must be within Play Region, including during manual targeting; keep modal UI outside it.
- Cancellation restores parent/anchors/pose/order, feedback, raycast state, and hand layout.

Controller notifications are local **C# events**, not Inspector UnityEvents:

| Event | Meaning |
| --- | --- |
| `PlayRequested(view, target)` | Release reached submission; not yet accepted. |
| `PlayAccepted(view)` | ActionSystem accepted the action; effects may still be resolving. |
| `PlayRejected(view, failure)` | Reason: `MissingBackend`, `NotInHand`, `Busy`, `Blocked`, `NotEnoughMana`, `InvalidTarget`, `TutorialBlocked`, or `InvalidDrop`. |
| `InteractionCancelled(view)` | Back, focus loss, disabled input, or interrupted interaction. |
| `TargetingChanged(view, screenPosition)`, `TargetingEnded()` | Hooks for a separate targeting presentation; no arrow drawn automatically. |

```csharp
void OnEnable() => controller.PlayRejected += OnRejected;
void OnDisable() => controller.PlayRejected -= OnRejected;
void OnRejected(CardView view, CardPlayFailure failure) =>
    Debug.Log($"{view.name}: {failure}");
```

## Presentation debug

[`CardViewAdder`](../../Assets/_Project/Scripts/Views/Debug/CardViewAdder.cs) runs in Editor Play Mode / development builds.

| Field | Contract |
| --- | --- |
| `_handView`, `_cardViewPrefab` | R: active hand and Canvas card prefab; use CardPointerInput on the prefab for interaction. |
| `_cardPool` | R: CardData assets sampled randomly with replacement; null entries ignored. |
| `_targetCount = 10` | Total registered view count to fill toward, including cards owned by other callers. Not a gameplay limit. |
| `_fillOnStart = true` | Fill on Start; false leaves spawning to the buttons. |
| `_previewCards` | O: explicit Editor-preview CardViews to hide on Start/spawn. Registered runtime views are skipped. |
| `_addedCard` | Read-only list of clones owned by this debug component. |

Buttons: **Add Random Card**, **Fill Hand**, **Clear Spawned Cards**. Adds run sequentially; repeated add/fill commands are ignored while filling. Clear cancels filling and removes only owned clones. Disabling the component clears its clones and restores captured preview active states; no hierarchy-wide deletion.

```csharp
adder.FillHand();       // Random views up to Target Count.
adder.RemoveAllCard();  // Clear only this adder's clones.
```

Each clone binds a separate runtime Card, but is registered only with HandView, not CardSystem. Leave `CombatHandController._playService` empty for isolated hover/drag checks; use CardSystem's injection path for actual playable cards.

## Current boundary

Scene/prefab wiring remains manual. Legacy draw/discard presentation, audio/VFX, and targeting-arrow rendering are not migrated here. The component refactor has not been verified in Play Mode.
