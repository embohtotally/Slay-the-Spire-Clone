using System.Collections.Generic;
using Gameseed26;
using UnityEngine;

public class MerchantController : MonoBehaviour
{
    [Header("Offers")]
    [SerializeField] private MerchantCardPool merchantPool;
    [SerializeField] private int offerCount = 5;
    [SerializeField] private bool includeNewCards = true;
    [SerializeField] private bool includeUpgrades = true;
    [SerializeField] private bool allowDuplicateOffers;

    [Header("UI")]
    [Tooltip("Parent that contains manually positioned MerchantOfferSlotView children. Their RectTransforms decide the layout.")]
    [SerializeField] private RectTransform slotsParent;
    [SerializeField] private MerchantOfferSlotView slotPrefab;
    [SerializeField] private bool autoFindSlotsInChildren = true;

    [Header("Navigation")]
    [SerializeField] private string mapSceneName = "Map";

    private readonly List<MerchantOffer> activeOffers = new();
    private readonly List<MerchantOfferSlotView> slots = new();
    private RunManager subscribedRunManager;

    private void Awake()
    {
        EnsureRunDeckManagerExists();
        CollectSlots();
    }

    private void OnEnable()
    {
        SubscribeToRunStateChanges();
    }

    private void OnDisable()
    {
        UnsubscribeFromRunStateChanges();
    }

    private void Start()
    {
        SubscribeToRunStateChanges();
        GenerateOffers();
    }

    public void GenerateOffers()
    {
        activeOffers.Clear();
        CollectSlots();

        if (merchantPool == null)
        {
            Gameseed26.Logger.LogWarning("MerchantController needs a MerchantCardPool.");
            ClearSlots();
            return;
        }

        List<MerchantOffer> candidates = merchantPool.BuildCandidateOffers(RunDeckManager.Instance, includeNewCards, includeUpgrades);
        if (candidates.Count == 0)
        {
            Gameseed26.Logger.LogWarning("Merchant has no valid offers. Add cards/upgrades to MerchantCardPool or make sure RunDeckManager has a deck.");
            ClearSlots();
            return;
        }

        int targetCount = Mathf.Max(0, offerCount);
        EnsureSlotCount(targetCount);

        for (int i = 0; i < targetCount; i++)
        {
            if (candidates.Count == 0) break;

            int randomIndex = Random.Range(0, candidates.Count);
            MerchantOffer offer = candidates[randomIndex];
            activeOffers.Add(offer);

            if (!allowDuplicateOffers)
            {
                candidates.RemoveAt(randomIndex);
            }
        }

        RefreshSlotAssignments();
    }

    public void BuyOffer(MerchantOffer offer, MerchantOfferSlotView slotView)
    {
        if (offer == null || offer.IsSold) return;
        EnsureRunDeckManagerExists();

        if (!CanAfford(offer))
        {
            int currentGold = RunManager.Instance != null ? RunManager.Instance.Gold : 0;
            Gameseed26.Logger.Log($"Not enough gold for merchant offer '{offer.GetTitle()}'. Need {offer.Price}, have {currentGold}.");
            slotView?.Refresh();
            return;
        }

        bool success = offer.Type switch
        {
            MerchantOfferType.NewCard => BuyNewCard(offer),
            MerchantOfferType.UpgradeCard => BuyUpgrade(offer),
            _ => false
        };

        if (!success) return;

        if (!SpendOfferPrice(offer))
        {
            Gameseed26.Logger.LogWarning($"Merchant offer '{offer.GetTitle()}' was applied but gold could not be spent. Check RunManager setup.");
            return;
        }

        offer.MarkSold();
        RefreshSlots();
    }

    public bool CanAfford(MerchantOffer offer)
    {
        if (offer == null) return false;
        if (offer.Price <= 0) return true;
        return RunManager.Instance != null && RunManager.Instance.Gold >= offer.Price;
    }

    public void ReturnToMap()
    {
        if (string.IsNullOrWhiteSpace(mapSceneName))
        {
            Gameseed26.Logger.LogWarning("MerchantController has no map scene name.");
            return;
        }

        SceneLoader.LoadScene(mapSceneName);
    }

    private bool BuyNewCard(MerchantOffer offer)
    {
        if (offer.Card == null) return false;

        RunDeckManager.Instance.AddCard(offer.Card);
        Gameseed26.Logger.Log($"Bought card: {offer.Card.Title} for {offer.Price} gold.");
        return true;
    }

    private bool BuyUpgrade(MerchantOffer offer)
    {
        CardUpgradeRecipe recipe = offer.UpgradeRecipe;
        if (recipe == null || recipe.BaseCard == null || recipe.UpgradedCard == null) return false;

        bool replaced = RunDeckManager.Instance.ReplaceFirst(recipe.BaseCard, recipe.UpgradedCard);
        if (!replaced)
        {
            Gameseed26.Logger.LogWarning($"Could not upgrade {recipe.BaseCard.Title}; the base card is not in the current run deck.");
            return false;
        }

        Gameseed26.Logger.Log($"Upgraded card: {recipe.BaseCard.Title} -> {recipe.UpgradedCard.Title} for {offer.Price} gold.");
        return true;
    }

    private void CollectSlots()
    {
        slots.Clear();

        if (autoFindSlotsInChildren)
        {
            Transform root = slotsParent != null ? slotsParent : transform;
            slots.AddRange(root.GetComponentsInChildren<MerchantOfferSlotView>(true));
        }

        slots.RemoveAll(slot => slot == null);
    }

    private void EnsureSlotCount(int targetCount)
    {
        if (targetCount <= slots.Count || slotPrefab == null) return;

        Transform parent = slotsParent != null ? slotsParent : transform;
        while (slots.Count < targetCount)
        {
            MerchantOfferSlotView slot = Instantiate(slotPrefab, parent);
            slots.Add(slot);
        }
    }

    private void RefreshSlotAssignments()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (i < activeOffers.Count)
            {
                slots[i].Setup(this, activeOffers[i]);
            }
            else
            {
                slots[i].Clear();
            }
        }
    }

    private void ClearSlots()
    {
        foreach (MerchantOfferSlotView slot in slots)
        {
            if (slot != null)
            {
                slot.Clear();
            }
        }
    }

    private bool SpendOfferPrice(MerchantOffer offer)
    {
        if (offer == null || offer.Price <= 0) return true;
        return RunManager.Instance != null && RunManager.Instance.SpendGold(offer.Price);
    }

    private void RefreshSlots()
    {
        foreach (MerchantOfferSlotView slot in slots)
        {
            if (slot != null)
            {
                slot.Refresh();
            }
        }
    }

    private void SubscribeToRunStateChanges()
    {
        if (RunManager.Instance == null || subscribedRunManager == RunManager.Instance) return;

        UnsubscribeFromRunStateChanges();
        subscribedRunManager = RunManager.Instance;
        subscribedRunManager.RunStateChanged += RefreshSlots;
    }

    private void UnsubscribeFromRunStateChanges()
    {
        if (subscribedRunManager == null) return;

        subscribedRunManager.RunStateChanged -= RefreshSlots;
        subscribedRunManager = null;
    }

    private static void EnsureRunDeckManagerExists()
    {
        if (RunDeckManager.Instance != null) return;

        GameObject deckManagerObject = new("Run Deck Manager");
        deckManagerObject.AddComponent<RunDeckManager>();
    }
}
