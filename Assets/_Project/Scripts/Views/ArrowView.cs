using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowView : MonoBehaviour
{
    [SerializeField] private GameObject arrowHead;
    [SerializeField] private LineRenderer lineRenderer;

    private Vector3 startPosition;
    private Transform targetTransform;

    private void Update()
    {
        Vector3 endPosition = targetTransform != null ? targetTransform.position : MouseUtils.GetMousePositionInWorldSpace();
        Vector3 direction = (endPosition - startPosition).normalized;
        
        if (direction != Vector3.zero)
        {
            lineRenderer.SetPosition(1, endPosition - direction * 0.5f);
            arrowHead.transform.right = direction;
        }
        arrowHead.transform.position = endPosition;
    }

    public void SetupArrow(Vector3 startPosition, Transform targetTransform = null)
    {
        this.startPosition = startPosition;
        this.targetTransform = targetTransform;
        lineRenderer.SetPosition(0, startPosition);
        Update();
    }

    public void SetStartPosition(Vector3 startPosition)
    {
        this.startPosition = startPosition;
        lineRenderer.SetPosition(0, startPosition);
    }
}