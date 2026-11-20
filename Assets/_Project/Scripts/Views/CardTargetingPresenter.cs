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

        private CardView _card;
        private Vector2 _pointerPosition;

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

            Rect area = rect.rect;
            Vector3 localStart = new Vector3(
                Mathf.LerpUnclamped(area.xMin, area.xMax, _cardAnchor.x),
                Mathf.LerpUnclamped(area.yMin, area.yMax, _cardAnchor.y), 0f);
            Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(localStart));

            RectTransform arrowRect = _canvasArrow.RectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    arrowRect, startScreen, arrowCamera, out Vector2 startLocal) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    arrowRect, _pointerPosition, arrowCamera, out Vector2 endLocal))
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
            if (_canvasArrow != null) _canvasArrow.Hide();
        }
    }
}
