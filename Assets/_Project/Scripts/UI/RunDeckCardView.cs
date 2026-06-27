using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class RunDeckCardView : CardView
{
    [Header("Run Deck")]
    [SerializeField] private GameObject countRoot;
    [SerializeField] private TMP_Text countText;

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
}
