using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class PotionDataUnityEvent : UnityEvent<PotionData>
{
}

[DisallowMultipleComponent]
public class PotionSlotView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text slotIndexText;
    [SerializeField] private GameObject filledRoot;
    [SerializeField] private GameObject emptyRoot;

    [Header("Fallback Labels")]
    [SerializeField] private string emptyTitle = "Empty Potion Slot";
    [SerializeField] private string missingDescription = "No description.";
    [SerializeField] private string slotIndexFormat = "{0}";

    [Header("Events")]
    public PotionDataUnityEvent OnPotionClicked;
    public UnityEvent<int> OnSlotClicked;

    public PotionData PotionData { get; private set; }
    public int SlotIndex { get; private set; } = -1;
    public bool HasPotion => PotionData != null;

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

    public void Setup(PotionData potionData, int slotIndex, bool interactable = true)
    {
        PotionData = potionData;
        SlotIndex = slotIndex;
        CacheReferences();

        gameObject.name = potionData != null
            ? $"PotionSlot_{slotIndex:00}_{potionData.Title}"
            : $"PotionSlot_{slotIndex:00}_Empty";

        if (slotIndexText != null)
        {
            slotIndexText.text = string.Format(slotIndexFormat, slotIndex + 1);
        }

        if (potionData == null)
        {
            ClearVisuals(interactable);
            return;
        }

        if (filledRoot != null) filledRoot.SetActive(true);
        if (emptyRoot != null) emptyRoot.SetActive(false);

        if (titleText != null) titleText.text = potionData.Title;
        if (descriptionText != null) descriptionText.text = string.IsNullOrWhiteSpace(potionData.Description)
            ? missingDescription
            : potionData.Description;
        if (rarityText != null) rarityText.text = potionData.Rarity.ToString();

        if (iconImage != null)
        {
            iconImage.sprite = potionData.Image;
            iconImage.enabled = potionData.Image != null;
        }

        SetButtonInteractable(interactable);
    }

    public void Clear(int slotIndex = -1, bool interactable = false)
    {
        PotionData = null;
        SlotIndex = slotIndex;
        gameObject.name = slotIndex >= 0 ? $"PotionSlot_{slotIndex:00}_Empty" : "PotionSlot_Empty";
        ClearVisuals(interactable);
    }

    public void SelectThisPotion()
    {
        OnSlotClicked?.Invoke(SlotIndex);
        if (PotionData != null)
        {
            OnPotionClicked?.Invoke(PotionData);
        }
    }

    private void ClearVisuals(bool interactable)
    {
        if (filledRoot != null) filledRoot.SetActive(false);
        if (emptyRoot != null) emptyRoot.SetActive(true);

        if (titleText != null) titleText.text = emptyTitle;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (rarityText != null) rarityText.text = string.Empty;
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
        if (slotIndexText != null)
        {
            slotIndexText.text = SlotIndex >= 0 ? string.Format(slotIndexFormat, SlotIndex + 1) : string.Empty;
        }

        SetButtonInteractable(interactable);
    }

    private void CacheReferences()
    {
        if (button == null) button = GetComponentInChildren<Button>(true);
        if (iconImage == null) iconImage = GetComponentInChildren<Image>(true);

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (titleText == null && texts.Length > 0) titleText = texts[0];
        if (descriptionText == null && texts.Length > 1) descriptionText = texts[1];
        if (rarityText == null && texts.Length > 2) rarityText = texts[2];
        if (slotIndexText == null && texts.Length > 3) slotIndexText = texts[3];
    }

    private void RegisterButton()
    {
        CacheReferences();
        if (button == null) return;

        button.onClick.RemoveListener(SelectThisPotion);
        button.onClick.AddListener(SelectThisPotion);
    }

    private void UnregisterButton()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(SelectThisPotion);
        }
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (button != null) button.interactable = interactable;
    }
}
