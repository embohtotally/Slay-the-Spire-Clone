using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeroView : CombatantView
{
    [SerializeField] private Slider stressSlider;
    [SerializeField] private int maxStress = 100;

    private readonly List<GameObject> heroSprites = new();
    private readonly List<RunHeroStressState> stressStates = new();
    private readonly List<HeroData> heroTeam = new();

    public int CurrentStress { get; private set; }
    public int MaxStress { get; private set; } = 100;
    public bool IsStressed { get; private set; }
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
        int setupMaxStress = Mathf.Max(1, maxStress);
        bool shouldUseRunState = RunManager.Instance != null && RunManager.Instance.HasActiveRun;

        if (shouldUseRunState)
        {
            RunManager.Instance.InitializeHeroState(heroTeam, setupMaxStress);
            setupMaxHealth = RunManager.Instance.HeroMaxHealth;
            setupMaxStress = RunManager.Instance.HeroMaxStress > 0 ? RunManager.Instance.HeroMaxStress : setupMaxStress;
        }

        MaxStress = setupMaxStress;
        SetupBase(setupMaxHealth, null);
        ClearStats();

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        foreach (GameObject obj in heroSprites)
        {
            if (obj != null) Destroy(obj);
        }
        heroSprites.Clear();

        float spacing = 2.0f;
        float startX = (heroTeam.Count - 1) * (spacing / 2.0f) * -1.0f;

        for (int i = 0; i < heroTeam.Count; i++)
        {
            GameObject spriteObj = new($"HeroSprite_{i}");
            spriteObj.transform.SetParent(transform);
            spriteObj.transform.localPosition = new Vector3(startX + (i * spacing), 0, 0);

            SpriteRenderer sr = spriteObj.AddComponent<SpriteRenderer>();
            sr.sprite = heroTeam[i].Image;
            sr.sortingOrder = 5;

            heroSprites.Add(spriteObj);
        }

        if (shouldUseRunState && RunManager.Instance.HasHeroState)
        {
            SetCurrentHealth(RunManager.Instance.HeroCurrentHealth);
            SetStressStates(RunManager.Instance.GetHeroStressStateCopies());
        }
        else
        {
            InitializeStressStates(setupMaxStress);
        }
    }

    public override void ClearStats()
    {
        base.ClearStats();
        CurrentStress = 0;
        IsStressed = false;
        HealthPenalty = 0;
        UpdateStressSlider();
        StressStateChanged?.Invoke();
    }

    public override void Damage(int damageAmount)
    {
        base.Damage(damageAmount);
        // Fear/stress from regular enemy damage is reduced to 0. It can now only be inflicted by intent effects.
    }

    public override void AddStress(int amount)
    {
        AddStressToAllHeroes(amount);
    }

    public void AddStressToHero(int heroIndex, int amount)
    {
        if (amount <= 0 || heroIndex < 0 || heroIndex >= stressStates.Count) return;

        stressStates[heroIndex].AddStress(amount);
        RefreshStressState();
    }

    public void AddStressToAllHeroes(int amount)
    {
        if (amount <= 0) return;

        foreach (RunHeroStressState stressState in stressStates)
        {
            stressState.AddStress(amount);
        }

        RefreshStressState();
    }

    public void ReduceStressToAllHeroes(int amount)
    {
        if (amount <= 0) return;

        foreach (RunHeroStressState stressState in stressStates)
        {
            stressState.ReduceStress(amount);
        }

        RefreshStressState();
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

    private void InitializeStressStates(int setupMaxStress)
    {
        stressStates.Clear();
        foreach (HeroData heroData in heroTeam)
        {
            stressStates.Add(new RunHeroStressState(heroData, setupMaxStress));
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
            InitializeStressStates(MaxStress);
            return;
        }

        RefreshStressState(false);
    }

    private void RefreshStressState(bool applyPenalty = true)
    {
        CurrentStress = 0;
        MaxStress = Mathf.Max(1, MaxStress);
        IsStressed = false;

        foreach (RunHeroStressState stressState in stressStates)
        {
            if (stressState == null) continue;
            CurrentStress = Mathf.Max(CurrentStress, stressState.CurrentStress);
            MaxStress = Mathf.Max(MaxStress, stressState.MaxStress);
            IsStressed |= stressState.IsStressed;
        }

        if (applyPenalty)
        {
            ApplyStressPenaltyState();
        }

        UpdateStressSlider();
        StressStateChanged?.Invoke();
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
            if (!stressStates[i].IsStressed) continue;

            int heroHealth = i < heroTeam.Count && heroTeam[i] != null ? heroTeam[i].Health : MaxHealth;
            penaltyAmount += Mathf.Max(1, heroHealth / 5);
        }

        return Mathf.Clamp(penaltyAmount, 0, Mathf.Max(0, MaxHealth - 1));
    }

    private void UpdateStressSlider()
    {
        if (stressSlider != null)
        {
            stressSlider.value = MaxStress > 0 ? (float)CurrentStress / MaxStress : 0f;
        }
    }
}
