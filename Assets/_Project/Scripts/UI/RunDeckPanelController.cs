using System.Collections.Generic;
using System.Linq;
using Gameseed26;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
public enum RunDeckDisplayMode
{
    ShowEveryCopy,
    GroupDuplicates
}

public class RunDeckPanelController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root panel for the deck UI. Use your Animation Sequencer Controller on this object if you want custom open/close animation.")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Parent that receives generated RunDeckCardView instances. Usually a GridLayoutGroup/VerticalLayoutGroup content object.")]
    [SerializeField] private Transform cardContainer;
    [SerializeField] private RunDeckCardView cardViewPrefab;
    [SerializeField] private TMP_Text deckCountText;
    [SerializeField] private TMP_Text emptyDeckText;

    [Header("Behavior")]
    [SerializeField] private bool startClosed = true;
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool closeWithEscape = true;
    [SerializeField] private RunDeckDisplayMode displayMode = RunDeckDisplayMode.GroupDuplicates;
    [Tooltip("Useful when Animation Sequencer handles close animation. If false, ClosePanel only fires OnCloseRequested and leaves hiding to your animation event.")]
    [SerializeField] private bool hideRootImmediatelyOnClose = true;
    [Tooltip("If no RunDeckManager exists yet, show empty UI instead of creating a persistent manager.")]
    [SerializeField] private bool createRunDeckManagerIfMissing;

    [Header("Labels")]
    [SerializeField] private string deckCountFormat = "Deck: {0} Cards";
    [SerializeField] private string emptyDeckMessage = "No cards in this run yet.";

    [Header("Animation Hooks")]
    [Tooltip("Hook Animation Sequencer Controller open/play method here if desired.")]
    public UnityEvent OnOpenRequested;
    [Tooltip("Hook Animation Sequencer Controller close/play method here if desired.")]
    public UnityEvent OnCloseRequested;
    public UnityEvent OnDeckRefreshed;

    private readonly List<RunDeckCardView> spawnedViews = new();
    private bool isOpen;

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        if (cardContainer == null) cardContainer = transform;
        EnsureRunDeckManagerIfWanted();
    }

    private void Start()
    {
        if (startClosed)
        {
            SetPanelVisible(false);
        }
        else
        {
            OpenPanel();
        }
    }

    private void OnEnable()
    {
        SubscribeToDeckManager();

        if (refreshOnEnable)
        {
            RefreshDeckView();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromDeckManager();
    }

    [Button("Open Deck Panel", EButtonEnableMode.Playmode)]
    public void OpenPanel()
    {
        isOpen = true;
        SetPanelVisible(true);
        RefreshDeckView();
        OnOpenRequested?.Invoke();
    }

    [Button("Close Deck Panel", EButtonEnableMode.Playmode)]
    public void ClosePanel()
    {
        isOpen = false;
        OnCloseRequested?.Invoke();

        if (hideRootImmediatelyOnClose)
        {
            SetPanelVisible(false);
        }
    }

    public void TogglePanel()
    {
        if (isOpen)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    [Button("Refresh Deck View", EButtonEnableMode.Playmode)]
    public void RefreshDeckView()
    {
        IReadOnlyList<CardData> deck = RunDeckManager.Instance != null
            ? RunDeckManager.Instance.CurrentDeck
            : new List<CardData>();

        List<DeckDisplayEntry> entries = BuildDisplayEntries(deck);
        EnsureViewCount(entries.Count);

        for (int i = 0; i < spawnedViews.Count; i++)
        {
            RunDeckCardView view = spawnedViews[i];
            if (view == null) continue;

            if (i < entries.Count)
            {
                view.gameObject.SetActive(true);
                view.Setup(entries[i].CardData, entries[i].CopyCount, i);
            }
            else
            {
                view.gameObject.SetActive(false);
            }
        }

        if (deckCountText != null)
        {
            deckCountText.text = string.Format(deckCountFormat, deck.Count);
        }

        if (emptyDeckText != null)
        {
            bool isEmpty = deck.Count == 0;
            emptyDeckText.gameObject.SetActive(isEmpty);
            emptyDeckText.text = emptyDeckMessage;
        }

        OnDeckRefreshed?.Invoke();
    }

    // Call this from the last frame/event of your close Animation Sequencer if hideRootImmediatelyOnClose is false.
    public void HideRootAfterCloseAnimation()
    {
        if (!isOpen)
        {
            SetPanelVisible(false);
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null && panelRoot.activeSelf != visible)
        {
            panelRoot.SetActive(visible);
        }
    }

    private List<DeckDisplayEntry> BuildDisplayEntries(IReadOnlyList<CardData> deck)
    {
        List<DeckDisplayEntry> entries = new();
        if (deck == null) return entries;

        if (displayMode == RunDeckDisplayMode.ShowEveryCopy)
        {
            foreach (CardData card in deck)
            {
                if (card != null)
                {
                    entries.Add(new DeckDisplayEntry(card, 1));
                }
            }

            return entries;
        }

        Dictionary<CardData, DeckDisplayEntry> groupedEntries = new();
        foreach (CardData card in deck)
        {
            if (card == null) continue;

            if (groupedEntries.TryGetValue(card, out DeckDisplayEntry entry))
            {
                entry.CopyCount++;
            }
            else
            {
                entry = new DeckDisplayEntry(card, 1);
                groupedEntries.Add(card, entry);
                entries.Add(entry);
            }
        }

        return entries
            .OrderBy(entry => entry.CardData.Mana)
            .ThenBy(entry => entry.CardData.Type)
            .ThenBy(entry => entry.CardData.Title)
            .ToList();
    }

    private void EnsureViewCount(int targetCount)
    {
        if (cardViewPrefab == null)
        {
            if (targetCount > 0)
            {
                Gameseed26.Logger.LogWarning("RunDeckPanelController needs a RunDeckCardView prefab to show the run deck.");
            }
            return;
        }

        Transform parent = cardContainer != null ? cardContainer : transform;
        while (spawnedViews.Count < targetCount)
        {
            RunDeckCardView view = Instantiate(cardViewPrefab, parent);
            spawnedViews.Add(view);
        }
    }

    private void SubscribeToDeckManager()
    {
        if (RunDeckManager.Instance != null)
        {
            RunDeckManager.Instance.DeckChanged -= HandleDeckChanged;
            RunDeckManager.Instance.DeckChanged += HandleDeckChanged;
        }
    }

    private void UnsubscribeFromDeckManager()
    {
        if (RunDeckManager.Instance != null)
        {
            RunDeckManager.Instance.DeckChanged -= HandleDeckChanged;
        }
    }

    private void HandleDeckChanged(IReadOnlyList<CardData> _)
    {
        RefreshDeckView();
    }

    private void EnsureRunDeckManagerIfWanted()
    {
        if (!createRunDeckManagerIfMissing || RunDeckManager.Instance != null) return;

        GameObject deckManagerObject = new("Run Deck Manager");
        deckManagerObject.AddComponent<RunDeckManager>();
    }

    private sealed class DeckDisplayEntry
    {
        public CardData CardData { get; }
        public int CopyCount { get; set; }

        public DeckDisplayEntry(CardData cardData, int copyCount)
        {
            CardData = cardData;
            CopyCount = copyCount;
        }
    }
}
