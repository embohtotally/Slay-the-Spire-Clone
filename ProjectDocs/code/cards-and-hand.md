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
| `CardTargetingPresenter` | Per hand rig, with explicit local references | Converts card/pointer screen positions into arrow-local UI coordinates. |
| `CanvasArrowView` | Full-stretch UI object in a targeting layer | Coordinates a line graphic and child Image head; owns path, pose and visibility, not artwork. |
| `CanvasArrowLineGraphic` | Full-stretch child of CanvasArrowView | Textured/tapered uGUI strip with standard Graphic tint, UI material and masking. |
| `CardViewHoverSystem` | Local preview owner, outside the preview card's hierarchy | One enlarged display-only card; **not a singleton**. |
| `CardPlayService` | Per battle, on a scene services object | Membership/cost/rule checks and action submission. |
| `CardTargetResolver` | Per battle, on a scene services object | Camera raycast and combatant validity/taunt checks. |
| `CardViewAdder` | Optional debug component on the hand rig | Random presentation cards and owned-card cleanup; not gameplay injection. |

None creates persistent objects. Hand-internal references can live in a prefab; scene gameplay references are assigned on the instance.

`CardData` is an authored asset; `Card` is a runtime copy; `CardDisplayData` is a value snapshot. Neither runtime type is a component.

```text
CardRoot [CardView, CardPointerInput, optional CardVisualFeedback]
└── VisualRoot [Images, TMP, optional state overlays]

HandRig [CombatHandController, optional CardTargetingPresenter]
├── HandRoot [HandView, CardFanLayout]
├── PlayRegion [RectTransform]
├── DragLayer [RectTransform, CanvasGroup]
├── PreviewLayer [RectTransform]
│   └── PreviewCard [CardView, CanvasGroup]
└── TargetingLayer [CanvasArrowView, full-stretch RectTransform]
    ├── Line [CanvasArrowLineGraphic, full-stretch RectTransform]
    └── Head [Image, fixed-size RectTransform]
```

Keep hand/drag/preview under the same Canvas setup. Later siblings normally render above earlier ones; put TargetingLayer after cards/previews to draw above them. The hover owner stays outside PreviewCard; no Layout Group should also position hand cards.

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
| CardTargetingPresenter | `_handController`, `_canvasArrow`, `_canvas` | R: local controller, active CanvasArrowView, and Canvas containing the hand cards. |
| | `_cardAnchor` | Normalized point in the CardView root rectangle; default `(0.5, 0.5)` (center). |
| CanvasArrowView | `_line`, `_head` | Assign the child line graphic and Image; head must not be on the arrow root. Either visual can be omitted. |
| | `_curveHeight` | Signed bend perpendicular to the chord, limited for short arrows; `0` = straight, default `60` UI units. |
| | `_headGap`, `_headAngleOffset` | Line gap before head pivot (`24` UI units), clamped for short arrows; artwork rotation offset (`0` degrees for +X, `-90` for +Y). |
| | `_previewInEditor`, `_previewStart`, `_previewEnd` | Editor-only sample visibility and local endpoints; ignored by runtime targeting. |
| CanvasArrowLineGraphic | `_startWidth`, `_endWidth`, `_segments` | Shaft widths (`8`, `3` UI units) and subdivisions (`2–128`, default `32`). No automatic width scaling. |
| | `_texture`, `_textureRepeatLength` | Optional standalone Texture2D strip; `0` stretches once, positive value sets UI units per repeat. Set texture Wrap Mode to Repeat for tiling. |
| | `color`, `material` | Standard Graphic tint and optional UI-compatible material; unset texture uses material texture or white. |
| Head Image | Sprite, color, material, RectTransform | Standard UI appearance and size. Pivot is placed at endpoint; coordinator changes only position, rotation, enabled and Raycast Target. |
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
                         ├── CardViewHoverSystem → preview CardView
                         └── CardTargetingPresenter → CanvasArrowView
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
| `TargetingChanged(view, screenPosition)`, `TargetingEnded()` | Manual-target feedback; CardTargetingPresenter subscribes locally. |

```csharp
void OnEnable() => controller.PlayRejected += OnRejected;
void OnDisable() => controller.PlayRejected -= OnRejected;
void OnRejected(CardView view, CardPlayFailure failure) =>
    Debug.Log($"{view.name}: {failure}");
```

### Manual-target arrow

Add CardTargetingPresenter to the hand rig. Under the Canvas, create a full-stretch CanvasArrowView root with two children:

| Object | Setup |
| --- | --- |
| Line | Full-stretch RectTransform, zero offsets, CanvasArrowLineGraphic (not Image). Assign a texture and/or UI material for artwork. |
| Head | Standard UI Image with artist-authored sprite, tint, material and fixed RectTransform size. For right-pointing artwork, pivot `(1, 0.5)` puts its tip at the endpoint. Up-pointing artwork can use pivot `(0.5, 1)` and Head Angle Offset `-90`. |

Assign both children to CanvasArrowView, and the root/controller/card Canvas to CardTargetingPresenter. Keep these objects active, outside hand/preview-only CanvasGroups and masks. Use unit local scale; the CanvasScaler controls overall sizing. Put Head after Line to render it above the strip.

- Uses controller pointer events, not additional input polling. Release, cancellation, or presenter disable hides the arrow.
- Converts screen positions into the arrow's RectTransform space; updates each LateUpdate so the origin follows card movement. No world camera or depth setting is needed for Overlay UI.
- Path/gap belong to CanvasArrowView; line width, texture, tint and material belong to Line; head sprite, size, pivot, tint and material belong to the Image. Head size/scale are never overwritten or automatically shrunk. Raycast Target is disabled automatically on both visuals.
- Use a standalone line texture rather than an atlas sprite. Texture U runs along sampled arc length and V across width, so repeat spacing stays consistent around bends. With no texture/material it draws a plain tinted strip.
- For visual authoring, enable **Preview In Editor** on CanvasArrowView. Select the root for **Start**, **End**, and perpendicular **Curve** Scene handles; select either child for normal visual editing. Enable Gizmos if handles are hidden. Changes update the preview live. Handles support Undo and prefab overrides; they appear only for a single selected arrow outside Play Mode.
- Preview endpoints are editor-only. The Curve handle edits the shared `_curveHeight` style used at runtime; endpoints do not change card/pointer positions. Turn preview off to hide the sample.
- Place the targeting layer after card/preview siblings for front rendering, or before them for back rendering. Separate/nested canvases still obey their sorting orders.
- Works without Play Service for presentation checks. Does not resolve/snap targets, draw automatic-target arrows, or execute effects.
- `CanvasArrowView.Show(startLocal, endLocal)` accepts root-local endpoints; `Hide()` hides the line mesh and Image without disabling their GameObjects. A world target can supply an endpoint by projecting its anchor to screen, then converting into arrow-local space.
- Migrating the all-procedural Canvas arrow: keep the root script and presenter reference; assign the new child visuals. Move width/tint/material choices onto Line and author the head Image. The root is now a MonoBehaviour, not a Graphic; its former Graphic appearance and generated-head fields are no longer used. Remove the root's leftover CanvasRenderer manually. Curve/preview field names and script GUID remain unchanged.
- Migrating the earlier world arrow: assign the new **Canvas Arrow** field and disable the old dedicated world-arrow object manually. The old `_arrow` reference is not reused across incompatible component types; `_worldCamera` and `_planeDistance` are removed. Controller/card Canvas references and the presenter script GUID remain unchanged. Legacy ArrowView/ManualTargetingSystem remain available for their existing callers.

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

Scene/prefab wiring remains manual. Legacy draw/discard presentation, audio/VFX, and automatic-target previews are not migrated here. The component refactor and manual-target arrow have not been verified in Play Mode.
