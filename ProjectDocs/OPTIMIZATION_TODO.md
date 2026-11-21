# Performance & FPS Optimization To-Do List

**Date Created:** 2026-11-21  
**Target Areas:** Card Dragging (Single-Target & Multi-Target), Hover Tooltips, Scene Transitions, Map Entrance, and Idle Combat Frame Time

---

## Phase 1 — High-Impact, Low-Risk Wins (Eliminates ~80% of Spikes)

- [ ] **Split `GameCanvas` into Nested Sub-Canvases (Canvas Batch Isolation)**
  - Add a nested `Canvas` component (no `CanvasScaler` needed; only add `GraphicRaycaster` where pointer input is required) to isolate dynamic UI layers from the static HUD:
    - [ ] `DragLayer` (own `Canvas`, no `GraphicRaycaster` needed during drag)
    - [ ] `HandView` (own `Canvas` + `GraphicRaycaster`)
    - [ ] `CombatTargetIndicators` in [CombatTargetingPresenter.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs) (own `Canvas`, no raycaster)
    - [ ] `EnemyIntentHUD` widgets layer in [EnemyIntentHUD.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/UI/Combat/EnemyIntentHUD.cs) (own `Canvas`, no raycaster)
    - [ ] `CombatKeywordTooltips` in [CombatKeywordTooltipUI.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs) (own `Canvas`, no raycaster)
    - [ ] `EndTurnButton` / `SmartGlowHalo` in [EndTurnButtonUI.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/UI/EndTurnButtonUI.cs) (own `Canvas`)
  - *Why:* Prevents Unity from rebuilding the entire combat HUD's vertex buffers every frame when dragging a card or animating enemy intents.

- [ ] **Switch All 12 Music MP3s to `Streaming` (`loadType = 2`)**
  - Update import settings on `Assets/_Project/Sounds/Music/*.mp3` from `DecompressOnLoad` (`loadType: 0`, `loadInBackground: 0`) to **`Streaming`** (Vorbis).
  - *Why:* Eliminates the 100–500 ms main-thread CPU/RAM spike caused by synchronously decoding 4–7 MB MP3s on every scene change.

- [ ] **Cache & Dirty-Check `CombatTargetingPresenter` + `CombatantView.ShowDamagePreview`**
  - In [CombatTargetingPresenter.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Views/CombatTargetingPresenter.cs):
    - [ ] Cache `(Card, CombatantView hovered, bool validLock, bool blockedByTaunt, bool inPlayRegion)` so `UpdateManualTargetReticles` and `OnCardDragUpdated` only recompute damage and update TMP text when the state actually changes (not on every pixel move).
    - [ ] Pulse reticles via `localScale` instead of mutating `sizeDelta` every frame in `LateUpdate()`.
    - [ ] Pre-warm 3–4 reticle slots in `Start()` to eliminate the first-drag instantiation hitch.
    - [ ] Cache `_camera` instead of falling back to `Camera.main` every frame.
  - In [CombatantView.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Views/CombatantView.cs):
    - [ ] Early-out in `ShowDamagePreview(int incomingDamage)` when `incomingDamage`, `CurrentHealth`, and `CurrentShield` have not changed.

---

## Phase 2 — Smooth Hovering & Zero-GC Combat Loops

- [ ] **Pool `CombatKeywordTooltipUI` & Pre-Compile Keyword Matching**
  - In [CombatKeywordTooltipUI.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/UI/Combat/CombatKeywordTooltipUI.cs):
    - [ ] Pre-create a fixed pool of 4 reusable tooltip cards and toggle `SetActive(true/false)` + update `.text`/`.color` instead of calling `Destroy()`/`Instantiate()` on every card hover.
    - [ ] Remove the 5 duplicate entries in `Catalog` (`Power`, `Scry`, `Discover`, `Status`, `Curse`).
    - [ ] Replace the 35 runtime `Regex.IsMatch` calls with pre-compiled `Regex` or fast word-boundary `IndexOf` scanning.

- [ ] **Cache Lookups & Guard Redundant TMP / DOTween Allocations**
  - In [CombatHandController.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Views/CombatHandController.cs):
    - [ ] Cache `HoverHitbox` and `Wrapper` `RectTransform`s inside `Binding` during `RegisterCard()` instead of calling `Transform.Find()` every frame in `LateUpdate()`.
  - In [CardView.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Views/CardView.cs):
    - [ ] Guard `SetText()` so `text.text` is only assigned when `!string.Equals(text.text, next, StringComparison.Ordinal)`.
  - In [CardSystem.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Systems/CardSystem.cs):
    - [ ] Cache `CombatHandController` reference instead of calling `FindFirstObjectByType<CombatHandController>()` on card state changes.
  - In [HandView.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Views/HandView.cs) & [CardVisualFeedback.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Views/CardVisualFeedback.cs):
    - [ ] Enable DOTween recycling (`.SetRecyclable(true)`) instead of `.SetRecyclable(false)`.
  - In [ArrowView.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Views/ArrowView.cs):
    - [ ] Avoid redundant `Update()` calls per frame and dirty-check `SetTint()`.

---

## Phase 3 — Buttery Scene Transitions & Map Rendering

- [ ] **Optimize Map Trail Dots & Layer Isolation**
  - In [ProceduralMapRendererUI.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/UI/Map/ProceduralMapRendererUI.cs):
    - [ ] Put `_linesLayer` and `_nodesLayer` on separate nested `Canvas`es.
    - [ ] Pool `TrailDot` GameObjects (or render dotted Bezier trails with a lightweight custom `MaskableGraphic` mesh) instead of instantiating 150–300+ `Image` GameObjects in a single frame.

- [ ] **Pre-Warm Blink Eyelids & Remove `Resources.FindObjectsOfTypeAll` Scans**
  - In [BlinkSceneTransition.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/General/Scenes/BlinkSceneTransition.cs):
    - [ ] Pre-warm the curved eyelid sprites at startup (or reduce from `512 × 256` to `256 × 128` bilinear—4× faster CPU generation, visually identical).
    - [ ] Only update `_topLidRt.sizeDelta` / `_bottomLidRt.sizeDelta` when screen height changes rather than every blink frame.
  - In [EndTurnButtonUI.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/UI/EndTurnButtonUI.cs):
    - [ ] Replace `Resources.FindObjectsOfTypeAll<Sprite>()` with a cached procedural glow sprite or serialized reference.

---

## Verification Checklist (After Implementing)
- [ ] Profile `Game.unity` in Unity Profiler while dragging single-target and AoE/multi-target cards: confirm `Canvas.BuildBatch` only rebuilds the isolated sub-Canvas and zero `GC.Alloc` spikes occur per mouse move.
- [ ] Profile scene transitions (`Map` ↔ `Game` ↔ `RunRewards` ↔ `Merchant`): confirm no main-thread audio decompression spike.
- [ ] Recompile with `0` errors and run Play Mode verification with `0` console errors/warnings.

