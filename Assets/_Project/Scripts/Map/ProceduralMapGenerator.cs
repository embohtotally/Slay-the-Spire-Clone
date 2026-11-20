using System.Collections.Generic;
using System.Linq;
using PlushieOdyssey.Data;
using UnityEngine;

namespace PlushieOdyssey.Map
{
    /// <summary>
    /// Procedural Slay-the-Spire style Left-to-Right map generator:
    /// - Variable depth between 6 and 8 columns (Start on Left -> Boss on Right)
    /// - Braided, non-crossing paths across vertical lanes with organic X/Y jitter &amp; anti-overlap relaxation
    /// - Balanced Slay-the-Spire node distribution (no back-to-back Elite/Rest/Shop/Treasure,
    ///   no early Elite/Rest, guaranteed opening Enemy row, mid-act Treasure/Elite/Shop/Event variety,
    ///   Rest/Sanctuary before the Boss, and sibling branch diversity).
    /// </summary>
    public static class ProceduralMapGenerator
    {
        private const float MinNodeSeparation = 104f;

        public static MapGraph Generate(MapGeneratorConfig config, MapEncounterPool encounterPool, int? explicitSeed = null)
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MapGeneratorConfig>();
                config.UseSlayTheSpireProceduralMode = true;
                config.LayerCountRange = new IntRange(6, 8);
                config.LaneCount = 4;
                config.NumberOfStartingNodes = new IntRange(2, 3);
                config.NumberOfPreBossNodes = new IntRange(2, 3);
                config.ExtraPaths = 2;
            }

            int seed = explicitSeed ?? System.Environment.TickCount ^ UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            Random.State previousState = Random.state;
            Random.InitState(seed);

            try
            {
                MapGraph graph = GenerateSlayTheSpireMap(config, encounterPool);
                graph.Seed = seed;
                return graph;
            }
            finally
            {
                Random.state = previousState;
            }
        }

        private static MapGraph GenerateSlayTheSpireMap(MapGeneratorConfig config, MapEncounterPool encounterPool)
        {
            int minLayers = Mathf.Clamp(Mathf.Min(config.LayerCountRange.Min, config.LayerCountRange.Max), 6, 8);
            int maxLayers = Mathf.Clamp(Mathf.Max(config.LayerCountRange.Min, config.LayerCountRange.Max), minLayers, 8);
            int layerCount = Random.Range(minLayers, maxLayers + 1); // Inclusive: 6, 7, or 8 layers
            int laneCount = Mathf.Clamp(config.LaneCount, 3, 6);

            MapGraph graph = new()
            {
                LayerCount = layerCount
            };

            // 1. Build candidate node grid (layer = horizontal X column Left->Right, lane = vertical Y row Top->Bottom)
            List<List<MapNode>> layers = new(layerCount);
            for (int layer = 0; layer < layerCount; layer++)
            {
                int nodesInLayer = layer == layerCount - 1 ? 1 : laneCount;
                List<MapNode> colNodes = new(nodesInLayer);
                for (int lane = 0; lane < nodesInLayer; lane++)
                {
                    MapNodeType initialType = layer == layerCount - 1 ? MapNodeType.Boss : MapNodeType.Enemy;
                    MapNode node = new($"{layer}_{lane}", layer, lane, initialType, Vector2.zero);
                    colNodes.Add(node);
                    graph.Nodes.Add(node);
                }
                layers.Add(colNodes);
            }

            // 2. Trace braided paths from Left (Layer 0) -> Pre-Boss (Layer N-2) -> Boss (Layer N-1)
            List<List<Vector2Int>> paths = BuildBraidedPaths(config, layerCount, laneCount);
            ApplyPathsToConnections(layers, paths);

            // 3. Remove X-crossing edges so dotted trails read cleanly like Slay the Spire
            RemoveCrossConnections(layers);

            // 4. Prune unused nodes and guarantee forward/boss connectivity
            HashSet<string> usedIds = CollectConnectedNodeIds(graph);
            EnsureForwardAndBossConnections(layers, usedIds);
            PruneUnusedNodes(graph, layers, usedIds);

            // 5. Assign organic Left-to-Right coordinates with jitter and anti-overlap relaxation
            AssignOrganicLeftToRightPositions(config, layers, layerCount, laneCount);

            // 6. Assign balanced Slay-the-Spire node types with strict anti-repetition & branch diversity
            AssignBalancedSlayTheSpireNodeTypes(config, graph, layers, layerCount);

            // 7. Assign varied combat encounters from pool (avoiding immediate duplicates on connected paths)
            AssignVariedEncounters(graph, encounterPool);

            graph.RefreshAvailability(null);
            return graph;
        }

        // ------------------------------------------------------------------
        // Step 2: Braided Path Generation
        // ------------------------------------------------------------------
        private static List<List<Vector2Int>> BuildBraidedPaths(MapGeneratorConfig config, int layerCount, int laneCount)
        {
            int bossLayer = layerCount - 1;
            int preBossLayer = layerCount - 2;
            Vector2Int bossPoint = new(bossLayer, 0);

            int startMin = Mathf.Clamp(config.NumberOfStartingNodes.Min, 2, laneCount);
            int startMax = Mathf.Clamp(config.NumberOfStartingNodes.Max, startMin, laneCount);
            int startCount = Random.Range(startMin, startMax + 1);

            int preMin = Mathf.Clamp(config.NumberOfPreBossNodes.Min, 2, laneCount);
            int preMax = Mathf.Clamp(config.NumberOfPreBossNodes.Max, preMin, laneCount);
            int preBossCount = Random.Range(preMin, preMax + 1);

            List<int> startLanes = PickWellSpreadLanes(laneCount, startCount);
            List<int> preBossLanes = PickWellSpreadLanes(laneCount, preBossCount);

            int totalPaths = Mathf.Max(startCount, preBossCount) + Mathf.Max(1, config.ExtraPaths);
            List<List<Vector2Int>> paths = new(totalPaths);

            for (int i = 0; i < totalPaths; i++)
            {
                int startLane = startLanes[i % startLanes.Count];
                int endLane = preBossLanes[(i + (i >= startLanes.Count ? Random.Range(0, preBossLanes.Count) : 0)) % preBossLanes.Count];

                List<Vector2Int> path = TraceSinglePath(startLane, endLane, preBossLayer, laneCount);
                path.Add(bossPoint);
                paths.Add(path);
            }

            return paths;
        }

        private static List<int> PickWellSpreadLanes(int laneCount, int pickCount)
        {
            if (pickCount >= laneCount)
            {
                return Enumerable.Range(0, laneCount).ToList();
            }

            // Guarantee visually balanced top-to-bottom entry points (like 1, 2, 3 in the reference)
            if (laneCount == 4 && pickCount == 3)
            {
                int[][] spreads =
                {
                    new[] { 0, 1, 3 },
                    new[] { 0, 2, 3 }
                };
                return spreads[Random.Range(0, spreads.Length)].ToList();
            }

            if (laneCount == 4 && pickCount == 2)
            {
                int[][] spreads =
                {
                    new[] { 0, 3 },
                    new[] { 0, 2 },
                    new[] { 1, 3 }
                };
                return spreads[Random.Range(0, spreads.Length)].ToList();
            }

            List<int> all = Enumerable.Range(0, laneCount).ToList();
            Shuffle(all);
            return all.Take(pickCount).OrderBy(x => x).ToList();
        }

        private static List<Vector2Int> TraceSinglePath(int startLane, int targetLane, int preBossLayer, int laneCount)
        {
            List<Vector2Int> path = new(preBossLayer + 1) { new Vector2Int(0, startLane) };
            int currentLane = startLane;
            List<int> candidates = new(3);

            for (int layer = 1; layer < preBossLayer; layer++)
            {
                candidates.Clear();
                int remainingSteps = preBossLayer - layer;

                for (int delta = -1; delta <= 1; delta++)
                {
                    int nextLane = currentLane + delta;
                    if (nextLane < 0 || nextLane >= laneCount) continue;
                    if (Mathf.Abs(targetLane - nextLane) <= remainingSteps)
                    {
                        candidates.Add(nextLane);
                    }
                }

                if (candidates.Count == 0)
                {
                    candidates.Add(Mathf.Clamp(currentLane, 0, laneCount - 1));
                }

                // Slightly favor diagonal weaves so paths don't collapse into boring parallel straight rails
                int chosenLane = candidates[Random.Range(0, candidates.Count)];
                if (candidates.Count > 1 && chosenLane == currentLane && Random.value < 0.42f)
                {
                    List<int> nonStraight = candidates.Where(c => c != currentLane).ToList();
                    chosenLane = nonStraight[Random.Range(0, nonStraight.Count)];
                }

                path.Add(new Vector2Int(layer, chosenLane));
                currentLane = chosenLane;
            }

            path.Add(new Vector2Int(preBossLayer, targetLane));
            return path;
        }

        private static void ApplyPathsToConnections(List<List<MapNode>> layers, List<List<Vector2Int>> paths)
        {
            foreach (List<Vector2Int> path in paths)
            {
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector2Int a = path[i];
                    Vector2Int b = path[i + 1];
                    AddConnection(layers[a.x][a.y], layers[b.x][b.y]);
                }
            }
        }

        // ------------------------------------------------------------------
        // Step 3 & 4: Untangle Crossings & Prune Unused Nodes
        // ------------------------------------------------------------------
        private static void RemoveCrossConnections(List<List<MapNode>> layers)
        {
            for (int layer = 0; layer < layers.Count - 2; layer++)
            {
                List<MapNode> curr = layers[layer];
                List<MapNode> next = layers[layer + 1];

                for (int r = 0; r < curr.Count - 1; r++)
                {
                    if (r + 1 >= next.Count) continue;

                    MapNode topLeft = curr[r];
                    MapNode botLeft = curr[r + 1];
                    MapNode topRight = next[r];
                    MapNode botRight = next[r + 1];

                    bool hasDiagDown = topLeft.NextNodeIds.Contains(botRight.Id);
                    bool hasDiagUp = botLeft.NextNodeIds.Contains(topRight.Id);
                    if (!hasDiagDown || !hasDiagUp) continue;

                    // Untangle cleanly: keep horizontal bridges and drop at least one crossing diagonal
                    AddConnection(topLeft, topRight);
                    AddConnection(botLeft, botRight);

                    float roll = Random.value;
                    if (roll < 0.34f)
                    {
                        topLeft.NextNodeIds.Remove(botRight.Id);
                        botLeft.NextNodeIds.Remove(topRight.Id);
                    }
                    else if (roll < 0.67f)
                    {
                        topLeft.NextNodeIds.Remove(botRight.Id);
                    }
                    else
                    {
                        botLeft.NextNodeIds.Remove(topRight.Id);
                    }
                }
            }
        }

        private static HashSet<string> CollectConnectedNodeIds(MapGraph graph)
        {
            HashSet<string> ids = new();
            foreach (MapNode node in graph.Nodes)
            {
                if (node.NextNodeIds.Count > 0) ids.Add(node.Id);
                foreach (string nextId in node.NextNodeIds) ids.Add(nextId);
            }
            return ids;
        }

        private static void EnsureForwardAndBossConnections(List<List<MapNode>> layers, HashSet<string> usedIds)
        {
            int bossLayer = layers.Count - 1;
            MapNode boss = layers[bossLayer][0];
            usedIds.Add(boss.Id);

            // Ensure every active node on layers 0..bossLayer-2 has at least one forward connection
            for (int layer = 0; layer < bossLayer - 1; layer++)
            {
                foreach (MapNode node in layers[layer])
                {
                    if (!usedIds.Contains(node.Id) || node.NextNodeIds.Count > 0) continue;

                    MapNode closest = layers[layer + 1]
                        .Where(candidate => usedIds.Contains(candidate.Id))
                        .OrderBy(candidate => Mathf.Abs(candidate.Column - node.Column))
                        .FirstOrDefault() ?? layers[layer + 1][Mathf.Clamp(node.Column, 0, layers[layer + 1].Count - 1)];

                    if (closest != null)
                    {
                        AddConnection(node, closest);
                        usedIds.Add(closest.Id);
                    }
                }
            }

            // Ensure every active pre-boss node connects directly to the Boss
            foreach (MapNode preBoss in layers[bossLayer - 1])
            {
                if (usedIds.Contains(preBoss.Id))
                {
                    AddConnection(preBoss, boss);
                }
            }
        }

        private static void PruneUnusedNodes(MapGraph graph, List<List<MapNode>> layers, HashSet<string> usedIds)
        {
            graph.Nodes.RemoveAll(n => !usedIds.Contains(n.Id));
            for (int i = 0; i < layers.Count; i++)
            {
                layers[i].RemoveAll(n => !usedIds.Contains(n.Id));
            }
        }

        // ------------------------------------------------------------------
        // Step 5: Organic Left-to-Right Positioning + Anti-Overlap Relaxation
        // ------------------------------------------------------------------
        private static void AssignOrganicLeftToRightPositions(
            MapGeneratorConfig config,
            List<List<MapNode>> layers,
            int layerCount,
            int laneCount)
        {
            float spanX = Mathf.Max(640f, config.MapCanvasSpan.x);
            float spanY = Mathf.Max(280f, config.MapCanvasSpan.y);
            float dx = spanX / Mathf.Max(1, layerCount - 1);
            float dy = spanY / Mathf.Max(1, laneCount - 1);

            float hJitter = Mathf.Clamp(config.HorizontalJitter, 0f, 0.40f);
            float vJitter = Mathf.Clamp(config.VerticalJitter, 0f, 0.40f);

            for (int layer = 0; layer < layers.Count; layer++)
            {
                bool isStart = layer == 0;
                bool isBoss = layer == layerCount - 1;

                foreach (MapNode node in layers[layer])
                {
                    if (isBoss)
                    {
                        node.Position = new Vector2(spanX * 0.5f, 0f);
                        continue;
                    }

                    float baseX = -spanX * 0.5f + layer * dx;
                    float baseY = spanY * 0.5f - node.Column * dy;

                    // Keep start edge neat on the left, give middle layers lively organic variation
                    float xOff = isStart
                        ? Random.Range(-0.07f, 0.07f) * dx
                        : Random.Range(-hJitter, hJitter) * dx;
                    float yOff = isStart
                        ? Random.Range(-0.16f, 0.16f) * dy
                        : Random.Range(-vJitter, vJitter) * dy;

                    node.Position = new Vector2(baseX + xOff, baseY + yOff);
                }
            }

            // Gentle separation passes so jittered neighbors never overlap visually
            List<MapNode> allNodes = layers.SelectMany(l => l).ToList();
            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = 0; i < allNodes.Count; i++)
                {
                    for (int j = i + 1; j < allNodes.Count; j++)
                    {
                        MapNode a = allNodes[i];
                        MapNode b = allNodes[j];
                        Vector2 delta = b.Position - a.Position;
                        float dist = delta.magnitude;
                        if (dist >= MinNodeSeparation || dist < 0.001f) continue;

                        Vector2 push = delta.normalized * ((MinNodeSeparation - dist) * 0.52f);
                        if (a.Type != MapNodeType.Boss) a.Position -= push;
                        if (b.Type != MapNodeType.Boss) b.Position += push;
                    }
                }
            }

            // Re-center vertical centroid cleanly so map sits balanced inside parchment frame
            if (allNodes.Count > 0)
            {
                float minY = allNodes.Min(n => n.Position.y);
                float maxY = allNodes.Max(n => n.Position.y);
                float midY = (minY + maxY) * 0.5f;
                foreach (MapNode n in allNodes)
                {
                    if (n.Type != MapNodeType.Boss)
                    {
                        n.Position = new Vector2(n.Position.x, n.Position.y - midY);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Step 6: Balanced Slay-the-Spire Node Type Assignment
        // ------------------------------------------------------------------
        private static void AssignBalancedSlayTheSpireNodeTypes(
            MapGeneratorConfig config,
            MapGraph graph,
            List<List<MapNode>> layers,
            int layerCount)
        {
            // Build reverse parent lookup so we enforce parent->child and sibling rules cleanly
            Dictionary<string, List<MapNode>> parentsById = new();
            foreach (MapNode node in graph.Nodes)
            {
                parentsById[node.Id] = new List<MapNode>();
            }
            foreach (MapNode parent in graph.Nodes)
            {
                foreach (string nextId in parent.NextNodeIds)
                {
                    if (parentsById.TryGetValue(nextId, out List<MapNode> list))
                    {
                        list.Add(parent);
                    }
                }
            }

            // 1. Fixed anchor layers: Layer 0 = Opening Combat Enemies, Final Layer = Boss
            foreach (MapNode startNode in layers[0])
            {
                startNode.Type = MapNodeType.Enemy;
            }
            foreach (MapNode bossNode in layers[layerCount - 1])
            {
                bossNode.Type = MapNodeType.Boss;
            }

            // 2. Pre-Boss sanctuary layer (layerCount - 2): Rest campfires + 1 Treasure if >= 3 nodes (matching StS + reference)
            List<MapNode> preBossNodes = layers[layerCount - 2];
            int treasurePreBossIndex = preBossNodes.Count >= 3 ? Random.Range(0, preBossNodes.Count) : -1;
            int totalTreasures = 0;
            int totalShops = 0;
            int totalElites = 0;
            int totalRests = 0;
            int totalEvents = 0;

            for (int i = 0; i < preBossNodes.Count; i++)
            {
                if (i == treasurePreBossIndex)
                {
                    preBossNodes[i].Type = MapNodeType.Treasure;
                    totalTreasures++;
                }
                else
                {
                    preBossNodes[i].Type = MapNodeType.Rest;
                    totalRests++;
                }
            }

            // 3. Middle layers (1 .. layerCount - 3): Weighted StS distribution with anti-repetition & sibling variety
            int midTreasureLayer = Mathf.Clamp(layerCount / 2, 2, Mathf.Max(2, layerCount - 3));

            for (int layer = 1; layer <= layerCount - 3; layer++)
            {
                List<MapNode> colNodes = layers[layer];
                HashSet<MapNodeType> usedInThisColumn = new();

                // Optional guaranteed mid-map Treasure if pre-boss didn't already get one
                int forcedMidTreasureIdx = (layer == midTreasureLayer && totalTreasures == 0 && colNodes.Count > 0)
                    ? Random.Range(0, colNodes.Count)
                    : -1;

                for (int i = 0; i < colNodes.Count; i++)
                {
                    MapNode node = colNodes[i];
                    if (i == forcedMidTreasureIdx)
                    {
                        node.Type = MapNodeType.Treasure;
                        totalTreasures++;
                        usedInThisColumn.Add(MapNodeType.Treasure);
                        continue;
                    }

                    MapNodeType chosen = PickBalancedNodeType(
                        config,
                        node,
                        layer,
                        layerCount,
                        parentsById[node.Id],
                        graph,
                        usedInThisColumn,
                        totalElites,
                        totalShops,
                        totalRests,
                        totalTreasures,
                        totalEvents);

                    node.Type = chosen;
                    usedInThisColumn.Add(chosen);

                    switch (chosen)
                    {
                        case MapNodeType.Elite: totalElites++; break;
                        case MapNodeType.Shop: totalShops++; break;
                        case MapNodeType.Rest: totalRests++; break;
                        case MapNodeType.Treasure: totalTreasures++; break;
                        case MapNodeType.Event: totalEvents++; break;
                    }
                }
            }

            // 4. Post-pass guarantees: every 6-8 layer map should offer at least 1 Elite, 1 Shop, and 1 Event!
            EnsureMinimumSpecialPresence(layers, layerCount, parentsById, MapNodeType.Elite, ref totalElites, minLayer: 2, maxLayer: layerCount - 3);
            EnsureMinimumSpecialPresence(layers, layerCount, parentsById, MapNodeType.Shop, ref totalShops, minLayer: 1, maxLayer: layerCount - 3);
            EnsureMinimumSpecialPresence(layers, layerCount, parentsById, MapNodeType.Event, ref totalEvents, minLayer: 1, maxLayer: layerCount - 3);
        }

        private static MapNodeType PickBalancedNodeType(
            MapGeneratorConfig config,
            MapNode node,
            int layer,
            int layerCount,
            List<MapNode> parents,
            MapGraph graph,
            HashSet<MapNodeType> usedInColumn,
            int totalElites,
            int totalShops,
            int totalRests,
            int totalTreasures,
            int totalEvents)
        {
            // Collect parent types, pre-assigned child types (e.g. Pre-Boss Rest/Treasure), and already-assigned sibling types
            HashSet<MapNodeType> parentTypes = new();
            HashSet<MapNodeType> childTypes = new();
            HashSet<MapNodeType> siblingTypes = new();
            foreach (string nextId in node.NextNodeIds)
            {
                MapNode child = graph.GetNode(nextId);
                if (child != null && child.Layer == layerCount - 2)
                {
                    childTypes.Add(child.Type);
                }
            }
            foreach (MapNode p in parents)
            {
                parentTypes.Add(p.Type);
                foreach (string siblingId in p.NextNodeIds)
                {
                    if (siblingId == node.Id) continue;
                    MapNode sib = graph.GetNode(siblingId);
                    if (sib != null && sib.Column < node.Column)
                    {
                        siblingTypes.Add(sib.Type);
                    }
                }
            }

            var candidates = new List<(MapNodeType type, float weight)>
            {
                (MapNodeType.Enemy, Mathf.Max(1f, config.EnemyWeight)),
                (MapNodeType.Event, Mathf.Max(0f, config.EventWeight)),
                (MapNodeType.Elite, Mathf.Max(0f, config.EliteWeight)),
                (MapNodeType.Shop, Mathf.Max(0f, config.ShopWeight)),
                (MapNodeType.Rest, Mathf.Max(0f, config.RestWeight)),
                (MapNodeType.Treasure, Mathf.Max(0f, config.TreasureWeight))
            };

            List<(MapNodeType type, float weight)> valid = new();
            foreach (var (type, baseWeight) in candidates)
            {
                if (baseWeight <= 0f) continue;

                // Slay the Spire positional rules
                if (type == MapNodeType.Elite && layer < 2) continue;
                if (type == MapNodeType.Rest && (layer < 2 || layer >= layerCount - 3)) continue;
                if (type == MapNodeType.Treasure && (layer < 2 || totalTreasures >= 2)) continue;
                if (type == MapNodeType.Shop && totalShops >= 2) continue;
                if (type == MapNodeType.Elite && totalElites >= 4) continue;

                // No back-to-back special nodes along any parent->node OR node->preBoss edge
                if (IsSpecialNoConsecutive(type) && (parentTypes.Contains(type) || childTypes.Contains(type))) continue;

                float w = baseWeight;

                // Boost underrepresented special types so maps stay balanced
                if (type == MapNodeType.Elite && layer >= 2 && totalElites == 0) w *= 1.55f;
                if (type == MapNodeType.Shop && totalShops == 0) w *= 1.35f;
                if (type == MapNodeType.Event && totalEvents == 0) w *= 1.30f;

                // Strong sibling diversity: encourage different choices when branching from the same parent!
                if (siblingTypes.Contains(type))
                {
                    w *= type == MapNodeType.Enemy ? 0.35f : 0.14f;
                }

                // Gentle column diversity so a column doesn't become all identical copies
                if (usedInColumn.Contains(type))
                {
                    w *= type == MapNodeType.Enemy ? 0.65f : 0.32f;
                }

                valid.Add((type, Mathf.Max(0.1f, w)));
            }

            if (valid.Count == 0) return MapNodeType.Enemy;

            float totalW = valid.Sum(v => v.weight);
            float roll = Random.Range(0f, totalW);
            float acc = 0f;
            foreach (var (type, weight) in valid)
            {
                acc += weight;
                if (roll <= acc) return type;
            }

            return valid[valid.Count - 1].type;
        }

        private static void EnsureMinimumSpecialPresence(
            List<List<MapNode>> layers,
            int layerCount,
            Dictionary<string, List<MapNode>> parentsById,
            MapNodeType desiredType,
            ref int currentCount,
            int minLayer,
            int maxLayer)
        {
            if (currentCount > 0 || minLayer > maxLayer) return;

            List<MapNode> candidates = new();
            for (int l = minLayer; l <= maxLayer && l < layerCount - 1; l++)
            {
                foreach (MapNode n in layers[l])
                {
                    if (n.Type != MapNodeType.Enemy) continue;
                    bool parentConflict = IsSpecialNoConsecutive(desiredType) && parentsById[n.Id].Any(p => p.Type == desiredType);
                    if (!parentConflict) candidates.Add(n);
                }
            }

            if (candidates.Count > 0)
            {
                MapNode pick = candidates[Random.Range(0, candidates.Count)];
                pick.Type = desiredType;
                currentCount++;
            }
        }

        private static bool IsSpecialNoConsecutive(MapNodeType type)
        {
            return type == MapNodeType.Elite ||
                   type == MapNodeType.Rest ||
                   type == MapNodeType.Shop ||
                   type == MapNodeType.Treasure;
        }

        // ------------------------------------------------------------------
        // Step 7: Varied Encounter Assignment
        // ------------------------------------------------------------------
        private static void AssignVariedEncounters(MapGraph graph, MapEncounterPool encounterPool)
        {
            if (encounterPool == null) return;

            Dictionary<string, EncounterData> parentEncounters = new();
            foreach (MapNode node in graph.Nodes.OrderBy(n => n.Layer).ThenBy(n => n.Column))
            {
                if (!node.StartsCombat)
                {
                    node.Encounter = null;
                    continue;
                }

                EncounterData chosen = encounterPool.GetEncounter(node.Type);

                // Retry up to 4 times so connected combat nodes avoid repeating the exact same fight
                for (int attempt = 0; attempt < 4 && chosen != null; attempt++)
                {
                    bool matchesParent = parentEncounters.TryGetValue(node.Id, out EncounterData parentEnc) && parentEnc == chosen;
                    if (!matchesParent) break;
                    chosen = encounterPool.GetEncounter(node.Type);
                }

                node.Encounter = chosen;

                foreach (string nextId in node.NextNodeIds)
                {
                    if (!parentEncounters.ContainsKey(nextId) && chosen != null)
                    {
                        parentEncounters[nextId] = chosen;
                    }
                }
            }
        }

        private static void AddConnection(MapNode from, MapNode to)
        {
            if (from == null || to == null) return;
            if (!from.NextNodeIds.Contains(to.Id))
            {
                from.NextNodeIds.Add(to.Id);
            }
        }

        private static void Shuffle<T>(List<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
