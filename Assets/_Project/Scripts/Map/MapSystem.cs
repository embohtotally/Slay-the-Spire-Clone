using UnityEngine;
using UnityEngine.SceneManagement;

public class MapSystem : MonoBehaviour
{
    [SerializeField] private MapGeneratorConfig generatorConfig;
    [SerializeField] private MapEncounterPool encounterPool;
    [SerializeField] private MapView mapView;
    [SerializeField] private string combatSceneName = "Game";

    private MapGraph graph;

    private void Awake()
    {
        EnsureRunManagerExists();
    }

    private void Start()
    {
        if (RunManager.Instance == null)
        {
            Debug.LogError("MapSystem could not create or find RunManager.");
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
                Debug.LogWarning($"Map node {node.Id} ({node.Type}) has no EncounterData. Combat will use MatchSetupSystem fallback enemies.");
            }

            SceneManager.LoadScene(combatSceneName);
            return;
        }

        Debug.Log($"Resolved non-combat map node: {node.Type}. Placeholder reward/event screen not implemented yet; path advanced on map.");
        RunManager.Instance.ClearSelectedEncounter();
    }

    private static void EnsureRunManagerExists()
    {
        if (RunManager.Instance != null) return;

        GameObject runManagerObject = new("Run Manager");
        runManagerObject.AddComponent<RunManager>();
    }
}
