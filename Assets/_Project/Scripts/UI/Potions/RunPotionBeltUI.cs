using System.Collections.Generic;
using Gameseed26;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class RunPotionBeltUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root panel for the potion UI. Use Animation Sequencer on this object if you want custom open/close animation.")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Parent that receives generated PotionSlotView instances. Usually a HorizontalLayoutGroup/GridLayoutGroup content object.")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private PotionSlotView slotViewPrefab;
    [SerializeField] private TMP_Text potionCountText;
    [SerializeField] private TMP_Text emptyPotionsText;
    [SerializeField] private TMP_Text selectedPotionText;

    [Header("Behavior")]
    [SerializeField] private bool startClosed;
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool closeWithEscape;
    [Tooltip("Show empty capacity slots, e.g. 1/3 potions still displays 3 slots.")]
    [SerializeField] private bool showEmptyCapacitySlots = true;
    [Tooltip("If false, ClosePanel only fires OnCloseRequested and leaves hiding to your animation event.")]
    [SerializeField] private bool hideRootImmediatelyOnClose = true;
    [Tooltip("If no RunPotionManager exists yet, create one through RunPotionManager.EnsureInstance(). Leave off for pure display-only scenes.")]
    [SerializeField] private bool createRunPotionManagerIfMissing;
    [Tooltip("If true, clicking a filled potion slot requests PotionUseSystem to execute combat effects.")]
    [SerializeField] private bool usePotionSystemOnClick;
    [Tooltip("If true, clicking a filled potion slot only removes it from the run without executing effects. Use mainly for non-combat/testing UI.")]
    [SerializeField] private bool consumePotionOnClick;

    [Header("Labels")]
    [SerializeField] private string potionCountFormat = "Potions: {0}/{1}";
    [SerializeField] private string emptyPotionsMessage = "No potions yet.";
    [SerializeField] private string selectedPotionFormat = "Selected: {0}";
    [SerializeField] private string noSelectedPotionLabel = "No potion selected.";

    [Header("Events")]
    public PotionDataUnityEvent OnPotionSelected;
    public PotionDataUnityEvent OnPotionUseRequested;
    public PotionDataUnityEvent OnPotionConsumed;
    public UnityEvent OnOpenRequested;
    public UnityEvent OnCloseRequested;
    public UnityEvent OnPotionsRefreshed;
    public UnityEvent OnConsumeFailed;

    [Header("Debug")]
    [ReadOnly][SerializeField] private int selectedPotionIndex = -1;
    [ReadOnly][SerializeField] private PotionData selectedPotion;

    private readonly List<PotionSlotView> spawnedViews = new();
    private bool isOpen;

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;
        if (slotContainer == null) slotContainer = transform;
        EnsureRunPotionManagerIfWanted();
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
        SubscribeToPotionManager();

        if (refreshOnEnable)
        {
            RefreshPotionBelt();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromPotionManager();
    }

    private void Update()
    {
        if (!closeWithEscape || !isOpen || Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClosePanel();
        }
    }

    [Button("Open Potion Belt", EButtonEnableMode.Playmode)]
    public void OpenPanel()
    {
        isOpen = true;
        SetPanelVisible(true);
        RefreshPotionBelt();
        OnOpenRequested?.Invoke();
    }

    [Button("Close Potion Belt", EButtonEnableMode.Playmode)]
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

    [Button("Refresh Potion Belt", EButtonEnableMode.Playmode)]
    public void RefreshPotionBelt()
    {
        IReadOnlyList<PotionData> potions = RunPotionManager.Instance != null
            ? RunPotionManager.Instance.CurrentPotions
            : new List<PotionData>();

        int capacity = RunPotionManager.Instance != null ? RunPotionManager.Instance.Capacity : potions.Count;
        int targetSlotCount = showEmptyCapacitySlots ? Mathf.Max(capacity, potions.Count) : potions.Count;
        EnsureViewCount(targetSlotCount);

        for (int i = 0; i < spawnedViews.Count; i++)
        {
            PotionSlotView view = spawnedViews[i];
            if (view == null) continue;

            if (i < targetSlotCount)
            {
                view.gameObject.SetActive(true);
                view.OnSlotClicked.RemoveListener(HandlePotionSlotClicked);
                view.OnSlotClicked.AddListener(HandlePotionSlotClicked);

                if (i < potions.Count)
                {
                    view.Setup(potions[i], i, true);
                }
                else
                {
                    view.Clear(i, false);
                }
            }
            else
            {
                view.OnSlotClicked.RemoveListener(HandlePotionSlotClicked);
                view.gameObject.SetActive(false);
            }
        }

        if (potionCountText != null)
        {
            potionCountText.text = string.Format(potionCountFormat, potions.Count, capacity);
        }

        if (emptyPotionsText != null)
        {
            bool isEmpty = potions.Count == 0;
            emptyPotionsText.gameObject.SetActive(isEmpty);
            emptyPotionsText.text = emptyPotionsMessage;
        }

        RefreshSelectedPotionLabel();
        OnPotionsRefreshed?.Invoke();
    }

    public void SelectPotionAtIndex(int index)
    {
        if (RunPotionManager.Instance == null || !RunPotionManager.Instance.HasIndex(index))
        {
            ClearSelection();
            return;
        }

        selectedPotionIndex = index;
        selectedPotion = RunPotionManager.Instance.CurrentPotions[index];
        RefreshSelectedPotionLabel();
        OnPotionSelected?.Invoke(selectedPotion);
    }

    public void ClearSelection()
    {
        selectedPotionIndex = -1;
        selectedPotion = null;
        RefreshSelectedPotionLabel();
    }

    [Button("Use Selected Potion", EButtonEnableMode.Playmode)]
    public void UseSelectedPotion()
    {
        UsePotionAtIndex(selectedPotionIndex);
    }

    public void UsePotionAtIndex(int index)
    {
        if (RunPotionManager.Instance == null || !RunPotionManager.Instance.HasIndex(index))
        {
            Gameseed26.Logger.Log(this, $"Cannot use potion at index {index}; no potion exists there.");
            OnConsumeFailed?.Invoke();
            RefreshPotionBelt();
            return;
        }

        PotionData potion = RunPotionManager.Instance.CurrentPotions[index];
        if (potion == null)
        {
            OnConsumeFailed?.Invoke();
            RefreshPotionBelt();
            return;
        }

        if (PotionUseSystem.Instance == null)
        {
            Gameseed26.Logger.Log(this, "Cannot use potion because PotionUseSystem is missing from the combat scene.");
            OnConsumeFailed?.Invoke();
            RefreshPotionBelt();
            return;
        }

        if (PotionUseSystem.Instance.TryUsePotionAtIndex(index))
        {
            OnPotionUseRequested?.Invoke(potion);
            ClearSelection();
            RefreshPotionBelt();
            return;
        }

        OnConsumeFailed?.Invoke();
        RefreshPotionBelt();
    }

    [Button("Consume Selected Potion", EButtonEnableMode.Playmode)]
    public void ConsumeSelectedPotion()
    {
        ConsumePotionAtIndex(selectedPotionIndex);
    }

    public void ConsumePotionAtIndex(int index)
    {
        if (RunPotionManager.Instance == null || !RunPotionManager.Instance.HasIndex(index))
        {
            Gameseed26.Logger.Log(this, $"Cannot consume potion at index {index}; no potion exists there.");
            OnConsumeFailed?.Invoke();
            RefreshPotionBelt();
            return;
        }

        PotionData potion = RunPotionManager.Instance.CurrentPotions[index];
        if (potion == null)
        {
            OnConsumeFailed?.Invoke();
            RefreshPotionBelt();
            return;
        }

        if (potion.Consumable && RunPotionManager.Instance.RemoveAt(index))
        {
            OnPotionConsumed?.Invoke(potion);
            ClearSelection();
            RefreshPotionBelt();
            return;
        }

        Gameseed26.Logger.Log(this, $"Potion '{potion.Title}' is not consumable or could not be removed.");
        OnConsumeFailed?.Invoke();
        RefreshPotionBelt();
    }

    // Call this from the last frame/event of your close Animation Sequencer if hideRootImmediatelyOnClose is false.
    public void HideRootAfterCloseAnimation()
    {
        if (!isOpen)
        {
            SetPanelVisible(false);
        }
    }

    private void HandlePotionSlotClicked(int slotIndex)
    {
        SelectPotionAtIndex(slotIndex);

        if (usePotionSystemOnClick)
        {
            UsePotionAtIndex(slotIndex);
            return;
        }

        if (consumePotionOnClick)
        {
            ConsumePotionAtIndex(slotIndex);
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null && panelRoot.activeSelf != visible)
        {
            panelRoot.SetActive(visible);
        }
    }

    private void EnsureViewCount(int targetCount)
    {
        if (slotViewPrefab == null)
        {
            if (targetCount > 0)
            {
                Gameseed26.Logger.LogWarning(this, "RunPotionBeltUI needs a PotionSlotView prefab to show run potions.");
            }
            return;
        }

        Transform parent = slotContainer != null ? slotContainer : transform;
        while (spawnedViews.Count < targetCount)
        {
            PotionSlotView view = Instantiate(slotViewPrefab, parent);
            spawnedViews.Add(view);
        }
    }

    private void SubscribeToPotionManager()
    {
        if (RunPotionManager.Instance != null)
        {
            RunPotionManager.Instance.PotionsChanged -= HandlePotionsChanged;
            RunPotionManager.Instance.PotionsChanged += HandlePotionsChanged;
        }
    }

    private void UnsubscribeFromPotionManager()
    {
        if (RunPotionManager.Instance != null)
        {
            RunPotionManager.Instance.PotionsChanged -= HandlePotionsChanged;
        }

        foreach (PotionSlotView view in spawnedViews)
        {
            if (view != null)
            {
                view.OnSlotClicked.RemoveListener(HandlePotionSlotClicked);
            }
        }
    }

    private void HandlePotionsChanged(IReadOnlyList<PotionData> _)
    {
        RefreshPotionBelt();
    }

    private void EnsureRunPotionManagerIfWanted()
    {
        if (!createRunPotionManagerIfMissing || RunPotionManager.Instance != null) return;

        RunPotionManager.EnsureInstance();
    }

    private void RefreshSelectedPotionLabel()
    {
        if (selectedPotionText == null) return;

        selectedPotionText.text = selectedPotion != null
            ? string.Format(selectedPotionFormat, selectedPotion.Title)
            : noSelectedPotionLabel;
    }
}
