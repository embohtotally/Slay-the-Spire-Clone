using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [DisallowMultipleComponent]
    public class CardViewHoverSystem : MonoBehaviour
    {
        [Tooltip("Separate display-only card; keep this component outside its hierarchy.")]
        [SerializeField] private CardView cardViewHover;
        [SerializeField] private CanvasGroup _inputGroup;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Vector2 _offset = new Vector2(140f, 180f);
        [SerializeField] private Vector2 _padding = new Vector2(12f, 12f);

        private readonly Vector3[] _corners = new Vector3[4];

        private void Awake() => Hide();
        private void OnDisable() => Hide();

        public bool Show(CardDisplayData data, Vector2 screenPosition, bool blocked = false)
        {
            if (cardViewHover == null || _inputGroup == null || _canvas == null)
            {
                Debug.LogWarning("Assign a Canvas preview CardView, CanvasGroup and Canvas.", this);
                return false;
            }
            if (transform.IsChildOf(cardViewHover.transform) ||
                !cardViewHover.transform.IsChildOf(_inputGroup.transform))
            {
                Debug.LogWarning("Keep the hover controller outside the preview, with CanvasGroup on the preview or its parent.", this);
                return false;
            }
            if (cardViewHover.transform is not RectTransform rect ||
                rect.parent is not RectTransform bounds) return false;
            if (_canvas.renderMode != RenderMode.ScreenSpaceOverlay && _canvas.worldCamera == null)
                return false;

            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                bounds, screenPosition, camera, out Vector2 position)) return false;

            _inputGroup.blocksRaycasts = false;
            _inputGroup.interactable = false;
            cardViewHover.Display(data);
            cardViewHover.SetBlocked(blocked);
            cardViewHover.gameObject.SetActive(true);
            rect.anchorMin = rect.anchorMax = bounds.pivot;
            rect.anchoredPosition3D = new Vector3(position.x + _offset.x, position.y + _offset.y, 0f);

            rect.GetWorldCorners(_corners);
            Vector2 min = bounds.InverseTransformPoint(_corners[0]);
            Vector2 max = min;
            for (int i = 1; i < _corners.Length; i++)
            {
                Vector2 corner = bounds.InverseTransformPoint(_corners[i]);
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }

            Rect area = bounds.rect;
            rect.anchoredPosition += new Vector2(
                GetShift(min.x, max.x, area.xMin + _padding.x, area.xMax - _padding.x),
                GetShift(min.y, max.y, area.yMin + _padding.y, area.yMax - _padding.y));
            return true;
        }

        public void Hide()
        {
            if (cardViewHover != null) cardViewHover.gameObject.SetActive(false);
        }

        private static float GetShift(float min, float max, float lower, float upper)
        {
            if (max - min > upper - lower) return (lower + upper - min - max) * 0.5f;
            if (min < lower) return lower - min;
            return max > upper ? upper - max : 0f;
        }
    }
}
