using System.Collections;
using Gameseed26;
using UnityEngine;
using UnityEngine.Events;

public class RunProgressSystem : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] private string mapSceneName = "Map";
    [SerializeField] private float returnToMapDelay = 0.75f;

    [Header("Combat Win Rewards")]
    [SerializeField] private bool openRewardSceneAfterCombatWin = true;
    [SerializeField] private string rewardSceneName = "RunRewards";
    [SerializeField] private bool loadRewardSceneAdditive = true;
    public UnityEvent OnCombatWon;

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<CombatWonGA>(CombatWonPerformer);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<CombatWonGA>();
    }

    private IEnumerator CombatWonPerformer(CombatWonGA combatWonGA)
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            Gameseed26.Logger.Log("Combat won. No active run exists, so staying in combat scene for direct-scene testing.");
            yield break;
        }

        RunManager.Instance.CompleteCurrentEncounter();
        OnCombatWon?.Invoke();

        yield return new WaitForSeconds(returnToMapDelay);

        if (openRewardSceneAfterCombatWin && !string.IsNullOrWhiteSpace(rewardSceneName))
        {
            Gameseed26.Logger.Log($"Combat won. Opening reward scene: {rewardSceneName}.");
            if (loadRewardSceneAdditive)
            {
                SceneLoader.LoadSceneAdditive(rewardSceneName);
            }
            else
            {
                SceneLoader.LoadScene(rewardSceneName);
            }

            yield break;
        }

        Gameseed26.Logger.Log("Combat won. Returning to map.");
        SceneLoader.LoadScene(mapSceneName);
    }
}
