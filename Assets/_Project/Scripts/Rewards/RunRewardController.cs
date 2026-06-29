using System;
using System.Collections;
using System.Collections.Generic;
using Gameseed26;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public enum RunRewardType
{
    Gold,
    Heal,
    ReduceStress,
    CardReward
}

[Serializable]
public class RunRewardDefinition
{
    public RunRewardType Type = RunRewardType.CardReward;
    public string TitleOverride;
    [TextArea(2, 4)] public string DescriptionOverride;
    [Min(0)] public int Amount = 20;
    public CardRewardRequest CardRewardRequest = new();

    public string GetTitle()
    {
        if (!string.IsNullOrWhiteSpace(TitleOverride)) return TitleOverride;

        return Type switch
        {
            RunRewardType.Gold => $"{Amount} Gold",
            RunRewardType.Heal => Amount > 0 ? $"Heal {Amount} HP" : "Full Heal",
            RunRewardType.ReduceStress => Amount > 0 ? $"Reduce {Amount} Stress" : "Clear Stress",
            RunRewardType.CardReward => "Card Reward",
            _ => "Reward"
        };
    }

    public string GetDescription()
    {
        if (!string.IsNullOrWhiteSpace(DescriptionOverride)) return DescriptionOverride;

        return Type switch
        {
            RunRewardType.Gold => $"Gain {Amount} gold for this run.",
            RunRewardType.Heal => Amount > 0 ? $"Restore {Amount} HP." : "Restore HP to full.",
            RunRewardType.ReduceStress => Amount > 0 ? $"Reduce stress by {Amount}." : "Remove all current stress.",
            RunRewardType.CardReward => "Choose one card to add to your run deck. You may skip the card choice.",
            _ => string.Empty
        };
    }
}

[DisallowMultipleComponent]
public class RunRewardController : MonoBehaviour
{
    [Header("Reward Source")]
    [SerializeField] private List<RunRewardDefinition> defaultRewards = new()
    {
        new RunRewardDefinition { Type = RunRewardType.Gold, Amount = 20 },
        new RunRewardDefinition { Type = RunRewardType.CardReward }
    };

    [Header("Card Reward Scene")]
    [SerializeField] private string cardRewardSceneName = "CardReward";

    [Header("UI")]
    [SerializeField] private GameObject rewardRoot;
    [SerializeField] private Transform rewardContainer;
    [SerializeField] private RunRewardOptionView rewardOptionPrefab;
    [SerializeField] private bool autoFindOptionViewsInChildren = true;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private string title = "Rewards";

    [Header("Navigation")]
    [SerializeField] private string mapSceneName = "Map";
    [SerializeField] private bool autoOpenOnStart = true;

    [Header("Events")]
    public UnityEvent OnRewardsOpened;
    public UnityEvent OnRewardClaimed;
    public UnityEvent OnRewardsSkipped;
    public UnityEvent OnRewardsCompleted;

    private readonly List<RunRewardDefinition> activeRewards = new();
    private readonly List<RunRewardOptionView> optionViews = new();
    private readonly HashSet<int> claimedRewardIndices = new();
    private bool isOpeningCardReward;

    private void Awake()
    {
        if (rewardRoot == null) rewardRoot = gameObject;
        if (rewardContainer == null) rewardContainer = transform;
        CollectOptionViews();
    }

    private void Start()
    {
        if (autoOpenOnStart)
        {
            OpenRewards();
        }
    }

    public void OpenRewards()
    {
        OpenRewards(defaultRewards);
    }

    public void OpenRewards(List<RunRewardDefinition> rewards)
    {
        activeRewards.Clear();
        claimedRewardIndices.Clear();

        if (rewards != null)
        {
            foreach (RunRewardDefinition reward in rewards)
            {
                if (reward != null) activeRewards.Add(reward);
            }
        }

        if (rewardRoot != null) rewardRoot.SetActive(true);
        if (titleText != null) titleText.text = title;

        RefreshRewardViews();
        OnRewardsOpened?.Invoke();
    }

    public void ClaimReward(int rewardIndex)
    {
        if (isOpeningCardReward) return;
        if (rewardIndex < 0 || rewardIndex >= activeRewards.Count) return;
        if (claimedRewardIndices.Contains(rewardIndex)) return;

        RunRewardDefinition reward = activeRewards[rewardIndex];
        if (reward == null) return;

        switch (reward.Type)
        {
            case RunRewardType.Gold:
                if (!TryGetRunManager(out RunManager goldRunManager)) return;
                goldRunManager.AddGold(reward.Amount);
                MarkRewardClaimed(rewardIndex);
                break;

            case RunRewardType.Heal:
                if (!TryGetRunManager(out RunManager healRunManager)) return;
                if (reward.Amount > 0) healRunManager.HealHero(reward.Amount);
                else healRunManager.HealHeroToFull();
                MarkRewardClaimed(rewardIndex);
                break;

            case RunRewardType.ReduceStress:
                if (!TryGetRunManager(out RunManager stressRunManager)) return;
                if (reward.Amount > 0) stressRunManager.ReduceStress(reward.Amount);
                else stressRunManager.ClearStress();
                MarkRewardClaimed(rewardIndex);
                break;

            case RunRewardType.CardReward:
                StartCoroutine(OpenCardRewardScene(rewardIndex, reward.CardRewardRequest));
                break;
        }
    }

    public void SkipRemainingRewards()
    {
        OnRewardsSkipped?.Invoke();
        ContinueToMap();
    }

    public void ContinueToMap()
    {
        OnRewardsCompleted?.Invoke();
        if (!string.IsNullOrWhiteSpace(mapSceneName))
        {
            SceneLoader.LoadScene(mapSceneName);
        }
    }

    private IEnumerator OpenCardRewardScene(int rewardIndex, CardRewardRequest request)
    {
        isOpeningCardReward = true;

        AsyncOperation loadOperation = SceneLoader.LoadSceneAdditive(cardRewardSceneName);
        if (loadOperation == null)
        {
            isOpeningCardReward = false;
            yield break;
        }

        yield return loadOperation;

        CardRewardController cardRewardController = FindCardRewardController(cardRewardSceneName);
        if (cardRewardController == null)
        {
            Gameseed26.Logger.LogWarning($"Could not find a CardRewardController in additive scene '{cardRewardSceneName}'.");
            isOpeningCardReward = false;
            yield break;
        }

        bool finished = false;
        void HandleCardRewardFinished(bool _)
        {
            finished = true;
        }

        cardRewardController.RewardFinished += HandleCardRewardFinished;
        cardRewardController.ConfigureForAdditiveReward(request ?? new CardRewardRequest());

        while (!finished)
        {
            yield return null;
        }

        if (cardRewardController != null)
        {
            cardRewardController.RewardFinished -= HandleCardRewardFinished;
        }

        MarkRewardClaimed(rewardIndex);
        isOpeningCardReward = false;
    }

    private CardRewardController FindCardRewardController(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                CardRewardController controller = rootObject.GetComponentInChildren<CardRewardController>(true);
                if (controller != null) return controller;
            }
        }

        return FindFirstObjectByType<CardRewardController>(FindObjectsInactive.Include);
    }

    private void MarkRewardClaimed(int rewardIndex)
    {
        claimedRewardIndices.Add(rewardIndex);
        RefreshRewardViews();
        OnRewardClaimed?.Invoke();
    }

    private void RefreshRewardViews()
    {
        CollectOptionViews();
        EnsureOptionViewCount(activeRewards.Count);

        for (int i = 0; i < optionViews.Count; i++)
        {
            if (i < activeRewards.Count)
            {
                optionViews[i].Setup(this, i, activeRewards[i], claimedRewardIndices.Contains(i));
            }
            else
            {
                optionViews[i].Clear();
            }
        }
    }

    private void CollectOptionViews()
    {
        optionViews.Clear();

        if (autoFindOptionViewsInChildren)
        {
            Transform root = rewardContainer != null ? rewardContainer : transform;
            optionViews.AddRange(root.GetComponentsInChildren<RunRewardOptionView>(true));
        }

        optionViews.RemoveAll(view => view == null);
    }

    private void EnsureOptionViewCount(int targetCount)
    {
        if (rewardOptionPrefab == null) return;
        if (targetCount <= optionViews.Count) return;

        Transform parent = rewardContainer != null ? rewardContainer : transform;
        while (optionViews.Count < targetCount)
        {
            RunRewardOptionView optionView = Instantiate(rewardOptionPrefab, parent);
            optionViews.Add(optionView);
        }
    }

    private static bool TryGetRunManager(out RunManager runManager)
    {
        runManager = RunManager.Instance;
        if (runManager != null) return true;

        Gameseed26.Logger.LogWarning("RunRewardController could not find RunManager. Keep persistent run systems on Resources/GameManager.");
        return false;
    }
}
