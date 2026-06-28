using UnityEngine;

[DisallowMultipleComponent]
public class RunStateActions : MonoBehaviour
{
    [SerializeField] private bool createRunManagerIfMissing;

    public void HealHero(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.HealHero(amount);
        }
    }

    public void HealHeroToFull()
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.HealHeroToFull();
        }
    }

    public void HealHeroByPercent(float percent)
    {
        if (!TryGetRunManager(out RunManager runManager) || !runManager.HasHeroState) return;

        int healAmount = Mathf.CeilToInt(runManager.HeroMaxHealth * Mathf.Clamp01(percent));
        runManager.HealHero(healAmount);
    }

    public void DamageHero(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.DamageHero(amount);
        }
    }

    public void SetHeroHealth(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.SetHeroHealth(amount);
        }
    }

    public void IncreaseMaxHealth(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ChangeHeroMaxHealth(Mathf.Abs(amount), true);
        }
    }

    public void DecreaseMaxHealth(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ChangeHeroMaxHealth(-Mathf.Abs(amount), false);
        }
    }

    public void AddStress(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.AddStress(amount);
        }
    }

    public void ReduceStress(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ReduceStress(amount);
        }
    }

    public void ClearStress()
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ClearStress();
        }
    }

    public void IncreaseMaxStress(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ChangeMaxStress(Mathf.Abs(amount));
        }
    }

    public void DecreaseMaxStress(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ChangeMaxStress(-Mathf.Abs(amount));
        }
    }

    public void AddGold(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.AddGold(amount);
        }
    }

    public void SetGold(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.SetGold(amount);
        }
    }

    public void SpendGold(int amount)
    {
        if (TryGetRunManager(out RunManager runManager) && !runManager.SpendGold(amount))
        {
            Debug.Log($"Not enough gold. Need {amount}, have {runManager.Gold}.");
        }
    }

    private bool TryGetRunManager(out RunManager runManager)
    {
        runManager = RunManager.Instance;
        if (runManager == null && createRunManagerIfMissing)
        {
            GameObject runManagerObject = new("Run Manager");
            runManager = runManagerObject.AddComponent<RunManager>();
        }

        if (runManager != null) return true;

        Debug.LogWarning("RunStateActions could not find a RunManager.", this);
        return false;
    }
}
