using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace PlushieOdyssey.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UltimatePointsView : MonoBehaviour
    {
        [Header("Procedural Ring")]
        [SerializeField, Min(0)] private int _maxPoints = 8;
        [SerializeField, Min(1f)] private float _diameter = 200f;
        [Tooltip("Diameter of the transparent center; ring thickness is half the diameter difference.")]
        [SerializeField, Min(0f)] private float _innerDiameter = 160f;
        [SerializeField, Range(0f, 360f)] private float _arcDegrees = 360f;
        [SerializeField, Min(0f)] private float _gapDegrees = 4f;
        [Tooltip("Z rotation of the arc's start; zero starts at the top.")]
        [SerializeField] private float _startAngle = 0f;
        [SerializeField] private bool _clockwise = true;

        [Header("Points")]
        [SerializeField, Min(0)] private int _currentPoints = 0;

        [Header("Colors")]
        [SerializeField] private Color _chargedColor = new(1f, 0.79f, 0.47f, 1f);
        [SerializeField] private Color _emptyColor = new(0.25f, 0.25f, 0.25f, 1f);

        private readonly List<UltimatePointSegmentGraphic> _generatedSegments = new();
        private RectTransform _generatedRoot;
        private bool _layoutPending;
        private bool _refreshPending;

        public int CurrentPoints => _currentPoints;
        public int MaxPoints => Mathf.Max(0, _maxPoints);

        private void OnEnable()
        {
            _layoutPending = true;
            _refreshPending = true;
            if (Application.IsPlaying(gameObject)) RebuildSegments();
        }

        private void OnDisable()
        {
            ClearGeneratedSegments();
        }

        private void OnValidate()
        {
            _innerDiameter = Mathf.Clamp(_innerDiameter, 0f, Mathf.Max(1f, _diameter));
            _currentPoints = Mathf.Clamp(_currentPoints, 0, MaxPoints);
            _layoutPending = true;
            _refreshPending = true;
        }

        private void Update()
        {
            // Apply Inspector previews outside OnValidate's loading callbacks.
            if (_layoutPending) RebuildSegments();
            if (_refreshPending) SetPoints(_currentPoints);
        }

        public void SetPoints(int points)
        {
            if (_layoutPending && isActiveAndEnabled) RebuildSegments();
            _currentPoints = Mathf.Clamp(points, 0, MaxPoints);
            _refreshPending = false;

            for (int i = 0; i < _generatedSegments.Count; i++)
            {
                if (_generatedSegments[i] != null)
                    _generatedSegments[i].color = i < _currentPoints ? _chargedColor : _emptyColor;
            }
        }

        public void SetMaxPoints(int maxPoints)
        {
            _maxPoints = Mathf.Max(0, maxPoints);
            RebuildSegments();
        }

        [Button(enabledMode: EButtonEnableMode.Editor)]
        public void RebuildSegments()
        {
            _layoutPending = false;
            if (!isActiveAndEnabled || !gameObject.scene.IsValid() || MaxPoints == 0)
            {
                ClearGeneratedSegments();
                SetPoints(_currentPoints);
                return;
            }

            if (_generatedRoot == null)
            {
                _generatedSegments.Clear();
                var root = new GameObject("GeneratedUltimatePoints", typeof(RectTransform));
                // Editor previews are transient; only the view's settings are saved.
                root.hideFlags = Application.IsPlaying(gameObject) ? HideFlags.None : HideFlags.DontSave;
                _generatedRoot = (RectTransform)root.transform;
                _generatedRoot.SetParent(transform, false);
                _generatedRoot.SetAsFirstSibling();
            }

            _generatedRoot.anchorMin = _generatedRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _generatedRoot.pivot = new Vector2(0.5f, 0.5f);
            _generatedRoot.anchoredPosition = Vector2.zero;
            _generatedRoot.sizeDelta = Vector2.one * Mathf.Max(1f, _diameter);

            while (_generatedSegments.Count > MaxPoints)
            {
                int last = _generatedSegments.Count - 1;
                if (_generatedSegments[last] != null) DestroyGenerated(_generatedSegments[last].gameObject);
                _generatedSegments.RemoveAt(last);
            }
            _generatedSegments.RemoveAll(segment => segment == null);
            while (_generatedSegments.Count < MaxPoints)
            {
                var segment = new GameObject($"Point{_generatedSegments.Count + 1:00}",
                    typeof(RectTransform), typeof(UltimatePointSegmentGraphic));
                segment.hideFlags = _generatedRoot.gameObject.hideFlags;
                segment.transform.SetParent(_generatedRoot, false);
                _generatedSegments.Add(segment.GetComponent<UltimatePointSegmentGraphic>());
            }

            float step = Mathf.Clamp(_arcDegrees, 0f, 360f) / MaxPoints;
            float gap = Mathf.Clamp(_gapDegrees, 0f, step);
            float direction = _clockwise ? -1f : 1f;
            float innerRatio = Mathf.Clamp01(_innerDiameter / Mathf.Max(1f, _diameter));
            bool closedRing = Mathf.Approximately(_arcDegrees, 360f);
            for (int i = 0; i < _generatedSegments.Count; i++)
            {
                UltimatePointSegmentGraphic segment = _generatedSegments[i];
                RectTransform rect = segment.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localRotation = Quaternion.Euler(0f, 0f, _startAngle + direction * (i * step + gap * 0.5f));
                segment.Configure(innerRatio, step - gap, _clockwise,
                    smoothStart: gap > 0f || (!closedRing && i == 0),
                    smoothEnd: gap > 0f || (!closedRing && i == _generatedSegments.Count - 1));
                segment.raycastTarget = false;
            }
            SetPoints(_currentPoints);
        }

        private void ClearGeneratedSegments()
        {
            if (_generatedRoot != null) DestroyGenerated(_generatedRoot.gameObject);
            _generatedRoot = null;
            _generatedSegments.Clear();
        }

        private static void DestroyGenerated(GameObject target)
        {
            target.SetActive(false);
            if (Application.IsPlaying(target)) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
