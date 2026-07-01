using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class CardDataUnityEvent : UnityEvent<CardData>
{
}

[DisallowMultipleComponent]
public class RunDeckCardView : CardView
{
    [Header("Run Deck")]
    [SerializeField] private GameObject countRoot;
    [SerializeField] private TMP_Text countText;

    [Header("Optional Selection")]
    [SerializeField] private Button selectButton;
    public CardDataUnityEvent OnCardClicked;

    protected override void Awake()
    {
        base.Awake();
        CacheSelectionButton();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        CacheSelectionButton();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        RegisterSelectionButton();
    }

    protected override void OnDisable()
    {
        UnregisterSelectionButton();
        base.OnDisable();
    }

    public void Setup(CardData newCardData, int copyCount, int displayIndex)
    {
        gameObject.name = newCardData != null ? $"RunDeckCard_{displayIndex:00}_{newCardData.Title}" : $"RunDeckCard_{displayIndex:00}_Empty";
        Setup(newCardData);
        SetCopyCount(copyCount);
    }

    public void Clear()
    {
        Setup(null, 0, 0);
        gameObject.SetActive(false);
    }

    private void SetCopyCount(int copyCount)
    {
        bool hasMultipleCopies = copyCount > 1;
        if (countRoot != null) countRoot.SetActive(hasMultipleCopies);
        if (countText != null) countText.text = hasMultipleCopies ? $"x{copyCount}" : string.Empty;
    }

    public void SelectThisCard()
    {
        if (CardData == null) return;
        OnCardClicked?.Invoke(CardData);
    }

    private void CacheSelectionButton()
    {
        if (selectButton == null) selectButton = GetComponentInChildren<Button>(true);
    }

    private void RegisterSelectionButton()
    {
        CacheSelectionButton();
        if (selectButton == null) return;

        selectButton.onClick.RemoveListener(SelectThisCard);
        selectButton.onClick.AddListener(SelectThisCard);
    }

    private void UnregisterSelectionButton()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(SelectThisCard);
        }
    }
}
