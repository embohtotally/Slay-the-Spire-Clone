using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[System.Serializable]
public class CardSystem : Singleton<CardSystem>
{
    [SerializeField] private HandView handView;
    [SerializeField] private Transform drawPilePoint;
    [SerializeField] private Transform discardPilePoint;
    [SerializeField] private float doTweenScaleDuration = 0.15f;
    [SerializeField] private float doTweenMoveDuration = 0.15f;
    [SerializeField] private int enemyDrawCardsAmount = 5;
    [SerializeField] private TMP_Text drawPileText;
    [SerializeField] private TMP_Text discardPileText;

    private readonly List<Card> drawPile = new();
    private readonly List<Card> discardPile = new();
    private readonly List<Card> hand = new();

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<DrawCardsGA>(DrawCardsPerformer);
        ActionSystem.AttachPerformer<DiscardAllCardsGA>(DiscardAllCardsPerformer);
        ActionSystem.AttachPerformer<PlayCardGA>(PlayCardPerformer);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(EnemyTurnPreReaction, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(EnemyTurnPostReaction, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<CombatWonGA>(CombatWonReaction, ReactionTiming.POST);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<DrawCardsGA>();
        ActionSystem.DetachPerformer<DiscardAllCardsGA>();
        ActionSystem.DetachPerformer<PlayCardGA>();
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(EnemyTurnPreReaction, ReactionTiming.PRE);
        ActionSystem.UnsubscribeReaction<EnemyTurnGA>(EnemyTurnPostReaction, ReactionTiming.POST);
        ActionSystem.UnsubscribeReaction<CombatWonGA>(CombatWonReaction, ReactionTiming.POST);
    }

    public void Setup(List<CardData> deckData)
    {
        drawPile.Clear();
        discardPile.Clear();
        hand.Clear();

        if (deckData == null)
        {
            UpdatePileTexts();
            return;
        }

        foreach (CardData cardData in deckData)
        {
            if (cardData == null) continue;

            Card card = new(cardData);
            drawPile.Add(card);
        }

        UpdatePileTexts();
    }

    #region Performers
    private IEnumerator DrawCardsPerformer(DrawCardsGA drawCardsGA)
    {
        int actualAmount = Mathf.Min(drawCardsGA.Amount, drawPile.Count);
        int notDrawnAmount = drawCardsGA.Amount - actualAmount;

        for (int i = 0; i < actualAmount; i++)
        {
            yield return DrawCard();
        }

        if (notDrawnAmount > 0)
        {
            RefillDeck();

            int remainingAmount = Mathf.Min(notDrawnAmount, drawPile.Count);
            for (int i = 0; i < remainingAmount; i++)
            {
                yield return DrawCard();
            }
        }
    }

    private IEnumerator DiscardAllCardsPerformer(DiscardAllCardsGA discardAllCardsGA)
    {
        foreach (Card card in hand)
        {
            CardView cardView = handView.RemoveCard(card);
            yield return DiscardCard(cardView);
        }

        hand.Clear();
    }

    private IEnumerator PlayCardPerformer(PlayCardGA playCardGA)
    {
        hand.Remove(playCardGA.Card);
        CardView cardView = handView.RemoveCard(playCardGA.Card);
        yield return DiscardCard(cardView);

        SpendMana(playCardGA);
        DoManualTargetEffect(playCardGA);
        DoAutoTargetEffect(playCardGA);
    }
    #endregion

    #region Reactions
    private void EnemyTurnPreReaction(EnemyTurnGA enemyTurnGA)
    {
        DiscardAllCardsGA discardAllCardsGA = new();
        ActionSystem.Instance.AddReaction(discardAllCardsGA);
    }

    private void EnemyTurnPostReaction(EnemyTurnGA enemyTurnGA)
    {
        if (HeroSystem.Instance.HeroView.IsStunned) return;

        DrawCardsGA drawCardsGA = new(enemyDrawCardsAmount);
        ActionSystem.Instance.AddReaction(drawCardsGA);
    }

    private void CombatWonReaction(CombatWonGA combatWonGA)
    {
        drawPile.Clear();
        discardPile.Clear();
        UpdatePileTexts();
    }
    #endregion

    #region Helpers
    private IEnumerator DiscardCard(CardView cardView)
    {
        discardPile.Add(cardView.Card);
        UpdatePileTexts();
        cardView.transform.DOScale(Vector3.zero, doTweenScaleDuration);
        Tween tween = cardView.transform.DOMove(discardPilePoint.position, doTweenMoveDuration);
        yield return tween.WaitForCompletion();
        Destroy(cardView.gameObject);
    }

    private IEnumerator DrawCard()
    {
        Card card = drawPile.Draw();
        UpdatePileTexts();
        CardView cardView = CardViewCreator.Instance.CreateCardView(card, drawPilePoint.position, drawPilePoint.rotation);
        hand.Add(card);
        yield return handView.AddCard(cardView);
    }

    private void RefillDeck()
    {
        for (int i = 0; i < discardPile.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, discardPile.Count);
            Card temp = discardPile[i];
            discardPile[i] = discardPile[randomIndex];
            discardPile[randomIndex] = temp;
        }

        drawPile.AddRange(discardPile);
        discardPile.Clear();
        UpdatePileTexts();
    }

    private void SpendMana(PlayCardGA playCardGA)
    {
        SpendManaGA spendManaGA = new(playCardGA.Card.Mana);
        ActionSystem.Instance.AddReaction(spendManaGA);
    }

    private void DoManualTargetEffect(PlayCardGA playCardGA)
    {
        if (playCardGA.Card.ManualTargetEffect != null)
        {
            PerformEffectGA performEffectGA = new(playCardGA.Card.ManualTargetEffect, new() { playCardGA.ManualTarget });
            ActionSystem.Instance.AddReaction(performEffectGA);
        }
    }

    private void DoAutoTargetEffect(PlayCardGA playCardGA)
    {
        foreach (AutoTargetEffect effectWrapper in playCardGA.Card.OtherEffects)
        {
            List<CombatantView> targets = effectWrapper.TargetMode.GetTargets();
            PerformEffectGA performEffectGA = new(effectWrapper.Effect, targets);
            ActionSystem.Instance.AddReaction(performEffectGA);
        }
    }

    private void UpdatePileTexts()
    {
        if (drawPileText != null) drawPileText.text = drawPile.Count.ToString();
        if (discardPileText != null) discardPileText.text = discardPile.Count.ToString();
    }
    #endregion
}