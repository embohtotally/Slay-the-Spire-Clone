using PlushieOdyssey.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Inputs
{
    /// <summary>Owns context leases and routes each Back press to one UI widget or active context.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-190)]
    public class InputContextRouter : MonoBehaviour
    {
        [Tooltip("Optional explicit reference. Otherwise searches this object's children, then the owning GameManager hierarchy (including sibling service objects).")]
        [SerializeField] private GameInputReader _reader = null;

        private readonly InputContextStack _contexts = new();
        private readonly List<UIInputBridge> _bridges = new();
        private InputContextStack.Entry _pendingContext;
        private UIInputBridge.BackCapture _pendingWidget;
        private bool _backPending;
        private bool _subscribed;
        private bool _warnedMissingBridge;

        public InputContext CurrentContext { get; private set; } = InputContext.None;
        public GameInputReader Reader => _reader;

        // Resolve through the existing composition root, not a second persistent singleton.
        public static InputContextRouter FromBootstrap => GameManager.Instance != null
            ? GameManager.Instance.GetComponentInChildren<InputContextRouter>(true) : null;

        private void OnEnable()
        {
            if (!TryInitialize()) return;
            _reader.BackRequested += OnBackRequested;
            _contexts.Changed += RefreshContext;
            _subscribed = true;
            RefreshContext();
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                _reader.BackRequested -= OnBackRequested;
                _contexts.Changed -= RefreshContext;
            }
            _subscribed = false;
            ClearPendingBack();
            CurrentContext = InputContext.None;
            if (_reader != null) _reader.SetContext(InputContext.None);
            ApplyUIContext();
            // Keep live leases so re-enabling the service restores the current scene's context.
        }

        private void OnDestroy() => _contexts.Clear();

        public bool TryInitialize()
        {
            if (_reader == null) _reader = GetComponentInChildren<GameInputReader>(true);
            if (_reader == null)
            {
                GameManager owner = GetComponentInParent<GameManager>(true);
                if (owner != null) _reader = owner.GetComponentInChildren<GameInputReader>(true);
            }
            if (_reader != null && _reader.isActiveAndEnabled && _reader.TryInitialize()) return true;
            Debug.LogError("[InputContextRouter] Assign an active, initialized GameInputReader or place one under the owning GameManager hierarchy.", this);
            return false;
        }

        public IDisposable EnterContext(MonoBehaviour owner, InputContext context,
            IBackHandler backHandler = null, int priorityOffset = 0)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            int priority = context switch
            {
                InputContext.Dialogue => 100,
                InputContext.Modal => 200,
                _ => 0
            };
            return _contexts.Enter(() => owner != null && owner.isActiveAndEnabled,
                context, backHandler, priority + priorityOffset);
        }

        internal void AttachUI(UIInputBridge bridge)
        {
            if (!_bridges.Contains(bridge)) _bridges.Add(bridge);
            _warnedMissingBridge = false;
            bridge.ApplyContext(CurrentContext);
            _reader.EnsureBackEnabled();
        }

        internal void DetachUI(UIInputBridge bridge)
        {
            _bridges.Remove(bridge);
            ClearPendingBack();
        }

        private UIInputBridge ActiveBridge
        {
            get
            {
                for (int i = _bridges.Count - 1; i >= 0; i--)
                {
                    UIInputBridge bridge = _bridges[i];
                    if (bridge != null && bridge.isActiveAndEnabled && bridge.IsBound) return bridge;
                }
                return null;
            }
        }

        private void RefreshContext()
        {
            if (!isActiveAndEnabled || !TryInitialize()) return;
            CurrentContext = _contexts.Active?.Context ?? InputContext.None;
            _reader.SetContext(CurrentContext);
            ApplyUIContext();
        }

        private void ApplyUIContext()
        {
            foreach (UIInputBridge bridge in _bridges)
            {
                if (bridge != null) bridge.ApplyContext(CurrentContext);
            }
        }

        private void OnBackRequested()
        {
            if (_backPending || !isActiveAndEnabled) return;
            InputContextStack.Entry active = _contexts.Active;
            UIInputBridge bridge = ActiveBridge;
            if (bridge == null)
            {
                if (active != null && !_warnedMissingBridge)
                    Debug.LogWarning("[InputContextRouter] Add UIInputBridge to the scene EventSystem. Back is ignored until it can have a single owner instead of also firing the module's Cancel.", this);
                if (active != null) _warnedMissingBridge = true;
                return;
            }

            _pendingContext = active;
            _pendingWidget = bridge.CaptureBack();
            _backPending = true;
        }

        private void LateUpdate()
        {
            _contexts.PruneInactive();
            if (!_backPending) return;

            InputContextStack.Entry context = _pendingContext;
            UIInputBridge.BackCapture widget = _pendingWidget;
            ClearPendingBack();

            // Do not close a newly opened panel if the UI/context changed during this same press.
            if (context != _contexts.Active || (context != null && !context.IsActive)) return;
            if (widget.TryHandle()) return;
            // Never broadcast to lower contexts, even if the active context declines Back.
            context?.Handler?.TryHandleBack();
        }

        private void ClearPendingBack()
        {
            _backPending = false;
            _pendingContext = null;
            _pendingWidget = default;
        }
    }
}
