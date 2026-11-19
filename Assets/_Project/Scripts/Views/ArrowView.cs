using PlushieOdyssey.Core.Extensions;
using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    public class ArrowView : MonoBehaviour
    {
        [SerializeField] private GameObject arrowHead;
        [SerializeField] private LineRenderer lineRenderer;

        private Vector3 startPosition;
        private Transform targetTransform;
        private Vector3? endPositionOverride;
        private SpriteRenderer headSpriteRenderer;

        private void Awake()
        {
            if (arrowHead != null)
            {
                headSpriteRenderer = arrowHead.GetComponent<SpriteRenderer>();
            }
        }

        private void Update()
        {
            Vector3 endPosition = targetTransform != null
                ? targetTransform.position
                : (endPositionOverride ?? MouseUtils.GetMousePositionInWorldSpace());

            Vector3 direction = (endPosition - startPosition).normalized;

            if (lineRenderer != null && direction != Vector3.zero)
            {
                lineRenderer.SetPosition(1, endPosition - direction * 0.5f);
            }

            if (arrowHead != null)
            {
                if (direction != Vector3.zero) arrowHead.transform.right = direction;
                arrowHead.transform.position = endPosition;
            }
        }

        public void SetupArrow(Vector3 startPosition, Transform targetTransform = null)
        {
            this.startPosition = startPosition;
            this.targetTransform = targetTransform;
            endPositionOverride = null;
            if (lineRenderer != null) lineRenderer.SetPosition(0, startPosition);
            Update();
        }

        public void SetStartPosition(Vector3 startPosition)
        {
            this.startPosition = startPosition;
            if (lineRenderer != null) lineRenderer.SetPosition(0, startPosition);
        }

        public void SetTargetTransform(Transform target)
        {
            targetTransform = target;
            if (target != null) endPositionOverride = null;
            Update();
        }

        public void SetEndPositionOverride(Vector3? worldEnd)
        {
            endPositionOverride = worldEnd;
            if (worldEnd.HasValue) targetTransform = null;
            Update();
        }

        public void SetTint(Color color)
        {
            if (lineRenderer != null)
            {
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }

            if (headSpriteRenderer == null && arrowHead != null)
            {
                headSpriteRenderer = arrowHead.GetComponent<SpriteRenderer>();
            }

            if (headSpriteRenderer != null)
            {
                headSpriteRenderer.color = color;
            }
        }
    }
}
