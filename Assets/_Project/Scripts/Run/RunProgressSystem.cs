using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class RunProgressSystem : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] private string mapSceneName = "Map";
    [SerializeField] private float returnToMapDelay = 0.75f;

    [Header("Combat Win Events")]
    [SerializeField] private bool openRewardSceneAfterCombatWin = false;
    [SerializeField] private string rewardSceneName = "Card Reward";
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
            Debug.Log("Combat won. No active run exists, so staying in combat scene for direct-scene testing.");
            yield break;
        }

        RunManager.Instance.CompleteCurrentEncounter();
        OnCombatWon?.Invoke();

        if (openRewardSceneAfterCombatWin && !string.IsNullOrWhiteSpace(rewardSceneName))
        {
            Debug.Log("Combat won. Opening card reward scene.");
            yield return new WaitForSeconds(returnToMapDelay);
            SceneManager.LoadScene(rewardSceneName);
            yield break;
        }

        Debug.Log("Combat won. Returning to map.");
        yield return new WaitForSeconds(returnToMapDelay);
        SceneManager.LoadScene(mapSceneName);
    }
}
