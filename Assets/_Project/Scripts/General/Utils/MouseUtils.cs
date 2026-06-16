using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class MouseUtils
{
    private static Camera _camera;
    private static Camera Camera
    {
        get
        {
            if (_camera == null) _camera = Camera.main;
            return _camera;
        }
    }

    public static Vector3 GetMousePositionInWorldSpace(float zValue = 0f)
    {
        Plane dragPlane = new(Camera.transform.forward, new Vector3(0, 0, zValue));
        Ray ray = Camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (dragPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return Vector3.zero;
    }
}