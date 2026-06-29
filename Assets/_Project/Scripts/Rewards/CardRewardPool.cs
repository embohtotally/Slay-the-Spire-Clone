using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardRewardOptionType
{
    NewCard,
    UpgradeCard
}

public class CardRewardOption
{
    public CardRewardOptionType Type { get; }
    public CardData Card { get; }
    public CardUpgradeRecipe UpgradeRecipe { get; }

    public CardData PreviewCard => Type == CardRewardOptionType.UpgradeCard ? UpgradeRecipe?.UpgradedCard : Card;

    public CardRewardOption(CardData card)
    {
        Type = CardRewardOptionType.NewCard;
        Card = card;
    }

    public CardRewardOption(CardUpgradeRecipe upgradeRecipe)
    {
        Type = CardRewardOptionType.UpgradeCard;
        UpgradeRecipe = upgradeRecipe;
    }

    public string GetTitle()
    {
        if (Type == CardRewardOptionType.UpgradeCard)
        {
            if (!string.IsNullOrWhiteSpace(UpgradeRecipe?.DisplayNameOverride))
            {
                return UpgradeRecipe.DisplayNameOverride;
            }

            string from = UpgradeRecipe?.BaseCard != null ? UpgradeRecipe.BaseCard.Title : "Card";
            string to = UpgradeRecipe?.UpgradedCard != null ? UpgradeRecipe.UpgradedCard.Title : "Upgrade";
            return $"Upgrade: {from} -> {to}";
        }

        return PreviewCard != null ? PreviewCard.Title : "Empty";
    }

    public string GetDescription()
    {
        CardData preview = PreviewCard;
        if (preview == null) return "No card assigned.";

        string prefix = Type == CardRewardOptionType.UpgradeCard ? "Upgrade one card in your deck.\n" : "Add this card to your deck.\n";
        return prefix + preview.Description;
    }
}

[Serializable]
public class CardRewardRequest
{
    [Min(1)] public int OptionCount = 3;
    public bool IncludeNewCards = true;
    public bool IncludeUpgrades;
    public bool OnlyOfferNewCardsNotInDeck = true;
    public bool AllowDuplicateOptions;
}

[CreateAssetMenu(menuName = "Data/Card Reward Pool")]
public class CardRewardPool : ScriptableObject
{
    [Header("New Card Rewards")]
    [SerializeField] private List<CardData> newCardRewards = new();

    [Header("Upgrade Rewards")]
    [SerializeField] private List<CardUpgradeRecipe> upgradeRewards = new();

    public List<CardRewardOption> BuildCandidateRewards(RunDeckManager deckManager, CardRewardRequest request)
    {
        CardRewardRequest safeRequest = request ?? new CardRewardRequest();
        List<CardRewardOption> candidates = new();

        if (safeRequest.IncludeNewCards)
        {
            foreach (CardData card in newCardRewards)
            {
                if (card == null) continue;
                if (safeRequest.OnlyOfferNewCardsNotInDeck && deckManager != null && deckManager.Contains(card)) continue;

                candidates.Add(new CardRewardOption(card));
            }
        }

        if (safeRequest.IncludeUpgrades && deckManager != null)
        {
            foreach (CardUpgradeRecipe recipe in upgradeRewards)
            {
                if (recipe == null || recipe.BaseCard == null || recipe.UpgradedCard == null) continue;
                if (!deckManager.Contains(recipe.BaseCard)) continue;

                candidates.Add(new CardRewardOption(recipe));
            }
        }

        return candidates;
    }
}
