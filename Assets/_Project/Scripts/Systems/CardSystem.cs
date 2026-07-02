using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

[Serializable]
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

    [Header("SFX")]
    [SerializeField] private Gameseed26.SfxID playCardSfx = Gameseed26.SfxID.PlayCard;
    [SerializeField] private Gameseed26.SfxID drawCardSfx = Gameseed26.SfxID.DrawCard;
    [SerializeField] private Gameseed26.SfxID shuffleSfx = Gameseed26.SfxID.Shuffle;
    [SerializeField] private Gameseed26.SfxID yourTurnSfx = Gameseed26.SfxID.YourTurn;

    private readonly List<Card> drawPile = new();
    private readonly List<Card> discardPile = new();
    private readonly List<Card> hand = new();

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<DrawCardsGA>(DrawCardsPerformer);
        ActionSystem.AttachPerformer<DiscardAllCardsGA>(DiscardAllCardsPerformer);
        ActionSystem.AttachPerformer<PlayCardGA>(PlayCardPerformer);
        ActionSystem.AttachPerformer<ApplyCostModifierGA>(ApplyCostModifierPerformer);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(EnemyTurnPreReaction, ReactionTiming.PRE);
        ActionSystem.SubscribeReaction<EnemyTurnGA>(EnemyTurnPostReaction, ReactionTiming.POST);
        ActionSystem.SubscribeReaction<CombatWonGA>(CombatWonReaction, ReactionTiming.POST);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<DrawCardsGA>();
        ActionSystem.DetachPerformer<DiscardAllCardsGA>();
        ActionSystem.DetachPerformer<PlayCardGA>();
        ActionSystem.DetachPerformer<ApplyCostModifierGA>();
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
        
        if (playCardSfx != Gameseed26.SfxID.None) Gameseed26.Tune.SFX(playCardSfx);

        if (playCardGA.Card.PlaySfx != Gameseed26.SfxID.None)
        {
            Gameseed26.Tune.SFX(playCardGA.Card.PlaySfx);
        }

        PlayCardVisuals(playCardGA);

        yield return PlayAndDiscardCard(cardView);

        SpendMana(playCardGA);
        DoManualTargetEffect(playCardGA);
        DoAutoTargetEffect(playCardGA);
    }

    private IEnumerator ApplyCostModifierPerformer(ApplyCostModifierGA applyCostModifierGA)
    {
        if (RunManager.Instance != null)
        {
            if (!RunManager.Instance.CardCostModifiers.ContainsKey(applyCostModifierGA.TargetCardName))
            {
                RunManager.Instance.CardCostModifiers[applyCostModifierGA.TargetCardName] = 0;
            }
            RunManager.Instance.CardCostModifiers[applyCostModifierGA.TargetCardName] += applyCostModifierGA.ReductionAmount;
        }
        yield return null;
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

        if (yourTurnSfx != Gameseed26.SfxID.None) Gameseed26.Tune.SFX(yourTurnSfx);

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
    private IEnumerator PlayAndDiscardCard(CardView cardView)
    {
        discardPile.Add(cardView.Card);
        UpdatePileTexts();

        cardView.transform.DOMove(new Vector3(0, 0, -1f), doTweenMoveDuration);
        cardView.transform.DORotate(Vector3.zero, doTweenMoveDuration);
        Tween scaleTween = cardView.transform.DOScale(Vector3.one * 1.5f, doTweenScaleDuration);
        yield return scaleTween.WaitForCompletion();

        yield return new WaitForSeconds(0.2f);

        cardView.transform.DOScale(Vector3.zero, doTweenScaleDuration);
        Tween moveTween = cardView.transform.DOMove(discardPilePoint.position, doTweenMoveDuration);
        yield return moveTween.WaitForCompletion();

        cardView.transform.DOKill();
        Destroy(cardView.gameObject);
    }

    private IEnumerator DiscardCard(CardView cardView)
    {
        discardPile.Add(cardView.Card);
        UpdatePileTexts();
        cardView.transform.DOScale(Vector3.zero, doTweenScaleDuration);
        Tween tween = cardView.transform.DOMove(discardPilePoint.position, doTweenMoveDuration);
        yield return tween.WaitForCompletion();
        
        cardView.transform.DOKill();
        Destroy(cardView.gameObject);
    }

    private IEnumerator DrawCard()
    {
        Card card = drawPile.Draw();
        UpdatePileTexts();
        if (drawCardSfx != Gameseed26.SfxID.None) Gameseed26.Tune.SFX(drawCardSfx);
        CardView cardView = CardViewCreator.Instance.CreateCardView(card, drawPilePoint.position, drawPilePoint.rotation);
        hand.Add(card);
        yield return handView.AddCard(cardView);
    }

    private void RefillDeck()
    {
        if (shuffleSfx != Gameseed26.SfxID.None) Gameseed26.Tune.SFX(shuffleSfx);
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
        int cost = playCardGA.Card.Mana;
        if (RunManager.Instance != null && RunManager.Instance.CardCostModifiers.TryGetValue(playCardGA.Card.Title, out int modifier))
        {
            cost = Mathf.Max(0, cost - modifier);
        }
        SpendManaGA spendManaGA = new(cost);
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
            List<CombatantView> targets = effectWrapper.TargetMode.GetTargets(HeroSystem.Instance.HeroView);
            PerformEffectGA performEffectGA = new(effectWrapper.Effect, targets);
            ActionSystem.Instance.AddReaction(performEffectGA);
        }
    }

    private void UpdatePileTexts()
    {
        if (drawPileText != null) drawPileText.text = drawPile.Count.ToString();
        if (discardPileText != null) discardPileText.text = discardPile.Count.ToString();
    }

    private void PlayCardVisuals(PlayCardGA playCardGA)
    {
        HashSet<CombatantView> targets = new();
        if (playCardGA.ManualTarget != null)
        {
            targets.Add(playCardGA.ManualTarget);
        }

        if (playCardGA.Card.OtherEffects != null)
        {
            foreach (AutoTargetEffect effectWrapper in playCardGA.Card.OtherEffects)
            {
                if (effectWrapper.TargetMode != null)
                {
                    List<CombatantView> autoTargets = effectWrapper.TargetMode.GetTargets(HeroSystem.Instance.HeroView);
                    if (autoTargets != null)
                    {
                        foreach (CombatantView target in autoTargets)
                        {
                            if (target != null) targets.Add(target);
                        }
                    }
                }
            }
        }

        bool playParticle = playCardGA.Card.VisualType == CardVisualType.Particle || playCardGA.Card.VisualType == CardVisualType.Both;
        bool playAnimator = playCardGA.Card.VisualType == CardVisualType.Animator || playCardGA.Card.VisualType == CardVisualType.Both;

        if (playAnimator && !string.IsNullOrEmpty(playCardGA.Card.HeroAnimationTrigger))
        {
            if (HeroSystem.Instance.HeroView.Animator != null)
            {
                HeroSystem.Instance.HeroView.Animator.SetTrigger(playCardGA.Card.HeroAnimationTrigger);
            }
        }

        foreach (CombatantView target in targets)
        {
            if (playParticle && playCardGA.Card.PlayParticle != null)
            {
                Instantiate(playCardGA.Card.PlayParticle, target.transform.position, Quaternion.identity);
            }

            if (playAnimator && !string.IsNullOrEmpty(playCardGA.Card.TargetAnimationTrigger))
            {
                if (target.Animator != null)
                {
                    target.Animator.SetTrigger(playCardGA.Card.TargetAnimationTrigger);
                }
            }
        }
    }
    #endregion
}
