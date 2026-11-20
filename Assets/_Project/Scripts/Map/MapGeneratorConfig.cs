using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Map
{
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

        [Tooltip("Distance from the previous layer.")]
        public FloatRange DistanceFromPreviousLayer = new(140f, 170f);

        [Tooltip("Lane spacing between nodes on this layer.")]
        public float NodesApartDistance = 220f;

        [Tooltip("0 = straight row, 1 = strong random position offset.")]
        [Range(0f, 1f)] public float RandomizePosition = 0.25f;
    }

    [CreateAssetMenu(menuName = "Data/Map Generator Config")]
    public class MapGeneratorConfig : ScriptableObject
    {
        [Header("Slay-the-Spire Procedural Map (Left -> Right)")]
        [Tooltip("When enabled, generates a balanced Slay-the-Spire style branching map from Left (Start) to Right (Boss) with randomized depth in Layer Count Range.")]
        public bool UseSlayTheSpireProceduralMode = true;

        [Tooltip("Total columns/steps from Start (left) to Boss (right). Varies randomly between Min and Max (6 to 8 by default).")]
        public IntRange LayerCountRange = new(6, 8);

        [Tooltip("Vertical lanes available for branching paths (4 lanes produces 2-4 nodes per column like the reference).")]
        [Range(3, 6)] public int LaneCount = 4;

        [Tooltip("Left-to-Right horizontal span (X) and vertical span (Y) in map canvas units.")]
        public Vector2 MapCanvasSpan = new(1340f, 520f);

        [Tooltip("Organic horizontal position jitter fraction per node (prevents stiff grid columns).")]
        [Range(0f, 0.42f)] public float HorizontalJitter = 0.24f;

        [Tooltip("Organic vertical position jitter fraction per node (prevents stiff horizontal rows).")]
        [Range(0f, 0.42f)] public float VerticalJitter = 0.28f;

        [Header("Balanced Slay-the-Spire Node Weights (Middle Layers)")]
        [Min(0f)] public float EnemyWeight = 42f;
        [Min(0f)] public float EventWeight = 22f;
        [Min(0f)] public float EliteWeight = 15f;
        [Min(0f)] public float ShopWeight = 10f;
        [Min(0f)] public float RestWeight = 8f;
        [Min(0f)] public float TreasureWeight = 3f;

        [Header("Start Mode")]
        [Tooltip("Off: first-column nodes are immediately selectable on the left, like Slay the Spire.")]
        public bool UseSyntheticStartNode;

        [Tooltip("Distance between the optional START node and the first playable column.")]
        [Min(1f)] public float SyntheticStartDistance = 170f;

        [Header("Path Counts")]
        [Tooltip("How many different first-column nodes can be starting choices (left side).")]
        public IntRange NumberOfStartingNodes = new(2, 3);

        [Tooltip("How many different pre-boss nodes converge into the Boss (right side).")]
        public IntRange NumberOfPreBossNodes = new(2, 3);

        [Tooltip("Additional braided paths beyond max(starting nodes, pre-boss nodes).")]
        [Min(0)] public int ExtraPaths = 2;

        [Header("Legacy Layer List (Used only if UseSlayTheSpireProceduralMode is off)")]
        public List<MapNodeType> RandomNodeTypes = new()
        {
            MapNodeType.Event,
            MapNodeType.Shop,
            MapNodeType.Treasure,
            MapNodeType.Enemy,
            MapNodeType.Elite,
            MapNodeType.Rest
        };

        public List<MapLayerConfig> Layers = new();

        [Header("Compatibility Defaults")]
        [Min(3)] public int FallbackLayerCount = 7;
        [Min(2)] public int FallbackGridWidth = 4;

        public int GridWidth => UseSlayTheSpireProceduralMode
            ? Mathf.Clamp(LaneCount, 3, 6)
            : Mathf.Max(1, Mathf.Max(NumberOfStartingNodes.Max, NumberOfPreBossNodes.Max));

        public IReadOnlyList<MapLayerConfig> GetEffectiveLayers()
        {
            if (Layers != null && Layers.Count >= 3)
            {
                return Layers;
            }

            Layers ??= new List<MapLayerConfig>();
            Layers.Clear();
            int count = Mathf.Clamp(FallbackLayerCount, 6, 8);
            for (int i = 0; i < count; i++)
            {
                MapNodeType type = MapNodeType.Enemy;
                float randomizeNodes = 0.35f;

                if (i == 0)
                {
                    randomizeNodes = 0f;
                }
                else if (i == count - 2)
                {
                    type = MapNodeType.Rest;
                    randomizeNodes = 0f;
                }
                else if (i == count - 1)
                {
                    type = MapNodeType.Boss;
                    randomizeNodes = 0f;
                }

                Layers.Add(new MapLayerConfig
                {
                    NodeType = type,
                    RandomizeNodes = randomizeNodes,
                    DistanceFromPreviousLayer = new FloatRange(160f, 200f),
                    NodesApartDistance = 175f,
                    RandomizePosition = i == count - 1 ? 0f : 0.28f
                });
            }

            return Layers;
        }
    }
}
