using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RunStatusUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private bool hideWhenNoRunState = true;

    [Header("Health")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private string healthFormat = "HP: {0}/{1}";

    [Header("Stress")]
    [SerializeField] private TMP_Text stressText;
    [SerializeField] private Slider stressSlider;
    [SerializeField] private string stressFormat = "Stress: {0}/{1}";

    [Header("Gold")]
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private string goldFormat = "Gold: {0}";

    [Header("Deck")]
    [SerializeField] private TMP_Text deckCountText;
    [SerializeField] private string deckCountFormat = "Deck: {0}";

    private RunManager subscribedRunManager;
    private RunDeckManager subscribedDeckManager;

    private void OnEnable()
    {
        Subscribe();
        UpdateView();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (subscribedRunManager != RunManager.Instance || subscribedDeckManager != RunDeckManager.Instance)
        {
            Unsubscribe();
            Subscribe();
            UpdateView();
        }
    }

    public void UpdateView()
    {
        RunManager runManager = RunManager.Instance;
        bool hasRunState = runManager != null && runManager.HasHeroState;

        if (root != null && root != gameObject && hideWhenNoRunState)
        {
            root.SetActive(hasRunState);
        }

        if (!hasRunState)
        {
            SetText(healthText, string.Format(healthFormat, "--", "--"));
            SetText(stressText, string.Format(stressFormat, "--", "--"));
            SetText(goldText, string.Format(goldFormat, runManager != null ? runManager.Gold : 0));
            SetSlider(healthSlider, 0f);
            SetSlider(stressSlider, 0f);
            UpdateDeckCount();
            return;
        }

        SetText(healthText, string.Format(healthFormat, runManager.HeroCurrentHealth, runManager.HeroMaxHealth));
        SetText(stressText, string.Format(stressFormat, runManager.HeroCurrentStress, runManager.HeroMaxStress));
        SetText(goldText, string.Format(goldFormat, runManager.Gold));
        SetSlider(healthSlider, (float)runManager.HeroCurrentHealth / runManager.HeroMaxHealth);
        SetSlider(stressSlider, (float)runManager.HeroCurrentStress / runManager.HeroMaxStress);
        UpdateDeckCount();
    }

    private void Subscribe()
    {
        subscribedRunManager = RunManager.Instance;
        if (subscribedRunManager != null)
        {
            subscribedRunManager.RunStateChanged -= HandleRunStateChanged;
            subscribedRunManager.RunStateChanged += HandleRunStateChanged;
        }

        subscribedDeckManager = RunDeckManager.Instance;
        if (subscribedDeckManager != null)
        {
            subscribedDeckManager.DeckChanged -= HandleDeckChanged;
            subscribedDeckManager.DeckChanged += HandleDeckChanged;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedRunManager != null)
        {
            subscribedRunManager.RunStateChanged -= HandleRunStateChanged;
        }

        if (subscribedDeckManager != null)
        {
            subscribedDeckManager.DeckChanged -= HandleDeckChanged;
        }

        subscribedRunManager = null;
        subscribedDeckManager = null;
    }

    private void HandleRunStateChanged()
    {
        UpdateView();
    }

    private void HandleDeckChanged(IReadOnlyList<CardData> _)
    {
        UpdateDeckCount();
    }

    private void UpdateDeckCount()
    {
        if (deckCountText == null) return;

        int count = RunDeckManager.Instance != null ? RunDeckManager.Instance.CurrentDeck.Count : 0;
        deckCountText.text = string.Format(deckCountFormat, count);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }

    private static void SetSlider(Slider target, float value)
    {
        if (target != null) target.value = Mathf.Clamp01(value);
    }
}
