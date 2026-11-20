using System.Collections.Generic;
using System.Linq;

namespace PlushieOdyssey.Map
{
    [System.Serializable]
    public class MapGraph
    {
        public List<MapNode> Nodes = new();
        public List<string> VisitedOrder = new();
        public int LayerCount;
        public int Seed;

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

        public MapNode GetBossNode()
        {
            return Nodes.FirstOrDefault(node => node.Type == MapNodeType.Boss);
        }

        public bool IsBossCleared()
        {
            MapNode boss = GetBossNode();
            return boss != null && boss.IsVisited;
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

        public void MarkVisited(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            MapNode node = GetNode(nodeId);
            if (node != null) node.IsVisited = true;
            if (!VisitedOrder.Contains(nodeId))
            {
                VisitedOrder.Add(nodeId);
            }
        }

        public bool WasEdgeTraveled(string fromId, string toId)
        {
            if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId)) return false;

            if (VisitedOrder != null && VisitedOrder.Count >= 2)
            {
                for (int i = 0; i < VisitedOrder.Count - 1; i++)
                {
                    if (VisitedOrder[i] == fromId && VisitedOrder[i + 1] == toId)
                    {
                        return true;
                    }
                }
                return false;
            }

            MapNode a = GetNode(fromId);
            MapNode b = GetNode(toId);
            return a != null && b != null && a.IsVisited && b.IsVisited;
        }

        /// <summary>
        /// Returns every node ID reachable forward from the current node (or from the initial nodes
        /// if no node has been selected yet). Useful for dimming missed branches on the map.
        /// </summary>
        public HashSet<string> GetReachableFutureNodeIds(string currentNodeId)
        {
            var reachable = new HashSet<string>();
            var queue = new Queue<MapNode>();

            MapNode current = GetCurrentNode(currentNodeId);
            if (current != null)
            {
                foreach (MapNode next in GetNextNodes(current))
                {
                    if (reachable.Add(next.Id)) queue.Enqueue(next);
                }
            }
            else
            {
                foreach (MapNode initial in GetInitialNodes())
                {
                    if (reachable.Add(initial.Id)) queue.Enqueue(initial);
                }
            }

            while (queue.Count > 0)
            {
                MapNode node = queue.Dequeue();
                foreach (MapNode next in GetNextNodes(node))
                {
                    if (reachable.Add(next.Id)) queue.Enqueue(next);
                }
            }

            return reachable;
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
}
