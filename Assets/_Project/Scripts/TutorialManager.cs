using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

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
    public static TutorialManager Instance { get; private set; }

    [SerializeField] private NPCInteraction npcInteraction;
    
    [SerializeField] private List<NPCDialogueLine> playCardDialogue;
    [SerializeField] private List<NPCDialogueLine> endTurnDialogue;
    [SerializeField] private List<NPCDialogueLine> completeDialogue;

    [Header("Events")]
    public UnityEvent<TutorialPhase> onPhaseChanged;
    public UnityEvent onEndTurnDialogueStarted;

    public TutorialPhase CurrentPhase { get; private set; } = TutorialPhase.PlayCardDialogue;

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
        if (npcInteraction != null)
        {
            npcInteraction.onDialogueFinished.AddListener(OnDialogueFinished);
            ChangePhase(TutorialPhase.PlayCardDialogue);
            PlayDialogue(playCardDialogue);
        }
        else
        {
            // If no tutorial manager is fully set up, skip tutorial
            ChangePhase(TutorialPhase.Complete);
        }

        ActionSystem.SubscribeReaction<PlayCardGA>(OnCardPlayed, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(OnTurnEnded, ReactionTiming.POST);
    }
    
    private void OnDestroy()
    {
        if (npcInteraction != null)
        {
            npcInteraction.onDialogueFinished.RemoveListener(OnDialogueFinished);
        }
        ActionSystem.UnsubscribeReaction<PlayCardGA>(OnCardPlayed, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnTurnEnded, ReactionTiming.POST);
    }

    private void ChangePhase(TutorialPhase newPhase)
    {
        CurrentPhase = newPhase;
        ModularEvents.EventBus.Broadcast($"TutorialPhase_{newPhase}");
        onPhaseChanged?.Invoke(newPhase);

        if (newPhase == TutorialPhase.EndTurnDialogue)
        {
            onEndTurnDialogueStarted?.Invoke();
        }
    }

    private void PlayDialogue(List<NPCDialogueLine> lines)
    {
        if (lines != null && lines.Count > 0)
        {
            npcInteraction.StartDialogue(lines);
        }
        else
        {
            // If lines missing, skip dialogue
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
            ChangePhase(TutorialPhase.Complete);
            PlayerPrefs.SetInt("HasPlayedCardTutorial", 1);
            PlayerPrefs.Save();
        }
    }

    private void OnCardPlayed(PlayCardGA action)
    {
        if (CurrentPhase == TutorialPhase.PlayCardDialogue || CurrentPhase == TutorialPhase.PlayCard)
        {
            // Move to PlayCard phase in case we were still in Dialogue phase
            if (CurrentPhase != TutorialPhase.PlayCard)
            {
                ChangePhase(TutorialPhase.PlayCard);
            }

            // Delay the check slightly to allow mana to be spent first, or we check it immediately if the performer has already subtracted mana.
            // Since this is a POST reaction, Mana has already been spent.
            if (!CardSystem.Instance.HasPlayableCards())
            {
                if (npcInteraction != null && npcInteraction.IsDialogueActive)
                {
                    // Temporarily remove listener so ForceEndDialogue doesn't trigger OnDialogueFinished
                    npcInteraction.onDialogueFinished.RemoveListener(OnDialogueFinished);
                    npcInteraction.ForceEndDialogue();
                    npcInteraction.onDialogueFinished.AddListener(OnDialogueFinished);
                }

                ChangePhase(TutorialPhase.EndTurnDialogue);
                PlayDialogue(endTurnDialogue);
            }
        }
    }

    private void OnTurnEnded(EnemyTurnGA action)
    {
        if (CurrentPhase == TutorialPhase.EndTurnDialogue || CurrentPhase == TutorialPhase.EndTurn)
        {
            if (npcInteraction != null && npcInteraction.IsDialogueActive)
            {
                // Temporarily remove listener so ForceEndDialogue doesn't trigger OnDialogueFinished
                npcInteraction.onDialogueFinished.RemoveListener(OnDialogueFinished);
                npcInteraction.ForceEndDialogue();
                npcInteraction.onDialogueFinished.AddListener(OnDialogueFinished);
            }

            ChangePhase(TutorialPhase.CompleteDialogue);
            PlayDialogue(completeDialogue);
        }
    }

    public bool CanInteractWithCard(Card card)
    {
        return true;
    }

    public bool CanEndTurn()
    {
        if (CurrentPhase == TutorialPhase.Complete) return true;
        
        return CurrentPhase == TutorialPhase.EndTurnDialogue || CurrentPhase == TutorialPhase.EndTurn;
    }
}
