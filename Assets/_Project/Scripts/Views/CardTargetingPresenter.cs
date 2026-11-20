using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    [DisallowMultipleComponent]
    public class CardTargetingPresenter : MonoBehaviour
    {
        [SerializeField] private CombatHandController _handController;
        [SerializeField] private CanvasArrowView _canvasArrow;
        [Tooltip("Canvas containing the hand cards.")]
        [SerializeField] private Canvas _canvas;

        [Tooltip("Normalized point within the card root rectangle; center is (0.5, 0.5).")]
        [SerializeField] private Vector2 _cardAnchor = new Vector2(0.5f, 0.5f);
        [Tooltip("Anchor within the animated Wrapper instead of the card root.")]
        [SerializeField] private bool _useVisualRoot;

        private CardView _card;
        private Vector2 _pointerPosition;
        private Transform _target;
        private Camera _targetCamera;

        private void OnEnable()
        {
            Hide();
            if (_handController == null || _canvasArrow == null || _canvas == null)
            {
                Debug.LogWarning("Assign Hand Controller, Canvas Arrow and card Canvas to CardTargetingPresenter.", this);
                return;
            }

            _handController.TargetingChanged += OnTargetingChanged;
            _handController.TargetingEnded += Hide;
        }

        private void OnDisable()
        {
            if (_handController != null)
            {
                _handController.TargetingChanged -= OnTargetingChanged;
                _handController.TargetingEnded -= Hide;
            }
            Hide();
        }

        private void OnTargetingChanged(CardView card, Vector2 pointerPosition)
        {
            _card = card;
            _pointerPosition = pointerPosition;
        }

        public void SetTargetFeedback(Transform target, Camera worldCamera, Color tint)
        {
            _target = target;
            _targetCamera = worldCamera;
            if (_canvasArrow != null) _canvasArrow.SetTint(tint);
        }

        public void ClearTargetFeedback()
        {
            _target = null;
            _targetCamera = null;
            if (_canvasArrow != null) _canvasArrow.SetTint(Color.white);
        }

        private void LateUpdate()
        {
            if (_card == null)
            {
                if (_canvasArrow != null) _canvasArrow.Hide();
                return;
            }
            if (!_card.isActiveAndEnabled || _handController == null || !_handController.isActiveAndEnabled)
            {
                Hide();
                return;
            }
            if (_canvasArrow == null || _canvas == null ||
                _card.transform is not RectTransform rect)
            {
                Hide();
                return;
            }

            if (!TryGetEventCamera(_canvas, out Camera uiCamera) ||
                !TryGetEventCamera(_canvasArrow.Canvas, out Camera arrowCamera))
            {
                Hide();
                return;
            }

            if (_useVisualRoot && rect.Find("Wrapper") is RectTransform visualRoot)
                rect = visualRoot;
            Rect area = rect.rect;
            Vector3 localStart = new Vector3(
                Mathf.LerpUnclamped(area.xMin, area.xMax, _cardAnchor.x),
                Mathf.LerpUnclamped(area.yMin, area.yMax, _cardAnchor.y), 0f);
            Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(localStart));
            Vector2 endScreen = _pointerPosition;
            if (_target != null && _targetCamera != null && _target.gameObject.activeInHierarchy)
            {
                Vector3 targetScreen = _targetCamera.WorldToScreenPoint(_target.position);
                if (targetScreen.z > 0f) endScreen = targetScreen;
            }

            RectTransform arrowRect = _canvasArrow.RectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    arrowRect, startScreen, arrowCamera, out Vector2 startLocal) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    arrowRect, endScreen, arrowCamera, out Vector2 endLocal))
            {
                _canvasArrow.Hide();
                return;
            }

            _canvasArrow.Show(startLocal, endLocal);
        }

        private static bool TryGetEventCamera(Canvas canvas, out Camera camera)
        {
            camera = null;
            if (canvas == null) return false;
            Canvas root = canvas.rootCanvas;
            if (root.renderMode == RenderMode.ScreenSpaceOverlay) return true;
            camera = root.worldCamera;
            return camera != null;
        }

        private void Hide()
        {
            _card = null;
            ClearTargetFeedback();
            if (_canvasArrow != null) _canvasArrow.Hide();
        }
    }
}
