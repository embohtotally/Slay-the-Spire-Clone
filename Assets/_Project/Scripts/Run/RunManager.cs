using System;
using UnityEngine;

public class RunManager : PersistentSingleton<RunManager>
{
    [Header("Run Defaults")]
    [SerializeField] private int startingGold;

    public MapGraph CurrentMap { get; private set; }
    public string CurrentMapNodeId { get; private set; }
    public EncounterData SelectedEncounter { get; private set; }
    public bool HasActiveRun => CurrentMap != null;

    public bool HasHeroState { get; private set; }
    public int HeroCurrentHealth { get; private set; }
    public int HeroMaxHealth { get; private set; }
    public int HeroCurrentStress { get; private set; }
    public int HeroMaxStress { get; private set; }
    public int Gold { get; private set; }

    public event Action RunStateChanged;

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
        ClearHeroState();
        Gold = Mathf.Max(0, startingGold);

        if (RunDeckManager.Instance != null)
        {
            RunDeckManager.Instance.ClearDeck();
        }

        NotifyRunStateChanged();
    }

    public void SelectMapNode(MapNode selectedNode)
    {
        if (selectedNode == null) return;

        CurrentMapNodeId = selectedNode.Id;
        SelectedEncounter = selectedNode.Encounter;
        NotifyRunStateChanged();
    }

    public void ClearSelectedEncounter()
    {
        SelectedEncounter = null;
        NotifyRunStateChanged();
    }

    public void CompleteCurrentEncounter()
    {
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
        HeroMaxStress = maxStress;
        HeroCurrentStress = 0;
        NotifyRunStateChanged();
    }

    public void CaptureHeroState(HeroView heroView)
    {
        if (heroView == null) return;

        InitializeHeroState(heroView.MaxHealth, heroView.MaxStress);
        HeroMaxHealth = Mathf.Max(1, heroView.MaxHealth);
        HeroCurrentHealth = Mathf.Clamp(heroView.CurrentHealth, 0, HeroMaxHealth);
        HeroMaxStress = Mathf.Max(1, heroView.MaxStress);
        HeroCurrentStress = Mathf.Clamp(heroView.CurrentStress, 0, HeroMaxStress);
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

        HeroCurrentStress = Mathf.Clamp(amount, 0, HeroMaxStress);
        NotifyRunStateChanged();
    }

    public void AddStress(int amount)
    {
        if (!HasHeroState || amount <= 0) return;
        SetStress(HeroCurrentStress + amount);
    }

    public void ReduceStress(int amount)
    {
        if (!HasHeroState || amount <= 0) return;
        SetStress(HeroCurrentStress - amount);
    }

    public void ClearStress()
    {
        if (!HasHeroState) return;
        SetStress(0);
    }

    public void ChangeMaxStress(int amount)
    {
        if (!HasHeroState || amount == 0) return;

        HeroMaxStress = Mathf.Max(1, HeroMaxStress + amount);
        HeroCurrentStress = Mathf.Clamp(HeroCurrentStress, 0, HeroMaxStress);
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
        SetGold(Gold + amount);
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (Gold < amount) return false;

        SetGold(Gold - amount);
        return true;
    }

    public void AbandonRun()
    {
        CurrentMap = null;
        CurrentMapNodeId = null;
        SelectedEncounter = null;
        ClearHeroState();
        Gold = 0;

        if (RunDeckManager.Instance != null)
        {
            RunDeckManager.Instance.ClearDeck();
        }

        NotifyRunStateChanged();
    }

    private void ClearHeroState()
    {
        HasHeroState = false;
        HeroCurrentHealth = 0;
        HeroMaxHealth = 0;
        HeroCurrentStress = 0;
        HeroMaxStress = 0;
    }

    private void NotifyRunStateChanged()
    {
        RunStateChanged?.Invoke();
    }
}
