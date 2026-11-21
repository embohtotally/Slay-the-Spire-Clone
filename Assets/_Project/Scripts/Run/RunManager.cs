using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.Data;
using PlushieOdyssey.Map;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Run
{
    public class RunManager : PersistentSingleton<RunManager>
    {
        [Header("Run Defaults")]
        [SerializeField] private int startingGold = 150;
        [SerializeField] private int startingCrystals = 100;

        private readonly List<RunHeroStressState> heroStressStates = new();
        private readonly List<HeroData> selectedParty = new();

        public MapGraph CurrentMap { get; private set; }
        public string CurrentMapNodeId { get; private set; }
        public EncounterData SelectedEncounter { get; private set; }
        public bool HasActiveRun => CurrentMap != null;
        public IReadOnlyList<HeroData> SelectedParty => selectedParty;
        public bool HasSelectedParty => selectedParty.Count > 0;
        public int CurrentChapterNumber { get; private set; } = 1;
        public string CurrentChapterName { get; private set; } = string.Empty;
        public bool IsCurrentNodeBoss =>
            CurrentMap != null &&
            !string.IsNullOrEmpty(CurrentMapNodeId) &&
            CurrentMap.GetNode(CurrentMapNodeId)?.Type == MapNodeType.Boss;

        public bool HasHeroState { get; private set; }
        public int HeroCurrentHealth { get; private set; }
        public int HeroMaxHealth { get; private set; }
        public int HeroCurrentStress { get; private set; }
        public int HeroMaxStress { get; private set; }
        public int HeroCurrentWill => Mathf.Clamp(HeroMaxStress - HeroCurrentStress, 0, Mathf.Max(1, HeroMaxStress));
        public int HeroMaxWill => Mathf.Max(1, HeroMaxStress);
        public IReadOnlyList<RunHeroStressState> HeroStressStates => heroStressStates;
        public int Gold { get; private set; }
        public int Crystals { get; private set; }

        // === Run Accomplishment Telemetry (displayed on Victory & Defeat Run Summary) ===
        public int ChaptersCleared { get; private set; }
        public int NodesVisitedTotal { get; private set; }
        public int CombatsWon { get; private set; }
        public int ElitesDefeated { get; private set; }
        public int BossesDefeated { get; private set; }
        public int TotalGoldEarned { get; private set; }
        public string LastEncounterName { get; private set; } = string.Empty;

        public Dictionary<string, int> Counters { get; private set; } = new();
        public Dictionary<string, int> CardCostModifiers { get; private set; } = new();

        public event Action RunStateChanged;
        public event Action<IReadOnlyList<HeroData>> SelectedPartyChanged;

        /// <summary>
        /// Sets the player's selected character party (called by future Character Selection screens).
        /// Combat, HP, Deck, Will meters, and Top Bar portraits automatically adapt to this roster.
        /// </summary>
        public void SetSelectedParty(IEnumerable<HeroData> party)
        {
            selectedParty.Clear();
            if (party != null)
            {
                foreach (HeroData hero in party)
                {
                    if (hero != null) selectedParty.Add(hero);
                }
            }

            SelectedPartyChanged?.Invoke(selectedParty);
            NotifyRunStateChanged();
        }

        public void ClearSelectedParty()
        {
            if (selectedParty.Count == 0) return;
            selectedParty.Clear();
            SelectedPartyChanged?.Invoke(selectedParty);
            NotifyRunStateChanged();
        }

        protected override void Awake()
        {
            base.Awake();
            if (Gold == 0 && startingGold > 0) Gold = startingGold;
            if (Crystals == 0 && startingCrystals > 0) Crystals = startingCrystals;
        }

        // Backward-compatible access for older scripts that only cared about current HP.
        public int? CurrentHeroHealth
        {
            get => HasHeroState ? HeroCurrentHealth : null;
            set
            {
                if (!value.HasValue)
                {
                    ClearHeroState();
                    NotifyRunStateChanged();
                    return;
                }

                if (!HasHeroState)
                {
                    InitializeHeroState(Mathf.Max(1, value.Value), HeroMaxStress > 0 ? HeroMaxStress : 100);
                }

                SetHeroHealth(value.Value);
            }
        }

        public void StartNewRun(MapGraph mapGraph)
        {
            CurrentMap = mapGraph;
            CurrentMapNodeId = null;
            SelectedEncounter = null;
            CurrentChapterNumber = 1;
            CurrentChapterName = string.Empty;
            ClearHeroState();
            Gold = Mathf.Max(0, startingGold);
            Crystals = Mathf.Max(0, startingCrystals);
            ResetRunTelemetry();
            Counters.Clear();
            CardCostModifiers.Clear();

            if (RunDeckManager.Instance != null)
            {
                RunDeckManager.Instance.ClearDeck();
            }

            if (RunPotionManager.Instance != null)
            {
                RunPotionManager.Instance.ClearPotions();
            }

            NotifyRunStateChanged();
        }

        public void SetChapterInfo(int chapterNumber, string chapterName)
        {
            CurrentChapterNumber = Mathf.Max(1, chapterNumber);
            CurrentChapterName = string.IsNullOrWhiteSpace(chapterName) ? string.Empty : chapterName.Trim();
            NotifyRunStateChanged();
        }

        /// <summary>
        /// Replaces the active map with the next chapter's fresh procedural map while preserving
        /// the player's run state (HP, Will, Gold, Crystals, Deck, Relics, Potions) in campaign mode.
        /// </summary>
        public void AdvanceToNextChapterMap(MapGraph nextChapterMap, int chapterNumber, string chapterName, bool resetPlayerState = false)
        {
            if (resetPlayerState)
            {
                StartNewRun(nextChapterMap);
                SetChapterInfo(chapterNumber, chapterName);
                return;
            }

            CurrentMap = nextChapterMap;
            CurrentMapNodeId = null;
            SelectedEncounter = null;
            CurrentChapterNumber = Mathf.Max(1, chapterNumber);
            CurrentChapterName = string.IsNullOrWhiteSpace(chapterName) ? string.Empty : chapterName.Trim();
            NotifyRunStateChanged();
        }

        /// <summary>
        /// Re-rolls only the current unstarted chapter's procedural map without wiping the active run's
        /// party, HP, Will, Gold, Deck, Relics, Potions, Chapter number, or run telemetry.
        /// </summary>
        public void ReplaceCurrentChapterMap(MapGraph freshMap)
        {
            if (freshMap == null) return;
            CurrentMap = freshMap;
            CurrentMapNodeId = null;
            SelectedEncounter = null;
            NotifyRunStateChanged();
        }

        public void RecordChapterCleared()
        {
            ChaptersCleared++;
            NotifyRunStateChanged();
        }

        public string GetChapterHeaderLabel()
        {
            return string.IsNullOrWhiteSpace(CurrentChapterName)
                ? $"CHAPTER {CurrentChapterNumber}"
                : $"CHAPTER {CurrentChapterNumber}: {CurrentChapterName.ToUpperInvariant()}";
        }

        public void SelectMapNode(MapNode selectedNode)
        {
            if (selectedNode == null) return;

            CurrentMap?.MarkVisited(selectedNode.Id);
            CurrentMapNodeId = selectedNode.Id;
            SelectedEncounter = selectedNode.Encounter;
            NodesVisitedTotal++;

            if (selectedNode.Encounter != null)
            {
                LastEncounterName = FormatEncounterDisplayName(selectedNode.Encounter);
            }
            else
            {
                LastEncounterName = selectedNode.Type.ToString();
            }

            NotifyRunStateChanged();
        }

        /// <summary>
        /// Guarantees an active procedural map exists for the current run (useful when entering
        /// Game.unity directly for testing or peeking at the current map mid-combat).
        /// </summary>
        public MapGraph EnsureActiveProceduralMap(
            MapGeneratorConfig config,
            MapEncounterPool encounterPool,
            bool placeOnStartingCombatNode = false)
        {
            if (CurrentMap == null || CurrentMap.Nodes == null || CurrentMap.Nodes.Count == 0)
            {
                CurrentMap = ProceduralMapGenerator.Generate(config, encounterPool);
                CurrentMapNodeId = null;
            }

            if (placeOnStartingCombatNode && string.IsNullOrEmpty(CurrentMapNodeId))
            {
                List<MapNode> initialNodes = CurrentMap.GetInitialNodes();
                if (initialNodes != null && initialNodes.Count > 0)
                {
                    MapNode activeStart = initialNodes[0];
                    CurrentMap.MarkVisited(activeStart.Id);
                    CurrentMapNodeId = activeStart.Id;
                    if (SelectedEncounter == null) SelectedEncounter = activeStart.Encounter;
                }
            }

            CurrentMap.RefreshAvailability(CurrentMapNodeId);
            NotifyRunStateChanged();
            return CurrentMap;
        }

        public void ClearSelectedEncounter()
        {
            SelectedEncounter = null;
            NotifyRunStateChanged();
        }

        public void CompleteCurrentEncounter()
        {
            MapNode currentNode = CurrentMap != null && !string.IsNullOrEmpty(CurrentMapNodeId)
                ? CurrentMap.GetNode(CurrentMapNodeId)
                : null;

            CombatsWon++;
            if (currentNode != null)
            {
                if (currentNode.Type == MapNodeType.Elite) ElitesDefeated++;
                else if (currentNode.Type == MapNodeType.Boss) BossesDefeated++;
            }

            ClearSelectedEncounter();
            if (HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null)
            {
                CaptureHeroState(HeroSystem.Instance.HeroView);
            }
        }

        public void InitializeHeroState(int maxHealth, int maxStress)
        {
            maxHealth = Mathf.Max(1, maxHealth);
            maxStress = Mathf.Max(1, maxStress);

            if (HasHeroState)
            {
                return;
            }

            HasHeroState = true;
            HeroMaxHealth = maxHealth;
            HeroCurrentHealth = maxHealth;
            heroStressStates.Clear();
            heroStressStates.Add(new RunHeroStressState(null, maxStress));
            RefreshAggregateStress();
            NotifyRunStateChanged();
        }

        public void InitializeHeroState(IReadOnlyList<HeroData> heroTeam, int maxStress)
        {
            int maxHealth = GetTotalHeroHealth(heroTeam);
            if (maxHealth <= 0) return;

            if (HasHeroState)
            {
                int validHeroCount = CountValidHeroes(heroTeam);
                if (heroStressStates.Count == Mathf.Max(1, validHeroCount)) return;

                RebuildHeroStressStates(heroTeam, maxStress, HeroCurrentWill);
                HeroMaxHealth = Mathf.Max(1, maxHealth);
                HeroCurrentHealth = Mathf.Clamp(HeroCurrentHealth, 0, HeroMaxHealth);
                RefreshAggregateStress();
                NotifyRunStateChanged();
                return;
            }

            HasHeroState = true;
            HeroMaxHealth = maxHealth;
            HeroCurrentHealth = maxHealth;
            RebuildHeroStressStates(heroTeam, maxStress, null);
            RefreshAggregateStress();
            NotifyRunStateChanged();
        }

        public void CaptureHeroState(HeroView heroView)
        {
            if (heroView == null) return;

            InitializeHeroState(heroView.HeroTeam, heroView.MaxStress);
            HeroMaxHealth = Mathf.Max(1, heroView.MaxHealth);
            HeroCurrentHealth = Mathf.Clamp(heroView.CurrentHealth, 0, HeroMaxHealth);

            heroStressStates.Clear();
            foreach (RunHeroStressState stressState in heroView.GetStressStateCopies())
            {
                heroStressStates.Add(stressState);
            }

            if (heroStressStates.Count == 0)
            {
                heroStressStates.Add(new RunHeroStressState(null, Mathf.Max(1, heroView.MaxStress), heroView.CurrentWill));
            }

            RefreshAggregateStress();
            NotifyRunStateChanged();
        }

        public List<RunHeroStressState> GetHeroStressStateCopies()
        {
            List<RunHeroStressState> copies = new();
            foreach (RunHeroStressState stressState in heroStressStates)
            {
                copies.Add(new RunHeroStressState(stressState));
            }

            return copies;
        }

        public void RestorePlayerState(
            int currentHealth,
            int maxHealth,
            IReadOnlyList<RunHeroStressState> restoredStressStates,
            int goldAmount,
            int? crystalsAmount = null)
        {
            HeroMaxHealth = Mathf.Max(1, maxHealth);
            HeroCurrentHealth = Mathf.Clamp(currentHealth, 0, HeroMaxHealth);
            HasHeroState = true;

            heroStressStates.Clear();
            if (restoredStressStates != null)
            {
                foreach (RunHeroStressState stressState in restoredStressStates)
                {
                    if (stressState != null)
                    {
                        heroStressStates.Add(new RunHeroStressState(stressState));
                    }
                }
            }

            if (heroStressStates.Count == 0)
            {
                heroStressStates.Add(new RunHeroStressState(null, HeroMaxStress > 0 ? HeroMaxStress : 100));
            }

            Gold = Mathf.Max(0, goldAmount);
            if (crystalsAmount.HasValue)
            {
                Crystals = Mathf.Max(0, crystalsAmount.Value);
            }
            RefreshAggregateStress();
            NotifyRunStateChanged();
        }

        public void SetHeroHealth(int amount)
        {
            if (!HasHeroState) return;

            HeroCurrentHealth = Mathf.Clamp(amount, 0, HeroMaxHealth);
            NotifyRunStateChanged();
        }

        public void HealHero(int amount)
        {
            if (!HasHeroState || amount <= 0) return;
            SetHeroHealth(HeroCurrentHealth + amount);
        }

        public void HealHeroToFull()
        {
            if (!HasHeroState) return;
            SetHeroHealth(HeroMaxHealth);
        }

        public void DamageHero(int amount)
        {
            if (!HasHeroState || amount <= 0) return;
            SetHeroHealth(HeroCurrentHealth - amount);
        }

        public void ChangeHeroMaxHealth(int amount, bool healByIncrease = true)
        {
            if (!HasHeroState || amount == 0) return;

            int oldMaxHealth = HeroMaxHealth;
            HeroMaxHealth = Mathf.Max(1, HeroMaxHealth + amount);

            if (healByIncrease && amount > 0)
            {
                HeroCurrentHealth += HeroMaxHealth - oldMaxHealth;
            }

            HeroCurrentHealth = Mathf.Clamp(HeroCurrentHealth, 0, HeroMaxHealth);
            NotifyRunStateChanged();
        }

        public void SetStress(int amount)
        {
            if (!HasHeroState) return;

            foreach (RunHeroStressState stressState in heroStressStates)
            {
                stressState.SetStress(amount);
            }

            RefreshAggregateStress();
            NotifyRunStateChanged();
        }

        public void SetHeroStress(int heroIndex, int amount)
        {
            if (!HasHeroState || heroIndex < 0 || heroIndex >= heroStressStates.Count) return;

            heroStressStates[heroIndex].SetStress(amount);
            RefreshAggregateStress();
            NotifyRunStateChanged();
        }

        public void AddStress(int amount)
        {
            if (!HasHeroState || amount <= 0) return;

            foreach (RunHeroStressState stressState in heroStressStates)
            {
                stressState.AddStress(amount);
            }

            RefreshAggregateStress();
            NotifyRunStateChanged();
        }

        public void AddStressToHero(int heroIndex, int amount)
        {
            if (!HasHeroState || heroIndex < 0 || heroIndex >= heroStressStates.Count || amount <= 0) return;

            heroStressStates[heroIndex].AddStress(amount);
            RefreshAggregateStress();
            NotifyRunStateChanged();
        }

        public void ReduceStress(int amount)
        {
            if (!HasHeroState || amount <= 0) return;

            foreach (RunHeroStressState stressState in heroStressStates)
            {
                stressState.ReduceStress(amount);
            }

            RefreshAggregateStress();
            NotifyRunStateChanged();
        }

        public void ClearStress()
        {
            if (!HasHeroState) return;

            foreach (RunHeroStressState stressState in heroStressStates)
            {
                stressState.ClearStress();
            }

            RefreshAggregateStress();
            NotifyRunStateChanged();
        }

        public void ChangeMaxStress(int amount)
        {
            if (!HasHeroState || amount == 0) return;

            foreach (RunHeroStressState stressState in heroStressStates)
            {
                stressState.ChangeMaxStress(amount);
            }

            RefreshAggregateStress();
            NotifyRunStateChanged();
        }

        public void SetGold(int amount)
        {
            Gold = Mathf.Max(0, amount);
            NotifyRunStateChanged();
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            TotalGoldEarned += amount;
            SetGold(Gold + amount);
        }

        public bool SpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (Gold < amount) return false;

            SetGold(Gold - amount);
            return true;
        }

        public void SetCrystals(int amount)
        {
            Crystals = Mathf.Max(0, amount);
            NotifyRunStateChanged();
        }

        public void AddCrystals(int amount)
        {
            if (amount <= 0) return;
            SetCrystals(Crystals + amount);
        }

        public bool SpendCrystals(int amount)
        {
            if (amount <= 0) return true;
            if (Crystals < amount) return false;

            SetCrystals(Crystals - amount);
            return true;
        }

        public void AbandonRun()
        {
            CurrentMap = null;
            CurrentMapNodeId = null;
            SelectedEncounter = null;
            CurrentChapterNumber = 1;
            CurrentChapterName = string.Empty;
            ClearHeroState();
            Gold = 0;
            ResetRunTelemetry();
            Counters.Clear();
            CardCostModifiers.Clear();

            if (RunDeckManager.Instance != null)
            {
                RunDeckManager.Instance.ClearDeck();
            }

            if (RunPotionManager.Instance != null)
            {
                RunPotionManager.Instance.ClearPotions();
            }

            NotifyRunStateChanged();
        }

        private void ResetRunTelemetry()
        {
            ChaptersCleared = 0;
            NodesVisitedTotal = 0;
            CombatsWon = 0;
            ElitesDefeated = 0;
            BossesDefeated = 0;
            TotalGoldEarned = 0;
            LastEncounterName = string.Empty;
        }

        private static string FormatEncounterDisplayName(EncounterData encounter)
        {
            if (encounter == null) return string.Empty;
            if (encounter.Enemies != null && encounter.Enemies.Count > 0)
            {
                List<string> unique = new();
                for (int i = 0; i < encounter.Enemies.Count; i++)
                {
                    EnemyData e = encounter.Enemies[i];
                    if (e != null && !string.IsNullOrWhiteSpace(e.name) && !unique.Contains(e.name))
                    {
                        unique.Add(e.name);
                    }
                }
                if (unique.Count > 0) return string.Join(" & ", unique);
            }
            return encounter.name ?? string.Empty;
        }

        private void ClearHeroState()
        {
            HasHeroState = false;
            HeroCurrentHealth = 0;
            HeroMaxHealth = 0;
            HeroCurrentStress = 0;
            HeroMaxStress = 0;
            heroStressStates.Clear();
        }

        private static int GetTotalHeroHealth(IReadOnlyList<HeroData> heroTeam)
        {
            int totalHealth = 0;
            if (heroTeam == null) return totalHealth;

            foreach (HeroData heroData in heroTeam)
            {
                if (heroData != null) totalHealth += heroData.Health;
            }

            return totalHealth;
        }

        private static int CountValidHeroes(IReadOnlyList<HeroData> heroTeam)
        {
            int count = 0;
            if (heroTeam == null) return count;

            foreach (HeroData heroData in heroTeam)
            {
                if (heroData != null) count++;
            }

            return count;
        }

        private void RebuildHeroStressStates(IReadOnlyList<HeroData> heroTeam, int maxWill, int? startingWill = null)
        {
            heroStressStates.Clear();

            if (heroTeam != null)
            {
                foreach (HeroData heroData in heroTeam)
                {
                    if (heroData == null) continue;
                    int resolvedMax = heroData.WillProfile != null ? heroData.EffectiveMaxWill : maxWill;
                    int? resolvedStart = startingWill ?? heroData.EffectiveStartingWill;
                    heroStressStates.Add(new RunHeroStressState(heroData, resolvedMax, resolvedStart));
                }
            }

            if (heroStressStates.Count == 0)
            {
                heroStressStates.Add(new RunHeroStressState(null, maxWill, startingWill ?? RunHeroStressState.DefaultStartingWill));
            }
        }

        private void RefreshAggregateStress()
        {
            HeroCurrentStress = 0;
            HeroMaxStress = 0;

            foreach (RunHeroStressState stressState in heroStressStates)
            {
                if (stressState == null) continue;
                HeroCurrentStress = Mathf.Max(HeroCurrentStress, stressState.CurrentStress);
                HeroMaxStress = Mathf.Max(HeroMaxStress, stressState.MaxStress);
            }
        }

        private void NotifyRunStateChanged()
        {
            RunStateChanged?.Invoke();
        }
    }
}
