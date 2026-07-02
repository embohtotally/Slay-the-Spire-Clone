using System.Collections.Generic;
using System.Linq;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public enum ManualMapProgressDirection
{
    BottomToTop,
    TopToBottom,
    LeftToRight,
    RightToLeft
}

public class ManualMapController : MonoBehaviour
{
    private const string AutoLineParentName = "Auto Lines";
    private const string AutoLineNamePrefix = "AutoLine_";

    [Header("Node Discovery")]
    [SerializeField] private bool autoFindNodesInChildren = true;
    [SerializeField] private List<ManualMapNode> nodes = new();

    [Header("Run / Combat")]
    [SerializeField] private MapEncounterPool encounterPool;
    [SerializeField, Scene] private string defaultCombatSceneName = "Game";
    [Tooltip("Optional default scene used when a Shop node resolves as merchant. Leave empty if shop uses custom UnityEvents instead.")]
    [SerializeField, Scene] private string defaultMerchantSceneName = "Merchant";
    [Tooltip("Optional default scene used when a Rest node resolves by node type.")]
    [SerializeField, Scene] private string defaultRestSceneName = "Rest";
    [Tooltip("Optional default scene used when an Event node resolves by node type. Leave empty if events use UnityEvents/custom logic.")]
    [SerializeField, Scene] private string defaultEventSceneName = "Event";
    [Tooltip("Optional default scene used when a Treasure node resolves by node type. Leave empty if treasure uses UnityEvents/custom logic.")]
    [SerializeField, Scene] private string defaultTreasureSceneName = "Treasure";
    [Tooltip("Creates a lightweight RunManager map so combat victory can return to the map scene.")]
    [SerializeField] private bool createRunForCombatReturn = true;
    [Tooltip("Reset RunManager only when there is no saved manual map state yet. Keeps combat returns from restarting the map.")]
    [SerializeField] private bool resetRunWhenNoSavedState = true;
    [Tooltip("When a node is completed, disable every other unfinished node except its Next Nodes. This makes the manual map behave like Slay the Spire path selection.")]
    [SerializeField] private bool enforceSinglePathProgression = true;

    [Header("Runtime Run State")]
    [Tooltip("Unique key for saving this manual map's runtime node states between scene reloads.")]
    [SerializeField] private string mapId = "ManualMap";
    [Tooltip("Useful during testing. If true, inspector Initial State is applied every time this scene starts.")]
    [SerializeField] private bool forceResetStateOnStart;

    [Header("Level Completion")]
    [Tooltip("If true, a cleared terminal node completes the current LevelProgressionManager level.")]
    [SerializeField] private bool completeCurrentLevelWhenCleared;
    [SerializeField, ShowIf("completeCurrentLevelWhenCleared"), Scene] private string levelSelectSceneName = "Levels";
    [SerializeField, ShowIf("completeCurrentLevelWhenCleared")] private bool returnToLevelSelectOnComplete = true;
    [SerializeField, ShowIf("completeCurrentLevelWhenCleared")] private UnityEvent OnMapCompleted;

    [Header("Audio")]
    [SerializeField] private TuneSfxCue nodeSelectedSfx;
    [SerializeField] private TuneSfxCue combatNodeSfx;
    [SerializeField] private TuneSfxCue eliteNodeSfx;
    [SerializeField] private TuneSfxCue bossNodeSfx;
    [SerializeField] private TuneSfxCue shopNodeSfx;
    [SerializeField] private TuneSfxCue restNodeSfx;
    [SerializeField] private TuneSfxCue eventNodeSfx;
    [SerializeField] private TuneSfxCue treasureNodeSfx;
    [SerializeField] private TuneSfxCue mapCompletedSfx;

    [Header("Designer Auto Setup")]
    [Tooltip("Optional root used by the auto setup button. Leave empty to use this controller's transform.")]
    [SerializeField] private Transform autoSetupRoot;
    [SerializeField] private ManualMapProgressDirection autoProgressDirection = ManualMapProgressDirection.BottomToTop;
    [Tooltip("Nodes whose progress-axis positions are this close are treated as the same layer/row.")]
    [Min(1f)] [SerializeField] private float autoLayerTolerance = 90f;
    [Tooltip("How many closest nodes on the next layer each node connects to.")]
    [Min(1)] [SerializeField] private int autoMaxConnectionsPerNode = 2;
    [Tooltip("0 = unlimited. If above 0, next-layer nodes farther than this on the side axis are ignored unless no candidate remains.")]
    [Min(0f)] [SerializeField] private float autoMaxSideDistance = 0f;
    [SerializeField] private bool autoEnsureEveryNodeHasIncoming = true;
    [SerializeField] private bool autoOverwriteIds = true;
    [SerializeField] private string autoIdPrefix = "map";
    [SerializeField] private bool autoRenameGameObjects;
    [Tooltip("If on, one node on the first layer becomes START and the last layer becomes BOSS. Middle node types are left as the designer set them.")]
    [SerializeField] private bool autoAssignEndpointTypes = true;
    [SerializeField] private bool autoSetSingleFirstLayerNodeAsStart = true;
    [SerializeField] private bool autoSetLastLayerAsBoss = true;
    [Tooltip("When auto setup/rebuild runs, clear legacy Activate/Deactivate/Hide lists so Next Nodes remains the single source of truth.")]
    [SerializeField] private bool autoClearLegacyOutputLists = true;

    [Header("Designer Auto Lines")]
    [SerializeField] private bool autoRebuildLinesAfterSetup = true;
    [SerializeField] private RectTransform autoLineParent;
    [SerializeField] private float autoLineThickness = 6f;
    [SerializeField] private Color autoLineColor = new(0.32f, 0.32f, 0.38f, 0.75f);

    [Header("Node Type Icons")]
    [Tooltip("If enabled, Auto Setup From Positions also applies icon sprites after generating node data. Keep this off on Level-select layouts; enable it only for Map layouts that should use Map node icons.")]
    [SerializeField] private bool autoApplyNodeIconsAfterAutoSetup;
    [SerializeField] private bool autoLoadDefaultNodeIconsWhenMissing = true;
    [SerializeField] private bool hideNodeLabelsWhenApplyingIcons = true;
    [SerializeField] private bool setNodeIconsNativeSize;
    [InfoBox("Custom node type is intentionally skipped. Start/Treasure icons are optional because this project may use first-row selectable nodes and non-combat reward scenes instead of dedicated icons.")]
    [SerializeField] private Sprite startNodeIcon;
    [SerializeField] private Sprite enemyNodeIcon;
    [SerializeField] private Sprite eliteNodeIcon;
    [SerializeField] private Sprite eventNodeIcon;
    [SerializeField] private Sprite shopNodeIcon;
    [SerializeField] private Sprite restNodeIcon;
    [SerializeField] private Sprite treasureNodeIcon;
    [SerializeField] private Sprite bossNodeIcon;

    private static readonly Dictionary<string, Dictionary<string, ManualMapNodeState>> SavedStatesByMapId = new();
    private bool mapCompletionHandled;

    public string MapId => mapId;
    public IReadOnlyList<ManualMapNode> Nodes => nodes;

    public void SetRuntimeMapId(string newMapId)
    {
        if (string.IsNullOrWhiteSpace(newMapId)) return;
        mapId = newMapId.Trim();
    }

    public void ConfigureLevelCompletion(bool enabled, string returnSceneName, bool autoReturn)
    {
        completeCurrentLevelWhenCleared = enabled;
        if (!string.IsNullOrWhiteSpace(returnSceneName))
        {
            levelSelectSceneName = returnSceneName.Trim();
        }

        returnToLevelSelectOnComplete = autoReturn;
    }

    public static void ClearSavedState(string targetMapId)
    {
        if (string.IsNullOrWhiteSpace(targetMapId)) return;
        SavedStatesByMapId.Remove(targetMapId.Trim());
    }

    public static void ClearAllSavedStates()
    {
        SavedStatesByMapId.Clear();
    }

    private void Awake()
    {
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
                StartRunFromCurrentMapGraph();
            }
        }

        CheckForMapCompletion();
    }

    public void SelectNode(ManualMapNode node)
    {
        if (node == null || !node.CanSelect) return;

        nodeSelectedSfx?.Play(this, node.transform);
        PlayNodeTypeSfx(node);
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
        if (enforceSinglePathProgression)
        {
            DisableNodesOutsideSelectedPath(node);
        }

        RefreshAllUnlockRules();
        SaveStates();
        if (!node.StartsCombat())
        {
            CheckForMapCompletion();
        }
    }

    public void ResetManualMap()
    {
        SavedStatesByMapId.Remove(mapId);
        ApplyInitialStates();
        RefreshAllUnlockRules();
        SaveStates();

        if (createRunForCombatReturn && RunManager.Instance != null)
        {
            StartRunFromCurrentMapGraph();
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
        node.SetDisabled();
        SaveStates();
    }

    public void HideNode(ManualMapNode node)
    {
        if (node == null || node.IsCompleted) return;
        node.SetHiddenDisabled();
        SaveStates();
    }

    [Button("Auto Setup From Positions", EButtonEnableMode.Editor)]
    private void AutoSetupManualMapFromScene()
    {
        CollectNodes();
        List<AutoLayer> layers = BuildAutoLayers();
        if (layers.Count == 0)
        {
            Gameseed26.Logger.LogWarning("Manual map auto setup found no ManualMapNode children.");
            return;
        }

        Dictionary<ManualMapNode, List<ManualMapNode>> outgoing = CreateNodeListMap();
        Dictionary<ManualMapNode, List<ManualMapNode>> incoming = CreateNodeListMap();
        BuildAutoConnections(layers, outgoing, incoming);

        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            AutoLayer layer = layers[layerIndex];
            for (int columnIndex = 0; columnIndex < layer.Nodes.Count; columnIndex++)
            {
                ManualMapNode node = layer.Nodes[columnIndex];
                RecordEditorObject(node, "Auto Setup Manual Map Node");

                if (autoAssignEndpointTypes)
                {
                    ApplyEndpointTypeIfNeeded(node, layerIndex, layers.Count, layer.Nodes.Count);
                }

                ManualMapNodeState initialState = layerIndex == 0 ? ManualMapNodeState.Active : ManualMapNodeState.Inactive;
                ManualMapUnlockRule unlockRule = layerIndex == 0 ? ManualMapUnlockRule.AlwaysActive : ManualMapUnlockRule.AfterAnyRequiredCompleted;
                string generatedId = GenerateNodeId(layerIndex, columnIndex, node);

                node.ApplyDesignerAutoSetup(
                    generatedId,
                    layerIndex,
                    columnIndex,
                    initialState,
                    unlockRule,
                    incoming[node],
                    outgoing[node],
                    autoOverwriteIds,
                    autoClearLegacyOutputLists);

                if (autoRenameGameObjects)
                {
                    node.gameObject.name = generatedId;
                }

                MarkEditorObjectDirty(node);
                MarkEditorObjectDirty(node.gameObject);
            }
        }

        nodes = layers.SelectMany(layer => layer.Nodes).ToList();
        if (autoApplyNodeIconsAfterAutoSetup)
        {
            ApplyNodeTypeIconsToNodes(false);
        }

        if (autoRebuildLinesAfterSetup)
        {
            RebuildAutoLines();
        }

        MarkEditorObjectDirty(this);
        MarkSceneDirty();
        Gameseed26.Logger.Log($"Manual map auto setup finished: {nodes.Count} nodes, {layers.Count} layers, {outgoing.Sum(pair => pair.Value.Count)} connections.");
    }

    [Button("Rebuild From Edited Next Nodes", EButtonEnableMode.Editor)]
    private void RebuildFromEditedNextNodes()
    {
        CollectNodes();
        List<AutoLayer> layers = BuildAutoLayers();
        if (layers.Count == 0)
        {
            Gameseed26.Logger.LogWarning("Manual map graph rebuild found no ManualMapNode children.");
            return;
        }

        Dictionary<ManualMapNode, List<ManualMapNode>> outgoing = CreateNodeListMap();
        Dictionary<ManualMapNode, List<ManualMapNode>> incoming = CreateNodeListMap();
        BuildConnectionsFromEditedNextNodes(outgoing, incoming);

        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            AutoLayer layer = layers[layerIndex];
            for (int columnIndex = 0; columnIndex < layer.Nodes.Count; columnIndex++)
            {
                ManualMapNode node = layer.Nodes[columnIndex];
                RecordEditorObject(node, "Rebuild Manual Map From Next Nodes");

                if (autoAssignEndpointTypes)
                {
                    ApplyEndpointTypeIfNeeded(node, layerIndex, layers.Count, layer.Nodes.Count);
                }

                bool isStartNode = incoming[node].Count == 0;
                ManualMapNodeState initialState = isStartNode ? ManualMapNodeState.Active : ManualMapNodeState.Inactive;
                ManualMapUnlockRule unlockRule = isStartNode ? ManualMapUnlockRule.AlwaysActive : ManualMapUnlockRule.AfterAnyRequiredCompleted;
                string generatedId = GenerateNodeId(layerIndex, columnIndex, node);

                node.ApplyDesignerAutoSetup(
                    generatedId,
                    layerIndex,
                    columnIndex,
                    initialState,
                    unlockRule,
                    incoming[node],
                    outgoing[node],
                    false,
                    autoClearLegacyOutputLists);

                MarkEditorObjectDirty(node);
            }
        }

        nodes = layers.SelectMany(layer => layer.Nodes).ToList();
        if (autoApplyNodeIconsAfterAutoSetup)
        {
            ApplyNodeTypeIconsToNodes(false);
        }

        if (autoRebuildLinesAfterSetup)
        {
            RebuildAutoLines();
        }

        MarkEditorObjectDirty(this);
        MarkSceneDirty();
        Gameseed26.Logger.Log($"Manual map rebuilt from edited Next Nodes: {nodes.Count} nodes, {outgoing.Sum(pair => pair.Value.Count)} connections.");
    }

    [Button("Rebuild Lines From Next Nodes", EButtonEnableMode.Editor)]
    private void RebuildAutoLines()
    {
        CollectNodes();
        RectTransform lineParent = GetOrCreateLineParent();
        if (lineParent == null) return;

        ClearAutoGeneratedLines(lineParent);

        int lineCount = 0;
        foreach (ManualMapNode node in nodes)
        {
            if (node == null) continue;

            foreach (ManualMapNode nextNode in node.NextNodes)
            {
                if (nextNode == null) continue;
                CreateAutoLine(lineParent, node, nextNode);
                lineCount++;
            }
        }

        MarkEditorObjectDirty(lineParent.gameObject);
        MarkSceneDirty();
        Gameseed26.Logger.Log($"Manual map auto lines rebuilt: {lineCount} lines.");
    }

    [Button("Clear Auto Lines", EButtonEnableMode.Editor)]
    private void ClearAutoLinesButton()
    {
        RectTransform lineParent = autoLineParent != null ? autoLineParent : FindAutoLineParent();
        if (lineParent == null) return;

        ClearAutoGeneratedLines(lineParent);
        MarkEditorObjectDirty(lineParent.gameObject);
        MarkSceneDirty();
    }

    [Button("Collect Nodes In Children", EButtonEnableMode.Editor)]
    private void CollectNodesInChildrenButton()
    {
        CollectNodes();
        MarkEditorObjectDirty(this);
        Gameseed26.Logger.Log($"ManualMapController collected {nodes.Count} nodes.");
    }

    [Button("Load Default Node Icons From Art Folder", EButtonEnableMode.Editor)]
    private void LoadDefaultNodeIconsFromArtFolderButton()
    {
        LoadDefaultNodeIconsFromArtFolder();
        MarkEditorObjectDirty(this);
        MarkSceneDirty();
    }

    [Button("Apply Node Type Icons To Nodes", EButtonEnableMode.Editor)]
    private void ApplyNodeTypeIconsToNodesButton()
    {
        CollectNodes();
        ApplyNodeTypeIconsToNodes(true);
    }

    private void ResolveByNodeType(ManualMapNode node)
    {
        if (node.StartsCombat())
        {
            StartCombat(node);
            return;
        }

        string sceneName = GetDefaultSceneForNode(node);
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            LoadNonCombatNodeScene(node, sceneName);
            return;
        }

        Gameseed26.Logger.Log($"Manual map node '{node.NodeId}' resolved as {node.NodeType}. No default scene is configured, so only node events were invoked.");
        if (RunManager.Instance != null)
        {
            RunManager.Instance.ClearSelectedEncounter();
        }
    }

    private string GetDefaultSceneForNode(ManualMapNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.SceneNameOverride)) return node.SceneNameOverride;

        return node.NodeType switch
        {
            MapNodeType.Shop => defaultMerchantSceneName,
            MapNodeType.Rest => defaultRestSceneName,
            MapNodeType.Event => defaultEventSceneName,
            MapNodeType.Treasure => defaultTreasureSceneName,
            _ => string.Empty
        };
    }

    private void LoadNonCombatNodeScene(ManualMapNode node, string sceneName)
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.ClearSelectedEncounter();
        }

        SceneLoader.LoadScene(sceneName);
    }

    private void StartCombat(ManualMapNode node)
    {
        if (RunManager.Instance == null)
        {
            Gameseed26.Logger.LogWarning("ManualMapController could not find RunManager. Keep persistent run systems on Resources/GameManager.");
        }

        if (RunManager.Instance != null)
        {
            if (createRunForCombatReturn && !RunManager.Instance.HasActiveRun)
            {
                StartRunFromCurrentMapGraph();
            }

            MapNode runtimeNode = node.CreateRuntimeMapNode(encounterPool, nodes.IndexOf(node));
            RunManager.Instance.SelectMapNode(runtimeNode);
        }

        if (node.ResolveEncounter(encounterPool) == null)
        {
            Gameseed26.Logger.LogWarning($"Manual map node '{node.NodeId}' ({node.NodeType}) has no EncounterData. Combat will use MatchSetupSystem fallback enemies.");
        }

        string sceneName = string.IsNullOrWhiteSpace(node.SceneNameOverride) ? defaultCombatSceneName : node.SceneNameOverride;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Gameseed26.Logger.LogWarning($"Manual map node '{node.NodeId}' tried to start combat, but no combat scene name is set.");
            return;
        }

        if (PlayerPrefs.GetInt("HasPlayedCardTutorial", 0) == 0)
        {
            PlayerPrefs.SetInt("HasPlayedCardTutorial", 1);
            PlayerPrefs.Save();
            sceneName = "GameTutorial";
        }

        SceneLoader.LoadScene(sceneName);
    }

    private void LoadNodeScene(ManualMapNode node)
    {
        string sceneName = !string.IsNullOrWhiteSpace(node.SceneNameOverride)
            ? node.SceneNameOverride
            : GetDefaultSceneForNode(node);

        if (string.IsNullOrWhiteSpace(sceneName)) sceneName = defaultCombatSceneName;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Gameseed26.Logger.LogWarning($"Manual map node '{node.NodeId}' tried to load a scene, but no scene name is set.");
            return;
        }

        if (node.StartsCombat())
        {
            SceneLoader.LoadScene(sceneName);
        }
        else
        {
            LoadNonCombatNodeScene(node, sceneName);
        }
    }

    private void CollectNodes()
    {
        nodes ??= new List<ManualMapNode>();

        if (!autoFindNodesInChildren)
        {
            nodes.RemoveAll(node => node == null);
            return;
        }

        Transform searchRoot = autoSetupRoot != null ? autoSetupRoot : transform;
        nodes = searchRoot.GetComponentsInChildren<ManualMapNode>(true)
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

    private void StartRunFromCurrentMapGraph()
    {
        if (RunManager.Instance == null) return;

        RunManager.Instance.StartNewRun(CreateRuntimeGraph());
        LevelProgressionManager.Instance?.RestoreCampaignStateToActiveRun();
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

    private void DisableNodesOutsideSelectedPath(ManualMapNode completedNode)
    {
        HashSet<ManualMapNode> allowedNextNodes = new(completedNode.PathProgressionTargets.Where(node => node != null));
        foreach (ManualMapNode node in nodes)
        {
            if (node == null || node == completedNode || node.IsCompleted) continue;
            if (allowedNextNodes.Contains(node)) continue;

            node.SetDisabled();
        }
    }

    private void CheckForMapCompletion()
    {
        if (!completeCurrentLevelWhenCleared || mapCompletionHandled) return;
        if (!IsMapCleared()) return;

        mapCompletionHandled = true;
        Gameseed26.Logger.Log(this, $"Manual map '{mapId}' cleared. Completing current level.");
        mapCompletedSfx?.Play(this, transform);
        OnMapCompleted?.Invoke();

        if (LevelProgressionManager.Instance != null)
        {
            LevelProgressionManager.Instance.CompleteCurrentLevel();
        }
        else
        {
            Gameseed26.Logger.LogWarning(this, "Manual map was cleared, but LevelProgressionManager was not found.");
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.AbandonRun();
        }

        if (ManualMapRunSelection.Instance != null)
        {
            ManualMapRunSelection.Instance.ClearSelection();
        }

        if (returnToLevelSelectOnComplete && !string.IsNullOrWhiteSpace(levelSelectSceneName))
        {
            SceneLoader.LoadScene(levelSelectSceneName);
        }
    }

    private bool IsMapCleared()
    {
        bool hasCompletedTerminalNode = false;
        foreach (ManualMapNode node in nodes)
        {
            if (node == null) continue;
            if (node.CanSelect) return false;

            if (node.IsCompleted && node.PathProgressionTargets.Count == 0)
            {
                hasCompletedTerminalNode = true;
            }
        }

        return hasCompletedTerminalNode;
    }

    private void PlayNodeTypeSfx(ManualMapNode node)
    {
        if (node == null) return;

        TuneSfxCue cue = node.NodeType switch
        {
            MapNodeType.Enemy => combatNodeSfx,
            MapNodeType.Elite => eliteNodeSfx,
            MapNodeType.Boss => bossNodeSfx,
            MapNodeType.Shop => shopNodeSfx,
            MapNodeType.Rest => restNodeSfx,
            MapNodeType.Event => eventNodeSfx,
            MapNodeType.Treasure => treasureNodeSfx,
            _ => null
        };

        cue?.Play(this, node.transform);
    }

    private List<AutoLayer> BuildAutoLayers()
    {
        List<ManualMapNode> sortedNodes = nodes
            .Where(node => node != null)
            .OrderBy(GetProgressCoordinate)
            .ToList();

        List<AutoLayer> layers = new();
        foreach (ManualMapNode node in sortedNodes)
        {
            float progress = GetProgressCoordinate(node);
            AutoLayer targetLayer = layers.LastOrDefault();
            if (targetLayer == null || Mathf.Abs(progress - targetLayer.Progress) > autoLayerTolerance)
            {
                targetLayer = new AutoLayer(progress);
                layers.Add(targetLayer);
            }

            targetLayer.Add(node, progress);
        }

        foreach (AutoLayer layer in layers)
        {
            layer.Nodes = layer.Nodes.OrderBy(GetSideCoordinate).ToList();
        }

        return layers;
    }

    private Dictionary<ManualMapNode, List<ManualMapNode>> CreateNodeListMap()
    {
        Dictionary<ManualMapNode, List<ManualMapNode>> map = new();
        foreach (ManualMapNode node in nodes)
        {
            if (node != null && !map.ContainsKey(node))
            {
                map[node] = new List<ManualMapNode>();
            }
        }

        return map;
    }

    private void BuildAutoConnections(
        List<AutoLayer> layers,
        Dictionary<ManualMapNode, List<ManualMapNode>> outgoing,
        Dictionary<ManualMapNode, List<ManualMapNode>> incoming)
    {
        for (int layerIndex = 0; layerIndex < layers.Count - 1; layerIndex++)
        {
            List<ManualMapNode> fromLayer = layers[layerIndex].Nodes;
            List<ManualMapNode> nextLayer = layers[layerIndex + 1].Nodes;

            foreach (ManualMapNode fromNode in fromLayer)
            {
                List<ManualMapNode> candidates = nextLayer
                    .Where(toNode => autoMaxSideDistance <= 0f || Mathf.Abs(GetSideCoordinate(toNode) - GetSideCoordinate(fromNode)) <= autoMaxSideDistance)
                    .OrderBy(toNode => Mathf.Abs(GetSideCoordinate(toNode) - GetSideCoordinate(fromNode)))
                    .ThenBy(GetProgressCoordinate)
                    .ToList();

                if (candidates.Count == 0)
                {
                    candidates = nextLayer
                        .OrderBy(toNode => Mathf.Abs(GetSideCoordinate(toNode) - GetSideCoordinate(fromNode)))
                        .ToList();
                }

                int connectionCount = Mathf.Min(Mathf.Max(1, autoMaxConnectionsPerNode), candidates.Count);
                for (int i = 0; i < connectionCount; i++)
                {
                    AddAutoConnection(fromNode, candidates[i], outgoing, incoming);
                }
            }
        }

        if (!autoEnsureEveryNodeHasIncoming) return;

        for (int layerIndex = 1; layerIndex < layers.Count; layerIndex++)
        {
            List<ManualMapNode> previousLayer = layers[layerIndex - 1].Nodes;
            foreach (ManualMapNode targetNode in layers[layerIndex].Nodes)
            {
                if (incoming[targetNode].Count > 0) continue;

                ManualMapNode nearestPrevious = previousLayer
                    .OrderBy(fromNode => Mathf.Abs(GetSideCoordinate(fromNode) - GetSideCoordinate(targetNode)))
                    .FirstOrDefault();

                AddAutoConnection(nearestPrevious, targetNode, outgoing, incoming);
            }
        }
    }

    private void AddAutoConnection(
        ManualMapNode fromNode,
        ManualMapNode toNode,
        Dictionary<ManualMapNode, List<ManualMapNode>> outgoing,
        Dictionary<ManualMapNode, List<ManualMapNode>> incoming)
    {
        if (fromNode == null || toNode == null || fromNode == toNode) return;

        if (!outgoing.ContainsKey(fromNode) || !incoming.ContainsKey(toNode)) return;

        if (!outgoing[fromNode].Contains(toNode))
        {
            outgoing[fromNode].Add(toNode);
        }

        if (!incoming[toNode].Contains(fromNode))
        {
            incoming[toNode].Add(fromNode);
        }
    }

    private void BuildConnectionsFromEditedNextNodes(
        Dictionary<ManualMapNode, List<ManualMapNode>> outgoing,
        Dictionary<ManualMapNode, List<ManualMapNode>> incoming)
    {
        foreach (ManualMapNode fromNode in nodes)
        {
            if (fromNode == null || !outgoing.ContainsKey(fromNode)) continue;

            foreach (ManualMapNode toNode in fromNode.NextNodes)
            {
                AddAutoConnection(fromNode, toNode, outgoing, incoming);
            }
        }
    }

    private void ApplyNodeTypeIconsToNodes(bool logResult)
    {
        if (autoLoadDefaultNodeIconsWhenMissing)
        {
            LoadDefaultNodeIconsFromArtFolder();
        }

        int applied = 0;
        int skippedCustom = 0;
        int missing = 0;

        foreach (ManualMapNode node in nodes)
        {
            if (node == null) continue;
            if (node.NodeType == MapNodeType.Custom)
            {
                skippedCustom++;
                continue;
            }

            Sprite icon = GetIconForNodeType(node.NodeType);
            if (icon == null)
            {
                missing++;
                continue;
            }

            RecordEditorObject(node, "Apply Manual Map Node Type Icon");
            if (node.ApplyNodeTypeIconVisual(icon, hideNodeLabelsWhenApplyingIcons, setNodeIconsNativeSize))
            {
                applied++;
                MarkEditorObjectDirty(node);
                MarkEditorObjectDirty(node.gameObject);
            }
        }

        MarkEditorObjectDirty(this);
        MarkSceneDirty();

        if (logResult)
        {
            Gameseed26.Logger.Log($"Manual map node icons applied: {applied} updated, {skippedCustom} custom skipped, {missing} missing icon assignments.");
        }
    }

    private Sprite GetIconForNodeType(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.Start => startNodeIcon,
            MapNodeType.Enemy => enemyNodeIcon,
            MapNodeType.Elite => eliteNodeIcon,
            MapNodeType.Event => eventNodeIcon,
            MapNodeType.Shop => shopNodeIcon,
            MapNodeType.Rest => restNodeIcon,
            MapNodeType.Treasure => treasureNodeIcon,
            MapNodeType.Boss => bossNodeIcon,
            _ => null
        };
    }

    private void LoadDefaultNodeIconsFromArtFolder()
    {
#if UNITY_EDITOR
        enemyNodeIcon = enemyNodeIcon != null ? enemyNodeIcon : LoadSpriteAsset("Assets/_Project/Art/Map/Icon/Icon_Enemy.png");
        eliteNodeIcon = eliteNodeIcon != null ? eliteNodeIcon : LoadSpriteAsset("Assets/_Project/Art/Map/Icon/Icon_Elite Boss.png");
        eventNodeIcon = eventNodeIcon != null ? eventNodeIcon : LoadSpriteAsset("Assets/_Project/Art/Map/Icon/Icon_Event.png");
        shopNodeIcon = shopNodeIcon != null ? shopNodeIcon : LoadSpriteAsset("Assets/_Project/Art/Map/Icon/Icon_Merchant.png");
        restNodeIcon = restNodeIcon != null ? restNodeIcon : LoadSpriteAsset("Assets/_Project/Art/Map/Icon/Icon_Rest.png");
        bossNodeIcon = bossNodeIcon != null ? bossNodeIcon : LoadSpriteAsset("Assets/_Project/Art/Map/Icon/Icon_Boss.png");

        if (treasureNodeIcon == null)
        {
            treasureNodeIcon = LoadSpriteAsset("Assets/_Project/Art/Map/Icon/Icon_Treasure.png");
            if (treasureNodeIcon == null)
            {
                treasureNodeIcon = LoadSpriteAsset("Assets/_Project/Art/Map/Icon/Icon_Event2.png");
            }
        }

        startNodeIcon = startNodeIcon != null ? startNodeIcon : LoadSpriteAsset("Assets/_Project/Art/Map/Icon/Icon_Start.png");
#endif
    }

#if UNITY_EDITOR
    private static Sprite LoadSpriteAsset(string assetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault();
    }
#endif

    private void ApplyEndpointTypeIfNeeded(ManualMapNode node, int layerIndex, int layerCount, int nodesOnLayer)
    {
        if (autoSetSingleFirstLayerNodeAsStart && layerIndex == 0 && nodesOnLayer == 1)
        {
            node.SetNodeType(MapNodeType.Start);
            return;
        }

        if (autoSetLastLayerAsBoss && layerIndex == layerCount - 1)
        {
            node.SetNodeType(MapNodeType.Boss);
        }
    }

    private string GenerateNodeId(int layerIndex, int columnIndex, ManualMapNode node)
    {
        string safePrefix = string.IsNullOrWhiteSpace(autoIdPrefix) ? "map" : autoIdPrefix.Trim().ToLowerInvariant().Replace(' ', '_');

        if (node.NodeType == MapNodeType.Start && layerIndex == 0)
        {
            return $"{safePrefix}_start";
        }

        if (node.NodeType == MapNodeType.Boss)
        {
            return $"{safePrefix}_boss_{columnIndex + 1:00}";
        }

        return $"{safePrefix}_{layerIndex + 1:00}_{columnIndex + 1:00}";
    }

    private float GetProgressCoordinate(ManualMapNode node)
    {
        Vector2 position = node.GetDesignerPosition();
        return autoProgressDirection switch
        {
            ManualMapProgressDirection.BottomToTop => position.y,
            ManualMapProgressDirection.TopToBottom => -position.y,
            ManualMapProgressDirection.LeftToRight => position.x,
            ManualMapProgressDirection.RightToLeft => -position.x,
            _ => position.y
        };
    }

    private float GetSideCoordinate(ManualMapNode node)
    {
        Vector2 position = node.GetDesignerPosition();
        return autoProgressDirection switch
        {
            ManualMapProgressDirection.BottomToTop => position.x,
            ManualMapProgressDirection.TopToBottom => position.x,
            ManualMapProgressDirection.LeftToRight => position.y,
            ManualMapProgressDirection.RightToLeft => position.y,
            _ => position.x
        };
    }

    private RectTransform GetOrCreateLineParent()
    {
        if (autoLineParent != null) return autoLineParent;

        RectTransform existing = FindAutoLineParent();
        if (existing != null)
        {
            autoLineParent = existing;
            return autoLineParent;
        }

        GameObject lineParentObject = new(AutoLineParentName, typeof(RectTransform));
        RegisterCreatedEditorObject(lineParentObject, "Create Manual Map Auto Lines Parent");
        RectTransform rectTransform = lineParentObject.GetComponent<RectTransform>();
        rectTransform.SetParent(transform, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.SetAsFirstSibling();
        autoLineParent = rectTransform;
        return autoLineParent;
    }

    private RectTransform FindAutoLineParent()
    {
        Transform found = transform.Find(AutoLineParentName);
        return found == null ? null : found as RectTransform;
    }

    private void ClearAutoGeneratedLines(RectTransform lineParent)
    {
        for (int i = lineParent.childCount - 1; i >= 0; i--)
        {
            Transform child = lineParent.GetChild(i);
            if (!child.name.StartsWith(AutoLineNamePrefix)) continue;

            DestroyEditorAware(child.gameObject);
        }
    }

    private void CreateAutoLine(RectTransform lineParent, ManualMapNode fromNode, ManualMapNode toNode)
    {
        GameObject lineObject = new($"{AutoLineNamePrefix}{fromNode.NodeId}_to_{toNode.NodeId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RegisterCreatedEditorObject(lineObject, "Create Manual Map Auto Line");
        RectTransform rectTransform = lineObject.GetComponent<RectTransform>();
        rectTransform.SetParent(lineParent, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.SetAsFirstSibling();

        Image image = lineObject.GetComponent<Image>();
        image.color = autoLineColor;
        image.raycastTarget = false;

        Vector2 startPosition = GetLocalPositionInLineParent(fromNode, lineParent);
        Vector2 endPosition = GetLocalPositionInLineParent(toNode, lineParent);
        Vector2 direction = endPosition - startPosition;
        float distance = direction.magnitude;

        rectTransform.anchoredPosition = startPosition + direction * 0.5f;
        rectTransform.sizeDelta = new Vector2(distance, autoLineThickness);
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private Vector2 GetLocalPositionInLineParent(ManualMapNode node, RectTransform lineParent)
    {
        if (node.transform is RectTransform nodeRectTransform)
        {
            Vector3 worldPosition = nodeRectTransform.TransformPoint(nodeRectTransform.rect.center);
            return lineParent.InverseTransformPoint(worldPosition);
        }

        return lineParent.InverseTransformPoint(node.transform.position);
    }

    private void DestroyEditorAware(GameObject target)
    {
        if (target == null) return;

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(target);
#else
            DestroyImmediate(target);
#endif
        }
    }

    private sealed class AutoLayer
    {
        public float Progress { get; private set; }
        public List<ManualMapNode> Nodes { get; set; } = new();

        public AutoLayer(float progress)
        {
            Progress = progress;
        }

        public void Add(ManualMapNode node, float progress)
        {
            Nodes.Add(node);
            Progress = Mathf.Lerp(Progress, progress, 1f / Nodes.Count);
        }
    }

    private static void RecordEditorObject(Object target, string undoName)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && target != null)
        {
            Undo.RecordObject(target, undoName);
        }
#endif
    }

    private static void RegisterCreatedEditorObject(Object target, string undoName)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && target != null)
        {
            Undo.RegisterCreatedObjectUndo(target, undoName);
        }
#endif
    }

    private static void MarkEditorObjectDirty(Object target)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && target != null)
        {
            EditorUtility.SetDirty(target);
        }
#endif
    }

    private void MarkSceneDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }
}
