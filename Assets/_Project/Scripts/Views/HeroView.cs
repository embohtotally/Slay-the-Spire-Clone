using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeroView : CombatantView
{
    [SerializeField] private Slider stressSlider;
    
    public int CurrentStress { get; private set; }
    public int MaxStress { get; private set; } = 100;

    private List<GameObject> heroSprites = new List<GameObject>();

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
        UpdateStressSlider();
    }

    public override void Damage(int damageAmount)
    {
        int initialHealth = CurrentHealth;
        base.Damage(damageAmount);
        int damageTaken = initialHealth - CurrentHealth;

        if (damageTaken > 0)
        {
            AddStress(damageTaken);
        }
    }

    public override void AddStress(int amount)
    {
        CurrentStress += amount;

        if (CurrentStress >= MaxStress)
        {
            CurrentStress = 0;
            // Breakdown: Deal 20% max HP direct damage (minimum 1)
            int breakdownDamage = Mathf.Max(1, MaxHealth / 5);
            ReduceHealth(breakdownDamage);
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
}