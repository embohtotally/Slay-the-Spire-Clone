using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StatusEffectIconView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text stackText;
    [SerializeField] private TMP_Text durationText;
    [SerializeField] private GameObject debuffMarker;
    [SerializeField] private string stackFormat = "x{0}";
    [SerializeField] private string durationFormat = "{0}";

    public void Bind(StatusEffectDisplayData data)
    {
        gameObject.SetActive(true);

        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.enabled = data.Icon != null;
        }

        if (nameText != null) nameText.text = data.DisplayName;
        if (stackText != null)
        {
            stackText.gameObject.SetActive(data.Stack > 0);
            stackText.text = string.Format(stackFormat, data.Stack);
        }

        if (durationText != null)
        {
            durationText.gameObject.SetActive(data.RemainingTurns > 0);
            durationText.text = string.Format(durationFormat, data.RemainingTurns);
        }

        if (debuffMarker != null) debuffMarker.SetActive(data.IsDebuff);
    }
}
