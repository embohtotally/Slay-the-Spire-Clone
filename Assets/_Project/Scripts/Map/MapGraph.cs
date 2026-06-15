using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class MapGraph
{
    public List<MapNode> Nodes = new();

    public MapNode GetNode(string id)
    {
        return Nodes.FirstOrDefault(node => node.Id == id);
    }

    public List<MapNode> GetNextNodes(MapNode node)
    {
        List<MapNode> result = new();
        if (node == null) return result;

        foreach (string nextNodeId in node.NextNodeIds)
        {
            MapNode nextNode = GetNode(nextNodeId);
            if (nextNode != null)
            {
                result.Add(nextNode);
            }
        }

        return result;
    }

    public MapNode GetStartNode()
    {
        return Nodes.FirstOrDefault(node => node.Type == MapNodeType.Start);
    }

    public List<MapNode> GetInitialNodes()
    {
        MapNode startNode = GetStartNode();
        if (startNode != null)
        {
            return new List<MapNode> { startNode };
        }

        int firstLayer = Nodes
            .Where(node => node.Type != MapNodeType.Boss)
            .Select(node => node.Layer)
            .DefaultIfEmpty(0)
            .Min();

        return Nodes
            .Where(node => node.Layer == firstLayer && node.NextNodeIds.Count > 0)
            .OrderBy(node => node.Column)
            .ToList();
    }

    public MapNode GetCurrentNode(string currentNodeId)
    {
        return string.IsNullOrEmpty(currentNodeId) ? null : GetNode(currentNodeId);
    }

    public void RefreshAvailability(string currentNodeId)
    {
        foreach (MapNode node in Nodes)
        {
            node.IsAvailable = false;
        }

        MapNode currentNode = GetCurrentNode(currentNodeId);
        if (currentNode == null)
        {
            foreach (MapNode initialNode in GetInitialNodes())
            {
                if (!initialNode.IsVisited)
                {
                    initialNode.IsAvailable = true;
                }
            }

            return;
        }

        foreach (MapNode nextNode in GetNextNodes(currentNode))
        {
            if (!nextNode.IsVisited)
            {
                nextNode.IsAvailable = true;
            }
        }
    }
}
