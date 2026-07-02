using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

[Flags]
public enum PotionRarityMask
{
    None = 0,
    Common = 1 << 0,
    Uncommon = 1 << 1,
    Rare = 1 << 2,
    Special = 1 << 3,
    All = Common | Uncommon | Rare | Special
}

[Serializable]
public class PotionRewardEntry
{
    public bool Enabled = true;
    public PotionData Potion;
    [Min(1)] public int Weight = 1;
}

[CreateAssetMenu(menuName = "Data/Potion Reward Pool")]
public class PotionRewardPool : ScriptableObject
{
    [Header("Rules")]
    [SerializeField] private bool excludeOwnedUniquePotions = true;
    [SerializeField] private bool excludeWhenPotionSlotsFull = true;

    [Header("Rewards")]
    [ReorderableList]
    [SerializeField] private List<PotionRewardEntry> potions = new();

    public List<PotionData> BuildCandidateRewards(RunPotionManager potionManager, PotionRarityMask allowedRarities = PotionRarityMask.All)
    {
        List<PotionData> candidates = new();

        foreach (PotionRewardEntry entry in potions)
        {
            if (!IsEligible(entry, potionManager, allowedRarities)) continue;
            candidates.Add(entry.Potion);
        }

        return candidates;
    }

    public bool TryGetRandomPotion(RunPotionManager potionManager, out PotionData potion, PotionRarityMask allowedRarities = PotionRarityMask.All)
    {
        potion = null;

        int totalWeight = 0;
        foreach (PotionRewardEntry entry in potions)
        {
            if (!IsEligible(entry, potionManager, allowedRarities)) continue;
            totalWeight += Mathf.Max(1, entry.Weight);
        }

        if (totalWeight <= 0) return false;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        foreach (PotionRewardEntry entry in potions)
        {
            if (!IsEligible(entry, potionManager, allowedRarities)) continue;

            roll -= Mathf.Max(1, entry.Weight);
            if (roll < 0)
            {
                potion = entry.Potion;
                return potion != null;
            }
        }

        return false;
    }

    private bool IsEligible(PotionRewardEntry entry, RunPotionManager potionManager, PotionRarityMask allowedRarities)
    {
        if (entry == null || !entry.Enabled || entry.Potion == null) return false;
        if (!IsRarityAllowed(entry.Potion.Rarity, allowedRarities)) return false;

        if (excludeWhenPotionSlotsFull && potionManager != null && potionManager.IsFull)
        {
            return false;
        }

        if (excludeOwnedUniquePotions && entry.Potion.Unique && potionManager != null && potionManager.Contains(entry.Potion))
        {
            return false;
        }

        return true;
    }

    private static bool IsRarityAllowed(PotionRarity rarity, PotionRarityMask allowedRarities)
    {
        PotionRarityMask mask = rarity switch
        {
            PotionRarity.Common => PotionRarityMask.Common,
            PotionRarity.Uncommon => PotionRarityMask.Uncommon,
            PotionRarity.Rare => PotionRarityMask.Rare,
            PotionRarity.Special => PotionRarityMask.Special,
            _ => PotionRarityMask.None
        };

        return (allowedRarities & mask) != 0;
    }
}
