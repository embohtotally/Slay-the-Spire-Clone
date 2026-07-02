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
    [SerializeField] private Gameseed26.SfxID yourTurnSfx = Gameseed26.SfxID.YourTurn;

    private void Start()
    {
        HeroSystem.Instance.Setup(heroTeam);

        List<EnemyData> enemiesForThisMatch = enemyDataList;
        if (RunManager.Instance != null && RunManager.Instance.SelectedEncounter != null)
        {
            enemiesForThisMatch = RunManager.Instance.SelectedEncounter.Enemies;
        }

        EnemySystem.Instance.Setup(enemiesForThisMatch);

        List<CardData> combinedDeck = BuildStartingDeck();
        EnsureRunDeckManagerExists();
        RunDeckManager.Instance.InitializeIfEmpty(combinedDeck);
        CardSystem.Instance.Setup(RunDeckManager.Instance.GetDeckCopy());

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
        ActionSystem.Instance.Perform(new CombatStartedGA(), DrawStartingHand);
    }

    private void DrawStartingHand()
    {
        if (yourTurnSfx != Gameseed26.SfxID.None) Gameseed26.Tune.SFX(yourTurnSfx);
        DrawCardsGA drawCardsGA = new(startingHandSize);
        ActionSystem.Instance.Perform(drawCardsGA);
    }

    private List<CardData> BuildStartingDeck()
    {
        List<CardData> combinedDeck = new();
        foreach (HeroData hero in heroTeam)
        {
            if (hero != null && hero.Deck != null)
            {
                combinedDeck.AddRange(hero.Deck);
            }
        }

        return combinedDeck;
    }

    private static void EnsureRunDeckManagerExists()
    {
        if (RunDeckManager.Instance != null) return;

        GameObject deckManagerObject = new("Run Deck Manager");
        deckManagerObject.AddComponent<RunDeckManager>();
    }
}
