using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private void Awake()
    {
        EnsureRunDeckManagerExists();
        CollectSlots();
    }

    private void Start()
    {
        GenerateOffers();
    }

    public void GenerateOffers()
    {
        activeOffers.Clear();
        CollectSlots();

        if (merchantPool == null)
        {
            Debug.LogWarning("MerchantController needs a MerchantCardPool.");
            ClearSlots();
            return;
        }

        List<MerchantOffer> candidates = merchantPool.BuildCandidateOffers(RunDeckManager.Instance, includeNewCards, includeUpgrades);
        if (candidates.Count == 0)
        {
            Debug.LogWarning("Merchant has no valid offers. Add cards/upgrades to MerchantCardPool or make sure RunDeckManager has a deck.");
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

    public void BuyOffer(MerchantOffer offer, MerchantOfferSlotView slotView)
    {
        if (offer == null || offer.IsSold) return;
        EnsureRunDeckManagerExists();

        bool success = offer.Type switch
        {
            MerchantOfferType.NewCard => BuyNewCard(offer),
            MerchantOfferType.UpgradeCard => BuyUpgrade(offer),
            _ => false
        };

        if (!success) return;

        offer.MarkSold();
        slotView?.Refresh();
    }

    public void ReturnToMap()
    {
        if (string.IsNullOrWhiteSpace(mapSceneName))
        {
            Debug.LogWarning("MerchantController has no map scene name.");
            return;
        }

        SceneManager.LoadScene(mapSceneName);
    }

    private bool BuyNewCard(MerchantOffer offer)
    {
        if (offer.Card == null) return false;

        RunDeckManager.Instance.AddCard(offer.Card);
        Debug.Log($"Bought card: {offer.Card.Title}");
        return true;
    }

    private bool BuyUpgrade(MerchantOffer offer)
    {
        CardUpgradeRecipe recipe = offer.UpgradeRecipe;
        if (recipe == null || recipe.BaseCard == null || recipe.UpgradedCard == null) return false;

        bool replaced = RunDeckManager.Instance.ReplaceFirst(recipe.BaseCard, recipe.UpgradedCard);
        if (!replaced)
        {
            Debug.LogWarning($"Could not upgrade {recipe.BaseCard.Title}; the base card is not in the current run deck.");
            return false;
        }

        Debug.Log($"Upgraded card: {recipe.BaseCard.Title} -> {recipe.UpgradedCard.Title}");
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

    private static void EnsureRunDeckManagerExists()
    {
        if (RunDeckManager.Instance != null) return;

        GameObject deckManagerObject = new("Run Deck Manager");
        deckManagerObject.AddComponent<RunDeckManager>();
    }
}
