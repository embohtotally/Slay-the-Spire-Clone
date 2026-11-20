using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public class CanvasArrowLineGraphic : UnityEngine.UI.MaskableGraphic
    {
        [Header("Line (UI Units)")]
        [SerializeField, Min(0f)] private float _startWidth = 8f;
        [SerializeField, Min(0f)] private float _endWidth = 3f;
        [SerializeField, Range(2, 128)] private int _segments = 32;

        [Header("Appearance")]
        [SerializeField] private Texture2D _texture;
        [Tooltip("UI units per texture repeat; zero stretches once. Repeating textures need Wrap Mode Repeat.")]
        [SerializeField, Min(0f)] private float _textureRepeatLength;

        private Vector2 _start;
        private Vector2 _control;
        private Vector2 _end;
        private bool _visible;

        public override Texture mainTexture => _texture != null ? _texture :
            material != null && material.mainTexture != null ? material.mainTexture : base.mainTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnDisable()
        {
            _visible = false;
            base.OnDisable();
        }

        public void Show(Vector2 startLocal, Vector2 controlLocal, Vector2 endLocal)
        {
            if (_visible && _start == startLocal && _control == controlLocal && _end == endLocal) return;
            _start = startLocal;
            _control = controlLocal;
            _end = endLocal;
            _visible = true;
            SetVerticesDirty();
        }

        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertices)
        {
            vertices.Clear();
            if (!_visible) return;

            int segments = Mathf.Clamp(_segments, 2, 128);
            Vector2 previous = _start;
            float totalLength = 0f;
            for (int i = 1; i <= segments; i++)
            {
                Vector2 point = Evaluate((float)i / segments);
                totalLength += Vector2.Distance(previous, point);
                previous = point;
            }
            if (totalLength < 0.01f) return;

            float uvLength = _textureRepeatLength > 0f ? _textureRepeatLength : totalLength;
            float distance = 0f;
            previous = _start;
            Color32 tint = color;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector2 point = Evaluate(t);
                distance += Vector2.Distance(previous, point);
                previous = point;
                Vector2 tangent = (1f - t) * (_control - _start) + t * (_end - _control);
                tangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : (_end - _start).normalized;
                Vector2 side = new Vector2(-tangent.y, tangent.x);
                float width = Mathf.Max(0f, Mathf.Lerp(_startWidth, _endWidth, distance / totalLength));
                float u = distance / uvLength;
                int index = vertices.currentVertCount;
                vertices.AddVert(point + side * (width * 0.5f), tint, new Vector2(u, 1f));
                vertices.AddVert(point - side * (width * 0.5f), tint, new Vector2(u, 0f));
                if (i == 0) continue;
                vertices.AddTriangle(index - 2, index, index - 1);
                vertices.AddTriangle(index, index + 1, index - 1);
            }
        }

        private Vector2 Evaluate(float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * _start + 2f * inverse * t * _control + t * t * _end;
        }
    }
}
