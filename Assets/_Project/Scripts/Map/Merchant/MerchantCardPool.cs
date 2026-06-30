using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

public enum MerchantOfferType
{
    NewCard,
    UpgradeCard
}

[Serializable]
public class CardUpgradeRecipe
{
    [Tooltip("Card that must exist in the player's current run deck.")]
    public CardData BaseCard;

    [Tooltip("Card that replaces one copy of Base Card when the upgrade is bought.")]
    public CardData UpgradedCard;

    [Tooltip("Optional text shown in the merchant UI. Empty = generated automatically.")]
    public string DisplayNameOverride;
}

[Serializable]
public class MerchantCardPriceOverride
{
    [Tooltip("Card that should use a custom merchant price.")]
    public CardData Card;

    [Min(0)]
    [Tooltip("Gold cost for this specific card.")]
    public int Price = 75;
}

public class MerchantOffer
{
    public MerchantOfferType Type { get; }
    public CardData Card { get; }
    public CardUpgradeRecipe UpgradeRecipe { get; }
    public int Price { get; }
    public bool IsSold { get; private set; }

    public CardData PreviewCard => Type == MerchantOfferType.UpgradeCard ? UpgradeRecipe?.UpgradedCard : Card;

    public MerchantOffer(CardData card, int price)
    {
        Type = MerchantOfferType.NewCard;
        Card = card;
        Price = Mathf.Max(0, price);
    }

    public MerchantOffer(CardUpgradeRecipe upgradeRecipe, int price)
    {
        Type = MerchantOfferType.UpgradeCard;
        UpgradeRecipe = upgradeRecipe;
        Price = Mathf.Max(0, price);
    }

    public string GetTitle()
    {
        if (Type == MerchantOfferType.UpgradeCard)
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

        string prefix = Type == MerchantOfferType.UpgradeCard ? "Upgrade existing card.\n" : "Add new card to deck.\n";
        return prefix + preview.Description;
    }

    public void MarkSold()
    {
        IsSold = true;
    }
}

[CreateAssetMenu(menuName = "Data/Merchant Card Pool")]
public class MerchantCardPool : ScriptableObject
{
    [Header("Pricing")]
    [Min(0)]
    [SerializeField] private int defaultNewCardPrice = 75;

    [Min(0)]
    [SerializeField] private int defaultUpgradePrice = 100;

    [Tooltip("Optional per-card price overrides. Cards not listed here use Default New Card Price.")]
    [ReorderableList]
    [SerializeField] private List<MerchantCardPriceOverride> cardPriceOverrides = new();

    [Header("New Cards")]
    [ReorderableList]
    [SerializeField] private List<CardData> cardsForSale = new();

    [Header("Upgrades")]
    [ReorderableList]
    [SerializeField] private List<CardUpgradeRecipe> upgradeRecipes = new();

    public List<MerchantOffer> BuildCandidateOffers(RunDeckManager deckManager, bool includeNewCards, bool includeUpgrades)
    {
        List<MerchantOffer> candidates = new();

        if (includeNewCards)
        {
            foreach (CardData card in cardsForSale)
            {
                if (card != null)
                {
                    candidates.Add(new MerchantOffer(card, GetNewCardPrice(card)));
                }
            }
        }

        if (includeUpgrades && deckManager != null)
        {
            foreach (CardUpgradeRecipe recipe in upgradeRecipes)
            {
                if (recipe == null || recipe.BaseCard == null || recipe.UpgradedCard == null) continue;
                if (!deckManager.Contains(recipe.BaseCard)) continue;

                candidates.Add(new MerchantOffer(recipe, GetUpgradePrice()));
            }
        }

        return candidates;
    }

    private int GetNewCardPrice(CardData card)
    {
        foreach (MerchantCardPriceOverride priceOverride in cardPriceOverrides)
        {
            if (priceOverride != null && priceOverride.Card == card)
            {
                return Mathf.Max(0, priceOverride.Price);
            }
        }

        return Mathf.Max(0, defaultNewCardPrice);
    }

    private int GetUpgradePrice()
    {
        return Mathf.Max(0, defaultUpgradePrice);
    }
}
