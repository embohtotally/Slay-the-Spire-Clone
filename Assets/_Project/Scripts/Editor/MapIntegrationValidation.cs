#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MapIntegrationValidation
{
    private const string MapScenePath = "Assets/_Project/Scenes/Map.unity";
    private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
    private const string ConfigPath = "Assets/_Project/ScriptableObjects/Map/Default Map Generator Config.asset";
    private const string PoolPath = "Assets/_Project/ScriptableObjects/Map/Default Map Encounter Pool.asset";

    public static void RunValidation()
    {
        MapGeneratorConfig config = AssetDatabase.LoadAssetAtPath<MapGeneratorConfig>(ConfigPath);
        MapEncounterPool pool = AssetDatabase.LoadAssetAtPath<MapEncounterPool>(PoolPath);
        if (config == null) throw new Exception("Missing map generator config asset.");
        if (pool == null) throw new Exception("Missing map encounter pool asset.");

        int totalNodesGenerated = 0;
        const int generationAttempts = 50;
        bool originalSyntheticStartMode = config.UseSyntheticStartNode;
        for (int i = 0; i < generationAttempts; i++)
        {
            MapGraph graph = ProceduralMapGenerator.Generate(config, pool);
            ValidateGraph(graph, i, config.UseSyntheticStartNode);
            totalNodesGenerated += graph.Nodes.Count;
        }

        config.UseSyntheticStartNode = true;
        for (int i = 0; i < 10; i++)
        {
            MapGraph graph = ProceduralMapGenerator.Generate(config, pool);
            ValidateGraph(graph, i, true);
            MapNode startNode = graph.GetStartNode();
            if (startNode == null) throw new Exception($"Synthetic start sample {i}: missing START node.");
            if (startNode.Position.y >= 0f) throw new Exception($"Synthetic start sample {i}: START node is not below first playable row.");
        }
        config.UseSyntheticStartNode = originalSyntheticStartMode;
        EditorUtility.SetDirty(config);

        EditorSceneManager.OpenScene(MapScenePath);
        if (UnityEngine.Object.FindFirstObjectByType<MapSystem>() == null) throw new Exception("Map scene has no MapSystem.");
        if (UnityEngine.Object.FindFirstObjectByType<MapView>() == null) throw new Exception("Map scene has no MapView.");

        EditorSceneManager.OpenScene(GameScenePath);
        if (UnityEngine.Object.FindFirstObjectByType<RunProgressSystem>() == null) throw new Exception("Game scene has no RunProgressSystem.");

        Debug.Log($"Map integration validation passed. Generated {generationAttempts} sample maps / {totalNodesGenerated} total nodes; every reachable non-boss node can progress to boss; scenes wired.");
    }

    private static void ValidateGraph(MapGraph graph, int sampleIndex, bool expectSyntheticStart)
    {
        if (graph == null) throw new Exception($"Sample {sampleIndex}: generated graph was null.");
        if (graph.Nodes.Count < 3) throw new Exception($"Sample {sampleIndex}: generated graph had too few nodes: {graph.Nodes.Count}.");

        List<MapNode> initialNodes = graph.GetInitialNodes();
        if (initialNodes.Count == 0) throw new Exception($"Sample {sampleIndex}: generated graph has no initial selectable nodes.");

        MapNode boss = graph.Nodes.Find(node => node.Type == MapNodeType.Boss);
        if (boss == null) throw new Exception($"Sample {sampleIndex}: generated graph has no boss node.");
        foreach (MapNode initialNode in initialNodes)
        {
            if (!CanReach(graph, initialNode, boss.Id))
            {
                throw new Exception($"Sample {sampleIndex}: boss node is not reachable from initial node {initialNode.Id}.");
            }
        }

        foreach (MapNode node in graph.Nodes)
        {
            if (node.StartsCombat && node.Encounter == null)
            {
                throw new Exception($"Sample {sampleIndex}: combat map node {node.Id} ({node.Type}) has no encounter.");
            }

            if (node.Type != MapNodeType.Boss && node.NextNodeIds.Count == 0)
            {
                throw new Exception($"Sample {sampleIndex}: non-boss node {node.Id} ({node.Type}) has no outgoing connection and can trap the player.");
            }

            if (node.Type != MapNodeType.Boss && !CanReach(graph, node, boss.Id))
            {
                throw new Exception($"Sample {sampleIndex}: node {node.Id} ({node.Type}) cannot reach boss and can trap the player.");
            }
        }
    }

    private static bool CanReach(MapGraph graph, MapNode start, string targetId)
    {
        HashSet<string> visited = new();
        Queue<MapNode> queue = new();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            MapNode node = queue.Dequeue();
            if (node == null || !visited.Add(node.Id)) continue;
            if (node.Id == targetId) return true;

            foreach (MapNode next in graph.GetNextNodes(node))
            {
                queue.Enqueue(next);
            }
        }

        return false;
    }
}
#endif
