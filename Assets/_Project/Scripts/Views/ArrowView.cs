using PlushieOdyssey.Core.Extensions;
using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    public class ArrowView : MonoBehaviour
    {
        [SerializeField] private GameObject arrowHead;
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField, Min(0f)] private float _headGap = 0.5f;

        private Vector3 startPosition;
        private Transform targetTransform;
        private bool _followLegacyTarget;

        private void Update()
        {
            if (!_followLegacyTarget) return;
            UpdateLegacyArrow();
        }

        private void UpdateLegacyArrow()
        {
            Vector3 endPosition = targetTransform != null ? targetTransform.position : MouseUtils.GetMousePositionInWorldSpace();
            Draw(startPosition, endPosition);
        }

        public void Show(Vector3 startWorld, Vector3 endWorld)
        {
            SetEndpoints(startWorld, endWorld);
            SetVisible(true);
        }

        public void SetEndpoints(Vector3 startWorld, Vector3 endWorld)
        {
            _followLegacyTarget = false;
            Draw(startWorld, endWorld);
        }

        public void Hide()
        {
            _followLegacyTarget = false;
            SetVisible(false);
        }

        // Compatibility for the legacy ManualTargetingSystem.
        public void SetupArrow(Vector3 startPosition, Transform targetTransform = null)
        {
            this.startPosition = startPosition;
            this.targetTransform = targetTransform;
            _followLegacyTarget = true;
            UpdateLegacyArrow();
            SetVisible(true);
        }

        public void SetStartPosition(Vector3 startPosition)
        {
            this.startPosition = startPosition;
            if (lineRenderer != null) lineRenderer.SetPosition(0, startPosition);
        }

        private void Draw(Vector3 startWorld, Vector3 endWorld)
        {
            Vector3 offset = endWorld - startWorld;
            float length = offset.magnitude;
            Vector3 direction = length > 0f ? offset / length : Vector3.zero;

            if (lineRenderer != null)
            {
                lineRenderer.useWorldSpace = true;
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, startWorld);
                lineRenderer.SetPosition(1, endWorld - direction * Mathf.Min(_headGap, length));
            }
            if (arrowHead != null)
            {
                arrowHead.transform.position = endWorld;
                if (length > 0f) arrowHead.transform.right = direction;
            }
        }

        private void SetVisible(bool visible)
        {
            if (lineRenderer != null) lineRenderer.enabled = visible;
            if (arrowHead != null) arrowHead.SetActive(visible);
        }
    }
}
