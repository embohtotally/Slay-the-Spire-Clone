using System;
using System.Collections;
using PlushieOdyssey.Inputs;
using PlushieOdyssey.Tweens;
using UnityEngine;

namespace PlushieOdyssey.UI.Menus
{
    /// <summary>
    /// Scene-local navigation between the main menu, settings, credits, or others.
    /// Owns panel playback; buttons only request a destination.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuNavigation : MonoBehaviour, IBackHandler
    {
        public enum Page
        {
            MainMenu,
            Settings,
            Credits
        }

        [Header("Panel Tweens")]
        [SerializeField] private PanelTransitionTween _mainMenuPanel = null;
        [SerializeField] private PanelTransitionTween _settingsPanel = null;
        [SerializeField] private PanelTransitionTween _creditsPanel = null;

        [Header("Startup")]
        [SerializeField] private bool _animateMainMenuOnStart = true;

        [Header("Input")]
        [Tooltip("Optional explicit router reference. Otherwise resolves through the existing GameManager bootstrap.")]
        [SerializeField] private InputContextRouter _inputRouter = null;

        [Header("Optional Keyboard / Gamepad Focus")]
        [SerializeField] private GameObject _mainMenuFirstSelected = null;
        [SerializeField] private GameObject _settingsFirstSelected = null;
        [SerializeField] private GameObject _creditsFirstSelected = null;

        private Coroutine _transition;
        private bool _started;
        private int _transitionVersion;
        private IDisposable _inputContext;

        // CurrentPage remains the last settled page until both animations finish.
        public Page CurrentPage { get; private set; } = Page.MainMenu;
        public bool IsTransitioning { get; private set; }

        private void Start()
        {
            if (!ValidateBindings())
            {
                enabled = false;
                return;
            }

            _started = true;
            RegisterInput();
            _settingsPanel?.CloseInstant();
            _creditsPanel?.CloseInstant();

            if (_animateMainMenuOnStart)
            {
                _mainMenuPanel.CloseInstant();
                BeginTransition(null, _mainMenuPanel, Page.MainMenu);
            }
            else
            {
                RestoreCurrentPage();
            }
        }

        private void OnEnable()
        {
            if (_started)
            {
                RestoreCurrentPage();
                RegisterInput();
            }
        }

        private void OnDisable()
        {
            _inputContext?.Dispose();
            _inputContext = null;
            _transitionVersion++;
            if (_transition != null) StopCoroutine(_transition);
            _transition = null;
            IsTransitioning = false;

            if (!_started) return;
            _mainMenuPanel?.Pause();
            _settingsPanel?.Pause();
            _creditsPanel?.Pause();
        }

        private void RegisterInput()
        {
            if (_inputContext != null) return;
            if (_inputRouter == null) _inputRouter = InputContextRouter.FromBootstrap;
            if (_inputRouter == null || !_inputRouter.isActiveAndEnabled)
            {
                Debug.LogWarning("[MainMenuNavigation] Add InputContextRouter to the GameManager prefab or assign one. Button navigation still works; action-based Back needs the router and an EventSystem UIInputBridge.", this);
                return;
            }
            _inputContext = _inputRouter.EnterContext(this, InputContext.Menu, this);
        }

        public bool TryHandleBack()
        {
            if (!_started || !isActiveAndEnabled || IsTransitioning || CurrentPage == Page.MainMenu) return false;
            Back();
            return true;
        }

        public void ShowMainMenu() => NavigateTo(Page.MainMenu);
        public void ShowSettings() => NavigateTo(Page.Settings);
        public void ShowCredits() => NavigateTo(Page.Credits);
        public void Back() => ShowMainMenu();

        private void NavigateTo(Page page)
        {
            // Requests during a transition are ignored, not queued or replayed later.
            if (!Application.isPlaying || !isActiveAndEnabled || !_started || IsTransitioning || page == CurrentPage) return;

            PanelTransitionTween destination = GetPanel(page);
            if (destination == null || !destination.isActiveAndEnabled)
            {
                Debug.LogWarning($"[MainMenuNavigation] Assign an active {page} panel tween driver before navigating to it.", this);
                return;
            }

            BeginTransition(GetPanel(CurrentPage), destination, page);
        }

        private void BeginTransition(PanelTransitionTween source, PanelTransitionTween destination, Page page)
        {
            int version = ++_transitionVersion;
            IsTransitioning = true;
            ClearSelection();
            if (!IsCurrentTransition(version)) return;
            Coroutine transition = StartCoroutine(TransitionRoutine(source, destination, page, version));
            if (IsCurrentTransition(version)) _transition = transition;
        }

        private IEnumerator TransitionRoutine(PanelTransitionTween source, PanelTransitionTween destination, Page page, int version)
        {
            // Start together, matching the old pair of button callbacks.
            if (source != null) source.Close();
            if (!IsCurrentTransition(version)) yield break;
            destination.Open();
            if (!IsCurrentTransition(version)) yield break;

            // Always yield once so the coroutine handle is assigned before it can finish.
            yield return null;

            while (IsCurrentTransition(version) && destination != null && destination.isActiveAndEnabled &&
                   (destination.IsPlaying || (source != null && source.IsPlaying)))
            {
                yield return null;
            }

            if (!IsCurrentTransition(version)) yield break;

            if (destination == null || !destination.isActiveAndEnabled)
            {
                Debug.LogWarning("[MainMenuNavigation] The destination driver was disabled or destroyed during navigation. Restoring the previous page.", this);
                _transition = null;
                IsTransitioning = false;
                RestoreCurrentPage();
                yield break;
            }

            // Normalize the final poses without emitting duplicate tween completion events.
            // This also settles a tween that was externally paused while this controller owns it.
            if (source != null) source.CloseInstant();
            destination.OpenInstant();
            CurrentPage = page;
            _transition = null;
            IsTransitioning = false;
            SelectCurrentPage();
        }

        private void RestoreCurrentPage()
        {
            PanelTransitionTween current = GetPanel(CurrentPage);
            if (current == null || !current.isActiveAndEnabled) CurrentPage = Page.MainMenu;
            SetPanelState(_mainMenuPanel, CurrentPage == Page.MainMenu);
            SetPanelState(_settingsPanel, CurrentPage == Page.Settings);
            SetPanelState(_creditsPanel, CurrentPage == Page.Credits);
            SelectCurrentPage();
        }

        private bool IsCurrentTransition(int version)
        {
            return version == _transitionVersion && IsTransitioning && isActiveAndEnabled;
        }

        private static void SetPanelState(PanelTransitionTween panel, bool open)
        {
            if (panel == null) return;
            if (open) panel.OpenInstant();
            else panel.CloseInstant();
        }

        private PanelTransitionTween GetPanel(Page page)
        {
            return page switch
            {
                Page.MainMenu => _mainMenuPanel,
                Page.Settings => _settingsPanel,
                Page.Credits => _creditsPanel,
                _ => null
            };
        }

        private bool ValidateBindings()
        {
            if (_mainMenuPanel == null || !_mainMenuPanel.isActiveAndEnabled)
            {
                Debug.LogError("[MainMenuNavigation] Assign an active main-menu panel tween driver. Keep navigation and tween drivers outside content that gets deactivated.", this);
                return false;
            }

            if ((_settingsPanel != null && _settingsPanel == _mainMenuPanel) ||
                (_creditsPanel != null && (_creditsPanel == _mainMenuPanel || _creditsPanel == _settingsPanel)))
            {
                Debug.LogError("[MainMenuNavigation] Each page must use a different panel tween driver.", this);
                return false;
            }

            return true;
        }

        private static void ClearSelection()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        private void SelectCurrentPage()
        {
            if (UnityEngine.EventSystems.EventSystem.current == null) return;

            GameObject selection = CurrentPage switch
            {
                Page.MainMenu => _mainMenuFirstSelected,
                Page.Settings => _settingsFirstSelected,
                Page.Credits => _creditsFirstSelected,
                _ => null
            };

            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(
                selection != null && selection.activeInHierarchy ? selection : null);
        }
    }
}
