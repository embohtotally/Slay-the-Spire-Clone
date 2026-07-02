using System.Collections.Generic;
using System.Linq;
using Gameseed26;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public enum RunRelicDisplayMode
{
    ShowEveryCopy,
    GroupDuplicates
}

[DisallowMultipleComponent]
public class RunRelicListUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root panel for the relic UI. Use your Animation Sequencer Controller on this object if you want custom open/close animation.")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Parent that receives generated RelicIconView instances. Usually a GridLayoutGroup/HorizontalLayoutGroup content object.")]
    [SerializeField] private Transform relicContainer;
    [SerializeField] private RelicIconView relicViewPrefab;
    [SerializeField] private TMP_Text relicCountText;
    [SerializeField] private TMP_Text emptyRelicsText;

    [Header("Behavior")]
    [SerializeField] private bool startClosed;
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool closeWithEscape;
    [SerializeField] private RunRelicDisplayMode displayMode = RunRelicDisplayMode.GroupDuplicates;
    [Tooltip("Useful when Animation Sequencer handles close animation. If false, ClosePanel only fires OnCloseRequested and leaves hiding to your animation event.")]
    [SerializeField] private bool hideRootImmediatelyOnClose = true;
    [Tooltip("If no RunRelicManager exists yet, create one through RunRelicManager.EnsureInstance(). Leave off for pure display-only scenes.")]
    [SerializeField] private bool createRunRelicManagerIfMissing;

    [Header("Labels")]
    [SerializeField] private string relicCountFormat = "Relics: {0}";
    [SerializeField] private string emptyRelicsMessage = "No relics yet.";

    [Header("Animation Hooks")]
    [Tooltip("Hook Animation Sequencer Controller open/play method here if desired.")]
    public UnityEvent OnOpenRequested;
    [Tooltip("Hook Animation Sequencer Controller close/play method here if desired.")]
    public UnityEvent OnCloseRequested;
    public UnityEvent OnRelicsRefreshed;

    private readonly List<RelicIconView> spawnedViews = new();
    private bool isOpen;

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        if (relicContainer == null) relicContainer = transform;
        EnsureRunRelicManagerIfWanted();
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
        SubscribeToRelicManager();

        if (refreshOnEnable)
        {
            RefreshRelicList();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromRelicManager();
    }

    private void Update()
    {
        if (!closeWithEscape || !isOpen || Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClosePanel();
        }
    }

    [Button("Open Relic Panel", EButtonEnableMode.Playmode)]
    public void OpenPanel()
    {
        isOpen = true;
        SetPanelVisible(true);
        RefreshRelicList();
        OnOpenRequested?.Invoke();
    }

    [Button("Close Relic Panel", EButtonEnableMode.Playmode)]
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

    [Button("Refresh Relic List", EButtonEnableMode.Playmode)]
    public void RefreshRelicList()
    {
        IReadOnlyList<RelicData> relics = RunRelicManager.Instance != null
            ? RunRelicManager.Instance.CurrentRelics
            : new List<RelicData>();

        List<RelicDisplayEntry> entries = BuildDisplayEntries(relics);
        EnsureViewCount(entries.Count);

        for (int i = 0; i < spawnedViews.Count; i++)
        {
            RelicIconView view = spawnedViews[i];
            if (view == null) continue;

            if (i < entries.Count)
            {
                view.gameObject.SetActive(true);
                view.Setup(entries[i].RelicData, entries[i].CopyCount, i);
            }
            else
            {
                view.gameObject.SetActive(false);
            }
        }

        if (relicCountText != null)
        {
            relicCountText.text = string.Format(relicCountFormat, relics.Count);
        }

        if (emptyRelicsText != null)
        {
            bool isEmpty = relics.Count == 0;
            emptyRelicsText.gameObject.SetActive(isEmpty);
            emptyRelicsText.text = emptyRelicsMessage;
        }

        OnRelicsRefreshed?.Invoke();
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

    private List<RelicDisplayEntry> BuildDisplayEntries(IReadOnlyList<RelicData> relics)
    {
        List<RelicDisplayEntry> entries = new();
        if (relics == null) return entries;

        if (displayMode == RunRelicDisplayMode.ShowEveryCopy)
        {
            foreach (RelicData relic in relics)
            {
                if (relic != null)
                {
                    entries.Add(new RelicDisplayEntry(relic, 1));
                }
            }
        }
        else
        {
            Dictionary<RelicData, RelicDisplayEntry> groupedEntries = new();
            foreach (RelicData relic in relics)
            {
                if (relic == null) continue;

                if (groupedEntries.TryGetValue(relic, out RelicDisplayEntry entry))
                {
                    entry.CopyCount++;
                }
                else
                {
                    entry = new RelicDisplayEntry(relic, 1);
                    groupedEntries.Add(relic, entry);
                    entries.Add(entry);
                }
            }
        }

        return entries
            .OrderBy(entry => entry.RelicData.Rarity)
            .ThenBy(entry => entry.RelicData.Title)
            .ToList();
    }

    private void EnsureViewCount(int targetCount)
    {
        if (relicViewPrefab == null)
        {
            if (targetCount > 0)
            {
                Gameseed26.Logger.LogWarning(this, "RunRelicListUI needs a RelicIconView prefab to show run relics.");
            }
            return;
        }

        Transform parent = relicContainer != null ? relicContainer : transform;
        while (spawnedViews.Count < targetCount)
        {
            RelicIconView view = Instantiate(relicViewPrefab, parent);
            spawnedViews.Add(view);
        }
    }

    private void SubscribeToRelicManager()
    {
        if (RunRelicManager.Instance != null)
        {
            RunRelicManager.Instance.RelicsChanged -= HandleRelicsChanged;
            RunRelicManager.Instance.RelicsChanged += HandleRelicsChanged;
        }
    }

    private void UnsubscribeFromRelicManager()
    {
        if (RunRelicManager.Instance != null)
        {
            RunRelicManager.Instance.RelicsChanged -= HandleRelicsChanged;
        }
    }

    private void HandleRelicsChanged(IReadOnlyList<RelicData> _)
    {
        RefreshRelicList();
    }

    private void EnsureRunRelicManagerIfWanted()
    {
        if (!createRunRelicManagerIfMissing || RunRelicManager.Instance != null) return;

        RunRelicManager.EnsureInstance();
    }

    private sealed class RelicDisplayEntry
    {
        public RelicData RelicData { get; }
        public int CopyCount { get; set; }

        public RelicDisplayEntry(RelicData relicData, int copyCount)
        {
            RelicData = relicData;
            CopyCount = copyCount;
        }
    }
}
