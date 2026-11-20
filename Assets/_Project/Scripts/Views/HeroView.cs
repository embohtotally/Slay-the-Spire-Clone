using PlushieOdyssey.Core;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace PlushieOdyssey.Combat.Views
{
    public class HeroView : CombatantView
    {
        [Header("Will System (formerly Fear/Stress)")]
        [SerializeField] private Slider stressSlider;
        [SerializeField] private int maxStress = 100;
        [SerializeField, Range(0, 100)] private int startingWill = RunHeroStressState.DefaultStartingWill;
        [SerializeField, Tooltip("Positive multiplier on outgoing attack damage & shield when Will >= 75%.")]
        private float highWillBonusMultiplier = 1.25f;

        private readonly List<GameObject> heroSprites = new();
        private readonly List<RunHeroStressState> stressStates = new();
        private readonly List<HeroData> heroTeam = new();

        public int CurrentStress { get; private set; }
        public int MaxStress { get; private set; } = 100;
        public bool IsStressed { get; private set; }

        // First-class Will API (0..MaxWill):
        // >= 75% -> High Will (positive empowered bonus), < 25% -> Low Will (Fear breakdown penalty).
        public int MaxWill => Mathf.Max(1, MaxStress);
        public int CurrentWill => Mathf.Clamp(MaxWill - CurrentStress, 0, MaxWill);
        public float WillNormalized => (float)CurrentWill / MaxWill;
        public bool IsLowWill => IsStressed;
        public bool IsHighWill { get; private set; }
        public float HighWillBonusMultiplier => Mathf.Max(1f, highWillBonusMultiplier);

        public IReadOnlyList<RunHeroStressState> StressStates => stressStates;
        public IReadOnlyList<HeroData> HeroTeam => heroTeam;

        public event Action StressStateChanged;

        private void Awake()
        {
            if (stressSlider == null)
            {
                GameObject sliderObj = GameObject.Find("StressSliderUI");
                if (sliderObj != null)
                {
                    stressSlider = sliderObj.GetComponent<Slider>();
                }
            }
        }

        public void Setup(List<HeroData> setupHeroTeam)
        {
            if (setupHeroTeam == null || setupHeroTeam.Count == 0) return;

            heroTeam.Clear();
            foreach (HeroData heroData in setupHeroTeam)
            {
                if (heroData != null) heroTeam.Add(heroData);
            }

            if (heroTeam.Count == 0) return;

            int totalHealth = 0;
            foreach (HeroData hero in heroTeam)
            {
                totalHealth += hero.Health;
            }

            int setupMaxHealth = Mathf.Max(1, totalHealth);
            int setupMaxWill = Mathf.Max(1, maxStress);
            bool shouldUseRunState = RunManager.Instance != null && RunManager.Instance.HasActiveRun;

            if (shouldUseRunState)
            {
                RunManager.Instance.InitializeHeroState(heroTeam, setupMaxWill);
                setupMaxHealth = RunManager.Instance.HeroMaxHealth;
                setupMaxWill = RunManager.Instance.HeroMaxStress > 0 ? RunManager.Instance.HeroMaxStress : setupMaxWill;
            }

            MaxStress = setupMaxWill;
            SetupBase(setupMaxHealth, null);
            ClearStats();

            foreach (GameObject obj in heroSprites)
            {
                if (obj != null) Destroy(obj);
            }
            heroSprites.Clear();

            if (shouldUseRunState && RunManager.Instance.HasHeroState)
            {
                SetCurrentHealth(RunManager.Instance.HeroCurrentHealth);
                SetStressStates(RunManager.Instance.GetHeroStressStateCopies());
            }
            else
            {
                InitializeStressStates(setupMaxWill);
            }
        }

        public override void ClearStats()
        {
            base.ClearStats();
            CurrentStress = MaxWill - Mathf.Clamp(startingWill, 0, MaxWill);
            IsStressed = false;
            IsHighWill = false;
            HealthPenalty = 0;
            UpdateStressSlider();
            StressStateChanged?.Invoke();
        }

        public override void Damage(int damageAmount)
        {
            base.Damage(damageAmount);
        }

        public override void AddStress(int amount)
        {
            // Enemy Fear/Will-drain hits lower Will.
            DrainWillFromAllHeroes(amount);
        }

        public void DrainWillFromAllHeroes(int amount)
        {
            if (amount <= 0) return;
            foreach (RunHeroStressState state in stressStates)
            {
                state.DrainWill(amount);
            }
            RefreshStressState();
        }

        public void AddWillToAllHeroes(int amount)
        {
            if (amount <= 0) return;
            foreach (RunHeroStressState state in stressStates)
            {
                state.AddWill(amount);
            }
            RefreshStressState();
        }

        public void SetCurrentWill(int amount)
        {
            foreach (RunHeroStressState state in stressStates)
            {
                state.SetWill(amount);
            }
            RefreshStressState();
        }

        public void SetHeroWill(int heroIndex, int amount)
        {
            if (heroIndex < 0 || heroIndex >= stressStates.Count) return;
            stressStates[heroIndex].SetWill(amount);
            RefreshStressState();
        }

        public void AddStressToHero(int heroIndex, int amount)
        {
            if (amount <= 0 || heroIndex < 0 || heroIndex >= stressStates.Count) return;

            stressStates[heroIndex].AddStress(amount);
            RefreshStressState();
        }

        public void AddStressToAllHeroes(int amount)
        {
            DrainWillFromAllHeroes(amount);
        }

        public void ReduceStressToAllHeroes(int amount)
        {
            AddWillToAllHeroes(amount);
        }

        public void SetCurrentStress(int amount)
        {
            foreach (RunHeroStressState stressState in stressStates)
            {
                stressState.SetStress(amount);
            }

            RefreshStressState();
        }

        public void ClearStressedState()
        {
            foreach (RunHeroStressState stressState in stressStates)
            {
                stressState.ClearStress();
            }

            HealthPenalty = 0;
            RefreshStressState(false);
            UpdateHealthVisual();
        }

        public List<RunHeroStressState> GetStressStateCopies()
        {
            List<RunHeroStressState> copies = new();
            foreach (RunHeroStressState stressState in stressStates)
            {
                copies.Add(new RunHeroStressState(stressState));
            }

            return copies;
        }

        private void InitializeStressStates(int setupMaxWill)
        {
            stressStates.Clear();
            int initWill = Mathf.Clamp(startingWill, 0, setupMaxWill);
            foreach (HeroData heroData in heroTeam)
            {
                stressStates.Add(new RunHeroStressState(heroData, setupMaxWill, initWill));
            }

            RefreshStressState(false);
        }

        private void SetStressStates(IReadOnlyList<RunHeroStressState> newStressStates)
        {
            stressStates.Clear();
            if (newStressStates != null)
            {
                foreach (RunHeroStressState stressState in newStressStates)
                {
                    if (stressState != null) stressStates.Add(new RunHeroStressState(stressState));
                }
            }

            if (stressStates.Count == 0)
            {
                InitializeStressStates(MaxWill);
                return;
            }

            RefreshStressState(false);
        }

        private void RefreshStressState(bool applyPenalty = true)
        {
            bool wasLowWill = IsStressed;
            bool wasHighWill = IsHighWill;

            MaxStress = Mathf.Max(1, MaxStress);
            int maxStressFound = 0;
            bool anyLowWill = false;
            bool anyHighWill = false;

            foreach (RunHeroStressState stressState in stressStates)
            {
                if (stressState == null) continue;
                MaxStress = Mathf.Max(MaxStress, stressState.MaxStress);
                maxStressFound = Mathf.Max(maxStressFound, stressState.CurrentStress);
                anyLowWill |= stressState.IsLowWill;
                anyHighWill |= stressState.IsHighWill;
            }

            CurrentStress = stressStates.Count > 0 ? maxStressFound : MaxStress - Mathf.Clamp(startingWill, 0, MaxStress);
            IsStressed = anyLowWill;
            IsHighWill = !anyLowWill && anyHighWill;

            if (applyPenalty)
            {
                ApplyStressPenaltyState();

                if (IsHighWill && !wasHighWill)
                {
                    GameManager.GenerateFloatingText("High Will! (+25%)", transform, 1.2f, 1f, "#38D4FF");
                }
                else if (IsStressed && !wasLowWill)
                {
                    GameManager.GenerateFloatingText("Low Will! (Fear)", transform, 1.2f, 1f, "#FF4D5A");
                }
            }

            UpdateStressSlider();
            StressStateChanged?.Invoke();
            NotifyStatusEffectsChanged();
        }

        private void ApplyStressPenaltyState()
        {
            int penaltyAmount = CalculateStressHealthPenalty();
            if (HealthPenalty == penaltyAmount) return;

            HealthPenalty = penaltyAmount;
            if (CurrentHealth > EffectiveMaxHealth)
            {
                SetCurrentHealth(EffectiveMaxHealth);
            }

            UpdateHealthVisual();
        }

        private int CalculateStressHealthPenalty()
        {
            int penaltyAmount = 0;
            for (int i = 0; i < stressStates.Count; i++)
            {
                if (!stressStates[i].IsLowWill) continue;

                int heroHealth = i < heroTeam.Count && heroTeam[i] != null ? heroTeam[i].Health : MaxHealth;
                penaltyAmount += Mathf.Max(1, heroHealth / 5);
            }

            return Mathf.Clamp(penaltyAmount, 0, Mathf.Max(0, MaxHealth - 1));
        }

        private void UpdateStressSlider()
        {
            if (stressSlider != null)
            {
                float targetWill = WillNormalized;
                stressSlider.DOKill();
                if (stressSlider.fillRect != null)
                {
                    stressSlider.fillRect.gameObject.SetActive(targetWill > 0.001f);
                }
                stressSlider.DOValue(targetWill, 0.25f);
            }
        }
    }
}
