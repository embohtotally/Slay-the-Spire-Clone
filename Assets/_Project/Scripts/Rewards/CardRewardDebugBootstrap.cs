using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gameseed26;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class CardRewardDebugBootstrap : MonoBehaviour
{
    [Header("Safety")]
    [SerializeField] private bool enableDebugBootstrap = true;
    [Tooltip("Keep this on so the debug deck/reward setup only runs when CardReward is opened directly, not when loaded additively from RunRewards/Event/Treasure.")]
    [SerializeField] private bool standaloneSceneOnly = true;
    [SerializeField, Min(0)] private int waitFramesBeforeSetup = 1;

    [Header("Debug Deck")]
    [SerializeField] private bool createRunDeckManagerIfMissing = true;
    [SerializeField] private bool resetDeckOnStart = true;
    [SerializeField] private List<HeroData> heroesToReadDeckFrom = new();
    [SerializeField] private List<CardData> extraStartingCards = new();

    [Header("Debug UI")]
    [SerializeField] private bool openCardRewardAfterSetup = true;
    [SerializeField] private bool openDeckPanelAfterSetup = true;
    [SerializeField] private CardRewardController cardRewardController;
    [SerializeField] private RunDeckPanelController runDeckPanel;

    private IEnumerator Start()
    {
        if (!enableDebugBootstrap) yield break;
        if (standaloneSceneOnly && SceneManager.sceneCount > 1) yield break;

        for (int i = 0; i < waitFramesBeforeSetup; i++)
        {
            yield return null;
        }

        EnsureRunDeckManagerIfWanted();
        InitializeDebugDeck();
        ResolveSceneReferences();

        if (openCardRewardAfterSetup && cardRewardController != null)
        {
            cardRewardController.OpenReward();
        }

        if (openDeckPanelAfterSetup && runDeckPanel != null)
        {
            runDeckPanel.OpenPanel();
        }
        else if (runDeckPanel != null)
        {
            runDeckPanel.RefreshDeckView();
        }
    }

    [NaughtyAttributes.Button("Run Debug Bootstrap", NaughtyAttributes.EButtonEnableMode.Playmode)]
    public void RunDebugBootstrapNow()
    {
        if (!enableDebugBootstrap) return;
        EnsureRunDeckManagerIfWanted();
        InitializeDebugDeck();
        ResolveSceneReferences();
        if (cardRewardController != null) cardRewardController.OpenReward();
        if (runDeckPanel != null) runDeckPanel.OpenPanel();
    }

    private void InitializeDebugDeck()
    {
        RunDeckManager deckManager = RunDeckManager.Instance;
        if (deckManager == null)
        {
            Gameseed26.Logger.LogWarning(this, "CardRewardDebugBootstrap could not find or create a RunDeckManager.");
            return;
        }

        List<CardData> debugDeck = BuildDebugDeck();
        if (debugDeck.Count == 0)
        {
            Gameseed26.Logger.LogWarning(this, "CardRewardDebugBootstrap has no debug cards. Assign Heroes To Read Deck From or Extra Starting Cards.");
            return;
        }

        if (resetDeckOnStart)
        {
            deckManager.ResetDeck(debugDeck);
        }
        else
        {
            deckManager.InitializeIfEmpty(debugDeck);
        }

        deckManager.NotifyDeckChanged();
        Gameseed26.Logger.Log(this, $"CardReward debug deck ready: {deckManager.CurrentDeck.Count} cards.");
    }

    private List<CardData> BuildDebugDeck()
    {
        List<CardData> debugDeck = new();
        foreach (HeroData hero in heroesToReadDeckFrom)
        {
            if (hero?.Deck == null) continue;
            debugDeck.AddRange(hero.Deck.Where(card => card != null));
        }

        debugDeck.AddRange(extraStartingCards.Where(card => card != null));
        return debugDeck;
    }

    private void ResolveSceneReferences()
    {
        if (cardRewardController == null)
        {
            cardRewardController = FindFirstObjectByType<CardRewardController>(FindObjectsInactive.Include);
        }

        if (runDeckPanel == null)
        {
            runDeckPanel = FindFirstObjectByType<RunDeckPanelController>(FindObjectsInactive.Include);
        }
    }

    private void EnsureRunDeckManagerIfWanted()
    {
        if (!createRunDeckManagerIfMissing || RunDeckManager.Instance != null) return;

        GameObject deckManagerObject = new("Run Deck Manager");
        deckManagerObject.AddComponent<RunDeckManager>();
    }
}
