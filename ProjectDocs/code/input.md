# Input framework

[Code index](README.md) · [Source](../../Assets/_Project/Scripts/Input/)

Namespace: `PlushieOdyssey.Inputs`. Routes Back to the selected UI widget first, then the active screen.

## Setup

1. Add active `GameInputReader` and `InputContextRouter` components under the persistent GameManager. Separate child objects are supported.
2. Assign Reader **Actions** to
   [`InputSystem_Actions.inputactions`](../../Assets/_Project/InputSystem_Actions.inputactions).
3. Leave **Back Action** empty for `UI/Cancel`, or assign a Button action in the same asset's `UI` map.
4. Assign Router **Reader**, or leave empty to find it under GameManager.
5. Add `UIInputBridge` to the scene's EventSystem with an enabled `InputSystemUIInputModule`; disable any legacy `StandaloneInputModule`.
6. Configure menu navigation and a first-selected button for keyboard/gamepad use.

Use one Reader/Router pair and one active scene EventSystem. Initialize services before screen scripts.
The shared `UI` map needs `Point`, `Click`, `Navigate`, `Submit`, and the Back action.

**Do not assign UI module Cancel separately.** The bridge keeps it null so one press cannot close both a widget and its screen.

## Add Back handling to a screen

Implement `IBackHandler`, register when enabled, and dispose when disabled:

```csharp
using System;
using PlushieOdyssey.Inputs;
using UnityEngine;

namespace PlushieOdyssey.UI
{
    public class ExampleScreen : MonoBehaviour, IBackHandler
    {
        private IDisposable _lease;

        private void OnEnable()
        {
            var router = InputContextRouter.FromBootstrap;
            if (router != null && router.isActiveAndEnabled)
                _lease = router.EnterContext(this, InputContext.Menu, this);
        }

        private void OnDisable()
        {
            _lease?.Dispose();
            _lease = null;
        }

        public bool TryHandleBack()
        {
            gameObject.SetActive(false);
            return true;
        }
    }
}
```

Change `InputContext.Menu` to the required context. Replace `SetActive(false)` with your screen's close/navigation action.

## Customize routing

| Context                  | Priority |
| ------------------------ | -------: |
| `None`, `Menu`, `Combat` |        0 |
| `Dialogue`               |      100 |
| `Modal`                  |      200 |

- Highest priority wins; newest registration wins ties. Use `priorityOffset` in `EnterContext(...)` to adjust.
- Return `true` when Back is handled; `false` to ignore it. **Back never falls through to lower contexts.**
- Selected dropdowns/input fields consume Back before the screen. A bound bridge is required.
- Change key/button bindings in the shared action asset, not through direct device polling.
- `Combat` / `Dialogue` contexts enable their matching map if present; all other non-UI maps are disabled, including starter `Player`.
- Combat disables uGUI navigation/Submit events; pointer input remains available.

## Reference

| Type                 | Purpose                                              |
| -------------------- | ---------------------------------------------------- |
| `GameInputReader`    | Read shared actions                                  |
| `InputContextRouter` | Select active context and route Back                 |
| `UIInputBridge`      | Connect EventSystem actions and handle widget Cancel |
| `IBackHandler`       | Screen's Back callback                               |
| `InputContextStack`  | Internal priority/lifetime handling                  |

Example in use: [`MainMenuNavigation.cs`](../../Assets/_Project/Scripts/Menus/MainMenuNavigation.cs).
**Migration is partial:** other legacy polling and gamepad combat remain unmigrated; the asset currently has no Combat/Dialogue maps.
