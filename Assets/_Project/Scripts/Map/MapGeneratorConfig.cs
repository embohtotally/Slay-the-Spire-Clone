using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct IntRange
{
    [Min(0)] public int Min;
    [Min(0)] public int Max;

    public IntRange(int min, int max)
    {
        Min = min;
        Max = max;
    }

    public int GetValue()
    {
        int lower = Mathf.Min(Min, Max);
        int upper = Mathf.Max(Min, Max);
        return UnityEngine.Random.Range(lower, upper + 1);
    }
}

[Serializable]
public struct FloatRange
{
    public float Min;
    public float Max;

    public FloatRange(float min, float max)
    {
        Min = min;
        Max = max;
    }

    public float GetValue()
    {
        float lower = Mathf.Min(Min, Max);
        float upper = Mathf.Max(Min, Max);
        return UnityEngine.Random.Range(lower, upper);
    }
}

[Serializable]
public class MapLayerConfig
{
    [Tooltip("Default node type for this layer. If Randomize Nodes is 0, every node on this layer uses this type.")]
    public MapNodeType NodeType = MapNodeType.Enemy;

    [Tooltip("Chance for each node on this layer to use a random type from Random Node Types instead of the default type.")]
    [Range(0f, 1f)] public float RandomizeNodes;

    [Tooltip("Vertical distance from the previous layer. Similar to the original repo's distanceFromPreviousLayer.")]
    public FloatRange DistanceFromPreviousLayer = new(140f, 170f);

    [Tooltip("Horizontal distance between nodes on this layer.")]
    public float NodesApartDistance = 220f;

    [Tooltip("0 = straight row, 1 = strong random position offset, like the original repo.")]
    [Range(0f, 1f)] public float RandomizePosition = 0.25f;
}

[CreateAssetMenu(menuName = "Data/Map Generator Config")]
public class MapGeneratorConfig : ScriptableObject
{
    [Header("Start Mode")]
    [Tooltip("On: add a tutorial-style START node before the first playable row. Off: first-row nodes are immediately selectable, like Slay the Spire.")]
    public bool UseSyntheticStartNode;

    [Tooltip("Vertical distance between the optional START node and the first playable row. Prevents visual overlap when synthetic start is enabled.")]
    [Min(1f)] public float SyntheticStartDistance = 170f;

    [Header("Path Counts")]
    [Tooltip("How many different first-layer nodes can be starting choices.")]
    public IntRange NumberOfStartingNodes = new(2, 3);

    [Tooltip("How many different nodes on the layer before the boss should connect to boss.")]
    public IntRange NumberOfPreBossNodes = new(2, 3);

    [Tooltip("Additional paths beyond max(starting nodes, pre-boss nodes).")]
    [Min(0)] public int ExtraPaths = 2;

    [Header("Layer Design")]
    [Tooltip("Types allowed when a layer's Randomize Nodes value succeeds.")]
    public List<MapNodeType> RandomNodeTypes = new()
    {
        MapNodeType.Event,
        MapNodeType.Shop,
        MapNodeType.Treasure,
        MapNodeType.Enemy,
        MapNodeType.Rest
    };

    [Tooltip("Designer-authored layers. First layer is usually Enemy/Start-choice layer; final layer should be Boss.")]
    public List<MapLayerConfig> Layers = new();

    [Header("Compatibility Defaults")]
    [Tooltip("Used only when Layers is empty. Creates this many generated layers.")]
    [Min(3)] public int FallbackLayerCount = 6;

    [Tooltip("Used only when Layers is empty.")]
    [Min(2)] public int FallbackGridWidth = 4;

    public int GridWidth => Mathf.Max(1, Mathf.Max(NumberOfStartingNodes.Max, NumberOfPreBossNodes.Max));

    public IReadOnlyList<MapLayerConfig> GetEffectiveLayers()
    {
        if (Layers != null && Layers.Count >= 3)
        {
            return Layers;
        }

        Layers ??= new List<MapLayerConfig>();
        Layers.Clear();
        for (int i = 0; i < FallbackLayerCount; i++)
        {
            MapNodeType type = MapNodeType.Enemy;
            float randomizeNodes = 0.25f;

            if (i == FallbackLayerCount - 2)
            {
                type = MapNodeType.Rest;
                randomizeNodes = 0f;
            }
            else if (i == FallbackLayerCount - 1)
            {
                type = MapNodeType.Boss;
                randomizeNodes = 0f;
            }

            Layers.Add(new MapLayerConfig
            {
                NodeType = type,
                RandomizeNodes = randomizeNodes,
                DistanceFromPreviousLayer = new FloatRange(140f, 170f),
                NodesApartDistance = 220f,
                RandomizePosition = i == FallbackLayerCount - 1 ? 0f : 0.25f
            });
        }

        NumberOfStartingNodes.Max = Mathf.Max(NumberOfStartingNodes.Max, FallbackGridWidth);
        NumberOfPreBossNodes.Max = Mathf.Max(NumberOfPreBossNodes.Max, FallbackGridWidth);
        return Layers;
    }
}
