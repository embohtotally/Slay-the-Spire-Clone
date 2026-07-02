using System;
using System.Collections.Generic;
using UnityEngine;

public class RunPotionManager : PersistentSingleton<RunPotionManager>
{
    [SerializeField, Min(0)] private int defaultCapacity = 3;

    private readonly List<PotionData> currentPotions = new();

    public IReadOnlyList<PotionData> CurrentPotions => currentPotions;
    public int Capacity { get; private set; }
    public bool HasPotions => currentPotions.Count > 0;
    public bool IsFull => Capacity > 0 && currentPotions.Count >= Capacity;
    public event Action<IReadOnlyList<PotionData>> PotionsChanged;

    protected override void Awake()
    {
        base.Awake();
        if (Capacity <= 0)
        {
            Capacity = Mathf.Max(0, defaultCapacity);
        }
    }

    public static RunPotionManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject host = RunManager.Instance != null
            ? RunManager.Instance.gameObject
            : new GameObject("Run Potion Manager");

        RunPotionManager manager = host.GetComponent<RunPotionManager>();
        if (manager == null)
        {
            manager = host.AddComponent<RunPotionManager>();
        }

        return manager;
    }

    public List<PotionData> GetPotionsCopy()
    {
        return new List<PotionData>(currentPotions);
    }

    public void ResetPotions(IEnumerable<PotionData> startingPotions = null, int capacity = -1)
    {
        currentPotions.Clear();

        if (capacity >= 0)
        {
            Capacity = Mathf.Max(0, capacity);
        }
        else if (Capacity <= 0)
        {
            Capacity = Mathf.Max(0, defaultCapacity);
        }

        if (startingPotions != null)
        {
            foreach (PotionData potion in startingPotions)
            {
                AddPotionInternal(potion, false);
            }
        }

        NotifyPotionsChanged();
    }

    public void ClearPotions()
    {
        if (currentPotions.Count == 0) return;

        currentPotions.Clear();
        NotifyPotionsChanged();
    }

    public void SetCapacity(int capacity, bool trimOverflow = false)
    {
        Capacity = Mathf.Max(0, capacity);

        if (trimOverflow && Capacity > 0)
        {
            while (currentPotions.Count > Capacity)
            {
                currentPotions.RemoveAt(currentPotions.Count - 1);
            }
        }

        NotifyPotionsChanged();
    }

    public bool Contains(PotionData potion)
    {
        return potion != null && currentPotions.Contains(potion);
    }

    public int Count(PotionData potion)
    {
        if (potion == null) return 0;

        int count = 0;
        foreach (PotionData currentPotion in currentPotions)
        {
            if (currentPotion == potion) count++;
        }

        return count;
    }

    public bool HasIndex(int index)
    {
        return index >= 0 && index < currentPotions.Count;
    }

    public bool AddPotion(PotionData potion)
    {
        bool added = AddPotionInternal(potion, true);
        if (added) NotifyPotionsChanged();
        return added;
    }

    public bool RemovePotion(PotionData potion)
    {
        if (potion == null) return false;

        bool removed = currentPotions.Remove(potion);
        if (removed)
        {
            NotifyPotionsChanged();
        }

        return removed;
    }

    public bool RemoveAt(int index)
    {
        if (!HasIndex(index)) return false;

        currentPotions.RemoveAt(index);
        NotifyPotionsChanged();
        return true;
    }

    private bool AddPotionInternal(PotionData potion, bool logFailures)
    {
        if (potion == null)
        {
            if (logFailures) Gameseed26.Logger.LogWarning("Tried to add a null potion to the run.");
            return false;
        }

        if (IsFull)
        {
            if (logFailures) Gameseed26.Logger.Log($"Potion slots are full ({currentPotions.Count}/{Capacity}).");
            return false;
        }

        if (potion.Unique && Contains(potion))
        {
            if (logFailures) Gameseed26.Logger.Log(this, $"Potion '{potion.Title}' is unique and already owned.");
            return false;
        }

        currentPotions.Add(potion);
        return true;
    }

    public void NotifyPotionsChanged()
    {
        PotionsChanged?.Invoke(CurrentPotions);
    }
}
