using System.Collections.Generic;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class EventChoicesUIBinder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EventController eventController;
    [SerializeField] private List<EventChoiceButtonView> choiceViews = new();

    [Header("Behavior")]
    [SerializeField] private bool autoFindControllerInParents = true;
    [SerializeField] private bool autoFindChoiceViewsInChildren = true;
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool refreshOnStart = true;
    [Tooltip("When true, views without matching choices are SetActive(false). When false, they are left active but cleared/disabled.")]
    [SerializeField] private bool hideUnusedChoiceViews = true;
    [Tooltip("Useful while manually wiring UI. Logs when this binder has fewer views than EventController choices.")]
    [SerializeField] private bool logMissingChoiceViews = true;
    [Tooltip("If true, refreshes every Update. Leave off normally; use only if availability can change without manager events.")]
    [SerializeField] private bool refreshEveryFrame;

    [Header("Events")]
    public UnityEvent OnChoicesRefreshed;

    [Header("Debug")]
    [ReadOnly][SerializeField] private int boundChoiceCount;
    [ReadOnly][SerializeField] private int visibleChoiceViewCount;

    private bool subscribed;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (refreshOnStart)
        {
            RefreshChoices();
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();

        if (refreshOnEnable)
        {
            RefreshChoices();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (refreshEveryFrame)
        {
            RefreshChoices();
        }
    }

    public void SetEventController(EventController controller)
    {
        if (eventController == controller) return;

        Unsubscribe();
        eventController = controller;
        Subscribe();
        RefreshChoices();
    }

    [Button("Refresh Event Choices", EButtonEnableMode.Playmode)]
    public void RefreshChoices()
    {
        ResolveReferences();

        if (eventController == null)
        {
            ClearAllViews();
            Gameseed26.Logger.Log(this, "EventChoicesUIBinder needs an EventController reference.");
            return;
        }

        IReadOnlyList<EventChoiceDefinition> choices = eventController.Choices;
        boundChoiceCount = choices != null ? choices.Count : 0;
        visibleChoiceViewCount = 0;

        if (choiceViews.Count < boundChoiceCount && logMissingChoiceViews)
        {
            Gameseed26.Logger.Log(this, $"EventChoicesUIBinder has {choiceViews.Count} choice views but EventController has {boundChoiceCount} choices. Extra choices will not be displayed.");
        }

        for (int i = 0; i < choiceViews.Count; i++)
        {
            EventChoiceButtonView view = choiceViews[i];
            if (view == null) continue;

            if (choices != null && i < choices.Count)
            {
                bool canSelect = eventController.CanSelectChoice(i);
                string unavailableReason = canSelect ? string.Empty : eventController.GetUnavailableReason(i);
                view.gameObject.SetActive(true);
                view.Setup(eventController, i, choices[i], canSelect, unavailableReason);
                visibleChoiceViewCount++;
            }
            else
            {
                view.Clear(i);
                if (hideUnusedChoiceViews)
                {
                    view.gameObject.SetActive(false);
                }
                else
                {
                    view.gameObject.SetActive(true);
                    visibleChoiceViewCount++;
                }
            }
        }

        OnChoicesRefreshed?.Invoke();
    }

    public void SelectChoice(int choiceIndex)
    {
        if (eventController == null)
        {
            Gameseed26.Logger.Log(this, "Cannot select event choice because EventController reference is missing.");
            return;
        }

        eventController.SelectChoice(choiceIndex);
        RefreshChoices();
    }

    public void SelectChoice0() => SelectChoice(0);
    public void SelectChoice1() => SelectChoice(1);
    public void SelectChoice2() => SelectChoice(2);
    public void SelectChoice3() => SelectChoice(3);

    [Button("Auto Cache Child Choice Views")]
    public void AutoCacheChildChoiceViews()
    {
        choiceViews.Clear();
        choiceViews.AddRange(GetComponentsInChildren<EventChoiceButtonView>(true));
        visibleChoiceViewCount = choiceViews.Count;
    }

    private void ResolveReferences()
    {
        if (eventController == null && autoFindControllerInParents)
        {
            eventController = GetComponentInParent<EventController>();
            if (eventController == null)
            {
                eventController = FindFirstObjectByType<EventController>(FindObjectsInactive.Include);
            }
        }

        if (autoFindChoiceViewsInChildren && choiceViews.Count == 0)
        {
            AutoCacheChildChoiceViews();
        }
    }

    private void ClearAllViews()
    {
        boundChoiceCount = 0;
        visibleChoiceViewCount = 0;

        foreach (EventChoiceButtonView view in choiceViews)
        {
            if (view == null) continue;
            view.Clear();
            if (hideUnusedChoiceViews) view.gameObject.SetActive(false);
        }
    }

    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;

        if (eventController != null)
        {
            eventController.OnEventOpened.RemoveListener(RefreshChoices);
            eventController.OnChoiceSelected.RemoveListener(RefreshChoices);
            eventController.OnChoiceCompleted.RemoveListener(RefreshChoices);
            eventController.OnEventOpened.AddListener(RefreshChoices);
            eventController.OnChoiceSelected.AddListener(RefreshChoices);
            eventController.OnChoiceCompleted.AddListener(RefreshChoices);
        }

        if (RunManager.Instance != null) RunManager.Instance.RunStateChanged += RefreshChoices;
        if (RunDeckManager.Instance != null) RunDeckManager.Instance.DeckChanged += HandleDeckChanged;
        if (RunRelicManager.Instance != null) RunRelicManager.Instance.RelicsChanged += HandleRelicsChanged;
        if (RunPotionManager.Instance != null) RunPotionManager.Instance.PotionsChanged += HandlePotionsChanged;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        if (eventController != null)
        {
            eventController.OnEventOpened.RemoveListener(RefreshChoices);
            eventController.OnChoiceSelected.RemoveListener(RefreshChoices);
            eventController.OnChoiceCompleted.RemoveListener(RefreshChoices);
        }

        if (RunManager.Instance != null) RunManager.Instance.RunStateChanged -= RefreshChoices;
        if (RunDeckManager.Instance != null) RunDeckManager.Instance.DeckChanged -= HandleDeckChanged;
        if (RunRelicManager.Instance != null) RunRelicManager.Instance.RelicsChanged -= HandleRelicsChanged;
        if (RunPotionManager.Instance != null) RunPotionManager.Instance.PotionsChanged -= HandlePotionsChanged;
    }

    private void HandleDeckChanged(IReadOnlyList<CardData> _) => RefreshChoices();
    private void HandleRelicsChanged(IReadOnlyList<RelicData> _) => RefreshChoices();
    private void HandlePotionsChanged(IReadOnlyList<PotionData> _) => RefreshChoices();
}
