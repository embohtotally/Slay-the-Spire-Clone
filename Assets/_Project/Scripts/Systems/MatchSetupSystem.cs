using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MatchSetupSystem : MonoBehaviour
{
    [SerializeField] private List<HeroData> heroTeam;
    [SerializeField] private List<EnemyData> enemyDataList;
    [SerializeField] private int startingHandSize = 5;

    private void Start()
    {
        HeroSystem.Instance.Setup(heroTeam);

        List<EnemyData> enemiesForThisMatch = enemyDataList;
        if (RunManager.Instance != null && RunManager.Instance.SelectedEncounter != null)
        {
            enemiesForThisMatch = RunManager.Instance.SelectedEncounter.Enemies;
        }

        EnemySystem.Instance.Setup(enemiesForThisMatch);
        List<CardData> combinedDeck = new List<CardData>();
        foreach (var hero in heroTeam)
        {
            if (hero.Deck != null)
                combinedDeck.AddRange(hero.Deck);
        }
        CardSystem.Instance.Setup(combinedDeck);

        foreach (var hero in heroTeam)
        {
            if (hero.StartingPerks != null)
            {
                foreach (PerkData perkData in hero.StartingPerks)
                {
                    PerkSystem.Instance.AddPerk(new Perk(perkData));
                }
            }
        }
        DrawCardsGA drawCardsGA = new(startingHandSize);
        ActionSystem.Instance.Perform(drawCardsGA);
    }
}