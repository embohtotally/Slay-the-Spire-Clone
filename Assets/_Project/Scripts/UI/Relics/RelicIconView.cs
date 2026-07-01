using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RelicIconView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private GameObject countRoot;
    [SerializeField] private TMP_Text countText;

    [Header("Fallback Labels")]
    [SerializeField] private string emptyTitle = "No Relic";
    [SerializeField] private string missingDescription = "No description.";
    [SerializeField] private string copyCountFormat = "x{0}";

    public RelicData RelicData { get; private set; }
    public int CopyCount { get; private set; }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void Setup(RelicData relicData, int copyCount = 1, int displayIndex = 0)
    {
        RelicData = relicData;
        CopyCount = Mathf.Max(0, copyCount);
        CacheReferences();

        gameObject.name = relicData != null
            ? $"Relic_{displayIndex:00}_{relicData.Title}"
            : $"Relic_{displayIndex:00}_Empty";

        if (relicData == null)
        {
            ClearVisuals();
            return;
        }

        if (titleText != null) titleText.text = relicData.Title;
        if (descriptionText != null) descriptionText.text = string.IsNullOrWhiteSpace(relicData.Description)
            ? missingDescription
            : relicData.Description;
        if (rarityText != null) rarityText.text = relicData.Rarity.ToString();

        if (iconImage != null)
        {
            iconImage.sprite = relicData.Image;
            iconImage.enabled = relicData.Image != null;
        }

        bool hasMultipleCopies = CopyCount > 1;
        if (countRoot != null) countRoot.SetActive(hasMultipleCopies);
        if (countText != null) countText.text = hasMultipleCopies ? string.Format(copyCountFormat, CopyCount) : string.Empty;
    }

    public void Clear()
    {
        Setup(null, 0, 0);
        gameObject.SetActive(false);
    }

    private void ClearVisuals()
    {
        if (titleText != null) titleText.text = emptyTitle;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (rarityText != null) rarityText.text = string.Empty;
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
        if (countRoot != null) countRoot.SetActive(false);
        if (countText != null) countText.text = string.Empty;
    }

    private void CacheReferences()
    {
        if (iconImage == null) iconImage = GetComponentInChildren<Image>(true);

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (titleText == null && texts.Length > 0) titleText = texts[0];
        if (descriptionText == null && texts.Length > 1) descriptionText = texts[1];
        if (rarityText == null && texts.Length > 2) rarityText = texts[2];
        if (countText == null && texts.Length > 3) countText = texts[3];
    }
}
