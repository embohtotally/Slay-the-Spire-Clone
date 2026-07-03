using System;
using System.Collections;
using System.Collections.Generic;
using Gameseed26;
using UnityEngine;
using UnityEngine.Events;

public class CardRewardController : MonoBehaviour
{
    [Header("Reward Source")]
    [SerializeField] private CardRewardPool rewardPool;
    [SerializeField] private CardRewardRequest defaultRequest = new();

    [Header("UI")]
    [Tooltip("Parent that contains manually positioned CardRewardOptionView children. Their RectTransforms decide the layout.")]
    [SerializeField] private RectTransform optionsParent;
    [SerializeField] private CardRewardOptionView optionPrefab;
    [SerializeField] private bool autoFindOptionsInChildren = true;
    [SerializeField] private bool autoOpenOnStart = true;
    [SerializeField] private GameObject rewardRoot;
    [SerializeField] private CardRewardAnimationDirector animationDirector;

    [Header("After Choice")]
    [SerializeField] private bool hideAfterChoice = true;
    [SerializeField] private bool loadSceneAfterChoice = true;
    [SerializeField] private bool unloadAdditiveSceneAfterChoice;
    [SerializeField] private string sceneAfterChoice = "Map";

    [Header("Events")]
    public UnityEvent OnRewardOpened;
    public UnityEvent OnRewardChosen;
    public UnityEvent OnRewardSkipped;
    public UnityEvent OnRewardClosed;

    [Header("Audio")]
    [SerializeField] private TuneSfxCue rewardOpenedSfx;
    [SerializeField] private TuneSfxCue rewardChosenSfx;
    [SerializeField] private TuneSfxCue rewardSkippedSfx;
    [SerializeField] private TuneSfxCue rewardClosedSfx;

    private readonly List<CardRewardOption> activeOptions = new();
    private readonly List<CardRewardOptionView> optionViews = new();
    private bool rewardAlreadyChosen;
    private bool rewardFinished;
    private bool rewardTransitionInProgress;
    private Coroutine openAnimationRoutine;

    public event Action<bool> RewardFinished;

    private void Awake()
    {
        EnsureRunDeckManagerExists();
        if (animationDirector == null) animationDirector = GetComponentInChildren<CardRewardAnimationDirector>(true);
        CollectOptionViews();
    }

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<OpenCardRewardGA>(OpenCardRewardPerformer);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<OpenCardRewardGA>();
    }

    private void Start()
    {
        if (autoOpenOnStart)
        {
            OpenReward();
        }
    }

    public void ConfigureForAdditiveReward(CardRewardRequest request)
    {
        loadSceneAfterChoice = false;
        unloadAdditiveSceneAfterChoice = true;
        hideAfterChoice = true;
        OpenReward(request ?? defaultRequest);
    }

    public void OpenReward()
    {
        OpenReward(defaultRequest);
    }

    private IEnumerator OpenCardRewardPerformer(OpenCardRewardGA openCardRewardGA)
    {
        OpenReward(openCardRewardGA?.Request ?? defaultRequest);
        yield break;
    }

    public void OpenReward(CardRewardRequest request)
    {
        rewardAlreadyChosen = false;
        rewardFinished = false;
        if (rewardRoot != null) rewardRoot.SetActive(true);

        GenerateOptions(request ?? defaultRequest);
        QueueOpenAnimationAfterLayout();
        rewardOpenedSfx?.Play(this, transform);
        OnRewardOpened?.Invoke();
    }

    private void QueueOpenAnimationAfterLayout()
    {
        if (openAnimationRoutine != null)
        {
            StopCoroutine(openAnimationRoutine);
        }

        openAnimationRoutine = StartCoroutine(PlayOpenAnimationAfterLayout());
    }

    private IEnumerator PlayOpenAnimationAfterLayout()
    {
        // Wait one frame so LayoutGroups/content-size fitters finish placing generated reward cards.
        // Without this, newly spawned options can all report their prefab/default anchoredPosition
        // and animate into a stack near the parent corner.
        yield return null;
        Canvas.ForceUpdateCanvases();
        animationDirector?.PlayOptionsEnter(optionViews);
        openAnimationRoutine = null;
    }

    public void ChooseReward(CardRewardOptionView optionView)
    {
        if (optionView == null) return;
        ChooseReward(optionView.Option, optionView);
    }

    public void ChooseReward(CardRewardOption option)
    {
        ChooseReward(option, FindOptionView(option));
    }

    private void ChooseReward(CardRewardOption option, CardRewardOptionView optionView)
    {
        if (rewardAlreadyChosen || rewardFinished || rewardTransitionInProgress || option == null) return;
        EnsureRunDeckManagerExists();
        if (!CanApplyReward(option)) return;

        StartCoroutine(ChooseRewardRoutine(option, optionView));
    }

    private IEnumerator ChooseRewardRoutine(CardRewardOption option, CardRewardOptionView optionView)
    {
        rewardAlreadyChosen = true;
        rewardTransitionInProgress = true;
        SetOptionButtonsInteractable(false);

        if (animationDirector != null && optionView != null)
        {
            yield return animationDirector.PlayChooseReward(optionView, optionViews);
        }

        bool applied = option.Type switch
        {
            CardRewardOptionType.NewCard => AddNewCard(option),
            CardRewardOptionType.UpgradeCard => ApplyUpgrade(option),
            _ => false
        };

        rewardTransitionInProgress = false;
        if (!applied)
        {
            rewardAlreadyChosen = false;
            SetOptionButtonsInteractable(true);
            yield break;
        }

        FinishReward(true);
    }

    public void SkipReward()
    {
        if (rewardFinished || rewardTransitionInProgress) return;
        StartCoroutine(SkipRewardRoutine());
    }

    private IEnumerator SkipRewardRoutine()
    {
        rewardTransitionInProgress = true;
        SetOptionButtonsInteractable(false);

        if (animationDirector != null)
        {
            yield return animationDirector.PlaySkipReward(optionViews);
        }

        rewardTransitionInProgress = false;
        FinishReward(false);
    }

    public void CloseReward()
    {
        if (rewardRoot != null) rewardRoot.SetActive(false);
        rewardClosedSfx?.Play(this, transform);
        OnRewardClosed?.Invoke();
    }

    private void FinishReward(bool claimed)
    {
        if (rewardFinished) return;
        rewardFinished = true;

        if (claimed)
        {
            rewardChosenSfx?.Play(this, transform);
            OnRewardChosen?.Invoke();
        }
        else
        {
            rewardSkippedSfx?.Play(this, transform);
            OnRewardSkipped?.Invoke();
        }

        if (hideAfterChoice)
        {
            CloseReward();
        }

        RewardFinished?.Invoke(claimed);

        if (unloadAdditiveSceneAfterChoice)
        {
            SceneLoader.UnloadScene(gameObject.scene.name);
            return;
        }

        if (loadSceneAfterChoice && !string.IsNullOrWhiteSpace(sceneAfterChoice))
        {
            SceneLoader.LoadScene(sceneAfterChoice);
        }
    }

    private void GenerateOptions(CardRewardRequest request)
    {
        activeOptions.Clear();
        CollectOptionViews();
        animationDirector?.RestoreOptionHomes(optionViews);

        if (rewardPool == null)
        {
            Gameseed26.Logger.LogWarning("CardRewardController needs a CardRewardPool.");
            ClearOptionViews();
            return;
        }

        CardRewardRequest safeRequest = request ?? new CardRewardRequest();
        List<CardRewardOption> candidates = rewardPool.BuildCandidateRewards(RunDeckManager.Instance, safeRequest);
        if (candidates.Count == 0)
        {
            Gameseed26.Logger.LogWarning("Card reward has no valid options. Add reward cards or upgrade recipes to the CardRewardPool.");
            ClearOptionViews();
            return;
        }

        int targetCount = Mathf.Max(1, safeRequest.OptionCount);
        EnsureOptionViewCount(targetCount);

        for (int i = 0; i < targetCount; i++)
        {
            if (candidates.Count == 0) break;

            int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
            CardRewardOption option = candidates[randomIndex];
            activeOptions.Add(option);

            if (!safeRequest.AllowDuplicateOptions)
            {
                candidates.RemoveAt(randomIndex);
            }
        }

        for (int i = 0; i < optionViews.Count; i++)
        {
            if (i < activeOptions.Count)
            {
                optionViews[i].Setup(this, activeOptions[i]);
            }
            else
            {
                optionViews[i].Clear();
            }
        }
    }

    private CardRewardOptionView FindOptionView(CardRewardOption option)
    {
        if (option == null) return null;

        foreach (CardRewardOptionView optionView in optionViews)
        {
            if (optionView != null && optionView.Option == option)
            {
                return optionView;
            }
        }

        return null;
    }

    private void SetOptionButtonsInteractable(bool interactable)
    {
        foreach (CardRewardOptionView optionView in optionViews)
        {
            if (optionView != null)
            {
                optionView.SetInteractable(interactable);
            }
        }
    }

    private bool CanApplyReward(CardRewardOption option)
    {
        if (option == null) return false;

        switch (option.Type)
        {
            case CardRewardOptionType.NewCard:
                return option.Card != null;

            case CardRewardOptionType.UpgradeCard:
                CardUpgradeRecipe recipe = option.UpgradeRecipe;
                return recipe != null
                    && recipe.BaseCard != null
                    && recipe.UpgradedCard != null
                    && RunDeckManager.Instance != null
                    && RunDeckManager.Instance.Contains(recipe.BaseCard);

            default:
                return false;
        }
    }

    private bool AddNewCard(CardRewardOption option)
    {
        if (option.Card == null) return false;

        RunDeckManager.Instance.AddCard(option.Card);
        Gameseed26.Logger.Log($"Reward chosen: added {option.Card.Title}");
        return true;
    }

    private bool ApplyUpgrade(CardRewardOption option)
    {
        CardUpgradeRecipe recipe = option.UpgradeRecipe;
        if (recipe == null || recipe.BaseCard == null || recipe.UpgradedCard == null) return false;

        bool replaced = RunDeckManager.Instance.ReplaceFirst(recipe.BaseCard, recipe.UpgradedCard);
        if (!replaced)
        {
            Gameseed26.Logger.LogWarning($"Could not apply reward upgrade {recipe.BaseCard.Title}; the base card is not in the run deck.");
            return false;
        }

        Gameseed26.Logger.Log($"Reward chosen: upgraded {recipe.BaseCard.Title} -> {recipe.UpgradedCard.Title}");
        return true;
    }

    private void CollectOptionViews()
    {
        optionViews.Clear();

        if (autoFindOptionsInChildren)
        {
            Transform root = optionsParent != null ? optionsParent : transform;
            optionViews.AddRange(root.GetComponentsInChildren<CardRewardOptionView>(true));
        }

        optionViews.RemoveAll(optionView => optionView == null);
    }

    private void EnsureOptionViewCount(int targetCount)
    {
        if (targetCount <= optionViews.Count || optionPrefab == null) return;

        Transform parent = optionsParent != null ? optionsParent : transform;
        while (optionViews.Count < targetCount)
        {
            CardRewardOptionView optionView = Instantiate(optionPrefab, parent);
            optionViews.Add(optionView);
        }
    }

    private void ClearOptionViews()
    {
        foreach (CardRewardOptionView optionView in optionViews)
        {
            if (optionView != null)
            {
                optionView.Clear();
            }
        }
    }

    private static void EnsureRunDeckManagerExists()
    {
        if (RunDeckManager.Instance != null) return;

        GameObject deckManagerObject = new("Run Deck Manager");
        deckManagerObject.AddComponent<RunDeckManager>();
    }
}
