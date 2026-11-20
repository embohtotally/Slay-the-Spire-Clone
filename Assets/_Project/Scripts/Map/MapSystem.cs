using PlushieOdyssey.Core;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Levels;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI.Map;
using UnityEngine;

namespace PlushieOdyssey.Map
{
    public class MapSystem : MonoBehaviour
    {
        [Header("Procedural Generation")]
        [SerializeField] private MapGeneratorConfig generatorConfig;
        [SerializeField] private MapEncounterPool encounterPool;

        [Header("Views (ProceduralMapRendererUI preferred; MapView supported as legacy fallback)")]
        [SerializeField] private ProceduralMapRendererUI proceduralRenderer;
        [SerializeField] private MapView mapView;

        [Header("Scene Routing")]
        [SerializeField] private string combatSceneName = "Game";
        [SerializeField] private string merchantSceneName = "Merchant";
        [SerializeField] private string restSceneName = "Rest";
        [SerializeField] private string eventSceneName = "Event";
        [SerializeField] private string treasureSceneName = "Treasure";
        [SerializeField] private string levelSelectSceneName = "Levels";
        [SerializeField] private bool completeCurrentLevelOnBossCleared = true;

        private MapGraph graph;

        public MapGraph CurrentGraph => graph;

        private void Start()
        {
            if (proceduralRenderer == null)
            {
                proceduralRenderer = FindFirstObjectByType<ProceduralMapRendererUI>();
            }

            if (RunManager.Instance == null)
            {
                // Allow standalone Map.unity preview even if GameManager wasn't loaded first
                graph = ProceduralMapGenerator.Generate(generatorConfig, encounterPool);
                RenderCurrentGraph(null, animateEntrance: true);
                return;
            }

            if (!RunManager.Instance.HasActiveRun)
            {
                RunManager.Instance.StartNewRun(ProceduralMapGenerator.Generate(generatorConfig, encounterPool));
            }

            graph = RunManager.Instance.CurrentMap;

            // If returning to the map after defeating the Act Boss, complete the level cleanly
            if (completeCurrentLevelOnBossCleared && graph.IsBossCleared() && LevelProgressionManager.Instance != null && LevelProgressionManager.Instance.HasCurrentLevel)
            {
                LevelProgressionManager.Instance.CompleteCurrentLevel();
                if (!string.IsNullOrWhiteSpace(levelSelectSceneName))
                {
                    SceneLoader.LoadScene(levelSelectSceneName);
                    return;
                }
            }

            graph.RefreshAvailability(RunManager.Instance.CurrentMapNodeId);
            RenderCurrentGraph(RunManager.Instance.CurrentMapNodeId, animateEntrance: true);
        }

        public void SelectNode(MapNode selectedNode)
        {
            if (selectedNode == null || graph == null) return;
            if (!selectedNode.IsAvailable || selectedNode.IsVisited) return;

            graph.MarkVisited(selectedNode.Id);
            if (RunManager.Instance != null)
            {
                RunManager.Instance.SelectMapNode(selectedNode);
            }

            graph.RefreshAvailability(selectedNode.Id);
            RenderCurrentGraph(selectedNode.Id, animateEntrance: false);
            HandleNode(selectedNode);
        }

        public void StartNewRun()
        {
            MapGraph fresh = ProceduralMapGenerator.Generate(generatorConfig, encounterPool);
            if (RunManager.Instance != null)
            {
                RunManager.Instance.StartNewRun(fresh);
                graph = RunManager.Instance.CurrentMap;
            }
            else
            {
                graph = fresh;
            }

            graph.RefreshAvailability(null);
            RenderCurrentGraph(null, animateEntrance: true);
        }

        private void RenderCurrentGraph(string currentNodeId, bool animateEntrance)
        {
            if (proceduralRenderer != null)
            {
                proceduralRenderer.RenderMap(graph, currentNodeId, interactive: true, onNodeClicked: SelectNode, animateEntrance: animateEntrance);
                return;
            }

            if (mapView != null)
            {
                mapView.ShowMap(graph, this);
            }
        }

        private void HandleNode(MapNode node)
        {
            if (node.StartsCombat)
            {
                if (node.Encounter == null)
                {
                    global::PlushieOdyssey.Core.Logger.LogWarning($"Map node {node.Id} ({node.Type}) has no EncounterData. Combat will use MatchSetupSystem fallback enemies.");
                }

                string sceneToLoad = combatSceneName;
                if (PlayerPrefs.GetInt("HasPlayedCardTutorial", 0) == 0 && !string.IsNullOrEmpty(sceneToLoad))
                {
                    sceneToLoad = combatSceneName; // Route straight to active Game.unity scene
                }

                SceneLoader.LoadScene(sceneToLoad);
                return;
            }

            RunManager.Instance?.ClearSelectedEncounter();

            string targetScene = node.Type switch
            {
                MapNodeType.Shop => merchantSceneName,
                MapNodeType.Rest => restSceneName,
                MapNodeType.Event => eventSceneName,
                MapNodeType.Treasure => treasureSceneName,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(targetScene))
            {
                SceneLoader.LoadScene(targetScene);
            }
            else
            {
                global::PlushieOdyssey.Core.Logger.Log($"Resolved map node: {node.Type}.");
            }
        }
    }
}
