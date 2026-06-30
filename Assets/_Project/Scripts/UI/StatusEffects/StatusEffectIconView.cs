using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StatusEffectIconView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text stackText;
    [SerializeField] private TMP_Text durationText;
    [SerializeField] private GameObject debuffMarker;

    [Header("Text")]
    [SerializeField] private string stackFormat = "x{0}";
    [SerializeField] private string durationFormat = "{0}";
    [SerializeField] private bool showNameWhenIconMissing = true;

    public void Bind(StatusEffectDisplayData data)
    {
        SetVisible(true);

        bool hasIcon = data.Icon != null;
        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.enabled = hasIcon;
        }

        if (nameText != null)
        {
            nameText.gameObject.SetActive(showNameWhenIconMissing || !hasIcon);
            nameText.text = data.DisplayName;
        }

        if (stackText != null)
        {
            bool showStack = data.Stack > 0;
            stackText.gameObject.SetActive(showStack);
            if (showStack) stackText.text = string.Format(stackFormat, data.Stack);
        }

        if (durationText != null)
        {
            bool showDuration = data.RemainingTurns > 0;
            durationText.gameObject.SetActive(showDuration);
            if (showDuration) durationText.text = string.Format(durationFormat, data.RemainingTurns);
        }

        if (debuffMarker != null) debuffMarker.SetActive(data.IsDebuff);
    }

    public void Clear()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (nameText != null) nameText.text = string.Empty;
        if (stackText != null)
        {
            stackText.text = string.Empty;
            stackText.gameObject.SetActive(false);
        }

        if (durationText != null)
        {
            durationText.text = string.Empty;
            durationText.gameObject.SetActive(false);
        }

        if (debuffMarker != null) debuffMarker.SetActive(false);
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
