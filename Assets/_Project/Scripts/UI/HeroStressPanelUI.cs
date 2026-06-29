using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HeroStressPanelUI : MonoBehaviour
{
    [SerializeField] private HeroView heroView;
    [SerializeField] private Transform container;
    [SerializeField] private HeroStressBarView stressBarPrefab;
    [SerializeField] private bool autoFindHeroView = true;

    private readonly List<HeroStressBarView> barViews = new();

    private void Awake()
    {
        if (container == null) container = transform;
        EnsureHeroViewReference();
    }

    private void OnEnable()
    {
        EnsureHeroViewReference();
        if (heroView != null) heroView.StressStateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (heroView != null) heroView.StressStateChanged -= Refresh;
    }

    public void SetHeroView(HeroView target)
    {
        if (heroView == target) return;
        if (isActiveAndEnabled && heroView != null) heroView.StressStateChanged -= Refresh;
        heroView = target;
        if (isActiveAndEnabled && heroView != null) heroView.StressStateChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        if (heroView == null || stressBarPrefab == null || container == null) return;

        IReadOnlyList<RunHeroStressState> stressStates = heroView.StressStates;
        EnsureBarCount(stressStates.Count);

        for (int i = 0; i < barViews.Count; i++)
        {
            bool hasState = i < stressStates.Count;
            barViews[i].gameObject.SetActive(hasState);
            if (hasState) barViews[i].Bind(stressStates[i], i);
        }
    }

    private void EnsureHeroViewReference()
    {
        if (heroView != null || !autoFindHeroView) return;
        if (HeroSystem.Instance != null) heroView = HeroSystem.Instance.HeroView;
        if (heroView == null) heroView = FindFirstObjectByType<HeroView>();
    }

    private void EnsureBarCount(int targetCount)
    {
        while (barViews.Count < targetCount)
        {
            HeroStressBarView view = Instantiate(stressBarPrefab, container);
            barViews.Add(view);
        }
    }
}
