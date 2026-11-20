using PlushieOdyssey.Core;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Data;
using PlushieOdyssey.Map.Manual;
using PlushieOdyssey.Run;
using System;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

namespace PlushieOdyssey.Levels
{
    public enum PlayerProgressScope
    {
        PerDungeon,
        Campaign
    }

    public class LevelProgressionManager : PersistentSingleton<LevelProgressionManager>
    {
        private const int NoLevelIndex = -1;

        [BoxGroup("Chapters")]
        [Tooltip("Ordered list of campaign chapters (Level 1..N). Used automatically when starting directly in Map or advancing between bosses without revisiting Levels.")]
        [SerializeField] private List<LevelDefinition> defaultLevels = new();

        [BoxGroup("Player Progress Scope")]
        [Tooltip("PerDungeon = HP/stress/gold/deck reset for every dungeon. Campaign = carry HP/stress/gold/deck across Level 1-4.")]
        [SerializeField] private PlayerProgressScope playerProgressScope = PlayerProgressScope.Campaign;

        [BoxGroup("Save")]
        [SerializeField] private bool persistProgress = true;

        [BoxGroup("Save")]
        [SerializeField] private string highestUnlockedSaveKey = "SlayClone.HighestUnlockedLevelIndex";

        [BoxGroup("Debug")]
        [ReadOnly][SerializeField] private int highestUnlockedIndex;

        [BoxGroup("Debug")]
        [ReadOnly][SerializeField] private int currentLevelIndex = NoLevelIndex;

        [BoxGroup("Debug")]
        [ReadOnly][SerializeField] private CampaignRunState campaignRunState = new();

        private readonly List<LevelDefinition> levels = new();

        private bool isDebug;
        private string manualDebugLayoutId;

        public event Action ProgressChanged;

        public IReadOnlyList<LevelDefinition> Levels => levels;
        public int CurrentLevelIndex => currentLevelIndex;
        public LevelDefinition CurrentLevel => IsValidIndex(currentLevelIndex) ? levels[currentLevelIndex] : null;
        public int CurrentChapterNumber => HasCurrentLevel ? CurrentLevel.LevelNumber : 1;
        public bool HasNextLevel => HasCurrentLevel && currentLevelIndex + 1 < levels.Count;
        public LevelDefinition NextLevel => HasNextLevel ? levels[currentLevelIndex + 1] : null;
        public LevelDefinition PendingChapterIntro { get; private set; }
        public int HighestUnlockedIndex => Mathf.Clamp(highestUnlockedIndex, 0, Mathf.Max(0, levels.Count - 1));
        public bool HasCurrentLevel => CurrentLevel != null;
        public PlayerProgressScope PlayerProgressScope => playerProgressScope;
        public bool UsesCampaignPlayerState => playerProgressScope == PlayerProgressScope.Campaign;
        public bool HasCampaignPlayerState => campaignRunState.HasState;

        protected override void Awake()
        {
            base.Awake();
            if (levels.Count == 0 && defaultLevels != null && defaultLevels.Count > 0)
            {
                ConfigureLevels(defaultLevels);
            }
        }

        public static LevelProgressionManager GetOrCreate()
        {
            if (Instance != null) return Instance;

            GameObject managerObject = new("Level Progression Manager");
            return managerObject.AddComponent<LevelProgressionManager>();
        }

        public void ConfigureLevels(IEnumerable<LevelDefinition> definitions, bool resetCurrentLevel = true)
        {
            LevelDefinition previousCurrent = CurrentLevel;
            levels.Clear();
            if (definitions != null)
            {
                levels.AddRange(definitions
                    .Where(level => level != null)
                    .OrderBy(level => level.LevelNumber)
                    .ThenBy(level => level.LevelId, StringComparer.OrdinalIgnoreCase));
            }

            LoadProgress();
            if (resetCurrentLevel)
            {
                currentLevelIndex = NoLevelIndex;
            }
            else if (previousCurrent != null)
            {
                currentLevelIndex = GetLevelIndex(previousCurrent);
            }

            ProgressChanged?.Invoke();
        }

        /// <summary>
        /// Ensures a chapter is active when entering Map/Game directly without going through Levels.unity.
        /// </summary>
        public LevelDefinition EnsureActiveLevel(IEnumerable<LevelDefinition> fallbackDefinitions = null)
        {
            if (levels.Count == 0)
            {
                IEnumerable<LevelDefinition> source = (defaultLevels != null && defaultLevels.Count > 0)
                    ? defaultLevels
                    : fallbackDefinitions;
                if (source != null)
                {
                    ConfigureLevels(source, resetCurrentLevel: false);
                }
            }

            if (!HasCurrentLevel && levels.Count > 0)
            {
                currentLevelIndex = 0;
                ProgressChanged?.Invoke();
            }

            return CurrentLevel;
        }

        public LevelDefinition ConsumePendingChapterIntro()
        {
            LevelDefinition pending = PendingChapterIntro;
            PendingChapterIntro = null;
            return pending;
        }

        public void SetDebug(bool debug, string layoutId)
        {
            isDebug = debug;
            manualDebugLayoutId = layoutId;
        }

        public bool IsUnlocked(LevelDefinition level)
        {
            int index = GetLevelIndex(level);
            return index >= 0 && index <= HighestUnlockedIndex;
        }

        public bool IsCompleted(LevelDefinition level)
        {
            int index = GetLevelIndex(level);
            return index >= 0 && index < HighestUnlockedIndex;
        }

        public void StartLevel(LevelDefinition level)
        {
            int index = GetLevelIndex(level);
            if (index < 0)
            {
                global::PlushieOdyssey.Core.Logger.LogWarning(this, "Cannot start level because it is not registered in LevelProgressionManager.");
                return;
            }

            if (!IsUnlocked(level))
            {
                global::PlushieOdyssey.Core.Logger.LogWarning(this, $"Cannot start locked level '{level.DisplayName}'.");
                return;
            }

            currentLevelIndex = index;
            PrepareDungeonSelection(level);
            global::PlushieOdyssey.Core.Logger.Log(this, $"Starting level '{level.DisplayName}' with layout '{level.ManualMapLayoutId}' using {playerProgressScope} player progress.");

            if (string.IsNullOrWhiteSpace(level.MapSceneName))
            {
                global::PlushieOdyssey.Core.Logger.LogWarning(this, $"Level '{level.DisplayName}' has no map scene name.");
                return;
            }

            SceneLoader.LoadScene(level.MapSceneName);
        }

        public void StartLevelById(string levelId)
        {
            LevelDefinition level = levels.FirstOrDefault(candidate => candidate != null && string.Equals(candidate.LevelId, levelId, StringComparison.OrdinalIgnoreCase));
            StartLevel(level);
        }

        public void CompleteCurrentLevel()
        {
            if (!HasCurrentLevel)
            {
                global::PlushieOdyssey.Core.Logger.LogWarning(this, "Cannot complete level because no current level is active.");
                return;
            }

            CompleteLevel(CurrentLevel);
        }

        /// <summary>
        /// Completes the current chapter and immediately advances to the next chapter in sequence
        /// (e.g. Chapter 1 -> Chapter 2 -> Chapter 3 -> Chapter 4) without returning to the chapter-select screen.
        /// Returns true if a next chapter exists and was activated; returns false if the final chapter was cleared.
        /// </summary>
        public bool CompleteCurrentLevelAndAdvance(out LevelDefinition nextLevel, bool loadMapScene = false, bool abandonRunForNextChapter = false)
        {
            nextLevel = null;
            if (!HasCurrentLevel)
            {
                global::PlushieOdyssey.Core.Logger.LogWarning(this, "Cannot advance chapter because no current level is active.");
                return false;
            }

            CompleteLevel(CurrentLevel);

            if (!HasNextLevel)
            {
                PendingChapterIntro = null;
                global::PlushieOdyssey.Core.Logger.Log(this, $"Final chapter '{CurrentLevel.DisplayName}' cleared! Campaign complete.");
                return false;
            }

            currentLevelIndex++;
            nextLevel = CurrentLevel;
            PendingChapterIntro = nextLevel;

            PrepareDungeonSelection(nextLevel, allowAbandonRun: abandonRunForNextChapter || !UsesCampaignPlayerState);
            global::PlushieOdyssey.Core.Logger.Log(this, $"Auto-advancing directly into '{nextLevel.DisplayName}' ({nextLevel.Description}).");
            ProgressChanged?.Invoke();

            if (loadMapScene && !string.IsNullOrWhiteSpace(nextLevel.MapSceneName))
            {
                SceneLoader.LoadScene(nextLevel.MapSceneName);
            }

            return true;
        }

        public void CompleteLevel(LevelDefinition level)
        {
            int index = GetLevelIndex(level);
            if (index < 0) return;

            CaptureCampaignStateFromActiveRun();

            if (index >= highestUnlockedIndex && index < levels.Count - 1)
            {
                highestUnlockedIndex = index + 1;
                SaveProgress();
            }

            global::PlushieOdyssey.Core.Logger.Log(this, $"Completed level '{level.DisplayName}'. Highest unlocked index: {highestUnlockedIndex}.");
            ProgressChanged?.Invoke();
        }

        [Button("Reset Level Progress", EButtonEnableMode.Playmode)]
        public void ResetProgress()
        {
            highestUnlockedIndex = 0;
            currentLevelIndex = NoLevelIndex;
            campaignRunState.Clear();
            if (persistProgress)
            {
                PlayerPrefs.DeleteKey(highestUnlockedSaveKey);
                PlayerPrefs.Save();
            }

            ProgressChanged?.Invoke();
        }

        [Button("Begin New Campaign", EButtonEnableMode.Playmode)]
        public void BeginNewCampaign()
        {
            ResetProgress();
            ManualMapController.ClearAllSavedStates();

            if (ManualMapRunSelection.Instance != null)
            {
                ManualMapRunSelection.Instance.ClearSelection();
            }

            if (RunManager.Instance != null)
            {
                RunManager.Instance.AbandonRun();
            }
        }

        [Button("Unlock All Levels", EButtonEnableMode.Playmode)]
        public void UnlockAllLevels()
        {
            highestUnlockedIndex = Mathf.Max(0, levels.Count - 1);
            SaveProgress();
            ProgressChanged?.Invoke();
        }

        public void CaptureCampaignStateFromActiveRun()
        {
            if (!UsesCampaignPlayerState) return;

            if (RunManager.Instance == null)
            {
                global::PlushieOdyssey.Core.Logger.LogWarning(this, "Cannot capture campaign player state because RunManager is missing.");
                return;
            }

            campaignRunState.CaptureFrom(RunManager.Instance, RunDeckManager.Instance, RunPotionManager.Instance, RunRelicManager.Instance);
            global::PlushieOdyssey.Core.Logger.Log(this, "Captured campaign player state from active run.");
        }

        public bool RestoreCampaignStateToActiveRun()
        {
            if (!UsesCampaignPlayerState || !campaignRunState.HasState) return false;

            if (RunManager.Instance == null)
            {
                global::PlushieOdyssey.Core.Logger.LogWarning(this, "Cannot restore campaign player state because RunManager is missing.");
                return false;
            }

            campaignRunState.RestoreTo(RunManager.Instance, RunDeckManager.Instance, RunPotionManager.Instance, RunRelicManager.Instance);
            global::PlushieOdyssey.Core.Logger.Log(this, "Restored campaign player state to active run.");
            return true;
        }

        public void ClearCampaignPlayerState()
        {
            campaignRunState.Clear();
            ProgressChanged?.Invoke();
        }

        private void PrepareDungeonSelection(LevelDefinition level, bool allowAbandonRun = true)
        {
            EnsureManualMapSelectionExists();

            if (level.ClearMapProgressOnStart)
            {
                ManualMapController.ClearSavedState(level.GetProgressMapId());
            }

            if (ManualMapRunSelection.Instance != null)
            {
                ManualMapRunSelection.Instance.ClearSelection();
                if (level.HasManualMapLayout)
                {
                    if (isDebug)
                    {
                        ManualMapRunSelection.Instance.SelectLayout(manualDebugLayoutId);
                    }
                    else
                    {
                        ManualMapRunSelection.Instance.SelectLayout(level.ManualMapLayoutId);
                    }
                }
            }

            if (allowAbandonRun && level.AbandonExistingRunOnStart && RunManager.Instance != null)
            {
                RunManager.Instance.AbandonRun();
            }
        }

        private void LoadProgress()
        {
            if (levels.Count == 0)
            {
                highestUnlockedIndex = 0;
                return;
            }

            highestUnlockedIndex = persistProgress ? PlayerPrefs.GetInt(highestUnlockedSaveKey, 0) : highestUnlockedIndex;
            highestUnlockedIndex = Mathf.Clamp(highestUnlockedIndex, 0, levels.Count - 1);
        }

        private void SaveProgress()
        {
            if (!persistProgress) return;

            PlayerPrefs.SetInt(highestUnlockedSaveKey, highestUnlockedIndex);
            PlayerPrefs.Save();
        }

        private int GetLevelIndex(LevelDefinition level)
        {
            if (level == null) return NoLevelIndex;
            return levels.IndexOf(level);
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < levels.Count;
        }

        private static void EnsureManualMapSelectionExists()
        {
            if (ManualMapRunSelection.Instance != null) return;

            GameObject selectionObject = new("Manual Map Run Selection");
            selectionObject.AddComponent<ManualMapRunSelection>();
        }

        [Serializable]
        private sealed class CampaignRunState
        {
            [field: SerializeField] public bool HasState { get; private set; }
            [SerializeField] private int heroCurrentHealth;
            [SerializeField] private int heroMaxHealth;
            [SerializeField] private int gold;
            [SerializeField] private int crystals;
            [SerializeField] private List<RunHeroStressState> stressStates = new();
            [SerializeField] private List<CardData> deck = new();
            [SerializeField] private List<PotionData> potions = new();
            [SerializeField] private List<RelicData> relics = new();

            public void CaptureFrom(RunManager runManager, RunDeckManager deckManager, RunPotionManager potionManager, RunRelicManager relicManager)
            {
                if (runManager == null) return;

                HasState = runManager.HasHeroState || runManager.Gold > 0 || (deckManager != null && deckManager.HasDeck);
                heroCurrentHealth = runManager.HeroCurrentHealth;
                heroMaxHealth = runManager.HeroMaxHealth;
                gold = runManager.Gold;
                crystals = runManager.Crystals;

                stressStates.Clear();
                foreach (RunHeroStressState stressState in runManager.GetHeroStressStateCopies())
                {
                    stressStates.Add(stressState);
                }

                deck.Clear();
                if (deckManager != null)
                {
                    deck.AddRange(deckManager.GetDeckCopy().Where(cardData => cardData != null));
                }

                potions.Clear();
                if (potionManager != null)
                {
                    potions.AddRange(potionManager.GetPotionsCopy().Where(p => p != null));
                }

                relics.Clear();
                if (relicManager != null)
                {
                    relics.AddRange(relicManager.GetRelicsCopy().Where(r => r != null));
                }
            }

            public void RestoreTo(RunManager runManager, RunDeckManager deckManager, RunPotionManager potionManager, RunRelicManager relicManager)
            {
                if (!HasState || runManager == null) return;

                runManager.RestorePlayerState(heroCurrentHealth, heroMaxHealth, stressStates, gold, crystals);

                if (deckManager != null && deck.Count > 0)
                {
                    deckManager.ResetDeck(deck);
                }

                if (potionManager != null && potions.Count > 0)
                {
                    potionManager.ResetPotions(potions);
                }

                if (relicManager != null && relics.Count > 0)
                {
                    relicManager.ResetRelics(relics);
                }
            }

            public void Clear()
            {
                HasState = false;
                heroCurrentHealth = 0;
                heroMaxHealth = 0;
                gold = 0;
                crystals = 0;
                stressStates.Clear();
                deck.Clear();
                potions.Clear();
                relics.Clear();
            }
        }
    }
}
