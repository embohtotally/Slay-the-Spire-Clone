using NaughtyAttributes;
using UnityEngine;

using Gameseed26;
[DisallowMultipleComponent]
public class RunStateActions : MonoBehaviour
{
    [Header("Deck Editing")]
    [SerializeField] private bool allowDeckEditing = true;
    [SerializeField] private bool logDeckEditFailures = true;

    [Header("Relic Editing")]
    [SerializeField] private bool allowRelicEditing = true;
    [SerializeField] private bool logRelicEditFailures = true;

    [Header("Potion Editing")]
    [SerializeField] private bool allowPotionEditing = true;
    [SerializeField] private bool logPotionEditFailures = true;

    public void HealHero(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.HealHero(amount);
        }
    }

    public void HealHeroToFull()
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.HealHeroToFull();
        }
    }

    public void HealHeroByPercent(float percent)
    {
        if (!TryGetRunManager(out RunManager runManager) || !runManager.HasHeroState) return;

        int healAmount = Mathf.CeilToInt(runManager.HeroMaxHealth * Mathf.Clamp01(percent));
        runManager.HealHero(healAmount);
    }

    public void DamageHero(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.DamageHero(amount);
        }
    }

    public void SetHeroHealth(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.SetHeroHealth(amount);
        }
    }

    public void IncreaseMaxHealth(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ChangeHeroMaxHealth(Mathf.Abs(amount), true);
        }
    }

    public void DecreaseMaxHealth(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ChangeHeroMaxHealth(-Mathf.Abs(amount), false);
        }
    }

    public void AddStress(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.AddStress(amount);
        }
    }

    public void ReduceStress(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ReduceStress(amount);
        }
    }

    public void ClearStress()
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ClearStress();
        }
    }

    public void IncreaseMaxStress(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ChangeMaxStress(Mathf.Abs(amount));
        }
    }

    public void DecreaseMaxStress(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.ChangeMaxStress(-Mathf.Abs(amount));
        }
    }

    public void AddGold(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.AddGold(amount);
        }
    }

    public void SetGold(int amount)
    {
        if (TryGetRunManager(out RunManager runManager))
        {
            runManager.SetGold(amount);
        }
    }

    public void SpendGold(int amount)
    {
        if (TryGetRunManager(out RunManager runManager) && !runManager.SpendGold(amount))
        {
            Gameseed26.Logger.Log($"Not enough gold. Need {amount}, have {runManager.Gold}.");
        }
    }

    public void SetDeckEditingAllowed(bool allowed)
    {
        allowDeckEditing = allowed;
    }

    [Button("Enable Deck Editing", EButtonEnableMode.Playmode)]
    public void EnableDeckEditing()
    {
        allowDeckEditing = true;
    }

    [Button("Disable Deck Editing", EButtonEnableMode.Playmode)]
    public void DisableDeckEditing()
    {
        allowDeckEditing = false;
    }

    public void SetRelicEditingAllowed(bool allowed)
    {
        allowRelicEditing = allowed;
    }

    [Button("Enable Relic Editing", EButtonEnableMode.Playmode)]
    public void EnableRelicEditing()
    {
        allowRelicEditing = true;
    }

    [Button("Disable Relic Editing", EButtonEnableMode.Playmode)]
    public void DisableRelicEditing()
    {
        allowRelicEditing = false;
    }

    public void SetPotionEditingAllowed(bool allowed)
    {
        allowPotionEditing = allowed;
    }

    [Button("Enable Potion Editing", EButtonEnableMode.Playmode)]
    public void EnablePotionEditing()
    {
        allowPotionEditing = true;
    }

    [Button("Disable Potion Editing", EButtonEnableMode.Playmode)]
    public void DisablePotionEditing()
    {
        allowPotionEditing = false;
    }

    public void AddCardToDeck(CardData cardData)
    {
        if (!TryGetRunDeckManager(out RunDeckManager deckManager)) return;
        if (!CanEditDeck()) return;

        deckManager.AddCard(cardData);
    }

    public void RemoveFirstCardFromDeck(CardData cardData)
    {
        if (!TryGetRunDeckManager(out RunDeckManager deckManager)) return;
        if (!CanEditDeck()) return;

        if (!deckManager.RemoveFirst(cardData))
        {
            LogDeckEditFailure(cardData != null
                ? $"Could not remove '{cardData.Title}' because it is not in the current run deck."
                : "Could not remove card because no CardData was assigned.");
        }
    }

    public void RemoveAllCopiesFromDeck(CardData cardData)
    {
        if (!TryGetRunDeckManager(out RunDeckManager deckManager)) return;
        if (!CanEditDeck()) return;

        int removedCount = deckManager.RemoveAll(cardData);
        if (removedCount <= 0)
        {
            LogDeckEditFailure(cardData != null
                ? $"Could not remove copies of '{cardData.Title}' because it is not in the current run deck."
                : "Could not remove card copies because no CardData was assigned.");
        }
    }

    public void ReplaceFirstCardInDeck(CardData oldCard, CardData newCard)
    {
        if (!TryGetRunDeckManager(out RunDeckManager deckManager)) return;
        if (!CanEditDeck()) return;

        if (!deckManager.ReplaceFirst(oldCard, newCard))
        {
            string oldName = oldCard != null ? oldCard.Title : "<missing old card>";
            string newName = newCard != null ? newCard.Title : "<missing new card>";
            LogDeckEditFailure($"Could not replace '{oldName}' with '{newName}'. Check that both cards are assigned and the old card is in the deck.");
        }
    }

    public void RemoveCardAtDeckIndex(int index)
    {
        if (!TryGetRunDeckManager(out RunDeckManager deckManager)) return;
        if (!CanEditDeck()) return;

        if (!deckManager.RemoveAt(index))
        {
            LogDeckEditFailure($"Could not remove card at deck index {index}.");
        }
    }

    public void AddRelicToRun(RelicData relicData)
    {
        if (!CanEditRelics()) return;

        RunRelicManager relicManager = RunRelicManager.EnsureInstance();
        if (relicManager == null)
        {
            LogRelicEditFailure("Could not create or find a RunRelicManager.");
            return;
        }

        if (!relicManager.AddRelic(relicData))
        {
            LogRelicEditFailure(relicData != null
                ? $"Could not add relic '{relicData.Title}'. It may already be owned if it is unique."
                : "Could not add relic because no RelicData was assigned.");
        }
    }

    public void RemoveRelicFromRun(RelicData relicData)
    {
        if (!CanEditRelics()) return;

        RunRelicManager relicManager = RunRelicManager.EnsureInstance();
        if (relicManager == null)
        {
            LogRelicEditFailure("Could not create or find a RunRelicManager.");
            return;
        }

        if (!relicManager.RemoveRelic(relicData))
        {
            LogRelicEditFailure(relicData != null
                ? $"Could not remove relic '{relicData.Title}' because it is not owned."
                : "Could not remove relic because no RelicData was assigned.");
        }
    }

    public void ClearRunRelics()
    {
        if (!CanEditRelics()) return;

        RunRelicManager relicManager = RunRelicManager.EnsureInstance();
        if (relicManager == null)
        {
            LogRelicEditFailure("Could not create or find a RunRelicManager.");
            return;
        }

        relicManager.ClearRelics();
    }

    public void AddPotionToRun(PotionData potionData)
    {
        if (!CanEditPotions()) return;

        RunPotionManager potionManager = RunPotionManager.EnsureInstance();
        if (potionManager == null)
        {
            LogPotionEditFailure("Could not create or find a RunPotionManager.");
            return;
        }

        if (!potionManager.AddPotion(potionData))
        {
            LogPotionEditFailure(potionData != null
                ? $"Could not add potion '{potionData.Title}'. Potion slots may be full or the potion may already be owned if it is unique."
                : "Could not add potion because no PotionData was assigned.");
        }
    }

    public void RemovePotionFromRun(PotionData potionData)
    {
        if (!CanEditPotions()) return;

        RunPotionManager potionManager = RunPotionManager.EnsureInstance();
        if (potionManager == null)
        {
            LogPotionEditFailure("Could not create or find a RunPotionManager.");
            return;
        }

        if (!potionManager.RemovePotion(potionData))
        {
            LogPotionEditFailure(potionData != null
                ? $"Could not remove potion '{potionData.Title}' because it is not owned."
                : "Could not remove potion because no PotionData was assigned.");
        }
    }

    public void RemovePotionAtIndex(int index)
    {
        if (!CanEditPotions()) return;

        RunPotionManager potionManager = RunPotionManager.EnsureInstance();
        if (potionManager == null)
        {
            LogPotionEditFailure("Could not create or find a RunPotionManager.");
            return;
        }

        if (!potionManager.RemoveAt(index))
        {
            LogPotionEditFailure($"Could not remove potion at index {index}.");
        }
    }

    public void ClearRunPotions()
    {
        if (!CanEditPotions()) return;

        RunPotionManager potionManager = RunPotionManager.EnsureInstance();
        if (potionManager == null)
        {
            LogPotionEditFailure("Could not create or find a RunPotionManager.");
            return;
        }

        potionManager.ClearPotions();
    }

    private bool TryGetRunManager(out RunManager runManager)
    {
        runManager = RunManager.Instance;

        if (runManager != null) return true;

        Gameseed26.Logger.LogWarning(this, "RunStateActions could not find a RunManager.");
        return false;
    }

    private bool TryGetRunDeckManager(out RunDeckManager deckManager)
    {
        deckManager = RunDeckManager.Instance;

        if (deckManager != null) return true;

        Gameseed26.Logger.LogWarning(this, "RunStateActions could not find a RunDeckManager.");
        return false;
    }

    private bool CanEditDeck()
    {
        if (allowDeckEditing) return true;

        LogDeckEditFailure("Deck editing is disabled on this RunStateActions component.");
        return false;
    }

    private bool CanEditRelics()
    {
        if (allowRelicEditing) return true;

        LogRelicEditFailure("Relic editing is disabled on this RunStateActions component.");
        return false;
    }

    private bool CanEditPotions()
    {
        if (allowPotionEditing) return true;

        LogPotionEditFailure("Potion editing is disabled on this RunStateActions component.");
        return false;
    }

    private void LogDeckEditFailure(string message)
    {
        if (!logDeckEditFailures) return;
        Gameseed26.Logger.Log(this, message);
    }

    private void LogRelicEditFailure(string message)
    {
        if (!logRelicEditFailures) return;
        Gameseed26.Logger.Log(this, message);
    }

    private void LogPotionEditFailure(string message)
    {
        if (!logPotionEditFailures) return;
        Gameseed26.Logger.Log(this, message);
    }
}
