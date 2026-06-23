using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ManualMapController : MonoBehaviour
{
    [Header("Node Discovery")]
    [SerializeField] private bool autoFindNodesInChildren = true;
    [SerializeField] private List<ManualMapNode> nodes = new();

    [Header("Run / Combat")]
    [SerializeField] private MapEncounterPool encounterPool;
    [SerializeField] private string defaultCombatSceneName = "Game";
    [Tooltip("Optional default scene used when a Shop node resolves as merchant. Leave empty if shop uses custom UnityEvents instead.")]
    [SerializeField] private string defaultMerchantSceneName = "Merchant";
    [Tooltip("Creates a lightweight RunManager map so combat victory can return to the map scene.")]
    [SerializeField] private bool createRunForCombatReturn = true;
    [Tooltip("Reset RunManager only when there is no saved manual map state yet. Keeps combat returns from restarting the map.")]
    [SerializeField] private bool resetRunWhenNoSavedState = true;

    [Header("State Persistence")]
    [Tooltip("Unique key for saving this manual map's runtime node states between scene reloads.")]
    [SerializeField] private string mapId = "ManualMap";
    [Tooltip("Useful during testing. If true, inspector Initial State is applied every time this scene starts.")]
    [SerializeField] private bool forceResetStateOnStart;

    private static readonly Dictionary<string, Dictionary<string, ManualMapNodeState>> SavedStatesByMapId = new();

    private void Awake()
    {
        EnsureRunManagerExists();
        CollectNodes();
        InitializeNodes();
    }

    private void Start()
    {
        bool restored = !forceResetStateOnStart && RestoreSavedStates();
        if (!restored)
        {
            ApplyInitialStates();
        }

        RefreshAllUnlockRules();
        SaveStates();

        if (createRunForCombatReturn && RunManager.Instance != null)
        {
            bool shouldResetRun = resetRunWhenNoSavedState && !restored;
            if (!RunManager.Instance.HasActiveRun || shouldResetRun)
            {
                RunManager.Instance.StartNewRun(CreateRuntimeGraph());
            }
        }
    }

    public void SelectNode(ManualMapNode node)
    {
        if (node == null || !node.CanSelect) return;

        node.OnSelected?.Invoke();

        if (node.CompleteOnClick)
        {
            CompleteNode(node);
        }

        switch (node.ClickAction)
        {
            case ManualMapNodeAction.StartCombat:
                StartCombat(node);
                break;

            case ManualMapNodeAction.LoadScene:
                LoadNodeScene(node);
                break;

            case ManualMapNodeAction.InvokeEventsOnly:
                break;

            case ManualMapNodeAction.ResolveByNodeType:
            default:
                ResolveByNodeType(node);
                break;
        }
    }

    public void CompleteNode(ManualMapNode node)
    {
        if (node == null) return;

        node.CompleteAndApplyOutputs();
        RefreshAllUnlockRules();
        SaveStates();
    }

    public void ResetManualMap()
    {
        SavedStatesByMapId.Remove(mapId);
        ApplyInitialStates();
        RefreshAllUnlockRules();
        SaveStates();

        if (createRunForCombatReturn && RunManager.Instance != null)
        {
            RunManager.Instance.StartNewRun(CreateRuntimeGraph());
        }
    }

    public void ActivateNode(ManualMapNode node)
    {
        if (node == null || node.IsCompleted) return;
        node.SetActive();
        SaveStates();
    }

    public void DeactivateNode(ManualMapNode node)
    {
        if (node == null || node.IsCompleted) return;
        node.SetInactive();
        SaveStates();
    }

    public void HideNode(ManualMapNode node)
    {
        if (node == null || node.IsCompleted) return;
        node.SetHidden();
        SaveStates();
    }

    private void ResolveByNodeType(ManualMapNode node)
    {
        if (node.StartsCombat())
        {
            StartCombat(node);
            return;
        }

        if (node.NodeType == MapNodeType.Shop)
        {
            OpenMerchant(node);
            return;
        }

        Debug.Log($"Manual map node '{node.NodeId}' resolved as {node.NodeType}. Add UnityEvents to open a shop, rest screen, reward, or custom UI.");
        if (RunManager.Instance != null)
        {
            RunManager.Instance.ClearSelectedEncounter();
        }
    }

    private void OpenMerchant(ManualMapNode node)
    {
        string sceneName = string.IsNullOrWhiteSpace(node.SceneNameOverride) ? defaultMerchantSceneName : node.SceneNameOverride;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.Log($"Manual map shop node '{node.NodeId}' selected. No merchant scene is set, so only node events were invoked.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void StartCombat(ManualMapNode node)
    {
        EnsureRunManagerExists();

        if (RunManager.Instance != null)
        {
            if (createRunForCombatReturn && !RunManager.Instance.HasActiveRun)
            {
                RunManager.Instance.StartNewRun(CreateRuntimeGraph());
            }

            MapNode runtimeNode = node.CreateRuntimeMapNode(encounterPool, nodes.IndexOf(node));
            RunManager.Instance.SelectMapNode(runtimeNode);
        }

        if (node.ResolveEncounter(encounterPool) == null)
        {
            Debug.LogWarning($"Manual map node '{node.NodeId}' ({node.NodeType}) has no EncounterData. Combat will use MatchSetupSystem fallback enemies.");
        }

        string sceneName = string.IsNullOrWhiteSpace(node.SceneNameOverride) ? defaultCombatSceneName : node.SceneNameOverride;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"Manual map node '{node.NodeId}' tried to start combat, but no combat scene name is set.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void LoadNodeScene(ManualMapNode node)
    {
        string sceneName = string.IsNullOrWhiteSpace(node.SceneNameOverride) ? defaultCombatSceneName : node.SceneNameOverride;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"Manual map node '{node.NodeId}' tried to load a scene, but no scene name is set.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void CollectNodes()
    {
        nodes ??= new List<ManualMapNode>();

        if (!autoFindNodesInChildren)
        {
            nodes.RemoveAll(node => node == null);
            return;
        }

        nodes = GetComponentsInChildren<ManualMapNode>(true)
            .Where(node => node != null)
            .Distinct()
            .ToList();
    }

    private void InitializeNodes()
    {
        foreach (ManualMapNode node in nodes)
        {
            if (node != null)
            {
                node.Initialize(this);
            }
        }
    }

    private void ApplyInitialStates()
    {
        foreach (ManualMapNode node in nodes)
        {
            if (node != null)
            {
                node.ApplyInitialState();
            }
        }
    }

    private void RefreshAllUnlockRules()
    {
        foreach (ManualMapNode node in nodes)
        {
            if (node != null)
            {
                node.RefreshUnlockRule();
            }
        }
    }

    private bool RestoreSavedStates()
    {
        if (string.IsNullOrWhiteSpace(mapId)) return false;
        if (!SavedStatesByMapId.TryGetValue(mapId, out Dictionary<string, ManualMapNodeState> savedStates)) return false;

        foreach (ManualMapNode node in nodes)
        {
            if (node == null) continue;

            if (savedStates.TryGetValue(node.NodeId, out ManualMapNodeState savedState))
            {
                node.RestoreState(savedState);
            }
            else
            {
                node.ApplyInitialState();
            }
        }

        return true;
    }

    private void SaveStates()
    {
        if (string.IsNullOrWhiteSpace(mapId)) return;

        Dictionary<string, ManualMapNodeState> savedStates = new();
        foreach (ManualMapNode node in nodes)
        {
            if (node == null) continue;
            savedStates[node.NodeId] = node.CurrentState;
        }

        SavedStatesByMapId[mapId] = savedStates;
    }

    private MapGraph CreateRuntimeGraph()
    {
        MapGraph graph = new();
        for (int i = 0; i < nodes.Count; i++)
        {
            ManualMapNode node = nodes[i];
            if (node == null) continue;
            graph.Nodes.Add(node.CreateRuntimeMapNode(encounterPool, i));
        }

        return graph;
    }

    private static void EnsureRunManagerExists()
    {
        if (RunManager.Instance != null) return;

        GameObject runManagerObject = new("Run Manager");
        runManagerObject.AddComponent<RunManager>();
    }
}
