using System;
using System.Collections.Generic;
using Gameseed26;
using UnityEngine;

public class RunRelicManager : PersistentSingleton<RunRelicManager>
{
    private readonly List<RelicData> currentRelics = new();

    public IReadOnlyList<RelicData> CurrentRelics => currentRelics;
    public bool HasRelics => currentRelics.Count > 0;
    public event Action<IReadOnlyList<RelicData>> RelicsChanged;

    public static RunRelicManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject host = RunManager.Instance != null
            ? RunManager.Instance.gameObject
            : new GameObject("Run Relic Manager");

        RunRelicManager manager = host.GetComponent<RunRelicManager>();
        if (manager == null)
        {
            manager = host.AddComponent<RunRelicManager>();
        }

        return manager;
    }

    public List<RelicData> GetRelicsCopy()
    {
        return new List<RelicData>(currentRelics);
    }

    public void ResetRelics(IEnumerable<RelicData> startingRelics)
    {
        currentRelics.Clear();

        if (startingRelics != null)
        {
            foreach (RelicData relic in startingRelics)
            {
                AddRelicInternal(relic, false);
            }
        }

        NotifyRelicsChanged();
    }

    public void ClearRelics()
    {
        if (currentRelics.Count == 0) return;

        currentRelics.Clear();
        NotifyRelicsChanged();
    }

    public bool Contains(RelicData relic)
    {
        return relic != null && currentRelics.Contains(relic);
    }

    public int Count(RelicData relic)
    {
        if (relic == null) return 0;

        int count = 0;
        foreach (RelicData currentRelic in currentRelics)
        {
            if (currentRelic == relic) count++;
        }

        return count;
    }

    public bool AddRelic(RelicData relic)
    {
        bool added = AddRelicInternal(relic, true);
        if (added) NotifyRelicsChanged();
        return added;
    }

    public bool RemoveRelic(RelicData relic)
    {
        if (relic == null) return false;

        bool removed = currentRelics.Remove(relic);
        if (removed)
        {
            NotifyRelicsChanged();
        }

        return removed;
    }

    private bool AddRelicInternal(RelicData relic, bool logFailures)
    {
        if (relic == null)
        {
            if (logFailures) Gameseed26.Logger.LogWarning("Tried to add a null relic to the run.");
            return false;
        }

        if (relic.Unique && Contains(relic))
        {
            if (logFailures) Gameseed26.Logger.Log(this, $"Relic '{relic.Title}' is unique and already owned.");
            return false;
        }

        currentRelics.Add(relic);
        return true;
    }

    public void NotifyRelicsChanged()
    {
        RelicsChanged?.Invoke(CurrentRelics);
    }
}
