using System.Collections.Generic;
using UnityEngine;

public enum MapOrientation
{
    BottomToTop,
    TopToBottom,
    RightToLeft,
    LeftToRight
}

public class MapView : MonoBehaviour
{
    [Header("Prefabs / Parents")]
    [SerializeField] private MapNodeView nodeViewPrefab;
    [SerializeField] private MapLineView lineViewPrefab;
    [SerializeField] private Transform lineParent;
    [SerializeField] private Transform nodeParent;

    [Header("Layout")]
    [Tooltip("Visual direction of map progress, matching the original repo orientation options.")]
    [SerializeField] private MapOrientation orientation = MapOrientation.BottomToTop;

    [Tooltip("Extra offset from the origin. Useful when changing map orientation in the inspector.")]
    [SerializeField] private Vector2 orientationOffset;

    private readonly List<MapNodeView> nodeViews = new();
    private readonly List<MapLineView> lineViews = new();

    public MapOrientation Orientation
    {
        get => orientation;
        set => orientation = value;
    }

    public void ShowMap(MapGraph graph, MapSystem mapSystem)
    {
        Clear();
        if (graph == null) return;

        foreach (MapNode node in graph.Nodes)
        {
            foreach (string nextNodeId in node.NextNodeIds)
            {
                MapNode nextNode = graph.GetNode(nextNodeId);
                if (nextNode == null || lineViewPrefab == null) continue;

                MapLineView lineView = Instantiate(lineViewPrefab, lineParent != null ? lineParent : transform);
                bool visited = node.IsVisited && nextNode.IsVisited;
                lineView.Setup(GetNodePosition(node), GetNodePosition(nextNode), visited);
                lineViews.Add(lineView);
            }
        }

        foreach (MapNode node in graph.Nodes)
        {
            if (nodeViewPrefab == null) continue;

            MapNodeView nodeView = Instantiate(nodeViewPrefab, nodeParent != null ? nodeParent : transform);
            nodeView.Setup(node, mapSystem);
            nodeView.transform.localPosition = GetNodePosition(node);
            nodeViews.Add(nodeView);
        }
    }

    public void Refresh(MapGraph graph, MapSystem mapSystem)
    {
        ShowMap(graph, mapSystem);
    }

    private Vector2 GetNodePosition(MapNode node)
    {
        Vector2 position = node.Position;
        return orientation switch
        {
            MapOrientation.BottomToTop => position + orientationOffset,
            MapOrientation.TopToBottom => new Vector2(-position.x, -position.y) + orientationOffset,
            MapOrientation.RightToLeft => new Vector2(-position.y, position.x) + orientationOffset,
            MapOrientation.LeftToRight => new Vector2(position.y, -position.x) + orientationOffset,
            _ => position + orientationOffset
        };
    }

    private void Clear()
    {
        foreach (MapNodeView nodeView in nodeViews)
        {
            if (nodeView != null) Destroy(nodeView.gameObject);
        }
        nodeViews.Clear();

        foreach (MapLineView lineView in lineViews)
        {
            if (lineView != null) Destroy(lineView.gameObject);
        }
        lineViews.Clear();
    }
}
