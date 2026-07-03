using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Gameseed26;
[DisallowMultipleComponent]
public class RunPlayerStateInitializer : MonoBehaviour
{
    [SerializeField] private List<HeroData> heroTeam = new();
    [SerializeField] private int maxStress = 100;
    [SerializeField] private bool initializeOnStart = true;
    [SerializeField] private bool onlyWhenActiveRun = true;

    [Header("Run Deck")]
    [Tooltip("Useful for direct scene debugging: fills RunDeckManager from the assigned HeroData decks, matching MatchSetupSystem's starting deck behavior without entering the Game scene first.")]
    [SerializeField] private bool initializeRunDeckFromHeroes = true;
    [SerializeField] private bool createRunDeckManagerIfMissing = true;
    [Tooltip("Off by default so normal gameplay does not overwrite an existing run deck. Turn on only for isolated debug scenes.")]
    [SerializeField] private bool resetRunDeckOnInitialize;

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

        if (runManager == null)
        {
            Gameseed26.Logger.LogWarning(this, "RunPlayerStateInitializer could not find a RunManager.");
        }

        if (runManager != null && onlyWhenActiveRun && !runManager.HasActiveRun)
        {
            return;
        }

        if (runManager != null)
        {
            int totalHealth = GetTotalHeroHealth();
            if (totalHealth <= 0)
            {
                Gameseed26.Logger.LogWarning(this, "RunPlayerStateInitializer needs at least one HeroData with Health above 0.");
                return;
            }

            runManager.InitializeHeroState(heroTeam, maxStress);
        }

        InitializeRunDeckIfNeeded();
    }

    private void InitializeRunDeckIfNeeded()
    {
        if (!initializeRunDeckFromHeroes) return;
        if (onlyWhenActiveRun && (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)) return;

        RunDeckManager deckManager = RunDeckManager.Instance;
        if (deckManager == null && createRunDeckManagerIfMissing)
        {
            GameObject deckManagerObject = new("Run Deck Manager");
            deckManager = deckManagerObject.AddComponent<RunDeckManager>();
        }

        if (deckManager == null)
        {
            Gameseed26.Logger.LogWarning(this, "RunPlayerStateInitializer could not find a RunDeckManager.");
            return;
        }

        List<CardData> combinedDeck = BuildStartingDeck();
        if (combinedDeck.Count == 0)
        {
            Gameseed26.Logger.LogWarning(this, "RunPlayerStateInitializer found no cards in the assigned HeroData decks.");
            return;
        }

        if (resetRunDeckOnInitialize)
        {
            deckManager.ResetDeck(combinedDeck);
        }
        else
        {
            deckManager.InitializeIfEmpty(combinedDeck);
        }
    }

    private List<CardData> BuildStartingDeck()
    {
        List<CardData> combinedDeck = new();
        foreach (HeroData hero in heroTeam)
        {
            if (hero?.Deck == null) continue;
            foreach (CardData cardData in hero.Deck)
            {
                if (cardData != null)
                {
                    combinedDeck.Add(cardData);
                }
            }
        }

        return combinedDeck;
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
