using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunProgressSystem : MonoBehaviour
{
    [SerializeField] private string mapSceneName = "Map";
    [SerializeField] private float returnToMapDelay = 0.75f;

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
        Debug.Log("Combat won. Returning to map.");
        yield return new WaitForSeconds(returnToMapDelay);
        SceneManager.LoadScene(mapSceneName);
    }
}
