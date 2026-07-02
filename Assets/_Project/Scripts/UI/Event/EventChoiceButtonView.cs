using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class EventChoiceDefinitionUnityEvent : UnityEvent<EventChoiceDefinition>
{
}

[DisallowMultipleComponent]
public class EventChoiceButtonView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text unavailableReasonText;
    [SerializeField] private TMP_Text indexText;
    [SerializeField] private GameObject availableRoot;
    [SerializeField] private GameObject unavailableRoot;
    [SerializeField] private GameObject emptyRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Labels")]
    [SerializeField] private string emptyTitle = "No Choice";
    [SerializeField] private string missingDescription = "No description.";
    [SerializeField] private string indexFormat = "{0}";
    [SerializeField] private string unavailableFallback = "Unavailable.";

    [Header("Visual State")]
    [SerializeField] private float availableAlpha = 1f;
    [SerializeField] private float unavailableAlpha = 0.55f;
    [SerializeField] private float emptyAlpha = 0.25f;

    [Header("Events")]
    public UnityEvent<int> OnChoiceClicked;
    public EventChoiceDefinitionUnityEvent OnChoiceDefinitionClicked;

    public int ChoiceIndex { get; private set; } = -1;
    public EventChoiceDefinition Choice { get; private set; }
    public bool CanSelect { get; private set; }

    private EventController controller;

    private void Awake()
    {
        CacheReferences();
        RegisterButton();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        RegisterButton();
    }

    private void OnDisable()
    {
        UnregisterButton();
    }

    public void Setup(EventController eventController, int choiceIndex, EventChoiceDefinition choice, bool canSelect, string unavailableReason)
    {
        controller = eventController;
        ChoiceIndex = choiceIndex;
        Choice = choice;
        CanSelect = canSelect;
        CacheReferences();
        RegisterButton();

        if (choice == null)
        {
            Clear(choiceIndex);
            return;
        }

        gameObject.name = $"EventChoice_{choiceIndex:00}_{choice.Title}";

        if (titleText != null) titleText.text = string.IsNullOrWhiteSpace(choice.Title) ? emptyTitle : choice.Title;
        if (descriptionText != null) descriptionText.text = string.IsNullOrWhiteSpace(choice.Description) ? missingDescription : choice.Description;
        if (indexText != null) indexText.text = string.Format(indexFormat, choiceIndex + 1);

        bool hasUnavailableReason = !string.IsNullOrWhiteSpace(unavailableReason);
        if (unavailableReasonText != null)
        {
            unavailableReasonText.text = canSelect ? string.Empty : hasUnavailableReason ? unavailableReason : unavailableFallback;
            unavailableReasonText.gameObject.SetActive(!canSelect);
        }

        if (availableRoot != null) availableRoot.SetActive(canSelect);
        if (unavailableRoot != null) unavailableRoot.SetActive(!canSelect);
        if (emptyRoot != null) emptyRoot.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = canSelect ? availableAlpha : unavailableAlpha;
        if (button != null) button.interactable = canSelect;
    }

    public void Clear(int choiceIndex = -1)
    {
        controller = null;
        ChoiceIndex = choiceIndex;
        Choice = null;
        CanSelect = false;
        gameObject.name = choiceIndex >= 0 ? $"EventChoice_{choiceIndex:00}_Empty" : "EventChoice_Empty";

        if (titleText != null) titleText.text = emptyTitle;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (unavailableReasonText != null)
        {
            unavailableReasonText.text = string.Empty;
            unavailableReasonText.gameObject.SetActive(false);
        }
        if (indexText != null) indexText.text = choiceIndex >= 0 ? string.Format(indexFormat, choiceIndex + 1) : string.Empty;

        if (availableRoot != null) availableRoot.SetActive(false);
        if (unavailableRoot != null) unavailableRoot.SetActive(false);
        if (emptyRoot != null) emptyRoot.SetActive(true);
        if (canvasGroup != null) canvasGroup.alpha = emptyAlpha;
        if (button != null) button.interactable = false;
    }

    public void SelectThisChoice()
    {
        if (controller == null || ChoiceIndex < 0)
        {
            Gameseed26.Logger.Log(this, "Cannot select event choice because this view is not bound to an EventController.");
            return;
        }

        OnChoiceClicked?.Invoke(ChoiceIndex);
        if (Choice != null)
        {
            OnChoiceDefinitionClicked?.Invoke(Choice);
        }

        controller.SelectChoice(ChoiceIndex);
    }

    private void CacheReferences()
    {
        if (button == null) button = GetComponentInChildren<Button>(true);
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (titleText == null && texts.Length > 0) titleText = texts[0];
        if (descriptionText == null && texts.Length > 1) descriptionText = texts[1];
        if (unavailableReasonText == null && texts.Length > 2) unavailableReasonText = texts[2];
        if (indexText == null && texts.Length > 3) indexText = texts[3];
    }

    private void RegisterButton()
    {
        CacheReferences();
        if (button == null) return;

        button.onClick.RemoveListener(SelectThisChoice);
        button.onClick.AddListener(SelectThisChoice);
    }

    private void UnregisterButton()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(SelectThisChoice);
        }
    }
}
