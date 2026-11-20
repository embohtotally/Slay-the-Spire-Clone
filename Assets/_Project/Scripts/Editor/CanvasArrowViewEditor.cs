using PlushieOdyssey.Combat.Views;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushieOdyssey.Editor.Tools
{
    [CustomEditor(typeof(CanvasArrowView))]
    [CanEditMultipleObjects]
    public class CanvasArrowViewEditor : UnityEditor.Editor
    {
        private bool _singleSelection;

        private void OnEnable()
        {
            _singleSelection = targets.Length == 1;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox(
                "Enable Preview In Editor, then drag Start, End and Curve in the Scene view. " +
                "Styling updates live; runtime targeting ignores preview endpoints.",
                HelpBoxMessageType.Info));
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshPreview());
            return root;
        }

        private void OnSceneGUI()
        {
            var arrow = (CanvasArrowView)target;
            if (!_singleSelection || !arrow.isActiveAndEnabled || Application.IsPlaying(arrow.gameObject) ||
                !arrow.PreviewInEditor)
                return;

            arrow.GetEditorPreview(out Vector2 start, out Vector2 end, out float curveHeight);
            RectTransform rect = arrow.RectTransform;
            EditorGUI.BeginChangeCheck();
            start = DrawEndpoint(rect, start, "Start", new Color(0.3f, 1f, 0.5f));
            end = DrawEndpoint(rect, end, "End", new Color(1f, 0.65f, 0.2f));
            curveHeight = DrawCurve(rect, start, end, curveHeight);
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(arrow, "Edit Arrow Preview");
            arrow.SetEditorPreview(start, end, curveHeight);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow);
            EditorUtility.SetDirty(arrow);
            RepaintViews();
        }

        private static Vector2 DrawEndpoint(RectTransform rect, Vector2 localPosition, string label, Color color)
        {
            Vector3 worldPosition = rect.TransformPoint(localPosition);
            float size = HandleUtility.GetHandleSize(worldPosition) * 0.07f;
            using (new Handles.DrawingScope(color))
            {
                Handles.Label(worldPosition + rect.up * size * 1.5f, label);
                Vector3 moved = Handles.Slider2D(worldPosition, rect.forward, rect.right, rect.up,
                    size, Handles.CircleHandleCap, Vector2.zero);
                return rect.InverseTransformPoint(moved);
            }
        }

        private static float DrawCurve(RectTransform rect, Vector2 start, Vector2 end, float curveHeight)
        {
            Vector2 offset = end - start;
            float length = offset.magnitude;
            if (length < 0.01f) return curveHeight;

            Vector2 normal = new Vector2(-offset.y, offset.x) / length;
            Vector2 midpoint = (start + end) * 0.5f;
            float bend = Mathf.Clamp(curveHeight, -length * 0.5f, length * 0.5f);
            Vector3 worldControl = rect.TransformPoint(midpoint + normal * (bend * 2f));
            float size = HandleUtility.GetHandleSize(worldControl) * 0.07f;
            using (new Handles.DrawingScope(new Color(0.3f, 0.8f, 1f)))
            {
                Handles.DrawDottedLine(rect.TransformPoint(start), worldControl, 4f);
                Handles.DrawDottedLine(worldControl, rect.TransformPoint(end), 4f);
                Handles.Label(worldControl + rect.up * size * 1.5f, "Curve");
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider(worldControl, rect.TransformVector(normal).normalized,
                    size, Handles.DotHandleCap, 0f);
                if (!EditorGUI.EndChangeCheck()) return curveHeight;

                Vector2 localControl = rect.InverseTransformPoint(moved);
                return Mathf.Clamp(
                    Vector2.Dot(localControl - midpoint, normal) * 0.5f, -length * 0.5f, length * 0.5f);
            }
        }

        private void RefreshPreview()
        {
            foreach (Object selected in targets)
            {
                if (selected is CanvasArrowView arrow) arrow.RefreshVisuals();
            }
            RepaintViews();
        }

        private static void RepaintViews()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
}
