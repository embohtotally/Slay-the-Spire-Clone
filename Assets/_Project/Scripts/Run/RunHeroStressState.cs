using PlushieOdyssey.Data;
using System;
using UnityEngine;

namespace PlushieOdyssey.Run
{
    [Serializable]
    public class RunHeroStressState
    {
        public const int DefaultStartingWill = 50;
        public const float LowWillThreshold = 0.25f;
        public const float HighWillThreshold = 0.75f;

        [field: SerializeField] public HeroData HeroData { get; private set; }
        [field: SerializeField] public int CurrentStress { get; private set; }
        [field: SerializeField] public int MaxStress { get; private set; }

        // Will system (renamed from Fear/Stress):
        // 100% Will = 0 Stress, 0% Will = MaxStress.
        // >= 75% Will grants a positive empowered effect; < 25% Will triggers the Fear breakdown effect.
        public int MaxWill => Mathf.Max(1, MaxStress);
        public int CurrentWill => Mathf.Clamp(MaxWill - CurrentStress, 0, MaxWill);
        public float WillNormalized => (float)CurrentWill / MaxWill;
        public bool IsLowWill => WillNormalized < LowWillThreshold;
        public bool IsHighWill => WillNormalized >= HighWillThreshold;

        // Backward-compatible property: Fear effect triggers when Will drops below 25%.
        public bool IsStressed => IsLowWill;

        public RunHeroStressState(HeroData heroData, int maxWill, int? startingWill = null)
        {
            HeroData = heroData;
            MaxStress = Mathf.Max(1, maxWill);
            int initialWill = startingWill.HasValue
                ? Mathf.Clamp(startingWill.Value, 0, MaxStress)
                : Mathf.Clamp(DefaultStartingWill, 0, MaxStress);
            CurrentStress = MaxStress - initialWill;
        }

        public RunHeroStressState(RunHeroStressState source)
        {
            HeroData = source?.HeroData;
            MaxStress = Mathf.Max(1, source?.MaxStress ?? 100);
            CurrentStress = Mathf.Clamp(source?.CurrentStress ?? (MaxStress - DefaultStartingWill), 0, MaxStress);
        }

        public void SetWill(int amount)
        {
            int clampedWill = Mathf.Clamp(amount, 0, MaxWill);
            CurrentStress = MaxWill - clampedWill;
        }

        public void AddWill(int amount)
        {
            if (amount <= 0) return;
            SetWill(CurrentWill + amount);
        }

        public void DrainWill(int amount)
        {
            if (amount <= 0) return;
            SetWill(CurrentWill - amount);
        }

        // Legacy Stress/Fear methods kept mapped cleanly to Will:
        // Adding Fear/Stress drains Will; reducing Fear/Stress restores Will.
        public void SetStress(int amount)
        {
            CurrentStress = Mathf.Clamp(amount, 0, MaxWill);
        }

        public void AddStress(int amount)
        {
            DrainWill(amount);
        }

        public void ReduceStress(int amount)
        {
            AddWill(amount);
        }

        public void ClearStress()
        {
            SetWill(Mathf.Clamp(DefaultStartingWill, 0, MaxWill));
        }

        public void ChangeMaxStress(int amount)
        {
            int currentWill = CurrentWill;
            MaxStress = Mathf.Max(1, MaxStress + amount);
            SetWill(currentWill);
        }
    }
}
