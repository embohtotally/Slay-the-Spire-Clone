using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class LevelPreviewEvent : UnityEvent<LevelDefinition, ManualMapNode> { }

[DisallowMultipleComponent]
public class ManualLevelSelectController : MonoBehaviour
{
    [BoxGroup("Level Data")]
    [ReorderableList]
    [SerializeField] private List<LevelDefinition> levels = new();

    [BoxGroup("Manual Nodes")]
    [SerializeField] private bool autoFindNodesInChildren = true;

    [BoxGroup("Manual Nodes")]
    [ShowIf("autoFindNodesInChildren")]
    [SerializeField] private Transform nodesRoot;

    [BoxGroup("Manual Nodes")]
    [HideIf("autoFindNodesInChildren")]
    [ReorderableList]
    [SerializeField] private List<ManualMapNode> levelNodes = new();

    [BoxGroup("Labels")]
    [SerializeField] private bool overwriteNodeLabels = true;

    [BoxGroup("Labels")]
    [SerializeField] private string lockedSuffix = " (Locked)";

    [BoxGroup("Selection Preview")]
    [Tooltip("Panel that shows the selected level's details. Leave empty if you only want UnityEvents/Animation Sequencer wiring.")]
    [SerializeField] private LevelDetailsPanelController detailsPanel;

    [BoxGroup("Selection Preview")]
    [Tooltip("Usually the ManualMapLayoutLoader in the Levels scene. Used only for focusing/zooming the selected level icon.")]
    [SerializeField] private ManualMapLayoutLoader viewportController;

    [BoxGroup("Selection Preview")]
    [SerializeField] private bool focusSelectedNode = true;

    [BoxGroup("Selection Preview")]
    [Tooltip("Viewport-local target offset when focusing a level node. Negative X moves the selected node toward the left side so the right detail panel has room.")]
    [SerializeField] private Vector2 selectedNodeFocusOffset = new(-320f, 0f);

    [BoxGroup("Selection Preview")]
    [Tooltip("In the Levels scene, player map wheel/drag/keyboard controls should usually be off; this controller still performs scripted focus/zoom when a node is selected.")]
    [SerializeField] private bool disableViewportPlayerInput = true;

    [BoxGroup("Selection Preview")]
    [Tooltip("Keep the Levels map at its scene-authored overview position on load. Turn this on so zoom/pan only happens after selecting a level node.")]
    [SerializeField] private bool keepOverviewOnStart = true;

    [BoxGroup("Selection Preview")]
    [Tooltip("Cancel returns to the captured scene-authored overview instead of auto-fitting/zooming the whole map.")]
    [SerializeField] private bool restoreOverviewWhenSelectionCanceled = true;

    [BoxGroup("Audio")]
    [SerializeField] private TuneSfxCue previewLevelSfx;
    [BoxGroup("Audio")]
    [SerializeField] private TuneSfxCue confirmLevelSfx;
    [BoxGroup("Audio")]
    [SerializeField] private TuneSfxCue cancelSelectionSfx;

    [BoxGroup("Debug")]
    [SerializeField] private bool debug;
    [BoxGroup("Debug")]
    [SerializeField] private string mapLayoutId = "flowTest";

    [BoxGroup("Events")]
    public UnityEvent OnRefreshed;
    [BoxGroup("Events")]
    public LevelPreviewEvent OnLevelPreviewed;
    [BoxGroup("Events")]
    public UnityEvent OnSelectionCanceled;
    [BoxGroup("Events")]
    public UnityEvent OnSelectionConfirmed;

    private LevelProgressionManager progressionManager;
    private LevelDefinition selectedLevel;
    private ManualMapNode selectedNode;
    private LevelSelectNodeJuice selectedNodeJuice;

    public LevelDefinition SelectedLevel => selectedLevel;
    public ManualMapNode SelectedNode => selectedNode;

    private void Awake()
    {
        progressionManager = LevelProgressionManager.GetOrCreate();
        progressionManager.ConfigureLevels(levels);
        progressionManager.SetDebug(debug, mapLayoutId);
        progressionManager.ProgressChanged += Refresh;

        if (viewportController == null)
        {
            viewportController = FindFirstObjectByType<ManualMapLayoutLoader>();
        }

        if (viewportController != null && disableViewportPlayerInput)
        {
            viewportController.SetPlayerViewportInputEnabled(false);
        }

        if (viewportController != null && keepOverviewOnStart)
        {
            viewportController.SetFocusPathOnLoadEnabled(false);
        }

        if (detailsPanel != null)
        {
            detailsPanel.ConfigureOwner(this);
        }
    }

    private IEnumerator Start()
    {
        // Wait one frame so any legacy ManualMapController on the Levels scene can finish applying
        // its own saved node states before this dedicated level-select layer takes over visuals/clicks.
        yield return null;
        CaptureOverviewIfNeeded();
        Refresh();
    }

    private void OnDestroy()
    {
        if (progressionManager != null)
        {
            progressionManager.ProgressChanged -= Refresh;
        }
    }

    [Button("Refresh Manual Level Nodes", EButtonEnableMode.Playmode)]
    public void Refresh()
    {
        CollectNodesIfNeeded();

        List<LevelDefinition> orderedLevels = levels
            .Where(level => level != null)
            .OrderBy(level => level.LevelNumber)
            .ThenBy(level => level.LevelId)
            .ToList();

        for (int i = 0; i < levelNodes.Count; i++)
        {
            ManualMapNode node = levelNodes[i];
            if (node == null) continue;

            if (i >= orderedLevels.Count)
            {
                node.SetHiddenDisabled();
                node.SetInteractable(false);
                continue;
            }

            LevelDefinition level = orderedLevels[i];
            bool unlocked = progressionManager.IsUnlocked(level);
            bool completed = progressionManager.IsCompleted(level);
            LevelDefinition capturedLevel = level;
            ManualMapNode capturedNode = node;

            node.ConfigureButtonForDirectClick(() => PreviewLevel(capturedLevel, capturedNode));
            node.SetState(completed ? ManualMapNodeState.Completed : unlocked ? ManualMapNodeState.Active : ManualMapNodeState.Disabled);
            node.SetInteractable(unlocked);

            if (overwriteNodeLabels)
            {
                node.SetLabel(unlocked ? level.DisplayName : $"{level.DisplayName}{lockedSuffix}");
            }
        }

        RefreshSelectedNodeJuice();

        if (levelNodes.Count < orderedLevels.Count)
        {
            Gameseed26.Logger.LogWarning(this, $"Only {levelNodes.Count} level nodes are assigned for {orderedLevels.Count} levels.");
        }

        OnRefreshed?.Invoke();
    }

    public void PreviewLevel(LevelDefinition level, ManualMapNode node)
    {
        if (level == null || node == null) return;

        bool unlocked = progressionManager.IsUnlocked(level);
        bool completed = progressionManager.IsCompleted(level);

        selectedLevel = level;
        selectedNode = node;
        RefreshSelectedNodeJuice();
        previewLevelSfx?.Play(this, node.transform);

        if (focusSelectedNode && viewportController != null)
        {
            viewportController.FocusNode(node, selectedNodeFocusOffset);
        }

        if (detailsPanel != null)
        {
            detailsPanel.ConfigureOwner(this);
            detailsPanel.Show(level, unlocked, completed);
        }

        OnLevelPreviewed?.Invoke(level, node);
    }

    public void ConfirmSelectedLevel()
    {
        if (selectedLevel == null)
        {
            Gameseed26.Logger.LogWarning(this, "Cannot enter level because no level is selected.");
            return;
        }

        if (!progressionManager.IsUnlocked(selectedLevel))
        {
            Gameseed26.Logger.LogWarning(this, $"Cannot enter locked level '{selectedLevel.DisplayName}'.");
            return;
        }

        OnSelectionConfirmed?.Invoke();
        confirmLevelSfx?.Play(this, selectedNode != null ? selectedNode.transform : transform);
        StartLevel(selectedLevel);
    }

    public void CancelSelection()
    {
        ClearSelectedNodeJuice();
        cancelSelectionSfx?.Play(this, selectedNode != null ? selectedNode.transform : transform);
        selectedLevel = null;
        selectedNode = null;

        if (detailsPanel != null)
        {
            detailsPanel.Hide();
        }

        if (restoreOverviewWhenSelectionCanceled && viewportController != null)
        {
            viewportController.RestoreHomeView();
        }

        OnSelectionCanceled?.Invoke();
    }

    public void StartLevel(LevelDefinition level)
    {
        if (level == null) return;
        progressionManager.StartLevel(level);
    }

    public void StartLevelById(string levelId)
    {
        progressionManager.StartLevelById(levelId);
    }

    [Button("Reset Level Progress", EButtonEnableMode.Playmode)]
    public void ResetProgress()
    {
        CancelSelection();
        progressionManager.ResetProgress();
        Refresh();
    }

    [Button("Unlock All Levels", EButtonEnableMode.Playmode)]
    public void UnlockAllLevels()
    {
        CancelSelection();
        progressionManager.UnlockAllLevels();
        Refresh();
    }

    private void RefreshSelectedNodeJuice()
    {
        ClearSelectedNodeJuice();
        if (selectedNode == null) return;

        selectedNodeJuice = selectedNode.GetComponent<LevelSelectNodeJuice>();
        if (selectedNodeJuice == null)
        {
            selectedNodeJuice = selectedNode.GetComponentInChildren<LevelSelectNodeJuice>(true);
        }

        if (selectedNodeJuice != null)
        {
            selectedNodeJuice.SetSelected(true);
        }
    }

    private void ClearSelectedNodeJuice()
    {
        if (selectedNodeJuice != null)
        {
            selectedNodeJuice.SetSelected(false);
            selectedNodeJuice = null;
        }
    }

    private void CaptureOverviewIfNeeded()
    {
        if (!keepOverviewOnStart || viewportController == null) return;

        viewportController.CaptureHomeView();
        viewportController.RestoreHomeViewImmediate();
    }

    private void CollectNodesIfNeeded()
    {
        if (!autoFindNodesInChildren) return;

        Transform root = nodesRoot != null ? nodesRoot : transform;
        levelNodes = root.GetComponentsInChildren<ManualMapNode>(true)
            .OrderBy(node => node.LayerIndex)
            .ThenBy(node => node.ColumnIndex)
            .ThenBy(node => node.transform.GetSiblingIndex())
            .ToList();
    }
}
