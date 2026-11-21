using System.Collections;
using System.Collections.Generic;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Dialogue;
using PlushieOdyssey.UI;
using PlushieOdyssey.Vfx;
using UnityEngine;
using UnityEngine.Events;

namespace PlushieOdyssey.Tutorials
{
    public enum TutorialPhase
    {
        PlayCardDialogue,
        PlayCard,
        EndTurnDialogue,
        EndTurn,
        CompleteDialogue,
        Complete
    }

    public class TutorialManager : MonoBehaviour
    {
        public const string TutorialCompletedPrefKey = "HasPlayedCardTutorial";

        public static TutorialManager Instance { get; private set; }

        [SerializeField] private NPCInteraction npcInteraction;
        [SerializeField] private float startDelayAfterEntrance = 0.25f;

        [SerializeField] private List<NPCDialogueLine> playCardDialogue;
        [SerializeField] private List<NPCDialogueLine> endTurnDialogue;
        [SerializeField] private List<NPCDialogueLine> completeDialogue;

        [Header("Events")]
        public UnityEvent<TutorialPhase> onPhaseChanged;
        public UnityEvent onEndTurnDialogueStarted;

        public TutorialPhase CurrentPhase { get; private set; } = TutorialPhase.PlayCardDialogue;

        private Coroutine _startRoutine;
        private bool _hasMarkedCompleted;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (npcInteraction == null)
            {
                npcInteraction = FindFirstObjectByType<NPCInteraction>(FindObjectsInactive.Include);
            }

            ActionSystem.SubscribeReaction<PlayCardGA>(OnCardPlayed, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnTurnEnded, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<KillEnemyGA>(OnEnemyKilled, ReactionTiming.POST);

            if (npcInteraction != null)
            {
                npcInteraction.onDialogueFinished.AddListener(OnDialogueFinished);
                ChangePhase(TutorialPhase.PlayCardDialogue);
                _startRoutine = StartCoroutine(BeginTutorialWhenBattleReady());
            }
            else
            {
                CompleteTutorialAndSave();
            }
        }

        private IEnumerator BeginTutorialWhenBattleReady()
        {
            // Wait until the blink transition + staggered battle entrance finishes and opening cards are dealt
            float timeout = 4.0f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                CombatSceneEntranceDirector director = CombatSceneEntranceDirector.Instance;
                bool entranceDone = director == null || (!director.IsStagingEntrance && director.HasCompletedEntrance);
                bool hasHand = CardSystem.Instance == null || CardSystem.Instance.Hand.Count > 0;

                if (entranceDone && hasHand) break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (startDelayAfterEntrance > 0f)
            {
                yield return new WaitForSecondsRealtime(startDelayAfterEntrance);
            }

            _startRoutine = null;

            if (CurrentPhase == TutorialPhase.PlayCardDialogue && !_hasMarkedCompleted)
            {
                PlayDialogue(playCardDialogue);
            }
        }

        private void OnDestroy()
        {
            if (_startRoutine != null)
            {
                StopCoroutine(_startRoutine);
                _startRoutine = null;
            }

            if (npcInteraction != null)
            {
                npcInteraction.onDialogueFinished.RemoveListener(OnDialogueFinished);
            }

            ActionSystem.UnsubscribeReaction<PlayCardGA>(OnCardPlayed, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnTurnEnded, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<KillEnemyGA>(OnEnemyKilled, ReactionTiming.POST);

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SkipTutorial()
        {
            if (_startRoutine != null)
            {
                StopCoroutine(_startRoutine);
                _startRoutine = null;
            }

            ForceCloseDialogueSilently();
            CompleteTutorialAndSave();
            RefreshCombatInputStates();
        }

        private void ChangePhase(TutorialPhase newPhase)
        {
            CurrentPhase = newPhase;
            global::PlushieOdyssey.Core.Events.EventBus.Broadcast($"TutorialPhase_{newPhase}");
            onPhaseChanged?.Invoke(newPhase);

            if (newPhase == TutorialPhase.EndTurnDialogue)
            {
                onEndTurnDialogueStarted?.Invoke();
            }

            RefreshCombatInputStates();
        }

        private void PlayDialogue(List<NPCDialogueLine> lines)
        {
            if (npcInteraction != null && lines != null && lines.Count > 0)
            {
                npcInteraction.StartDialogue(lines);
            }
            else
            {
                OnDialogueFinished();
            }
        }

        private void OnDialogueFinished()
        {
            if (CurrentPhase == TutorialPhase.PlayCardDialogue)
            {
                ChangePhase(TutorialPhase.PlayCard);
            }
            else if (CurrentPhase == TutorialPhase.EndTurnDialogue)
            {
                ChangePhase(TutorialPhase.EndTurn);
            }
            else if (CurrentPhase == TutorialPhase.CompleteDialogue)
            {
                CompleteTutorialAndSave();
            }
        }

        private void OnCardPlayed(PlayCardGA action)
        {
            if (CurrentPhase != TutorialPhase.PlayCardDialogue && CurrentPhase != TutorialPhase.PlayCard)
            {
                return;
            }

            if (CurrentPhase != TutorialPhase.PlayCard)
            {
                ChangePhase(TutorialPhase.PlayCard);
            }

            // If the player already defeated all tutorial enemies on Turn 1, complete cleanly!
            if (AreAllEnemiesDefeated())
            {
                ForceCloseDialogueSilently();
                CompleteTutorialAndSave();
                return;
            }

            if (CardSystem.Instance != null && !CardSystem.Instance.HasPlayableCards())
            {
                ForceCloseDialogueSilently();
                ChangePhase(TutorialPhase.EndTurnDialogue);
                PlayDialogue(endTurnDialogue);
            }
        }

        private void OnTurnEnded(EnemyTurnGA action)
        {
            if (CurrentPhase == TutorialPhase.EndTurnDialogue || CurrentPhase == TutorialPhase.EndTurn)
            {
                ForceCloseDialogueSilently();
                ChangePhase(TutorialPhase.CompleteDialogue);
                PlayDialogue(completeDialogue);
            }
        }

        private void OnEnemyKilled(KillEnemyGA action)
        {
            if (_hasMarkedCompleted) return;
            if (AreAllEnemiesDefeated())
            {
                ForceCloseDialogueSilently();
                CompleteTutorialAndSave();
            }
        }

        private void ForceCloseDialogueSilently()
        {
            if (npcInteraction != null && npcInteraction.IsDialogueActive)
            {
                npcInteraction.onDialogueFinished.RemoveListener(OnDialogueFinished);
                npcInteraction.ForceEndDialogue();
                npcInteraction.onDialogueFinished.AddListener(OnDialogueFinished);
            }
        }

        private void CompleteTutorialAndSave()
        {
            _hasMarkedCompleted = true;
            if (CurrentPhase != TutorialPhase.Complete)
            {
                ChangePhase(TutorialPhase.Complete);
            }
            PlayerPrefs.SetInt(TutorialCompletedPrefKey, 1);
            PlayerPrefs.Save();
        }

        private static bool AreAllEnemiesDefeated()
        {
            return EnemySystem.Instance != null &&
                   EnemySystem.Instance.Enemies != null &&
                   EnemySystem.Instance.Enemies.Count == 0;
        }

        private static void RefreshCombatInputStates()
        {
            CardPlayService cps = FindFirstObjectByType<CardPlayService>();
            if (cps != null) cps.RefreshState();

            EndTurnButtonUI endTurn = FindFirstObjectByType<EndTurnButtonUI>();
            if (endTurn != null) endTurn.RefreshSmartGlow();
        }

        public bool CanInteractWithCard(Card card)
        {
            if (CurrentPhase == TutorialPhase.Complete) return true;

            // Block card plays while the player is clicking through non-interactive dialogue lines
            if (npcInteraction != null && npcInteraction.IsDialogueActive && npcInteraction.CurrentLineAllowsClickToAdvance)
            {
                return false;
            }

            // Also block card plays during the End Turn instruction step
            if (CurrentPhase == TutorialPhase.EndTurnDialogue || CurrentPhase == TutorialPhase.EndTurn)
            {
                return false;
            }

            return true;
        }

        public bool CanEndTurn()
        {
            if (CurrentPhase == TutorialPhase.Complete) return true;

            if (npcInteraction != null && npcInteraction.IsDialogueActive && npcInteraction.CurrentLineAllowsClickToAdvance)
            {
                return false;
            }

            return CurrentPhase == TutorialPhase.EndTurnDialogue || CurrentPhase == TutorialPhase.EndTurn;
        }
    }
}
