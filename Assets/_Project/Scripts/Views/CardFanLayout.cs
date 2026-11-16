using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class CardFanLayout : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        [Tooltip("Maximum span between card centers, in Canvas units.")]
        private float _width = 1000f;

        [SerializeField, Min(0f)]
        [Tooltip("How far the outer cards sit below the center, in Canvas units.")]
        private float _arcHeight = 80f;

        [SerializeField, Range(0f, 90f)]
        [Tooltip("Maximum outward card rotation, in degrees.")]
        private float _maxCardAngle = 15f;

        [Header("Editor Preview")]
        [SerializeField] private bool _showCurve = true;
        [SerializeField] private Color _curveColor = Color.cyan;

        public RectTransform RectTransform => (RectTransform)transform;

        public void GetCardPose(int index, int cardCount, int referenceCardCount,
            out Vector2 anchoredPosition, out float angle)
        {
            if (cardCount <= 1)
            {
                anchoredPosition = Vector2.zero;
                angle = 0f;
                return;
            }

            int intervals = Mathf.Max(1, Mathf.Max(referenceCardCount, cardCount) - 1);
            float offset = index - (cardCount - 1) * 0.5f;
            float normalizedOffset = Mathf.Clamp(offset * 2f / intervals, -1f, 1f);

            anchoredPosition = GetCurvePosition(normalizedOffset);
            angle = -_maxCardAngle * normalizedOffset;
        }

        private Vector2 GetCurvePosition(float normalizedOffset)
        {
            float width = Mathf.Min(_width, Mathf.Max(0f, RectTransform.rect.width));
            return new Vector2(
                normalizedOffset * width * 0.5f,
                -_arcHeight * normalizedOffset * normalizedOffset);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_showCurve) return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = _curveColor;

            const int segments = 48;
            Vector3 center = RectTransform.rect.center;
            Vector3 previousPoint = center + (Vector3)GetCurvePosition(-1f);
            for (int i = 1; i <= segments; i++)
            {
                float normalizedOffset = -1f + 2f * i / segments;
                Vector3 point = center + (Vector3)GetCurvePosition(normalizedOffset);
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
    }
}
