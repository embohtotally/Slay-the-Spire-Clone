using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

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

    [BoxGroup("Debug")]
    [SerializeField] private bool debug;
    [BoxGroup("Debug")]
    [SerializeField] private string mapLayoutId = "flowTest";

    [BoxGroup("Events")]
    public UnityEvent OnRefreshed;

    private LevelProgressionManager progressionManager;

    private void Awake()
    {
        progressionManager = LevelProgressionManager.GetOrCreate();
        progressionManager.ConfigureLevels(levels);
        progressionManager.SetDebug(debug, mapLayoutId);
        progressionManager.ProgressChanged += Refresh;
    }

    private IEnumerator Start()
    {
        // Wait one frame so any legacy ManualMapController on the Levels scene can finish applying
        // its own saved node states before this dedicated level-select layer takes over visuals/clicks.
        yield return null;
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

            node.ConfigureButtonForDirectClick(() => StartLevel(capturedLevel));
            node.SetState(completed ? ManualMapNodeState.Completed : unlocked ? ManualMapNodeState.Active : ManualMapNodeState.Disabled);
            node.SetInteractable(unlocked);

            if (overwriteNodeLabels)
            {
                node.SetLabel(unlocked ? level.DisplayName : $"{level.DisplayName}{lockedSuffix}");
            }
        }

        if (levelNodes.Count < orderedLevels.Count)
        {
            Gameseed26.Logger.LogWarning(this, $"Only {levelNodes.Count} level nodes are assigned for {orderedLevels.Count} levels.");
        }

        OnRefreshed?.Invoke();
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
        progressionManager.ResetProgress();
        Refresh();
    }

    [Button("Unlock All Levels", EButtonEnableMode.Playmode)]
    public void UnlockAllLevels()
    {
        progressionManager.UnlockAllLevels();
        Refresh();
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
