using UnityEngine;

namespace PlushieOdyssey.UI
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UltimatePointSegmentGraphic : UnityEngine.UI.MaskableGraphic
    {
        private static Material _sharedMaterial;
        private static int _materialUsers;

        private float _innerRatio;
        private float _arcDegrees;
        private bool _clockwise;
        private bool _smoothStart = true;
        private bool _smoothEnd = true;

        public override Material defaultMaterial => GetRingMaterial() ?? base.defaultMaterial;

        public void Configure(float innerRatio, float arcDegrees, bool clockwise,
            bool smoothStart = true, bool smoothEnd = true)
        {
            _innerRatio = Mathf.Clamp01(innerRatio);
            _arcDegrees = Mathf.Clamp(arcDegrees, 0f, 360f);
            _clockwise = clockwise;
            _smoothStart = smoothStart;
            _smoothEnd = smoothEnd;
            SetAllDirty();
        }

        protected override void OnEnable()
        {
            _materialUsers++;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _materialUsers = Mathf.Max(0, _materialUsers - 1);
            if (_materialUsers != 0 || _sharedMaterial == null) return;
            if (Application.isPlaying) Destroy(_sharedMaterial);
            else DestroyImmediate(_sharedMaterial);
            _sharedMaterial = null;
        }

        private static Material GetRingMaterial()
        {
            if (_sharedMaterial != null) return _sharedMaterial;
            // Resources keeps the automatically assigned shader included in builds.
            Shader shader = Resources.Load<Shader>("UltimateRingAntialiased");
            if (shader == null) return null;
            _sharedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _sharedMaterial;
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f || _arcDegrees <= 0f || _innerRatio >= 1f || GetRingMaterial() == null) return;

            // Padding leaves room for subpixel edge coverage outside the exact circle.
            float extent = radius + Mathf.Max(2f, radius * 0.1f);
            float flags = (_smoothStart ? 2f : 0f) + (_smoothEnd ? 4f : 0f);
            float sweep = _arcDegrees * Mathf.Deg2Rad * (_clockwise ? 1f : -1f);
            AddVertex(new Vector2(-extent, -extent));
            AddVertex(new Vector2(-extent, extent));
            AddVertex(new Vector2(extent, extent));
            AddVertex(new Vector2(extent, -extent));
            vertices.AddTriangle(0, 1, 2);
            vertices.AddTriangle(2, 3, 0);

            void AddVertex(Vector2 offset)
            {
                var vertex = UnityEngine.UIVertex.simpleVert;
                vertex.position = rect.center + offset;
                vertex.color = color;
                vertex.uv0 = new Vector4(offset.x / radius, offset.y / radius, _innerRatio + flags, sweep);
                vertices.AddVert(vertex);
            }
        }
    }
}
