using Gameseed26;
using UnityEngine;

public class MapSystem : MonoBehaviour
{
    [SerializeField] private MapGeneratorConfig generatorConfig;
    [SerializeField] private MapEncounterPool encounterPool;
    [SerializeField] private MapView mapView;
    [SerializeField] private string combatSceneName = "Game";

    private MapGraph graph;

    private void Start()
    {
        if (RunManager.Instance == null)
        {
            Gameseed26.Logger.LogError("MapSystem could not find RunManager. Keep persistent run systems on Resources/GameManager.");
            return;
        }

        if (!RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.StartNewRun(ProceduralMapGenerator.Generate(generatorConfig, encounterPool));
        }

        graph = RunManager.Instance.CurrentMap;
        graph.RefreshAvailability(RunManager.Instance.CurrentMapNodeId);
        mapView.ShowMap(graph, this);
    }

    public void SelectNode(MapNode selectedNode)
    {
        if (selectedNode == null || graph == null) return;
        if (!selectedNode.IsAvailable || selectedNode.IsVisited) return;

        selectedNode.IsVisited = true;
        RunManager.Instance.SelectMapNode(selectedNode);
        graph.RefreshAvailability(selectedNode.Id);
        mapView.Refresh(graph, this);

        HandleNode(selectedNode);
    }

    public void StartNewRun()
    {
        RunManager.Instance.StartNewRun(ProceduralMapGenerator.Generate(generatorConfig, encounterPool));
        graph = RunManager.Instance.CurrentMap;
        graph.RefreshAvailability(null);
        mapView.Refresh(graph, this);
    }

    private void HandleNode(MapNode node)
    {
        if (node.StartsCombat)
        {
            if (node.Encounter == null)
            {
                Gameseed26.Logger.LogWarning($"Map node {node.Id} ({node.Type}) has no EncounterData. Combat will use MatchSetupSystem fallback enemies.");
            }

            SceneLoader.LoadScene(combatSceneName);
            return;
        }

        Gameseed26.Logger.Log($"Resolved non-combat map node: {node.Type}. Placeholder reward/event screen not implemented yet; path advanced on map.");
        RunManager.Instance.ClearSelectedEncounter();
    }

}
