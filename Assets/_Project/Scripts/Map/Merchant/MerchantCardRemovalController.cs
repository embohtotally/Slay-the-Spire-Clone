using Gameseed26;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MerchantCardRemovalController : MonoBehaviour
{
    [Header("Pricing")]
    [SerializeField, Min(0)] private int baseRemovalPrice = 75;
    [SerializeField, Min(0)] private int priceIncreasePerRemoval = 0;

    [Header("Rules")]
    [SerializeField] private bool allowMultipleRemovals;
    [SerializeField, Min(0)] private int minimumDeckSizeAfterRemoval = 1;
    [SerializeField] private bool clearSelectionAfterRemoval = true;
    [SerializeField] private bool logFailures = true;

    [Header("Selection")]
    [SerializeField] private CardData selectedCard;

    [Header("Optional UI")]
    [SerializeField] private Button removeButton;
    [SerializeField] private TMP_Text selectedCardText;
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private TMP_Text unavailableReasonText;

    [Header("Labels")]
    [SerializeField] private string noSelectedCardLabel = "Select a card to remove.";
    [SerializeField] private string selectedCardFormat = "Selected: {0}";
    [SerializeField] private string removeActionFormat = "Remove - {0}g";
    [SerializeField] private string removedLabel = "Removed {0}";
    [SerializeField] private string notEnoughGoldFormat = "Not enough gold ({0}/{1}g).";
    [SerializeField] private string alreadyRemovedLabel = "Card removal already used.";
    [SerializeField] private string deckTooSmallLabel = "Deck is too small to remove a card.";

    [Header("Events")]
    public CardDataUnityEvent OnCardSelected;
    public CardDataUnityEvent OnCardRemoved;
    public UnityEvent OnRemovalCompleted;
    public UnityEvent OnRemovalUnavailable;
    public UnityEvent OnStateChanged;

    [Header("Debug")]
    [ReadOnly][SerializeField] private int removalCount;
    [ReadOnly][SerializeField] private string lastUnavailableReason;

    private RunManager subscribedRunManager;
    private RunDeckManager subscribedDeckManager;

    public CardData SelectedCard => selectedCard;
    public int CurrentRemovalPrice => Mathf.Max(0, baseRemovalPrice + removalCount * priceIncreasePerRemoval);
    public bool HasRemovedThisVisit => removalCount > 0;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeToRunStateChanges();
        SubscribeToDeckChanges();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeFromRunStateChanges();
        UnsubscribeFromDeckChanges();

        if (removeButton != null)
        {
            removeButton.onClick.RemoveListener(TryRemoveSelectedCard);
        }
    }

    private void OnValidate()
    {
        baseRemovalPrice = Mathf.Max(0, baseRemovalPrice);
        priceIncreasePerRemoval = Mathf.Max(0, priceIncreasePerRemoval);
        minimumDeckSizeAfterRemoval = Mathf.Max(0, minimumDeckSizeAfterRemoval);
        CacheReferences();
    }

    public void SetSelectedCard(CardData cardData)
    {
        selectedCard = cardData;
        OnCardSelected?.Invoke(selectedCard);
        RefreshUI();
    }

    public void SelectCard(CardData cardData)
    {
        SetSelectedCard(cardData);
    }

    public void ClearSelection()
    {
        selectedCard = null;
        RefreshUI();
    }

    public void SetBaseRemovalPrice(int price)
    {
        baseRemovalPrice = Mathf.Max(0, price);
        RefreshUI();
    }

    public void SetAllowMultipleRemovals(bool allowed)
    {
        allowMultipleRemovals = allowed;
        RefreshUI();
    }

    public void ResetRemovalCount()
    {
        removalCount = 0;
        RefreshUI();
    }

    [Button("Remove Selected Card", EButtonEnableMode.Playmode)]
    public void TryRemoveSelectedCard()
    {
        TryRemoveCard(selectedCard);
    }

    public void TryRemoveCard(CardData cardData)
    {
        selectedCard = cardData;

        if (!CanRemoveCard(cardData, out string unavailableReason))
        {
            lastUnavailableReason = unavailableReason;
            LogFailure(unavailableReason);
            OnRemovalUnavailable?.Invoke();
            RefreshUI();
            return;
        }

        RunManager runManager = RunManager.Instance;
        RunDeckManager deckManager = RunDeckManager.Instance;
        int price = CurrentRemovalPrice;

        if (price > 0 && !runManager.SpendGold(price))
        {
            lastUnavailableReason = FormatNotEnoughGold(runManager.Gold, price);
            LogFailure(lastUnavailableReason);
            OnRemovalUnavailable?.Invoke();
            RefreshUI();
            return;
        }

        if (!deckManager.RemoveFirst(cardData))
        {
            if (price > 0)
            {
                runManager.AddGold(price);
            }

            lastUnavailableReason = cardData != null
                ? $"Could not remove '{cardData.Title}' because it is not in the current run deck."
                : noSelectedCardLabel;
            LogFailure(lastUnavailableReason);
            OnRemovalUnavailable?.Invoke();
            RefreshUI();
            return;
        }

        removalCount++;
        lastUnavailableReason = string.Empty;
        Gameseed26.Logger.Log(this, string.Format(removedLabel, cardData.Title));
        OnCardRemoved?.Invoke(cardData);
        OnRemovalCompleted?.Invoke();

        if (clearSelectionAfterRemoval)
        {
            selectedCard = null;
        }

        RefreshUI();
    }

    public bool CanRemoveSelectedCard()
    {
        return CanRemoveCard(selectedCard, out _);
    }

    public bool CanRemoveCard(CardData cardData)
    {
        return CanRemoveCard(cardData, out _);
    }

    public bool CanRemoveCard(CardData cardData, out string unavailableReason)
    {
        unavailableReason = string.Empty;

        if (cardData == null)
        {
            unavailableReason = noSelectedCardLabel;
            return false;
        }

        if (!allowMultipleRemovals && removalCount > 0)
        {
            unavailableReason = alreadyRemovedLabel;
            return false;
        }

        RunDeckManager deckManager = RunDeckManager.Instance;
        if (deckManager == null)
        {
            unavailableReason = "MerchantCardRemovalController could not find a RunDeckManager.";
            return false;
        }

        if (deckManager.CurrentDeck.Count - 1 < minimumDeckSizeAfterRemoval)
        {
            unavailableReason = deckTooSmallLabel;
            return false;
        }

        if (!deckManager.CanRemove(cardData))
        {
            unavailableReason = $"'{cardData.Title}' is not in the current run deck.";
            return false;
        }

        int price = CurrentRemovalPrice;
        RunManager runManager = RunManager.Instance;
        if (price > 0 && runManager == null)
        {
            unavailableReason = "MerchantCardRemovalController could not find a RunManager.";
            return false;
        }

        if (price > 0 && runManager.Gold < price)
        {
            unavailableReason = FormatNotEnoughGold(runManager.Gold, price);
            return false;
        }

        return true;
    }

    [Button("Refresh Removal UI", EButtonEnableMode.Playmode)]
    public void RefreshUI()
    {
        SubscribeToRunStateChanges();
        SubscribeToDeckChanges();

        bool canRemove = CanRemoveCard(selectedCard, out string unavailableReason);
        lastUnavailableReason = canRemove ? string.Empty : unavailableReason;

        if (selectedCardText != null)
        {
            selectedCardText.text = selectedCard != null
                ? string.Format(selectedCardFormat, selectedCard.Title)
                : noSelectedCardLabel;
        }

        if (actionText != null)
        {
            actionText.text = canRemove
                ? string.Format(removeActionFormat, CurrentRemovalPrice)
                : unavailableReason;
        }

        if (unavailableReasonText != null)
        {
            unavailableReasonText.text = canRemove ? string.Empty : unavailableReason;
        }

        if (removeButton != null)
        {
            removeButton.interactable = canRemove;
        }

        OnStateChanged?.Invoke();
    }

    private void CacheReferences()
    {
        if (removeButton == null) removeButton = GetComponentInChildren<Button>(true);

        if (removeButton != null)
        {
            removeButton.onClick.RemoveListener(TryRemoveSelectedCard);
            removeButton.onClick.AddListener(TryRemoveSelectedCard);
        }
    }

    private void SubscribeToRunStateChanges()
    {
        if (RunManager.Instance == null || subscribedRunManager == RunManager.Instance) return;

        UnsubscribeFromRunStateChanges();
        subscribedRunManager = RunManager.Instance;
        subscribedRunManager.RunStateChanged += RefreshUI;
    }

    private void UnsubscribeFromRunStateChanges()
    {
        if (subscribedRunManager == null) return;

        subscribedRunManager.RunStateChanged -= RefreshUI;
        subscribedRunManager = null;
    }

    private void SubscribeToDeckChanges()
    {
        if (RunDeckManager.Instance == null || subscribedDeckManager == RunDeckManager.Instance) return;

        UnsubscribeFromDeckChanges();
        subscribedDeckManager = RunDeckManager.Instance;
        subscribedDeckManager.DeckChanged += HandleDeckChanged;
    }

    private void UnsubscribeFromDeckChanges()
    {
        if (subscribedDeckManager == null) return;

        subscribedDeckManager.DeckChanged -= HandleDeckChanged;
        subscribedDeckManager = null;
    }

    private void HandleDeckChanged(System.Collections.Generic.IReadOnlyList<CardData> _)
    {
        RefreshUI();
    }

    private string FormatNotEnoughGold(int currentGold, int price)
    {
        return string.Format(notEnoughGoldFormat, currentGold, price);
    }

    private void LogFailure(string message)
    {
        if (!logFailures) return;
        Gameseed26.Logger.Log(this, message);
    }
}
