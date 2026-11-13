# Tween framework

[Code index](README.md) · [Source](../../Assets/_Project/Scripts/Tweens/)

Namespace: `PlushieOdyssey.Tweens`. Requires DOTween and NaughtyAttributes.

## BaseTween: shared setup

[`BaseTween`](../../Assets/_Project/Scripts/Tweens/BaseTween.cs) handles playback, settings, events, and cleanup for all tween components.

1. Add a concrete tween component, not `BaseTween` itself.
2. Assign **Target** (`RectTransform`); empty uses the component's own RectTransform.
3. Set **Duration** (default: 1 second) and effect-specific fields.
4. Enable **Play On Enable** for automatic playback; otherwise call it from code or a UnityEvent.

### Customize in the Inspector

- **Change Base Settings:** show ease, delay, loops, and update options. Loop count `-1` means infinite.
- **Is Independent Update:** animate while `Time.timeScale = 0`.
- **Change Lifetime Settings → Set An Id:** optionally assign a DOTween ID.
- **Events:** On Play, On Reverse Play, On Complete, On Reverse Complete.
- **Apply Default Tween Settings** (component context menu): restore shared defaults without clearing targets, duration, or events.

**The Change Settings toggles only hide fields. Saved settings still apply. Configure before Play Mode.**

### Playback controls

| Call | Use |
| --- | --- |
| `Play()` / `PlayReverse()` | Play forward / backward |
| `Restart()` / `RestartReverse()` | Play from the start / end |
| `Pause()` | Hold current pose |
| `Stop()` | Reset to start; panels close instantly instead |
| `SetToStart()` / `SetToEnd()` | Snap without events |
| `Complete()` / `CompleteReverse()` | Snap to end / start and fire completion event |
| `IsPlaying` | Check playback state |

Playback needs an active driver in Play Mode. Disabling pauses; destroying cleans up.
Avoid multiple scripts/animators changing the same property.

## Available effects

| Component | Use / main fields |
| --- | --- |
| `SlideTransitionTween` | Slide by **Offset**, or left by one Canvas width with **Use Canvas Width** |
| `PanelTransitionTween` | Open/close UI panels; see below |
| `CanvasGroupFadeTween` | Fade a **CanvasGroup** between **From Alpha** and **To Alpha** |
| `AnchoredOffsetLoopTween` | Move back and forth; optional **Second Target** with its own offset |
| `RotationLoopTween` | Continuous local rotation; edit **Rotation** |

### PanelTransitionTween

- **Target:** moving content. Set **Open Position**, **Open Start Offset**, and **Closed Offset**.
- **Fade Group:** opacity target, e.g. dim background. **Input Group:** common root CanvasGroup for interaction; keep its alpha at `1`.
- Use `Open()` / `Close()` to animate; `OpenInstant()` / `CloseInstant()` to set state without events.
- With **Manage Input**, interaction is disabled during transitions; closed panels release raycast blocking.
- Keep the driver outside Target's hierarchy when **Deactivate When Closed** is enabled.
- Loops, relative movement, and speed-based timing are not supported.

## Create a custom tween

Inherit `BaseTween` and return a DOTween tween from `CreateTween()`:

```csharp
using DG.Tweening;
using UnityEngine;

namespace PlushieOdyssey.Tweens
{
    public class ScaleTween : BaseTween
    {
        [SerializeField] private Vector3 _endScale = Vector3.one * 1.1f;

        protected override Tween CreateTween() => Target.DOScale(_endScale, Duration);
    }
}
```

For custom defaults, override `ResetTweenSettings()`, call base, then `ConfigureBaseSettings(...)`.
If overriding Unity lifecycle methods, keep the base calls.
