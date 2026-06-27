using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum ManualMapNodeState
{
    Active,
    Inactive,
    Hidden,
    Completed,
    Disabled,
    HiddenDisabled
}

public enum ManualMapNodeAction
{
    ResolveByNodeType,
    StartCombat,
    LoadScene,
    InvokeEventsOnly
}

public enum ManualMapUnlockRule
{
    ManualOnly,
    AlwaysActive,
    AfterAnyRequiredCompleted,
    AfterAllRequiredCompleted
}

[DisallowMultipleComponent]
public class ManualMapNode : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text labelText;

    [Header("Node Data")]
    [Tooltip("Optional unique id. Leave empty to use this GameObject name.")]
    [SerializeField] private string id;
    [SerializeField] private MapNodeType nodeType = MapNodeType.Enemy;
    [SerializeField] private EncounterData encounter;
    [SerializeField] private bool useEncounterPoolWhenEmpty = true;

    [Header("State")]
    [SerializeField] private ManualMapNodeState initialState = ManualMapNodeState.Inactive;
    [SerializeField] private ManualMapUnlockRule unlockRule = ManualMapUnlockRule.ManualOnly;
    [SerializeField] private List<ManualMapNode> requiredCompletedNodes = new();

    [Header("Auto Graph / STS Style")]
    [Tooltip("Read-only helper values produced by ManualMapController auto setup. Useful for checking inferred layer/column in the inspector.")]
    [SerializeField] private int layerIndex = -1;
    [SerializeField] private int columnIndex = -1;
    [Tooltip("Next nodes in the Slay-the-Spire style path. Auto setup fills this from scene positions; designers can still tweak it manually afterwards.")]
    [SerializeField] private List<ManualMapNode> nextNodes = new();

    [Header("Click Action")]
    [SerializeField] private ManualMapNodeAction clickAction = ManualMapNodeAction.ResolveByNodeType;
    [SerializeField] private bool completeOnClick = true;
    [Tooltip("Optional override. Empty = use ManualMapController default combat scene.")]
    [SerializeField] private string sceneNameOverride;

    [Header("After Completed")]
    [SerializeField] private List<ManualMapNode> activateOnComplete = new();
    [SerializeField] private List<ManualMapNode> deactivateOnComplete = new();
    [SerializeField] private List<ManualMapNode> hideOnComplete = new();

    [Header("Visuals")]
    [SerializeField] private Color activeColor = new(1f, 0.9f, 0.25f, 1f);
    [SerializeField] private Color inactiveColor = new(0.35f, 0.35f, 0.4f, 1f);
    [SerializeField] private Color completedColor = new(0.25f, 0.95f, 0.45f, 1f);
    [SerializeField] private Color hiddenColor = new(0f, 0f, 0f, 0f);

    [Header("Events")]
    public UnityEvent OnSelected;
    public UnityEvent OnCompleted;

    private ManualMapController controller;
    private ManualMapNodeState currentState;

    public string NodeId => string.IsNullOrWhiteSpace(id) ? gameObject.name : id.Trim();
    public MapNodeType NodeType => nodeType;
    public ManualMapNodeState CurrentState => currentState;
    public ManualMapNodeAction ClickAction => clickAction;
    public bool CompleteOnClick => completeOnClick;
    public string SceneNameOverride => sceneNameOverride;
    public int LayerIndex => layerIndex;
    public int ColumnIndex => columnIndex;
    public IReadOnlyList<ManualMapNode> NextNodes => nextNodes;
    public IReadOnlyList<ManualMapNode> PathProgressionTargets => nextNodes.Count > 0 ? nextNodes : activateOnComplete;
    public bool IsCompleted => currentState == ManualMapNodeState.Completed;
    public bool IsDisabled => currentState == ManualMapNodeState.Disabled || currentState == ManualMapNodeState.HiddenDisabled;
    public bool CanSelect => currentState == ManualMapNodeState.Active;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
        if (!Application.isPlaying && labelText != null)
        {
            labelText.text = GetLabel(nodeType);
        }
    }

    public void Initialize(ManualMapController owner)
    {
        controller = owner;
        CacheReferences();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    public void ApplyInitialState()
    {
        SetState(initialState);
    }

    public void RestoreState(ManualMapNodeState restoredState)
    {
        SetState(restoredState);
    }

    public void SetActive()
    {
        SetState(ManualMapNodeState.Active);
    }

    public void SetInactive()
    {
        SetState(ManualMapNodeState.Inactive);
    }

    public void SetHidden()
    {
        SetState(ManualMapNodeState.Hidden);
    }

    public void SetDisabled()
    {
        SetState(ManualMapNodeState.Disabled);
    }

    public void SetHiddenDisabled()
    {
        SetState(ManualMapNodeState.HiddenDisabled);
    }

    public void SetCompleted()
    {
        SetState(ManualMapNodeState.Completed);
    }

    public void SetState(ManualMapNodeState newState)
    {
        currentState = newState;
        RefreshVisual();
    }

    public void RefreshUnlockRule()
    {
        if (currentState == ManualMapNodeState.Completed || IsDisabled || unlockRule == ManualMapUnlockRule.ManualOnly)
        {
            RefreshVisual();
            return;
        }

        bool shouldBeActive = unlockRule switch
        {
            ManualMapUnlockRule.AlwaysActive => true,
            ManualMapUnlockRule.AfterAnyRequiredCompleted => HasAnyRequiredCompleted(),
            ManualMapUnlockRule.AfterAllRequiredCompleted => HasAllRequiredCompleted(),
            _ => currentState == ManualMapNodeState.Active
        };

        SetState(shouldBeActive ? ManualMapNodeState.Active : ManualMapNodeState.Inactive);
    }

    public void CompleteAndApplyOutputs()
    {
        SetCompleted();
        OnCompleted?.Invoke();

        ActivateTargets(nextNodes);
        ActivateTargets(activateOnComplete);

        foreach (ManualMapNode node in deactivateOnComplete)
        {
            if (node != null && !node.IsCompleted) node.SetDisabled();
        }

        foreach (ManualMapNode node in hideOnComplete)
        {
            if (node != null && !node.IsCompleted) node.SetHiddenDisabled();
        }
    }

    public EncounterData ResolveEncounter(MapEncounterPool encounterPool)
    {
        if (encounter != null)
        {
            return encounter;
        }

        if (useEncounterPoolWhenEmpty && encounterPool != null && StartsCombat())
        {
            return encounterPool.GetEncounter(nodeType);
        }

        return null;
    }

    public MapNode CreateRuntimeMapNode(MapEncounterPool encounterPool, int fallbackColumn)
    {
        MapNode runtimeNode = new(NodeId, Mathf.Max(0, layerIndex), columnIndex >= 0 ? columnIndex : fallbackColumn, nodeType, GetMapPosition())
        {
            Encounter = ResolveEncounter(encounterPool),
            IsAvailable = CanSelect,
            IsVisited = IsCompleted
        };

        foreach (ManualMapNode nextNode in PathProgressionTargets)
        {
            if (nextNode == null) continue;
            string nextNodeId = nextNode.NodeId;
            if (!runtimeNode.NextNodeIds.Contains(nextNodeId))
            {
                runtimeNode.NextNodeIds.Add(nextNodeId);
            }
        }

        return runtimeNode;
    }

    public bool StartsCombat()
    {
        return nodeType == MapNodeType.Enemy || nodeType == MapNodeType.Elite || nodeType == MapNodeType.Boss;
    }

    public Vector2 GetDesignerPosition()
    {
        return GetMapPosition();
    }

    public void ApplyDesignerAutoSetup(
        string generatedId,
        int generatedLayerIndex,
        int generatedColumnIndex,
        ManualMapNodeState generatedInitialState,
        ManualMapUnlockRule generatedUnlockRule,
        List<ManualMapNode> generatedRequiredNodes,
        List<ManualMapNode> generatedNextNodes,
        bool overwriteId,
        bool clearManualOutputLists)
    {
        if (overwriteId || string.IsNullOrWhiteSpace(id))
        {
            id = generatedId;
        }

        layerIndex = generatedLayerIndex;
        columnIndex = generatedColumnIndex;
        initialState = generatedInitialState;
        unlockRule = generatedUnlockRule;
        requiredCompletedNodes = CleanNodeList(generatedRequiredNodes);
        nextNodes = CleanNodeList(generatedNextNodes);

        if (clearManualOutputLists)
        {
            activateOnComplete = new List<ManualMapNode>(nextNodes);
            deactivateOnComplete.Clear();
            hideOnComplete.Clear();
        }

        RefreshVisual();
    }

    public void SetNodeType(MapNodeType newNodeType)
    {
        nodeType = newNodeType;
        RefreshVisual();
    }

    private void HandleClick()
    {
        controller?.SelectNode(this);
    }

    private void ActivateTargets(List<ManualMapNode> targetNodes)
    {
        foreach (ManualMapNode node in targetNodes)
        {
            if (node != null && !node.IsCompleted) node.SetActive();
        }
    }

    private bool HasAnyRequiredCompleted()
    {
        foreach (ManualMapNode node in requiredCompletedNodes)
        {
            if (node != null && node.IsCompleted) return true;
        }

        return false;
    }

    private bool HasAllRequiredCompleted()
    {
        if (requiredCompletedNodes == null || requiredCompletedNodes.Count == 0) return false;

        foreach (ManualMapNode node in requiredCompletedNodes)
        {
            if (node == null || !node.IsCompleted) return false;
        }

        return true;
    }

    private Vector2 GetMapPosition()
    {
        if (transform is RectTransform rectTransform)
        {
            return rectTransform.anchoredPosition;
        }

        return transform.localPosition;
    }

    private void RefreshVisual()
    {
        bool hidden = currentState == ManualMapNodeState.Hidden || currentState == ManualMapNodeState.HiddenDisabled;
        if (gameObject.activeSelf == hidden)
        {
            gameObject.SetActive(!hidden);
        }

        if (button != null)
        {
            button.interactable = currentState == ManualMapNodeState.Active;
        }

        if (labelText != null)
        {
            labelText.text = GetLabel(nodeType);
        }

        if (iconImage == null) return;

        iconImage.color = currentState switch
        {
            ManualMapNodeState.Active => activeColor,
            ManualMapNodeState.Completed => completedColor,
            ManualMapNodeState.Hidden => hiddenColor,
            ManualMapNodeState.HiddenDisabled => hiddenColor,
            _ => inactiveColor
        };
    }

    private void CacheReferences()
    {
        if (button == null) button = GetComponent<Button>();
        if (iconImage == null) iconImage = GetComponent<Image>();
        if (labelText == null) labelText = GetComponentInChildren<TMP_Text>(true);
    }

    private static List<ManualMapNode> CleanNodeList(IEnumerable<ManualMapNode> source)
    {
        List<ManualMapNode> result = new();
        if (source == null) return result;

        foreach (ManualMapNode node in source)
        {
            if (node != null && !result.Contains(node))
            {
                result.Add(node);
            }
        }

        return result;
    }

    private static string GetLabel(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.Start => "START",
            MapNodeType.Enemy => "ENEMY",
            MapNodeType.Elite => "ELITE",
            MapNodeType.Event => "EVENT",
            MapNodeType.Shop => "SHOP",
            MapNodeType.Rest => "REST",
            MapNodeType.Treasure => "CHEST",
            MapNodeType.Boss => "BOSS",
            _ => type.ToString().ToUpperInvariant()
        };
    }
}
