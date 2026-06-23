using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum ManualMapNodeState
{
    Active,
    Inactive,
    Hidden,
    Completed
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
    [SerializeField] private Text labelText;

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
    public bool IsCompleted => currentState == ManualMapNodeState.Completed;
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
        if (currentState == ManualMapNodeState.Completed || unlockRule == ManualMapUnlockRule.ManualOnly)
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

        foreach (ManualMapNode node in activateOnComplete)
        {
            if (node != null && !node.IsCompleted) node.SetActive();
        }

        foreach (ManualMapNode node in deactivateOnComplete)
        {
            if (node != null && !node.IsCompleted) node.SetInactive();
        }

        foreach (ManualMapNode node in hideOnComplete)
        {
            if (node != null && !node.IsCompleted) node.SetHidden();
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

    public MapNode CreateRuntimeMapNode(MapEncounterPool encounterPool, int column)
    {
        MapNode runtimeNode = new(NodeId, 0, column, nodeType, GetMapPosition())
        {
            Encounter = ResolveEncounter(encounterPool),
            IsAvailable = CanSelect,
            IsVisited = IsCompleted
        };

        return runtimeNode;
    }

    public bool StartsCombat()
    {
        return nodeType == MapNodeType.Enemy || nodeType == MapNodeType.Elite || nodeType == MapNodeType.Boss;
    }

    private void HandleClick()
    {
        controller?.SelectNode(this);
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
        bool hidden = currentState == ManualMapNodeState.Hidden;
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
            _ => inactiveColor
        };
    }

    private void CacheReferences()
    {
        if (button == null) button = GetComponent<Button>();
        if (iconImage == null) iconImage = GetComponent<Image>();
        if (labelText == null) labelText = GetComponentInChildren<Text>(true);
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
