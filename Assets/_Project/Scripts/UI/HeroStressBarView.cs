using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HeroStressBarView : MonoBehaviour
{
    [SerializeField] private TMP_Text heroNameText;
    [SerializeField] private TMP_Text stressValueText;
    [SerializeField] private Slider stressSlider;
    [SerializeField] private GameObject stressedIndicator;
    [SerializeField] private string fallbackHeroName = "Hero";
    [SerializeField] private string valueFormat = "{0}/{1}";

    public void Bind(RunHeroStressState stressState, int heroIndex)
    {
        if (stressState == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        string heroName = stressState.HeroData != null ? stressState.HeroData.name : $"{fallbackHeroName} {heroIndex + 1}";
        if (heroNameText != null) heroNameText.text = heroName;
        if (stressValueText != null) stressValueText.text = string.Format(valueFormat, stressState.CurrentStress, stressState.MaxStress);
        if (stressSlider != null) stressSlider.value = stressState.MaxStress > 0 ? (float)stressState.CurrentStress / stressState.MaxStress : 0f;
        if (stressedIndicator != null) stressedIndicator.SetActive(stressState.IsStressed);
    }
}
