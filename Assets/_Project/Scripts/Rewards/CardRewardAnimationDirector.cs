using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CardRewardAnimationDirector : MonoBehaviour
{
    [Header("Enter")]
    [SerializeField] private bool animateOptionsOnOpen = true;
    [Tooltip("Cards fly in from this point. Leave empty to use Enter Offset from each card's authored position.")]
    [SerializeField] private RectTransform enterPoint;
    [SerializeField] private Vector2 enterOffset = new(0f, -220f);
    [SerializeField, Min(0f)] private float enterDuration = 0.35f;
    [SerializeField, Min(0f)] private float enterStagger = 0.06f;
    [SerializeField] private Ease enterEase = Ease.OutBack;

    [Header("Choice")]
    [SerializeField] private bool animateChoiceToInventory = true;
    [Tooltip("Optional fixed point where the selected card flies after lifting. Usually place this over the RunDeckContainer/inventory icon.")]
    [SerializeField] private RectTransform inventoryTargetPoint;
    [SerializeField] private Vector2 selectedLiftOffset = new(0f, 90f);
    [SerializeField] private Vector2 fallbackInventoryOffset = new(0f, -260f);
    [SerializeField, Min(0f)] private float selectedLiftDuration = 0.18f;
    [SerializeField, Min(0f)] private float selectedFlyDuration = 0.32f;
    [SerializeField, Min(0f)] private float selectedScale = 0.85f;
    [SerializeField] private Ease selectedLiftEase = Ease.OutQuad;
    [SerializeField] private Ease selectedFlyEase = Ease.InBack;

    [Header("Dismiss Other Options")]
    [SerializeField] private bool fadeOtherOptionsOnChoice = true;
    [SerializeField, Min(0f)] private float otherOptionsFadeDuration = 0.18f;
    [SerializeField, Range(0f, 1f)] private float otherOptionsFadeAlpha = 0f;

    [Header("Skip / Exit")]
    [SerializeField] private bool animateSkipExit = true;
    [Tooltip("Cards fly out to this point on skip. Leave empty to use Exit Offset from each card's authored position.")]
    [SerializeField] private RectTransform exitPoint;
    [SerializeField] private Vector2 exitOffset = new(0f, -260f);
    [SerializeField, Min(0f)] private float exitDuration = 0.22f;
    [SerializeField, Min(0f)] private float exitStagger = 0.035f;
    [SerializeField] private Ease exitEase = Ease.InBack;

    private readonly Dictionary<RectTransform, Vector2> homePositions = new();
    private readonly Dictionary<RectTransform, Vector3> homeScales = new();
    private Sequence enterSequence;

    public void PlayOptionsEnter(IReadOnlyList<CardRewardOptionView> optionViews)
    {
        if (!animateOptionsOnOpen || optionViews == null) return;

        enterSequence?.Kill();
        CaptureFreshHomes(optionViews);
        enterSequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < optionViews.Count; i++)
        {
            CardRewardOptionView optionView = optionViews[i];
            RectTransform rect = GetOptionRect(optionView);
            if (rect == null || !optionView.gameObject.activeInHierarchy) continue;

            RememberHome(rect);
            Vector2 homePosition = homePositions[rect];
            Vector3 homeScale = homeScales[rect];
            CanvasGroup canvasGroup = GetOrAddCanvasGroup(optionView.gameObject);
            canvasGroup.alpha = 0f;
            rect.anchoredPosition = GetPointLocalToParent(rect, enterPoint, homePosition + enterOffset);
            rect.localScale = Vector3.zero;

            float delay = enterStagger * i;
            enterSequence.Insert(delay, rect.DOAnchorPos(homePosition, enterDuration).SetEase(enterEase));
            enterSequence.Insert(delay, rect.DOScale(homeScale, enterDuration).SetEase(enterEase));
            enterSequence.Insert(delay, canvasGroup.DOFade(1f, Mathf.Min(enterDuration, 0.18f)));
        }
    }

    public IEnumerator PlayChooseReward(CardRewardOptionView selectedView, IReadOnlyList<CardRewardOptionView> optionViews)
    {
        if (!animateChoiceToInventory || selectedView == null)
        {
            yield break;
        }

        RectTransform selectedRect = GetOptionRect(selectedView);
        if (selectedRect == null)
        {
            yield break;
        }

        RememberHome(selectedRect);
        selectedRect.SetAsLastSibling();
        Vector2 selectedStart = selectedRect.anchoredPosition;
        Vector2 liftedPosition = selectedStart + selectedLiftOffset;
        Vector2 targetPosition = GetPointLocalToParent(selectedRect, inventoryTargetPoint, selectedStart + fallbackInventoryOffset);
        Vector3 startScale = selectedRect.localScale;
        Vector3 targetScale = startScale * Mathf.Max(0.01f, selectedScale);

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        if (fadeOtherOptionsOnChoice && optionViews != null)
        {
            for (int i = 0; i < optionViews.Count; i++)
            {
                CardRewardOptionView optionView = optionViews[i];
                if (optionView == null || optionView == selectedView) continue;
                CanvasGroup canvasGroup = GetOrAddCanvasGroup(optionView.gameObject);
                sequence.Join(canvasGroup.DOFade(otherOptionsFadeAlpha, otherOptionsFadeDuration));
            }
        }

        sequence.Append(selectedRect.DOAnchorPos(liftedPosition, selectedLiftDuration).SetEase(selectedLiftEase));
        sequence.Append(selectedRect.DOAnchorPos(targetPosition, selectedFlyDuration).SetEase(selectedFlyEase));
        sequence.Join(selectedRect.DOScale(targetScale, selectedFlyDuration).SetEase(selectedFlyEase));

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlaySkipReward(IReadOnlyList<CardRewardOptionView> optionViews)
    {
        if (!animateSkipExit || optionViews == null)
        {
            yield break;
        }

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < optionViews.Count; i++)
        {
            CardRewardOptionView optionView = optionViews[i];
            RectTransform rect = GetOptionRect(optionView);
            if (rect == null || !optionView.gameObject.activeInHierarchy) continue;

            RememberHome(rect);
            Vector2 targetPosition = GetPointLocalToParent(rect, exitPoint, homePositions[rect] + exitOffset);
            CanvasGroup canvasGroup = GetOrAddCanvasGroup(optionView.gameObject);
            float delay = exitStagger * i;
            sequence.Insert(delay, rect.DOAnchorPos(targetPosition, exitDuration).SetEase(exitEase));
            sequence.Insert(delay, rect.DOScale(Vector3.zero, exitDuration).SetEase(exitEase));
            sequence.Insert(delay, canvasGroup.DOFade(0f, exitDuration));
        }

        yield return sequence.WaitForCompletion();
    }

    public void RestoreOptionHomes(IReadOnlyList<CardRewardOptionView> optionViews)
    {
        if (optionViews == null) return;

        foreach (CardRewardOptionView optionView in optionViews)
        {
            RectTransform rect = GetOptionRect(optionView);
            if (rect == null) continue;

            RememberHome(rect);
            rect.anchoredPosition = homePositions[rect];
            rect.localScale = homeScales[rect];
            CanvasGroup canvasGroup = GetOrAddCanvasGroup(optionView.gameObject);
            canvasGroup.alpha = 1f;
        }
    }

    public void CaptureFreshHomes(IReadOnlyList<CardRewardOptionView> optionViews)
    {
        homePositions.Clear();
        homeScales.Clear();
        ForceLayout(optionViews);

        if (optionViews == null) return;
        foreach (CardRewardOptionView optionView in optionViews)
        {
            RectTransform rect = GetOptionRect(optionView);
            if (rect == null || !optionView.gameObject.activeInHierarchy) continue;
            homePositions[rect] = rect.anchoredPosition;
            homeScales[rect] = rect.localScale != Vector3.zero ? rect.localScale : Vector3.one;
        }
    }

    private void RememberHome(RectTransform rect)
    {
        if (rect == null) return;
        if (!homePositions.ContainsKey(rect)) homePositions.Add(rect, rect.anchoredPosition);
        if (!homeScales.ContainsKey(rect)) homeScales.Add(rect, rect.localScale);
    }

    private static RectTransform GetOptionRect(CardRewardOptionView optionView)
    {
        return optionView != null ? optionView.transform as RectTransform : null;
    }

    private static void ForceLayout(IReadOnlyList<CardRewardOptionView> optionViews)
    {
        Canvas.ForceUpdateCanvases();
        if (optionViews == null) return;

        foreach (CardRewardOptionView optionView in optionViews)
        {
            RectTransform rect = GetOptionRect(optionView);
            RectTransform parent = rect != null ? rect.parent as RectTransform : null;
            if (parent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = target.AddComponent<CanvasGroup>();
        return canvasGroup;
    }

    private static Vector2 GetPointLocalToParent(RectTransform movingRect, RectTransform point, Vector2 fallback)
    {
        if (movingRect == null || point == null || movingRect.parent == null) return fallback;

        RectTransform parentRect = movingRect.parent as RectTransform;
        if (parentRect == null) return fallback;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, point.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, null, out Vector2 localPoint)
            ? localPoint
            : fallback;
    }
}
