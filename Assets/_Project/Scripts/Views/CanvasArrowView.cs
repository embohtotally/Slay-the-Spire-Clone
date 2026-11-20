using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class CanvasArrowView : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private CanvasArrowLineGraphic _line;
        [Tooltip("Child Image; its pivot is placed at the endpoint.")]
        [SerializeField] private UnityEngine.UI.Image _head;

        [Header("Path (UI Units)")]
        [Tooltip("Signed bend perpendicular to the arrow; zero draws a straight arrow.")]
        [SerializeField] private float _curveHeight = 60f;
        [Tooltip("Distance from the head pivot back to the end of the line.")]
        [SerializeField, Min(0f)] private float _headGap = 24f;
        [Tooltip("Rotation offset in degrees; use -90 for artwork pointing up.")]
        [SerializeField] private float _headAngleOffset;

#if UNITY_EDITOR
        [Header("Editor Preview")]
        [Tooltip("Draw sample endpoints outside Play Mode; runtime targeting ignores them.")]
        [SerializeField] private bool _previewInEditor;
        [SerializeField] private Vector2 _previewStart = new Vector2(-200f, -100f);
        [SerializeField] private Vector2 _previewEnd = new Vector2(200f, 100f);

        private bool _wasPlaying;

        public bool PreviewInEditor => _previewInEditor;

        public void GetEditorPreview(out Vector2 startLocal, out Vector2 endLocal, out float curveHeight)
        {
            startLocal = _previewStart;
            endLocal = _previewEnd;
            curveHeight = _curveHeight;
        }

        public void SetEditorPreview(Vector2 startLocal, Vector2 endLocal, float curveHeight)
        {
            _previewStart = startLocal;
            _previewEnd = endLocal;
            _curveHeight = curveHeight;
            RefreshVisuals();
        }
#endif

        private Vector2 _start;
        private Vector2 _end;
        private bool _visible;

        public RectTransform RectTransform => (RectTransform)transform;
        public Canvas Canvas => GetComponentInParent<Canvas>();

        private void OnEnable() => RefreshVisuals();

        private void OnDisable()
        {
            _visible = false;
            HideVisuals();
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR
            bool playing = Application.IsPlaying(gameObject);
            if (_wasPlaying != playing)
            {
                _wasPlaying = playing;
                _visible = false;
            }
#endif
            RefreshVisuals();
        }

        public void Show(Vector2 startLocal, Vector2 endLocal)
        {
            _start = startLocal;
            _end = endLocal;
            _visible = true;
            RefreshVisuals();
        }

        public void Hide()
        {
            _visible = false;
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            Vector2 start = _start;
            Vector2 end = _end;
            bool visible = _visible;
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                visible = _previewInEditor;
                start = _previewStart;
                end = _previewEnd;
            }
#endif
            Vector2 offset = end - start;
            float length = offset.magnitude;
            if (!isActiveAndEnabled || !visible || length < 0.01f)
            {
                HideVisuals();
                return;
            }

            Vector2 direction = offset / length;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float bend = Mathf.Clamp(_curveHeight, -length * 0.5f, length * 0.5f);
            Vector2 control = (start + end) * 0.5f + normal * (bend * 2f);
            Vector2 endDirection = (end - control).normalized;
            RectTransform rect = RectTransform;
            if (_line != null)
            {
                float gap = _head != null ? Mathf.Clamp(_headGap, 0f, length * 0.5f) : 0f;
                Vector2 lineEnd = end - endDirection * gap;
                RectTransform lineRect = _line.rectTransform;
                _line.Show(
                    lineRect.InverseTransformPoint(rect.TransformPoint(start)),
                    lineRect.InverseTransformPoint(rect.TransformPoint(control)),
                    lineRect.InverseTransformPoint(rect.TransformPoint(lineEnd)));
            }
            if (_head != null && _head.transform != transform)
            {
                RectTransform headRect = _head.rectTransform;
                headRect.position = rect.TransformPoint(end);
                float angle = Mathf.Atan2(endDirection.y, endDirection.x) * Mathf.Rad2Deg;
                headRect.rotation = rect.rotation * Quaternion.Euler(0f, 0f, angle + _headAngleOffset);
                _head.raycastTarget = false;
                _head.enabled = true;
            }
        }

        private void HideVisuals()
        {
            if (_line != null) _line.Hide();
            if (_head != null) _head.enabled = false;
        }
    }
}
