using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RunPlayerStateInitializer : MonoBehaviour
{
    [SerializeField] private List<HeroData> heroTeam = new();
    [SerializeField] private int maxStress = 100;
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool onlyWhenActiveRun = true;
    [SerializeField] private bool createRunManagerIfMissing = true;

    private IEnumerator Start()
    {
        if (initializeOnStart)
        {
            // Wait one frame so map controllers can create/start the run first.
            yield return null;
            InitializeRunPlayerState();
        }
    }

    public void InitializeRunPlayerState()
    {
        RunManager runManager = RunManager.Instance;
        if (runManager == null && createRunManagerIfMissing)
        {
            GameObject runManagerObject = new("Run Manager");
            runManager = runManagerObject.AddComponent<RunManager>();
        }

        if (runManager == null)
        {
            Debug.LogWarning("RunPlayerStateInitializer could not find a RunManager.", this);
            return;
        }

        if (onlyWhenActiveRun && !runManager.HasActiveRun)
        {
            return;
        }

        int totalHealth = GetTotalHeroHealth();
        if (totalHealth <= 0)
        {
            Debug.LogWarning("RunPlayerStateInitializer needs at least one HeroData with Health above 0.", this);
            return;
        }

        runManager.InitializeHeroState(totalHealth, maxStress);
    }

    private int GetTotalHeroHealth()
    {
        int totalHealth = 0;
        foreach (HeroData heroData in heroTeam)
        {
            if (heroData != null)
            {
                totalHealth += heroData.Health;
            }
        }

        return totalHealth;
    }
}
