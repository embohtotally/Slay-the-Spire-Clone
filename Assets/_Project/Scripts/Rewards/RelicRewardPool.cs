using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

[Flags]
public enum RelicRarityMask
{
    None = 0,
    Common = 1 << 0,
    Uncommon = 1 << 1,
    Rare = 1 << 2,
    Boss = 1 << 3,
    Special = 1 << 4,
    All = Common | Uncommon | Rare | Boss | Special
}

[Serializable]
public class RelicRewardEntry
{
    public bool Enabled = true;
    public RelicData Relic;
    [Min(1)] public int Weight = 1;
}

[CreateAssetMenu(menuName = "Data/Relic Reward Pool")]
public class RelicRewardPool : ScriptableObject
{
    [Header("Rules")]
    [SerializeField] private bool excludeOwnedUniqueRelics = true;

    [Header("Rewards")]
    [ReorderableList]
    [SerializeField] private List<RelicRewardEntry> relics = new();

    public List<RelicData> BuildCandidateRewards(RunRelicManager relicManager, RelicRarityMask allowedRarities = RelicRarityMask.All)
    {
        List<RelicData> candidates = new();

        foreach (RelicRewardEntry entry in relics)
        {
            if (!IsEligible(entry, relicManager, allowedRarities)) continue;
            candidates.Add(entry.Relic);
        }

        return candidates;
    }

    public bool TryGetRandomRelic(RunRelicManager relicManager, out RelicData relic, RelicRarityMask allowedRarities = RelicRarityMask.All)
    {
        relic = null;

        int totalWeight = 0;
        foreach (RelicRewardEntry entry in relics)
        {
            if (!IsEligible(entry, relicManager, allowedRarities)) continue;
            totalWeight += Mathf.Max(1, entry.Weight);
        }

        if (totalWeight <= 0) return false;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        foreach (RelicRewardEntry entry in relics)
        {
            if (!IsEligible(entry, relicManager, allowedRarities)) continue;

            roll -= Mathf.Max(1, entry.Weight);
            if (roll < 0)
            {
                relic = entry.Relic;
                return relic != null;
            }
        }

        return false;
    }

    private bool IsEligible(RelicRewardEntry entry, RunRelicManager relicManager, RelicRarityMask allowedRarities)
    {
        if (entry == null || !entry.Enabled || entry.Relic == null) return false;
        if (!IsRarityAllowed(entry.Relic.Rarity, allowedRarities)) return false;

        if (excludeOwnedUniqueRelics && entry.Relic.Unique && relicManager != null && relicManager.Contains(entry.Relic))
        {
            return false;
        }

        return true;
    }

    private static bool IsRarityAllowed(RelicRarity rarity, RelicRarityMask allowedRarities)
    {
        RelicRarityMask mask = rarity switch
        {
            RelicRarity.Common => RelicRarityMask.Common,
            RelicRarity.Uncommon => RelicRarityMask.Uncommon,
            RelicRarity.Rare => RelicRarityMask.Rare,
            RelicRarity.Boss => RelicRarityMask.Boss,
            RelicRarity.Special => RelicRarityMask.Special,
            _ => RelicRarityMask.None
        };

        return (allowedRarities & mask) != 0;
    }
}
