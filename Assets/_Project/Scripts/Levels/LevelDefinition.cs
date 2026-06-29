using NaughtyAttributes;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Levels/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    [BoxGroup("Identity")]
    [SerializeField] private string levelId = "level_01";

    [BoxGroup("Identity")]
    [SerializeField] private string displayName = "Level 1";

    [BoxGroup("Identity")]
    [Min(1)] [SerializeField] private int levelNumber = 1;

    [BoxGroup("Identity")]
    [TextArea(2, 4)] [SerializeField] private string description;

    [BoxGroup("Dungeon Run")]
    [Scene] [SerializeField] private string mapSceneName = "Map";

    [BoxGroup("Dungeon Run")]
    [Tooltip("ManualMapLayoutRegistry id to load for this level. Example: loneliness.")]
    [SerializeField] private string manualMapLayoutId;

    [BoxGroup("Dungeon Run")]
    [Tooltip("Optional explicit save key for the spawned manual map. Empty = LevelId + layout id.")]
    [SerializeField] private string manualMapProgressIdOverride;

    [BoxGroup("Start Rules")]
    [Tooltip("Clear this level's manual-map node progress when starting it from Levels.")]
    [SerializeField] private bool clearMapProgressOnStart = true;

    [BoxGroup("Start Rules")]
    [Tooltip("Clear any active combat/run state before starting this level.")]
    [SerializeField] private bool abandonExistingRunOnStart = true;

    public string LevelId => string.IsNullOrWhiteSpace(levelId) ? name : levelId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? LevelId : displayName;
    public int LevelNumber => Mathf.Max(1, levelNumber);
    public string Description => description;
    public string MapSceneName => mapSceneName;
    public string ManualMapLayoutId => manualMapLayoutId;
    public bool HasManualMapLayout => !string.IsNullOrWhiteSpace(manualMapLayoutId);
    public bool ClearMapProgressOnStart => clearMapProgressOnStart;
    public bool AbandonExistingRunOnStart => abandonExistingRunOnStart;

    public string GetProgressMapId()
    {
        if (!string.IsNullOrWhiteSpace(manualMapProgressIdOverride))
        {
            return manualMapProgressIdOverride.Trim();
        }

        string safeLevelId = LevelId.Replace(' ', '_').ToLowerInvariant();
        string safeLayoutId = string.IsNullOrWhiteSpace(manualMapLayoutId) ? "default" : manualMapLayoutId.Trim().Replace(' ', '_').ToLowerInvariant();
        return $"{safeLevelId}_{safeLayoutId}";
    }
}
