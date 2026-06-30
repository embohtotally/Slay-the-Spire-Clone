using Gameseed26;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class RestSiteController : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField, Scene] private string mapSceneName = "Map";
    [SerializeField] private bool disableActionsAfterUse = true;
    [SerializeField] private bool allowMultipleActions;

    [Header("Available Actions")]
    [SerializeField] private bool allowRest = true;
    [SerializeField] private bool allowStressReduction = true;
    [SerializeField] private bool allowCardUpgrade = true;
    [SerializeField] private bool allowCardRemoval;

    [Header("Rest / Healing")]
    [Tooltip("If above 0, RestHeal heals this fixed amount.")]
    [Min(0)][SerializeField] private int flatHealAmount;
    [Tooltip("Used only when Flat Heal Amount is 0. 0.3 = heal 30% of max HP.")]
    [Range(0f, 1f)][SerializeField] private float healPercent = 0.3f;

    [Header("Stress")]
    [Min(0)][SerializeField] private int stressReduction = 25;

    [Header("Configured Card Upgrade")]
    [SerializeField] private bool allowDeckEditing = true;
    [SerializeField] private CardData upgradeSourceCard;
    [SerializeField] private CardData upgradeTargetCard;

    [Header("Configured Card Removal")]
    [SerializeField] private CardData removeCard;

    [Header("Logging")]
    [SerializeField] private bool logActionFailures = true;

    [Header("Events")]
    public UnityEvent OnActionUsed;
    public UnityEvent OnRestUsed;
    public UnityEvent OnStressReduced;
    public UnityEvent OnCardUpgraded;
    public UnityEvent OnCardRemoved;
    public UnityEvent OnReturnToMapRequested;

    [Header("Debug")]
    [ReadOnly][SerializeField] private bool actionAlreadyUsed;

    public bool CanUseAction => allowMultipleActions || !actionAlreadyUsed;

    public void SetDeckEditingAllowed(bool allowed)
    {
        allowDeckEditing = allowed;
    }

    public void SetAllowMultipleActions(bool allowed)
    {
        allowMultipleActions = allowed;
    }

    [Button("Rest / Heal", EButtonEnableMode.Playmode)]
    public void RestHeal()
    {
        if (!CanRunAction(allowRest, "Rest is disabled.")) return;
        if (!TryGetRunManager(out RunManager runManager)) return;
        if (!runManager.HasHeroState)
        {
            LogFailure("Cannot rest because the run has no hero state yet.");
            return;
        }

        int healAmount = flatHealAmount > 0
            ? flatHealAmount
            : Mathf.CeilToInt(runManager.HeroMaxHealth * Mathf.Clamp01(healPercent));

        if (healAmount > 0)
        {
            runManager.HealHero(healAmount);
        }
        else
        {
            runManager.HealHeroToFull();
        }

        MarkActionUsed();
        OnRestUsed?.Invoke();
    }

    [Button("Reduce Stress", EButtonEnableMode.Playmode)]
    public void ReduceStress()
    {
        if (!CanRunAction(allowStressReduction, "Stress reduction is disabled.")) return;
        if (!TryGetRunManager(out RunManager runManager)) return;
        if (!runManager.HasHeroState)
        {
            LogFailure("Cannot reduce stress because the run has no hero state yet.");
            return;
        }

        if (stressReduction > 0)
        {
            runManager.ReduceStress(stressReduction);
        }
        else
        {
            runManager.ClearStress();
        }

        MarkActionUsed();
        OnStressReduced?.Invoke();
    }

    [Button("Upgrade Configured Card", EButtonEnableMode.Playmode)]
    public void UpgradeConfiguredCard()
    {
        if (!CanRunAction(allowCardUpgrade, "Card upgrade is disabled.")) return;
        if (!CanEditDeck()) return;
        if (!TryGetRunDeckManager(out RunDeckManager deckManager)) return;

        if (!deckManager.ReplaceFirst(upgradeSourceCard, upgradeTargetCard))
        {
            string sourceName = upgradeSourceCard != null ? upgradeSourceCard.Title : "<missing source card>";
            string targetName = upgradeTargetCard != null ? upgradeTargetCard.Title : "<missing target card>";
            LogFailure($"Could not upgrade '{sourceName}' to '{targetName}'. Check assigned cards and current run deck.");
            return;
        }

        MarkActionUsed();
        OnCardUpgraded?.Invoke();
    }

    [Button("Remove Configured Card", EButtonEnableMode.Playmode)]
    public void RemoveConfiguredCard()
    {
        if (!CanRunAction(allowCardRemoval, "Card removal is disabled.")) return;
        if (!CanEditDeck()) return;
        if (!TryGetRunDeckManager(out RunDeckManager deckManager)) return;

        if (!deckManager.RemoveFirst(removeCard))
        {
            string cardName = removeCard != null ? removeCard.Title : "<missing card>";
            LogFailure($"Could not remove '{cardName}'. Check assigned card and current run deck.");
            return;
        }

        MarkActionUsed();
        OnCardRemoved?.Invoke();
    }

    public void ConfigureUpgrade(CardData sourceCard, CardData targetCard)
    {
        upgradeSourceCard = sourceCard;
        upgradeTargetCard = targetCard;
    }

    public void ConfigureRemoval(CardData cardData)
    {
        removeCard = cardData;
    }

    public void ResetActionUsage()
    {
        actionAlreadyUsed = false;
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

    private bool CanRunAction(bool actionEnabled, string disabledMessage)
    {
        if (!actionEnabled)
        {
            LogFailure(disabledMessage);
            return false;
        }

        if (CanUseAction) return true;

        LogFailure("A rest site action has already been used.");
        return false;
    }

    private bool CanEditDeck()
    {
        if (allowDeckEditing) return true;

        LogFailure("Deck editing is disabled on this RestSiteController.");
        return false;
    }

    private void MarkActionUsed()
    {
        actionAlreadyUsed = true;
        OnActionUsed?.Invoke();

        if (disableActionsAfterUse && !allowMultipleActions)
        {
            allowRest = false;
            allowStressReduction = false;
            allowCardUpgrade = false;
            allowCardRemoval = false;
        }
    }

    private bool TryGetRunManager(out RunManager runManager)
    {
        runManager = RunManager.Instance;
        if (runManager != null) return true;

        LogFailure("RestSiteController could not find a RunManager.");
        return false;
    }

    private bool TryGetRunDeckManager(out RunDeckManager deckManager)
    {
        deckManager = RunDeckManager.Instance;
        if (deckManager != null) return true;

        LogFailure("RestSiteController could not find a RunDeckManager.");
        return false;
    }

    private void LogFailure(string message)
    {
        if (!logActionFailures) return;
        Gameseed26.Logger.Log(this, message);
    }
}
