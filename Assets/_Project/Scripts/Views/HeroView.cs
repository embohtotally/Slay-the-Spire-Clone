using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeroView : CombatantView
{
    [SerializeField] private Slider stressSlider;
    
    public int CurrentStress { get; private set; }
    public int MaxStress { get; private set; } = 35;
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

        SetupBase(totalHealth, null);
        
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
        
        if (RunManager.Instance != null && RunManager.Instance.CurrentHeroHealth.HasValue)
        {
            SetCurrentHealth(RunManager.Instance.CurrentHeroHealth.Value);
        }

        ClearStats();
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
        if (IsStressed) return;

        CurrentStress += amount;

        if (CurrentStress >= MaxStress)
        {
            CurrentStress = MaxStress;
            IsStressed = true;
            // Breakdown: Reduce Max HP by 20% (minimum 1)
            int penaltyAmount = Mathf.Max(1, MaxHealth / 5);
            ApplyHealthPenalty(penaltyAmount);
        }

        UpdateStressSlider();
    }

    private void UpdateStressSlider()
    {
        if (stressSlider != null)
        {
            stressSlider.value = (float)CurrentStress / MaxStress;
        }
    }

    public void ClearStressedState()
    {
        if (IsStressed)
        {
            IsStressed = false;
            CurrentStress = 0;
            UpdateStressSlider();
        }
    }
}