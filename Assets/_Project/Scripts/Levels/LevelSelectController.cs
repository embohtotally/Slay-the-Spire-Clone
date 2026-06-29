using System.Collections.Generic;
using System.Linq;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class LevelSelectController : MonoBehaviour
{
    [BoxGroup("Level Data")]
    [ReorderableList]
    [SerializeField] private List<LevelDefinition> levels = new();

    [BoxGroup("Buttons")]
    [SerializeField] private bool autoFindButtonsInChildren = true;

    [BoxGroup("Buttons")]
    [ShowIf("autoFindButtonsInChildren")]
    [SerializeField] private Transform buttonRoot;

    [BoxGroup("Buttons")]
    [SerializeField] private LevelSelectButton buttonPrefab;

    [BoxGroup("Buttons")]
    [HideIf("autoFindButtonsInChildren")]
    [ReorderableList]
    [SerializeField] private List<LevelSelectButton> buttons = new();

    [BoxGroup("Startup")]
    [SerializeField] private bool refreshOnStart = true;

    [BoxGroup("Events")]
    public UnityEvent OnRefreshed;

    private LevelProgressionManager progressionManager;

    private void Awake()
    {
        progressionManager = LevelProgressionManager.GetOrCreate();
        progressionManager.ConfigureLevels(levels);
        progressionManager.ProgressChanged += Refresh;
    }

    private void Start()
    {
        if (refreshOnStart)
        {
            Refresh();
        }
    }

    private void OnDestroy()
    {
        if (progressionManager != null)
        {
            progressionManager.ProgressChanged -= Refresh;
        }
    }

    [Button("Refresh Level Buttons", EButtonEnableMode.Playmode)]
    public void Refresh()
    {
        CollectButtonsIfNeeded();
        EnsureEnoughButtons();

        List<LevelDefinition> orderedLevels = levels
            .Where(level => level != null)
            .OrderBy(level => level.LevelNumber)
            .ThenBy(level => level.LevelId)
            .ToList();

        for (int i = 0; i < buttons.Count; i++)
        {
            LevelSelectButton levelButton = buttons[i];
            if (levelButton == null) continue;

            bool hasLevel = i < orderedLevels.Count;
            levelButton.gameObject.SetActive(hasLevel);
            if (!hasLevel) continue;

            LevelDefinition level = orderedLevels[i];
            levelButton.Bind(this, level, progressionManager.IsUnlocked(level), progressionManager.IsCompleted(level));
        }

        OnRefreshed?.Invoke();
    }

    public void StartLevel(LevelDefinition level)
    {
        if (progressionManager == null)
        {
            progressionManager = LevelProgressionManager.GetOrCreate();
            progressionManager.ConfigureLevels(levels);
        }

        progressionManager.StartLevel(level);
    }

    public void StartLevelById(string levelId)
    {
        if (progressionManager == null)
        {
            progressionManager = LevelProgressionManager.GetOrCreate();
            progressionManager.ConfigureLevels(levels);
        }

        progressionManager.StartLevelById(levelId);
    }

    [Button("Reset Progress", EButtonEnableMode.Playmode)]
    public void ResetProgress()
    {
        progressionManager?.ResetProgress();
        Refresh();
    }

    [Button("Unlock All", EButtonEnableMode.Playmode)]
    public void UnlockAll()
    {
        progressionManager?.UnlockAllLevels();
        Refresh();
    }

    private void CollectButtonsIfNeeded()
    {
        if (!autoFindButtonsInChildren) return;

        Transform root = buttonRoot != null ? buttonRoot : transform;
        buttons = root.GetComponentsInChildren<LevelSelectButton>(true).ToList();
    }

    private void EnsureEnoughButtons()
    {
        if (buttonPrefab == null || buttonRoot == null) return;

        int validLevelCount = levels.Count(level => level != null);
        while (buttons.Count < validLevelCount)
        {
            LevelSelectButton createdButton = Instantiate(buttonPrefab, buttonRoot);
            buttons.Add(createdButton);
        }
    }
}
