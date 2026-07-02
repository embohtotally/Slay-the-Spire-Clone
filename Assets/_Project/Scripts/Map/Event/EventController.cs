using System;
using System.Collections;
using System.Collections.Generic;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[Serializable]
public class EventChoiceDefinition
{
    [Header("Text")]
    public string Title = "Choice";
    [TextArea(2, 5)] public string Description;
    public string UnavailableReason = "Requirements not met.";

    [Header("Availability / Cost")]
    public bool Enabled = true;
    [Min(0)] public int GoldCost;
    public CardData RequiredCardInDeck;
    public RelicData RequiredRelic;
    public PotionData RequiredPotion;

    [Header("Run State Effects")]
    public bool ReturnToMapAfterChoice = true;
    [Min(0)] public int GainGold;
    [Min(0)] public int HealAmount;
    [Min(0)] public int DamageAmount;
    [Min(0)] public int ReduceStressAmount;
    [Min(0)] public int AddStressAmount;

    [Header("Deck Effects")]
    public CardData AddCard;
    public CardData RemoveCard;
    public CardData ReplaceSourceCard;
    public CardData ReplaceTargetCard;

    [Header("Relic Effects")]
    public RelicData GrantRelic;
    public RelicData RemoveRelic;
    public bool GrantRandomRelic;
    public RelicRewardPool RelicRewardPool;
    public RelicRarityMask AllowedRelicRarities = RelicRarityMask.All;

    [Header("Potion Effects")]
    public PotionData GrantPotion;
    public PotionData RemovePotion;
    public bool GrantRandomPotion;
    public PotionRewardPool PotionRewardPool;
    public PotionRarityMask AllowedPotionRarities = PotionRarityMask.All;

    [Header("Card Reward")]
    public bool OpenCardReward;
    public CardRewardRequest CardRewardRequest = new();

    [Header("Optional Scene Override")]
    [Tooltip("If set, loads this scene after applying the choice instead of Return To Map. Leave empty for normal map return behavior.")]
    public string SceneAfterChoice;

    [Header("Events")]
    public UnityEvent OnChoiceSelected;
    public UnityEvent OnChoiceCompleted;
}

[DisallowMultipleComponent]
public class EventController : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField, Scene] private string mapSceneName = "Map";
    [SerializeField, Scene] private string cardRewardSceneName = "CardReward";
    [SerializeField] private bool autoOpenOnStart = true;
    [SerializeField] private bool allowMultipleChoices;
    [SerializeField] private bool disableAfterChoice = true;

    [Header("Choices")]
    [SerializeField]
    private List<EventChoiceDefinition> choices = new()
    {
        new EventChoiceDefinition { Title = "Gain Gold", Description = "Gain 50 gold.", GainGold = 50 },
        new EventChoiceDefinition { Title = "Leave", Description = "Return to the map." }
    };

    [Header("Logging")]
    [SerializeField] private bool logActionFailures = true;

    [Header("Events")]
    public UnityEvent OnEventOpened;
    public UnityEvent OnChoiceSelected;
    public UnityEvent OnChoiceCompleted;
    public UnityEvent OnCardRewardStarted;
    public UnityEvent OnCardRewardFinished;
    public UnityEvent OnReturnToMapRequested;

    [Header("Audio")]
    [SerializeField] private TuneSfxCue eventOpenedSfx;
    [SerializeField] private TuneSfxCue choiceSelectedSfx;
    [SerializeField] private TuneSfxCue choiceCompletedSfx;
    [SerializeField] private TuneSfxCue cardRewardStartedSfx;
    [SerializeField] private TuneSfxCue returnToMapSfx;
    [SerializeField] private TuneSfxCue choiceFailedSfx;

    [Header("Debug")]
    [ReadOnly][SerializeField] private bool eventOpened;
    [ReadOnly][SerializeField] private bool choiceAlreadyUsed;
    [ReadOnly][SerializeField] private bool cardRewardInProgress;

    public IReadOnlyList<EventChoiceDefinition> Choices => choices;
    public bool IsEventOpen => eventOpened;
    public bool CanChoose => allowMultipleChoices || !choiceAlreadyUsed;

    private void Start()
    {
        if (autoOpenOnStart)
        {
            OpenEvent();
        }
    }

    [Button("Open Event", EButtonEnableMode.Playmode)]
    public void OpenEvent()
    {
        eventOpened = true;
        eventOpenedSfx?.Play(this, transform);
        OnEventOpened?.Invoke();
    }

    public bool CanSelectChoice(int index)
    {
        if (!CanChoose) return false;
        if (cardRewardInProgress) return false;
        if (!TryGetChoice(index, out EventChoiceDefinition choice)) return false;
        if (choice == null || !choice.Enabled) return false;

        if (choice.GoldCost > 0)
        {
            RunManager runManager = RunManager.Instance;
            if (runManager == null || runManager.Gold < choice.GoldCost) return false;
        }

        if (choice.RequiredCardInDeck != null)
        {
            RunDeckManager deckManager = RunDeckManager.Instance;
            if (deckManager == null || !deckManager.Contains(choice.RequiredCardInDeck)) return false;
        }

        if (choice.RequiredRelic != null)
        {
            RunRelicManager relicManager = RunRelicManager.EnsureInstance();
            if (relicManager == null || !relicManager.Contains(choice.RequiredRelic)) return false;
        }

        if (choice.RequiredPotion != null)
        {
            RunPotionManager potionManager = RunPotionManager.EnsureInstance();
            if (potionManager == null || !potionManager.Contains(choice.RequiredPotion)) return false;
        }

        return true;
    }

    public string GetUnavailableReason(int index)
    {
        if (!TryGetChoice(index, out EventChoiceDefinition choice)) return "Choice does not exist.";
        if (choice == null) return "Choice is empty.";
        if (!choice.Enabled) return choice.UnavailableReason;
        if (!CanChoose) return "A choice has already been selected.";
        if (cardRewardInProgress) return "A card reward is still in progress.";

        if (choice.GoldCost > 0)
        {
            RunManager runManager = RunManager.Instance;
            if (runManager == null) return "RunManager not found.";
            if (runManager.Gold < choice.GoldCost) return $"Need {choice.GoldCost} gold.";
        }

        if (choice.RequiredCardInDeck != null)
        {
            RunDeckManager deckManager = RunDeckManager.Instance;
            if (deckManager == null) return "RunDeckManager not found.";
            if (!deckManager.Contains(choice.RequiredCardInDeck)) return $"Requires {choice.RequiredCardInDeck.Title} in deck.";
        }

        if (choice.RequiredRelic != null)
        {
            RunRelicManager relicManager = RunRelicManager.EnsureInstance();
            if (relicManager == null) return "RunRelicManager not found.";
            if (!relicManager.Contains(choice.RequiredRelic)) return $"Requires {choice.RequiredRelic.Title}.";
        }

        if (choice.RequiredPotion != null)
        {
            RunPotionManager potionManager = RunPotionManager.EnsureInstance();
            if (potionManager == null) return "RunPotionManager not found.";
            if (!potionManager.Contains(choice.RequiredPotion)) return $"Requires {choice.RequiredPotion.Title}.";
        }

        return string.Empty;
    }

    [Button("Select Choice 0", EButtonEnableMode.Playmode)]
    public void SelectChoice0() => SelectChoice(0);

    [Button("Select Choice 1", EButtonEnableMode.Playmode)]
    public void SelectChoice1() => SelectChoice(1);

    [Button("Select Choice 2", EButtonEnableMode.Playmode)]
    public void SelectChoice2() => SelectChoice(2);

    [Button("Select Choice 3", EButtonEnableMode.Playmode)]
    public void SelectChoice3() => SelectChoice(3);

    public void SelectChoice(int index)
    {
        if (!TryGetChoice(index, out EventChoiceDefinition choice)) return;

        if (!CanSelectChoice(index))
        {
            LogFailure(GetUnavailableReason(index));
            return;
        }

        choiceAlreadyUsed = true;
        choice.OnChoiceSelected?.Invoke();
        choiceSelectedSfx?.Play(this, transform);
        OnChoiceSelected?.Invoke();

        StartCoroutine(ApplyChoiceAndFinish(choice));
    }

    public void ResetChoiceState()
    {
        choiceAlreadyUsed = false;
        cardRewardInProgress = false;
    }

    public void ReturnToMap()
    {
        returnToMapSfx?.Play(this, transform);
        OnReturnToMapRequested?.Invoke();

        if (string.IsNullOrWhiteSpace(mapSceneName))
        {
            LogFailure("Cannot return to map because Map Scene Name is empty.");
            return;
        }

        SceneLoader.LoadScene(mapSceneName);
    }

    private IEnumerator ApplyChoiceAndFinish(EventChoiceDefinition choice)
    {
        ApplyRunStateEffects(choice);
        ApplyDeckEffects(choice);
        ApplyRelicEffects(choice);
        ApplyPotionEffects(choice);

        if (choice.OpenCardReward)
        {
            yield return OpenCardReward(choice.CardRewardRequest);
        }

        choice.OnChoiceCompleted?.Invoke();
        choiceCompletedSfx?.Play(this, transform);
        OnChoiceCompleted?.Invoke();

        if (disableAfterChoice && !allowMultipleChoices)
        {
            enabled = false;
        }

        if (!string.IsNullOrWhiteSpace(choice.SceneAfterChoice))
        {
            SceneLoader.LoadScene(choice.SceneAfterChoice);
            yield break;
        }

        if (choice.ReturnToMapAfterChoice)
        {
            ReturnToMap();
        }
    }

    private void ApplyRunStateEffects(EventChoiceDefinition choice)
    {
        RunManager runManager = RunManager.Instance;
        if (runManager == null)
        {
            if (choice.GoldCost > 0 || choice.GainGold > 0 || choice.HealAmount > 0 || choice.DamageAmount > 0 || choice.ReduceStressAmount > 0 || choice.AddStressAmount > 0)
            {
                LogFailure("Event choice needs RunManager, but none was found.");
            }
            return;
        }

        if (choice.GoldCost > 0 && !runManager.SpendGold(choice.GoldCost))
        {
            LogFailure($"Could not spend {choice.GoldCost} gold.");
            return;
        }

        if (choice.GainGold > 0) runManager.AddGold(choice.GainGold);
        if (choice.HealAmount > 0) runManager.HealHero(choice.HealAmount);
        if (choice.DamageAmount > 0) runManager.DamageHero(choice.DamageAmount);
        if (choice.ReduceStressAmount > 0) runManager.ReduceStress(choice.ReduceStressAmount);
        if (choice.AddStressAmount > 0) runManager.AddStress(choice.AddStressAmount);
    }

    private void ApplyDeckEffects(EventChoiceDefinition choice)
    {
        bool needsDeckManager = choice.AddCard != null || choice.RemoveCard != null || choice.ReplaceSourceCard != null || choice.ReplaceTargetCard != null;
        if (!needsDeckManager) return;

        RunDeckManager deckManager = RunDeckManager.Instance;
        if (deckManager == null)
        {
            LogFailure("Event choice needs RunDeckManager, but none was found.");
            return;
        }

        if (choice.AddCard != null)
        {
            deckManager.AddCard(choice.AddCard);
        }

        if (choice.RemoveCard != null && !deckManager.RemoveFirst(choice.RemoveCard))
        {
            LogFailure($"Could not remove '{choice.RemoveCard.Title}' from the run deck.");
        }

        if (choice.ReplaceSourceCard != null || choice.ReplaceTargetCard != null)
        {
            if (!deckManager.ReplaceFirst(choice.ReplaceSourceCard, choice.ReplaceTargetCard))
            {
                string source = choice.ReplaceSourceCard != null ? choice.ReplaceSourceCard.Title : "<missing source card>";
                string target = choice.ReplaceTargetCard != null ? choice.ReplaceTargetCard.Title : "<missing target card>";
                LogFailure($"Could not replace '{source}' with '{target}'.");
            }
        }
    }

    private void ApplyRelicEffects(EventChoiceDefinition choice)
    {
        bool needsRelicManager = choice.GrantRelic != null || choice.RemoveRelic != null || choice.GrantRandomRelic;
        if (!needsRelicManager) return;

        RunRelicManager relicManager = RunRelicManager.EnsureInstance();
        if (relicManager == null)
        {
            LogFailure("Event choice needs RunRelicManager, but none was found.");
            return;
        }

        if (choice.GrantRelic != null && !relicManager.AddRelic(choice.GrantRelic))
        {
            LogFailure($"Could not grant relic '{choice.GrantRelic.Title}'. It may already be owned if it is unique.");
        }

        if (choice.GrantRandomRelic)
        {
            if (choice.RelicRewardPool == null)
            {
                LogFailure("Event choice is set to grant a random relic, but Relic Reward Pool is empty.");
            }
            else if (choice.RelicRewardPool.TryGetRandomRelic(relicManager, out RelicData randomRelic, choice.AllowedRelicRarities))
            {
                if (!relicManager.AddRelic(randomRelic))
                {
                    LogFailure($"Could not grant random relic '{randomRelic.Title}'. It may already be owned if it is unique.");
                }
            }
            else
            {
                LogFailure("Relic Reward Pool has no eligible relic for this event choice.");
            }
        }

        if (choice.RemoveRelic != null && !relicManager.RemoveRelic(choice.RemoveRelic))
        {
            LogFailure($"Could not remove relic '{choice.RemoveRelic.Title}' because it is not owned.");
        }
    }

    private void ApplyPotionEffects(EventChoiceDefinition choice)
    {
        bool needsPotionManager = choice.GrantPotion != null || choice.RemovePotion != null || choice.GrantRandomPotion;
        if (!needsPotionManager) return;

        RunPotionManager potionManager = RunPotionManager.EnsureInstance();
        if (potionManager == null)
        {
            LogFailure("Event choice needs RunPotionManager, but none was found.");
            return;
        }

        if (choice.GrantPotion != null && !potionManager.AddPotion(choice.GrantPotion))
        {
            LogFailure($"Could not grant potion '{choice.GrantPotion.Title}'. Potion slots may be full or it may already be owned if it is unique.");
        }

        if (choice.GrantRandomPotion)
        {
            if (choice.PotionRewardPool == null)
            {
                LogFailure("Event choice is set to grant a random potion, but Potion Reward Pool is empty.");
            }
            else if (choice.PotionRewardPool.TryGetRandomPotion(potionManager, out PotionData randomPotion, choice.AllowedPotionRarities))
            {
                if (!potionManager.AddPotion(randomPotion))
                {
                    LogFailure($"Could not grant random potion '{randomPotion.Title}'. Potion slots may be full or it may already be owned if it is unique.");
                }
            }
            else
            {
                LogFailure("Potion Reward Pool has no eligible potion for this event choice.");
            }
        }

        if (choice.RemovePotion != null && !potionManager.RemovePotion(choice.RemovePotion))
        {
            LogFailure($"Could not remove potion '{choice.RemovePotion.Title}' because it is not owned.");
        }
    }

    private IEnumerator OpenCardReward(CardRewardRequest request)
    {
        if (string.IsNullOrWhiteSpace(cardRewardSceneName))
        {
            LogFailure("Cannot open card reward because Card Reward Scene Name is empty.");
            yield break;
        }

        cardRewardInProgress = true;
        cardRewardStartedSfx?.Play(this, transform);
        OnCardRewardStarted?.Invoke();

        AsyncOperation loadOperation = SceneLoader.LoadSceneAdditive(cardRewardSceneName);
        if (loadOperation == null)
        {
            cardRewardInProgress = false;
            yield break;
        }

        yield return loadOperation;

        CardRewardController cardRewardController = FindCardRewardController(cardRewardSceneName);
        if (cardRewardController == null)
        {
            LogFailure($"Could not find a CardRewardController in additive scene '{cardRewardSceneName}'.");
            cardRewardInProgress = false;
            yield break;
        }

        bool finished = false;
        void HandleCardRewardFinished(bool _)
        {
            finished = true;
        }

        cardRewardController.RewardFinished += HandleCardRewardFinished;
        cardRewardController.ConfigureForAdditiveReward(request ?? new CardRewardRequest());

        while (!finished)
        {
            yield return null;
        }

        if (cardRewardController != null)
        {
            cardRewardController.RewardFinished -= HandleCardRewardFinished;
        }

        cardRewardInProgress = false;
        OnCardRewardFinished?.Invoke();
    }

    private CardRewardController FindCardRewardController(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                CardRewardController controller = rootObject.GetComponentInChildren<CardRewardController>(true);
                if (controller != null) return controller;
            }
        }

        return FindFirstObjectByType<CardRewardController>(FindObjectsInactive.Include);
    }

    private bool TryGetChoice(int index, out EventChoiceDefinition choice)
    {
        choice = null;
        if (index < 0 || index >= choices.Count)
        {
            LogFailure($"Event choice index {index} is out of range.");
            return false;
        }

        choice = choices[index];
        return true;
    }

    private void LogFailure(string message)
    {
        if (!logActionFailures || string.IsNullOrWhiteSpace(message)) return;
        choiceFailedSfx?.Play(this, transform);
        Gameseed26.Logger.Log(this, message);
    }
}
