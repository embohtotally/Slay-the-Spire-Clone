using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeroView : CombatantView
{
    [SerializeField] private Slider stressSlider;
    [SerializeField] private int maxStress = 100;

    public int CurrentStress { get; private set; }
    public int MaxStress { get; private set; } = 100;
    public bool IsStressed { get; private set; } = false;

    private List<GameObject> heroSprites = new List<GameObject>();

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

    public void Setup(List<HeroData> heroTeam)
    {
        if (heroTeam == null || heroTeam.Count == 0) return;

        int totalHealth = 0;
        foreach (var hero in heroTeam)
        {
            totalHealth += hero.Health;
        }

        int setupMaxHealth = totalHealth;
        int setupMaxStress = Mathf.Max(1, maxStress);
        bool shouldUseRunState = RunManager.Instance != null && RunManager.Instance.HasActiveRun;

        if (shouldUseRunState)
        {
            RunManager.Instance.InitializeHeroState(setupMaxHealth, setupMaxStress);
            setupMaxHealth = RunManager.Instance.HeroMaxHealth;
            setupMaxStress = RunManager.Instance.HeroMaxStress;
        }

        MaxStress = setupMaxStress;
        SetupBase(setupMaxHealth, null);
        ClearStats();
        
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        // Clean up old sprites
        foreach (var obj in heroSprites)
        {
            if (obj != null) Destroy(obj);
        }
        heroSprites.Clear();

        // Spawn new sprites
        float spacing = 2.0f;
        float startX = (heroTeam.Count - 1) * (spacing / 2.0f) * -1.0f;

        for (int i = 0; i < heroTeam.Count; i++)
        {
            GameObject spriteObj = new GameObject($"HeroSprite_{i}");
            spriteObj.transform.SetParent(this.transform);
            spriteObj.transform.localPosition = new Vector3(startX + (i * spacing), 0, 0);
            
            SpriteRenderer sr = spriteObj.AddComponent<SpriteRenderer>();
            sr.sprite = heroTeam[i].Image;
            sr.sortingOrder = 5;
            
            heroSprites.Add(spriteObj);
        }
        
        if (shouldUseRunState && RunManager.Instance.HasHeroState)
        {
            SetCurrentHealth(RunManager.Instance.HeroCurrentHealth);
            SetCurrentStress(RunManager.Instance.HeroCurrentStress);
        }
        else
        {
            SetCurrentStress(0);
        }
    }

    public override void ClearStats()
    {
        base.ClearStats();
        CurrentStress = 0;
        IsStressed = false;
        UpdateStressSlider();
    }

    public override void Damage(int damageAmount)
    {
        base.Damage(damageAmount);
        // Fear/stress from regular enemy damage is reduced to 0. It can now only be inflicted by intent effects.
    }

    public override void AddStress(int amount)
    {
        if (amount <= 0 || IsStressed) return;

        SetCurrentStress(CurrentStress + amount);
    }

    public void SetCurrentStress(int amount)
    {
        MaxStress = Mathf.Max(1, MaxStress);
        CurrentStress = Mathf.Clamp(amount, 0, MaxStress);
        IsStressed = CurrentStress >= MaxStress;

        if (IsStressed)
        {
            ApplyStressPenaltyIfNeeded();
        }
        else if (HealthPenalty > 0)
        {
            HealthPenalty = 0;
            UpdateHealthVisual();
        }

        UpdateStressSlider();
    }

    private void ApplyStressPenaltyIfNeeded()
    {
        if (HealthPenalty > 0) return;

        // Breakdown: Reduce effective Max HP by 20% (minimum 1) while stress is full.
        int penaltyAmount = Mathf.Max(1, MaxHealth / 5);
        ApplyHealthPenalty(penaltyAmount);
    }

    private void UpdateStressSlider()
    {
        if (stressSlider != null)
        {
            stressSlider.value = MaxStress > 0 ? (float)CurrentStress / MaxStress : 0f;
        }
    }

    public void ClearStressedState()
    {
        CurrentStress = 0;
        IsStressed = false;
        HealthPenalty = 0;
        UpdateHealthVisual();
        UpdateStressSlider();
    }
}
