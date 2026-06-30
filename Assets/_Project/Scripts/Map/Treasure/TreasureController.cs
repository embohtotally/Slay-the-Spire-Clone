using System.Collections;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class TreasureController : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] private string mapSceneName = "Map";
    [SerializeField] private string cardRewardSceneName = "CardReward";
    [SerializeField] private bool autoOpenOnStart = true;
    [SerializeField] private bool allowMultipleClaims;
    [SerializeField] private bool disableAfterClaim = true;

    [Header("Rewards")]
    [Min(0)] [SerializeField] private int goldAmount = 50;
    [Min(0)] [SerializeField] private int healAmount;
    [Min(0)] [SerializeField] private int stressReduction;
    [SerializeField] private bool grantCardReward;
    [SerializeField] private CardRewardRequest cardRewardRequest = new();

    [Header("Logging")]
    [SerializeField] private bool logActionFailures = true;

    [Header("Events")]
    public UnityEvent OnTreasureOpened;
    public UnityEvent OnTreasureClaimed;
    public UnityEvent OnGoldClaimed;
    public UnityEvent OnHealClaimed;
    public UnityEvent OnStressReduced;
    public UnityEvent OnCardRewardStarted;
    public UnityEvent OnCardRewardFinished;
    public UnityEvent OnTreasureCompleted;
    public UnityEvent OnReturnToMapRequested;

    [Header("Debug")]
    [ReadOnly] [SerializeField] private bool treasureOpened;
    [ReadOnly] [SerializeField] private bool treasureClaimed;
    [ReadOnly] [SerializeField] private bool cardRewardInProgress;

    public bool CanClaim => allowMultipleClaims || !treasureClaimed;

    private void Start()
    {
        if (autoOpenOnStart)
        {
            OpenTreasure();
        }
    }

    [Button("Open Treasure", EButtonEnableMode.Playmode)]
    public void OpenTreasure()
    {
        treasureOpened = true;
        OnTreasureOpened?.Invoke();
    }

    [Button("Claim Treasure", EButtonEnableMode.Playmode)]
    public void ClaimTreasure()
    {
        if (!CanStartClaim()) return;

        ApplyImmediateRewards();
        treasureClaimed = true;
        OnTreasureClaimed?.Invoke();

        if (grantCardReward)
        {
            StartCoroutine(OpenCardRewardAndComplete());
            return;
        }

        CompleteTreasure();
    }

    [Button("Claim Gold Only", EButtonEnableMode.Playmode)]
    public void ClaimGoldOnly()
    {
        if (!CanStartClaim()) return;
        GrantGold();
        treasureClaimed = true;
        OnTreasureClaimed?.Invoke();
        CompleteTreasure();
    }

    [Button("Open Card Reward Only", EButtonEnableMode.Playmode)]
    public void OpenCardRewardOnly()
    {
        if (!CanStartClaim()) return;
        treasureClaimed = true;
        OnTreasureClaimed?.Invoke();
        StartCoroutine(OpenCardRewardAndComplete());
    }

    public void SetGoldAmount(int amount)
    {
        goldAmount = Mathf.Max(0, amount);
    }

    public void SetHealAmount(int amount)
    {
        healAmount = Mathf.Max(0, amount);
    }

    public void SetStressReduction(int amount)
    {
        stressReduction = Mathf.Max(0, amount);
    }

    public void SetGrantCardReward(bool enabled)
    {
        grantCardReward = enabled;
    }

    public void SetAllowMultipleClaims(bool enabled)
    {
        allowMultipleClaims = enabled;
    }

    public void ResetClaimState()
    {
        treasureClaimed = false;
        cardRewardInProgress = false;
    }

    public void ReturnToMap()
    {
        OnReturnToMapRequested?.Invoke();

        if (string.IsNullOrWhiteSpace(mapSceneName))
        {
            LogFailure("Cannot return to map because Map Scene Name is empty.");
            return;
        }

        SceneLoader.LoadScene(mapSceneName);
    }

    private bool CanStartClaim()
    {
        if (cardRewardInProgress)
        {
            LogFailure("Cannot claim treasure while card reward is still in progress.");
            return false;
        }

        if (CanClaim) return true;

        LogFailure("Treasure has already been claimed.");
        return false;
    }

    private void ApplyImmediateRewards()
    {
        GrantGold();
        GrantHeal();
        GrantStressReduction();
    }

    private void GrantGold()
    {
        if (goldAmount <= 0) return;
        if (!TryGetRunManager(out RunManager runManager)) return;

        runManager.AddGold(goldAmount);
        OnGoldClaimed?.Invoke();
    }

    private void GrantHeal()
    {
        if (healAmount <= 0) return;
        if (!TryGetRunManager(out RunManager runManager)) return;

        runManager.HealHero(healAmount);
        OnHealClaimed?.Invoke();
    }

    private void GrantStressReduction()
    {
        if (stressReduction <= 0) return;
        if (!TryGetRunManager(out RunManager runManager)) return;

        runManager.ReduceStress(stressReduction);
        OnStressReduced?.Invoke();
    }

    private IEnumerator OpenCardRewardAndComplete()
    {
        if (string.IsNullOrWhiteSpace(cardRewardSceneName))
        {
            LogFailure("Cannot open card reward because Card Reward Scene Name is empty.");
            CompleteTreasure();
            yield break;
        }

        cardRewardInProgress = true;
        OnCardRewardStarted?.Invoke();

        AsyncOperation loadOperation = SceneLoader.LoadSceneAdditive(cardRewardSceneName);
        if (loadOperation == null)
        {
            cardRewardInProgress = false;
            CompleteTreasure();
            yield break;
        }

        yield return loadOperation;

        CardRewardController cardRewardController = FindCardRewardController(cardRewardSceneName);
        if (cardRewardController == null)
        {
            LogFailure($"Could not find a CardRewardController in additive scene '{cardRewardSceneName}'.");
            cardRewardInProgress = false;
            CompleteTreasure();
            yield break;
        }

        bool finished = false;
        void HandleCardRewardFinished(bool _)
        {
            finished = true;
        }

        cardRewardController.RewardFinished += HandleCardRewardFinished;
        cardRewardController.ConfigureForAdditiveReward(cardRewardRequest ?? new CardRewardRequest());

        while (!finished)
        {
            yield return null;
        }

        if (cardRewardController != null)
        {
            cardRewardController.RewardFinished -= HandleCardRewardFinished;
        }

        cardRewardInProgress = false;
        OnCardRewardFinished?.Invoke();
        CompleteTreasure();
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

    private void CompleteTreasure()
    {
        OnTreasureCompleted?.Invoke();

        if (disableAfterClaim && !allowMultipleClaims)
        {
            enabled = false;
        }
    }

    private bool TryGetRunManager(out RunManager runManager)
    {
        runManager = RunManager.Instance;
        if (runManager != null) return true;

        LogFailure("TreasureController could not find a RunManager.");
        return false;
    }

    private void LogFailure(string message)
    {
        if (!logActionFailures) return;
        Gameseed26.Logger.Log(this, message);
    }
}
