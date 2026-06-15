using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ProceduralMapGenerator
{
    public static MapGraph Generate(MapGeneratorConfig config, MapEncounterPool encounterPool)
    {
        if (config == null)
        {
            Debug.LogWarning("MapGeneratorConfig is missing. Using default generated settings.");
            config = ScriptableObject.CreateInstance<MapGeneratorConfig>();
        }

        return GenerateWithConfig(config, encounterPool);
    }

    private static MapGraph GenerateWithConfig(MapGeneratorConfig config, MapEncounterPool encounterPool)
    {
        IReadOnlyList<MapLayerConfig> layerConfigs = config.GetEffectiveLayers();
        int gridWidth = Mathf.Max(1, config.GridWidth);
        List<float> layerDistances = GenerateLayerDistances(layerConfigs);

        MapGraph graph = new();
        List<List<MapNode>> layers = new();
        int graphLayerOffset = config.UseSyntheticStartNode ? 1 : 0;

        if (config.UseSyntheticStartNode)
        {
            float startY = -Mathf.Max(1f, config.SyntheticStartDistance);
            MapNode startNode = new("start", 0, gridWidth / 2, MapNodeType.Start, new Vector2(0f, startY));
            layers.Add(new List<MapNode> { startNode });
            graph.Nodes.Add(startNode);
        }

        for (int layer = 0; layer < layerConfigs.Count; layer++)
        {
            MapLayerConfig layerConfig = layerConfigs[layer];
            int graphLayer = layer + graphLayerOffset;
            int columnsOnLayer = layer == layerConfigs.Count - 1 ? 1 : gridWidth;
            List<MapNode> nodesOnLayer = new();

            for (int column = 0; column < columnsOnLayer; column++)
            {
                MapNodeType type = GetNodeType(layerConfig, config.RandomNodeTypes);
                if (layer == layerConfigs.Count - 1)
                {
                    type = MapNodeType.Boss;
                }

                Vector2 position = GetPosition(layer, column, columnsOnLayer, layerConfig, layerDistances);
                MapNode node = new($"{graphLayer}_{column}", graphLayer, column, type, position);

                if (node.StartsCombat && encounterPool != null)
                {
                    node.Encounter = encounterPool.GetEncounter(type);
                }

                nodesOnLayer.Add(node);
                graph.Nodes.Add(node);
            }

            layers.Add(nodesOnLayer);
        }

        List<List<Vector2Int>> paths = GeneratePaths(config, layerConfigs.Count, gridWidth);
        if (config.UseSyntheticStartNode)
        {
            ConnectStartToFirstLayer(layers, paths);
        }

        SetUpConnections(layers, paths, graphLayerOffset);
        HashSet<string> pathNodeIds = CollectConnectedNodeIds(graph);
        RemoveCrossConnections(layers, config.UseSyntheticStartNode ? 1 : 0);
        EnsureForwardConnections(layers, pathNodeIds);
        EnsureBossConnections(layers, pathNodeIds);
        RemoveDisconnectedNodes(graph);
        graph.RefreshAvailability(null);
        return graph;
    }

    private static List<float> GenerateLayerDistances(IReadOnlyList<MapLayerConfig> layers)
    {
        List<float> distances = new();
        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
        {
            if (i > 0)
            {
                total += Mathf.Max(1f, layers[i].DistanceFromPreviousLayer.GetValue());
            }

            distances.Add(total);
        }

        return distances;
    }

    private static MapNodeType GetNodeType(MapLayerConfig layer, List<MapNodeType> randomNodeTypes)
    {
        if (layer == null) return MapNodeType.Enemy;
        if (randomNodeTypes != null && randomNodeTypes.Count > 0 && Random.value < layer.RandomizeNodes)
        {
            return randomNodeTypes[Random.Range(0, randomNodeTypes.Count)];
        }

        return layer.NodeType;
    }

    private static Vector2 GetPosition(int layer, int column, int columnsOnLayer, MapLayerConfig layerConfig, List<float> layerDistances)
    {
        float nodesApart = Mathf.Max(1f, layerConfig.NodesApartDistance);
        float offset = nodesApart * (columnsOnLayer - 1) / 2f;
        Vector2 position = new(-offset + column * nodesApart, layerDistances[layer]);

        if (columnsOnLayer > 1 && layerConfig.RandomizePosition > 0f)
        {
            float previousDistance = layer == 0 ? 0f : Mathf.Abs(layerDistances[layer] - layerDistances[layer - 1]);
            float nextDistance = layer + 1 >= layerDistances.Count ? previousDistance : Mathf.Abs(layerDistances[layer + 1] - layerDistances[layer]);
            float xRandom = Random.Range(-0.5f, 0.5f) * nodesApart;
            float yRandomFactor = Random.Range(-0.5f, 0.5f);
            float yRandom = yRandomFactor < 0f ? previousDistance * yRandomFactor : nextDistance * yRandomFactor;
            position += new Vector2(xRandom, yRandom) * layerConfig.RandomizePosition;
        }

        return position;
    }

    private static List<List<Vector2Int>> GeneratePaths(MapGeneratorConfig config, int generatedLayerCount, int gridWidth)
    {
        int finalRow = generatedLayerCount - 1;
        int preBossRow = finalRow - 1;
        Vector2Int finalNode = new(0, finalRow);
        List<int> candidateColumns = Enumerable.Range(0, gridWidth).ToList();

        Shuffle(candidateColumns);
        int numberOfStartingNodes = Mathf.Clamp(config.NumberOfStartingNodes.GetValue(), 1, gridWidth);
        List<Vector2Int> startingPoints = candidateColumns
            .Take(numberOfStartingNodes)
            .Select(column => new Vector2Int(column, 0))
            .ToList();

        Shuffle(candidateColumns);
        int numberOfPreBossNodes = Mathf.Clamp(config.NumberOfPreBossNodes.GetValue(), 1, gridWidth);
        List<Vector2Int> preBossPoints = candidateColumns
            .Take(numberOfPreBossNodes)
            .Select(column => new Vector2Int(column, preBossRow))
            .ToList();

        int numberOfPaths = Mathf.Max(numberOfStartingNodes, numberOfPreBossNodes) + Mathf.Max(0, config.ExtraPaths);
        List<List<Vector2Int>> paths = new();
        for (int i = 0; i < numberOfPaths; i++)
        {
            Vector2Int start = startingPoints[i % numberOfStartingNodes];
            Vector2Int end = preBossPoints[i % numberOfPreBossNodes];
            List<Vector2Int> path = Path(start, end, gridWidth);
            path.Add(finalNode);
            paths.Add(path);
        }

        return paths;
    }

    private static List<Vector2Int> Path(Vector2Int fromPoint, Vector2Int toPoint, int gridWidth)
    {
        int toRow = toPoint.y;
        int toColumn = toPoint.x;
        int lastColumn = fromPoint.x;
        List<Vector2Int> path = new() { fromPoint };
        List<int> candidateColumns = new();

        for (int row = 1; row < toRow; row++)
        {
            candidateColumns.Clear();
            int verticalDistance = toRow - row;

            AddCandidateIfCanReach(candidateColumns, lastColumn, toColumn, verticalDistance, gridWidth);
            AddCandidateIfCanReach(candidateColumns, lastColumn - 1, toColumn, verticalDistance, gridWidth);
            AddCandidateIfCanReach(candidateColumns, lastColumn + 1, toColumn, verticalDistance, gridWidth);

            if (candidateColumns.Count == 0)
            {
                candidateColumns.Add(Mathf.Clamp(lastColumn, 0, gridWidth - 1));
            }

            int candidateColumn = candidateColumns[Random.Range(0, candidateColumns.Count)];
            path.Add(new Vector2Int(candidateColumn, row));
            lastColumn = candidateColumn;
        }

        path.Add(toPoint);
        return path;
    }

    private static void AddCandidateIfCanReach(List<int> candidates, int candidateColumn, int targetColumn, int verticalDistance, int gridWidth)
    {
        if (candidateColumn < 0 || candidateColumn >= gridWidth) return;
        if (Mathf.Abs(targetColumn - candidateColumn) <= verticalDistance)
        {
            candidates.Add(candidateColumn);
        }
    }

    private static void ConnectStartToFirstLayer(List<List<MapNode>> layers, List<List<Vector2Int>> paths)
    {
        if (layers.Count < 2) return;
        MapNode startNode = layers[0][0];
        foreach (Vector2Int firstPoint in paths.Select(path => path[0]).Distinct())
        {
            if (firstPoint.x >= 0 && firstPoint.x < layers[1].Count)
            {
                AddConnection(startNode, layers[1][firstPoint.x]);
            }
        }
    }

    private static void SetUpConnections(List<List<MapNode>> layers, List<List<Vector2Int>> paths, int layerOffset)
    {
        foreach (List<Vector2Int> path in paths)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                MapNode node = GetNode(layers, path[i], layerOffset);
                MapNode nextNode = GetNode(layers, path[i + 1], layerOffset);
                AddConnection(node, nextNode);
            }
        }
    }

    private static MapNode GetNode(List<List<MapNode>> layers, Vector2Int point, int layerOffset)
    {
        int layerIndex = point.y + layerOffset;
        if (layerIndex < 0 || layerIndex >= layers.Count) return null;
        if (point.x < 0 || point.x >= layers[layerIndex].Count) return null;
        return layers[layerIndex][point.x];
    }

    private static HashSet<string> CollectConnectedNodeIds(MapGraph graph)
    {
        HashSet<string> ids = new();
        foreach (MapNode node in graph.Nodes)
        {
            if (node.NextNodeIds.Count > 0)
            {
                ids.Add(node.Id);
            }

            foreach (string nextNodeId in node.NextNodeIds)
            {
                ids.Add(nextNodeId);
            }
        }

        return ids;
    }

    private static void AddConnection(MapNode from, MapNode to)
    {
        if (from == null || to == null) return;
        if (!from.NextNodeIds.Contains(to.Id))
        {
            from.NextNodeIds.Add(to.Id);
        }
    }

    private static void RemoveConnection(MapNode from, MapNode to)
    {
        if (from == null || to == null) return;
        from.NextNodeIds.Remove(to.Id);
    }

    private static void RemoveCrossConnections(List<List<MapNode>> layers, int firstPlayableLayerIndex)
    {
        for (int layer = firstPlayableLayerIndex; layer < layers.Count - 1; layer++)
        {
            for (int column = 0; column < layers[layer].Count - 1; column++)
            {
                MapNode node = layers[layer][column];
                MapNode right = layers[layer][column + 1];
                MapNode top = column < layers[layer + 1].Count ? layers[layer + 1][column] : null;
                MapNode topRight = column + 1 < layers[layer + 1].Count ? layers[layer + 1][column + 1] : null;

                if (node == null || right == null || top == null || topRight == null) continue;
                if (!node.NextNodeIds.Contains(topRight.Id)) continue;
                if (!right.NextNodeIds.Contains(top.Id)) continue;

                AddConnection(node, top);
                AddConnection(right, topRight);

                float roll = Random.value;
                if (roll < 0.2f)
                {
                    RemoveConnection(node, topRight);
                    RemoveConnection(right, top);
                }
                else if (roll < 0.6f)
                {
                    RemoveConnection(node, topRight);
                }
                else
                {
                    RemoveConnection(right, top);
                }
            }
        }
    }

    private static void EnsureForwardConnections(List<List<MapNode>> layers, HashSet<string> pathNodeIds)
    {
        for (int layer = 0; layer < layers.Count - 1; layer++)
        {
            List<MapNode> nextLayer = layers[layer + 1];
            if (nextLayer.Count == 0) continue;

            foreach (MapNode node in layers[layer])
            {
                if (!pathNodeIds.Contains(node.Id)) continue;
                if (node.NextNodeIds.Count > 0) continue;

                int nextColumn = Mathf.Clamp(node.Column, 0, nextLayer.Count - 1);
                AddConnection(node, nextLayer[nextColumn]);
            }
        }
    }

    private static void EnsureBossConnections(List<List<MapNode>> layers, HashSet<string> pathNodeIds)
    {
        if (layers.Count < 2) return;

        List<MapNode> preBossLayer = layers[layers.Count - 2];
        MapNode bossNode = layers[layers.Count - 1][0];
        foreach (MapNode node in preBossLayer)
        {
            if (pathNodeIds.Contains(node.Id))
            {
                AddConnection(node, bossNode);
            }
        }
    }

    private static void RemoveDisconnectedNodes(MapGraph graph)
    {
        HashSet<string> reachable = new();
        foreach (MapNode initialNode in graph.GetInitialNodes())
        {
            Visit(initialNode, graph, reachable);
        }

        graph.Nodes.RemoveAll(node => !reachable.Contains(node.Id));
    }

    private static void Visit(MapNode node, MapGraph graph, HashSet<string> reachable)
    {
        if (node == null || !reachable.Add(node.Id)) return;

        foreach (MapNode nextNode in graph.GetNextNodes(node))
        {
            Visit(nextNode, graph, reachable);
        }
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);
            (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
        }
    }
}
