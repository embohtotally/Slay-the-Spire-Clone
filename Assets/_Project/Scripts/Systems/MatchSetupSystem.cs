using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MatchSetupSystem : MonoBehaviour
{
    [SerializeField] private HeroData heroData;
    [SerializeField] private List<EnemyData> enemyDataList;
    [SerializeField] private int startingHandSize = 5;

    private void Start()
    {
        HeroSystem.Instance.Setup(heroData);

        List<EnemyData> enemiesForThisMatch = enemyDataList;
        if (RunManager.Instance != null && RunManager.Instance.SelectedEncounter != null)
        {
            enemiesForThisMatch = RunManager.Instance.SelectedEncounter.Enemies;
        }

        EnemySystem.Instance.Setup(enemiesForThisMatch);
        CardSystem.Instance.Setup(heroData.Deck);

        if (heroData.StartingPerks != null)
        {
            foreach (PerkData perkData in heroData.StartingPerks)
            {
                PerkSystem.Instance.AddPerk(new Perk(perkData));
            }
        }
        DrawCardsGA drawCardsGA = new(startingHandSize);
        ActionSystem.Instance.Perform(drawCardsGA);
    }
}