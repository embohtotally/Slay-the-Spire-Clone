using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.Core.Extensions;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
using PlushieOdyssey.UI.Combat;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [Serializable]
    public class CardSystem : Singleton<CardSystem>
    {
        [SerializeField] private HandView handView;
        public HandView HandView => handView;
        [SerializeField] private Transform drawPilePoint;
        [SerializeField] private Transform discardPilePoint;
        [Header("Animation Pacing (Non-Blocking Visuals)")]
        [SerializeField] private float doTweenScaleDuration = 0.20f;
        [SerializeField] private float doTweenMoveDuration = 0.22f;
        [SerializeField, Min(0f)] private float drawStaggerDelay = 0.095f;
        [SerializeField, Min(0f)] private float discardStaggerDelay = 0.06f;
        [SerializeField, Min(0.05f)] private float discardFlyDuration = 0.28f;
        [SerializeField] private int enemyDrawCardsAmount = 5;
        [SerializeField, Min(1)] private int maxHandSize = 10;
        [SerializeField] private TMP_Text drawPileText;
        [SerializeField] private TMP_Text discardPileText;
        [SerializeField] private TMP_Text exhaustPileText;

        [Header("SFX")]
        [SerializeField] private global::PlushieOdyssey.Audio.SfxID playCardSfx = global::PlushieOdyssey.Audio.SfxID.PlayCard;
        [SerializeField] private global::PlushieOdyssey.Audio.SfxID drawCardSfx = global::PlushieOdyssey.Audio.SfxID.DrawCard;
        [SerializeField] private global::PlushieOdyssey.Audio.SfxID shuffleSfx = global::PlushieOdyssey.Audio.SfxID.Shuffle;
        [SerializeField] private global::PlushieOdyssey.Audio.SfxID yourTurnSfx = global::PlushieOdyssey.Audio.SfxID.YourTurn;

        private readonly List<Card> drawPile = new();
        private readonly List<Card> discardPile = new();
        private readonly List<Card> exhaustPile = new();
        private readonly List<Card> hand = new();
        private readonly HashSet<Card> reservedPlayedCards = new();
        private bool prioritizeInnateOnOpeningDraw;

        public IReadOnlyList<Card> DrawPile => drawPile;
        public IReadOnlyList<Card> DiscardPile => discardPile;
        public IReadOnlyList<Card> ExhaustPile => exhaustPile;
        public IReadOnlyList<Card> Hand => hand;
        public int MaxHandSize => Mathf.Max(1, maxHandSize);

        // Tactical turn & combat counters (ComboDamageEffect, HandSizeDamageEffect, Retain scaling)
        public int CardsPlayedThisTurn { get; private set; }
        public int CardsDrawnThisTurn { get; private set; }
        public int AttacksPlayedThisTurn { get; private set; }
        public Card LastPlayedCard { get; private set; }
        public int CurrentEndTurnHandCount { get; private set; }

        public event Action PilesChanged;
        public event Action HandFullRejected;
        public event Action CardsPlayedThisTurnChanged;

        public bool ContainsCard(Card card) => card != null && hand.Contains(card);

        public int GetEffectiveManaCost(Card card)
        {
            if (card == null || card.IsXCost) return 0;
            int cost = card.Mana - card.CombatManaModifier - card.TurnManaModifier;
            if (RunManager.Instance != null &&
                RunManager.Instance.CardCostModifiers.TryGetValue(card.Title, out int modifier))
            {
                cost -= modifier;
            }
            return Mathf.Max(0, cost);
        }

        private void OnEnable()
        {
            ActionSystem.AttachPerformer<DrawCardsGA>(DrawCardsPerformer);
            ActionSystem.AttachPerformer<DiscardAllCardsGA>(DiscardAllCardsPerformer);
            ActionSystem.AttachPerformer<PlayCardGA>(PlayCardPerformer);
            ActionSystem.AttachPerformer<ApplyCostModifierGA>(ApplyCostModifierPerformer);
            ActionSystem.AttachPerformer<ScryGA>(ScryPerformer);
            ActionSystem.AttachPerformer<DiscoverCardGA>(DiscoverCardPerformer);
            ActionSystem.AttachPerformer<HandChoiceGA>(HandChoicePerformer);
            ActionSystem.AttachPerformer<InjectCardGA>(InjectCardPerformer);
            ActionSystem.AttachPerformer<ReduceHandCardCostGA>(ReduceHandCardCostPerformer);
            ActionSystem.AttachPerformer<InjectCardGA>(InjectCardPerformer);
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
            ActionSystem.DetachPerformer<ScryGA>();
            ActionSystem.DetachPerformer<DiscoverCardGA>();
            ActionSystem.DetachPerformer<HandChoiceGA>();
            ActionSystem.DetachPerformer<InjectCardGA>();
            ActionSystem.DetachPerformer<ReduceHandCardCostGA>();
            ActionSystem.DetachPerformer<InjectCardGA>();
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(EnemyTurnPreReaction, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(EnemyTurnPostReaction, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<CombatWonGA>(CombatWonReaction, ReactionTiming.POST);
        }

        public void Setup(List<CardData> deckData)
        {
            drawPile.Clear();
            discardPile.Clear();
            exhaustPile.Clear();
            hand.Clear();
            reservedPlayedCards.Clear();
            prioritizeInnateOnOpeningDraw = true;
            CardsPlayedThisTurn = 0;
            AttacksPlayedThisTurn = 0;
            LastPlayedCard = null;
            CurrentEndTurnHandCount = 0;
            CardsPlayedThisTurn = 0;
            CardsDrawnThisTurn = 0;

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

            // Shuffle drawPile at combat start so top-of-deck (end of list) is deterministic for Scry & DrawPileTop
            for (int i = 0; i < drawPile.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, drawPile.Count);
                Card temp = drawPile[i];
                drawPile[i] = drawPile[randomIndex];
                drawPile[randomIndex] = temp;
            }

            UpdatePileTexts();
        }

        /// <summary>
        /// Immediately removes a card from hand when queued for play, launches its non-blocking
        /// exit animation in parallel, and marks it reserved so PlayCardPerformer executes cleanly in FIFO order.
        /// </summary>
        public void ReserveCardPlay(Card card)
        {
            if (card == null) return;
            hand.Remove(card);
            reservedPlayedCards.Add(card);
            CardView cardView = handView != null ? handView.RemoveCard(card) : null;
            LaunchPlayExitAnimation(cardView, card);
        }

    #region Performers
        private IEnumerator DrawCardsPerformer(DrawCardsGA drawCardsGA)
        {
            int remainingToDraw = drawCardsGA.Amount;
            WaitForSeconds staggerWait = drawStaggerDelay > 0f ? new WaitForSeconds(drawStaggerDelay) : null;

            for (int i = 0; i < remainingToDraw; i++)
            {
                if (hand.Count >= MaxHandSize)
                {
                    HandFullRejected?.Invoke();
                    break;
                }

                if (drawPile.Count == 0)
                {
                    if (discardPile.Count == 0) break;
                    yield return RefillDeckWithAnimation();
                }

                if (drawPile.Count == 0) break;
                DrawCardNonBlocking();
                if (i < remainingToDraw - 1 && staggerWait != null)
                {
                    yield return staggerWait;
                }
            }

            prioritizeInnateOnOpeningDraw = false;
        }

        private IEnumerator DiscardAllCardsPerformer(DiscardAllCardsGA discardAllCardsGA)
        {
            List<Card> retained = new();
            List<Card> toProcess = new(hand);
            CurrentEndTurnHandCount = toProcess.Count;

            // Trigger End-of-Turn-in-Hand clutter effects (Burn, Doubt, Regret) before discarding
            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            for (int i = 0; i < toProcess.Count; i++)
            {
                Card c = toProcess[i];
                if (c?.EndTurnInHandEffects == null || c.EndTurnInHandEffects.Count == 0) continue;
                for (int e = 0; e < c.EndTurnInHandEffects.Count; e++)
                {
                    AutoTargetEffect wrapper = c.EndTurnInHandEffects[e];
                    if (wrapper?.Effect == null || wrapper.TargetMode == null) continue;
                    List<CombatantView> targets = wrapper.TargetMode.GetTargets(hero);
                    ActionSystem.Instance.AddReaction(new PerformEffectGA(wrapper.Effect, targets, hero, 1));
                }
            }


            hand.Clear();

            int animIndex = 0;
            foreach (Card card in toProcess)
            {
                if (card == null) continue;
                card.ResetTurnModifiers();

                if (card.HasKeyword(CardKeywords.Retain))
                {
                    card.TurnsRetained++;
                    retained.Add(card);
                    continue;
                }

                CardView cardView = handView != null ? handView.RemoveCard(card) : null;
                float delay = animIndex * discardStaggerDelay;
                animIndex++;

                if (card.HasKeyword(CardKeywords.Ethereal))
                {
                    LaunchExhaustAnimation(cardView, card, delay);
                }
                else
                {
                    LaunchDiscardAnimation(cardView, card, delay);
                }
            }

            // Reset any turn-scoped cost modifiers on draw/discard cards as well
            for (int i = 0; i < drawPile.Count; i++) drawPile[i]?.ResetTurnModifiers();
            for (int i = 0; i < discardPile.Count; i++) discardPile[i]?.ResetTurnModifiers();

            hand.AddRange(retained);
            UpdatePileTexts();

            // Let the end-turn discard cascade visibly swoop before the enemy turn swings
            if (animIndex > 0)
            {
                float breath = Mathf.Clamp(animIndex * discardStaggerDelay * 0.7f + discardFlyDuration * 0.45f, 0.14f, 0.34f);
                yield return new WaitForSeconds(breath);
            }
        }

        private IEnumerator PlayCardPerformer(PlayCardGA playCardGA)
        {
            Card card = playCardGA.Card;
            bool alreadyReserved = card != null && reservedPlayedCards.Remove(card);
            if (!alreadyReserved && card != null)
            {
                hand.Remove(card);
                CardView cardView = handView != null ? handView.RemoveCard(card) : null;
                LaunchPlayExitAnimation(cardView, card);
            }

            CardsPlayedThisTurn++;

            if (playCardSfx != global::PlushieOdyssey.Audio.SfxID.None) global::PlushieOdyssey.Audio.Tune.SFX(playCardSfx);

            if (card != null && card.PlaySfx != global::PlushieOdyssey.Audio.SfxID.None)
            {
                global::PlushieOdyssey.Audio.Tune.SFX(card.PlaySfx);
            }

            int spentMana = ResolveManaToSpend(playCardGA);
            int xMultiplier = card != null ? card.GetEffectiveX(spentMana) : 1;

            // Update turn counters AFTER evaluating Combo/Finisher effects or track LastPlayedCard first!
            LastPlayedCard = card;

            if (!(card != null && card.IsXCost && xMultiplier <= 0))
            {
                PlayCardVisuals(playCardGA);
            }

            // Execute mana spend and card effects immediately in strict FIFO order without waiting for card fly-out!
            SpendMana(spentMana);
            if (card != null && card.IsPersistentPower)
            {
                PowerSystem.Instance?.RegisterPowerCard(card);
            }
            DoManualTargetEffect(playCardGA, xMultiplier);
            DoAutoTargetEffect(playCardGA, xMultiplier);

            if (card != null)
            {
                CardsPlayedThisTurn++;
                if (card.Type == CardType.Attack) AttacksPlayedThisTurn++;
                CardsPlayedThisTurnChanged?.Invoke();
            }
            yield break;
        }

        private IEnumerator ScryPerformer(ScryGA scryGA)
        {
            if (scryGA == null || scryGA.Amount <= 0 || drawPile.Count == 0) yield break;

            int peekCount = Mathf.Min(scryGA.Amount, drawPile.Count);
            List<Card> topCards = new(peekCount);
            // Draw() pops from the end of drawPile (index drawPile.Count - 1)
            for (int i = 0; i < peekCount; i++)
            {
                topCards.Add(drawPile[drawPile.Count - 1 - i]);
            }

            bool done = false;
            List<Card> chosenToDiscard = null;
            CombatCardPickerUI.EnsureInstance().OpenScry(topCards, discarded =>
            {
                chosenToDiscard = discarded;
                done = true;
            });

            while (!done) yield return null;

            if (chosenToDiscard != null && chosenToDiscard.Count > 0)
            {
                for (int i = 0; i < chosenToDiscard.Count; i++)
                {
                    Card c = chosenToDiscard[i];
                    if (drawPile.Remove(c))
                    {
                        discardPile.Add(c);
                    }
                }
                CombatLogUI.Log($"<color=#7CE8FF>[Scry]</color> Discarded {chosenToDiscard.Count} card(s) from top of Draw Pile.");
                UpdatePileTexts();
            }
        }

        private IEnumerator DiscoverCardPerformer(DiscoverCardGA discoverGA)
        {
            if (discoverGA == null) yield break;
            List<CardData> pool = ResolveDiscoverPool(discoverGA.CandidatePool);
            if (pool.Count == 0) yield break;

            int count = Mathf.Min(discoverGA.ChoiceCount, pool.Count);
            List<CardData> choices = new(count);
            for (int i = 0; i < count; i++)
            {
                int idx = UnityEngine.Random.Range(0, pool.Count);
                choices.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            bool done = false;
            CardData picked = null;
            CombatCardPickerUI.EnsureInstance().OpenDiscover(choices, discoverGA.MakeZeroCostThisTurn, chosen =>
            {
                picked = chosen;
                done = true;
            });

            while (!done) yield return null;

            if (picked != null)
            {
                AddDiscoveredCardToHand(picked, discoverGA.MakeZeroCostThisTurn);
                CombatLogUI.Log($"<color=#FFD54F>[Discover]</color> Added <b>{picked.Title}</b> to hand{(discoverGA.MakeZeroCostThisTurn ? " (0 Mana this turn)" : string.Empty)}.");
            }
        }

        private IEnumerator HandChoicePerformer(HandChoiceGA choiceGA)
        {
            if (choiceGA == null || hand.Count == 0) yield break;

            List<Card> eligible = new();
            for (int i = 0; i < hand.Count; i++)
            {
                Card c = hand[i];
                if (c == null) continue;
                if (choiceGA.Mode == HandChoiceMode.UpgradeInCombat &&
                    (c.IsUpgradedInCombat || c.IsStatusOrCurse || c.HasKeyword(CardKeywords.Unplayable)))
                {
                    continue;
                }
                eligible.Add(c);
            }

            if (eligible.Count == 0) yield break;

            int targetCount = Mathf.Min(choiceGA.Count, eligible.Count);
            bool done = false;
            List<Card> chosen = null;

            if (eligible.Count == 1 && choiceGA.Mode != HandChoiceMode.UpgradeInCombat)
            {
                chosen = new List<Card> { eligible[0] };
                done = true;
            }
            else
            {
                CombatCardPickerUI.EnsureInstance().OpenHandChoice(choiceGA.Mode, eligible, targetCount, picked =>
                {
                    chosen = picked;
                    done = true;
                });
                while (!done) yield return null;
            }

            if (chosen == null || chosen.Count == 0) yield break;

            for (int i = 0; i < chosen.Count; i++)
            {
                Card c = chosen[i];
                if (c == null) continue;

                if (choiceGA.Mode == HandChoiceMode.UpgradeInCombat)
                {
                    if (c.UpgradeInCombat())
                    {
                        Tune.SFX(SfxID.card_upgrade);
                        RefreshSingleHandCardView(c);
                        CombatLogUI.Log($"<color=#59E892>[Upgrade]</color> Upgraded <b>{c.Title}</b> for this combat!");
                    }
                }
                else if (choiceGA.Mode == HandChoiceMode.Exhaust)
                {
                    if (hand.Remove(c))
                    {
                        CardView cv = handView != null ? handView.RemoveCard(c) : null;
                        LaunchExhaustAnimation(cv, c, 0f);
                        CombatLogUI.Log($"<color=#D18BFF>[Exhaust]</color> Exhausted <b>{c.Title}</b> from hand.");
                    }
                }
                else // Discard
                {
                    if (hand.Remove(c))
                    {
                        CardView cv = handView != null ? handView.RemoveCard(c) : null;
                        LaunchDiscardAnimation(cv, c, 0f);
                        CombatLogUI.Log($"<color=#FFAB40>[Discard]</color> Discarded <b>{c.Title}</b> from hand.");
                    }
                }
            }

            UpdatePileTexts();
            FindFirstObjectByType<CombatHandController>()?.RefreshCardStates();
        }

        private IEnumerator InjectCardPerformer(InjectCardGA injectGA)
        {
            if (injectGA?.CardData == null || injectGA.Count <= 0) yield break;

            for (int i = 0; i < injectGA.Count; i++)
            {
                Card injected = new(injectGA.CardData);
                switch (injectGA.Destination)
                {
                    case CardPileDestination.Hand:
                        if (hand.Count < MaxHandSize)
                        {
                            AddCardInstanceToHand(injected);
                        }
                        else
                        {
                            discardPile.Add(injected);
                        }
                        break;

                    case CardPileDestination.DrawPileTop:
                        drawPile.Add(injected);
                        break;

                    case CardPileDestination.DrawPileRandom:
                        int insertIdx = UnityEngine.Random.Range(0, drawPile.Count + 1);
                        drawPile.Insert(insertIdx, injected);
                        break;

                    default:
                        discardPile.Add(injected);
                        break;
                }
            }

            CombatLogUI.Log($"<color=#D18BFF>[Injected]</color> Added {injectGA.Count}× <b>{injectGA.CardData.Title}</b> ({injectGA.Destination}).");
            UpdatePileTexts();
            yield break;
        }

        private IEnumerator ReduceHandCardCostPerformer(ReduceHandCardCostGA ga)
        {
            if (ga == null || hand.Count == 0) yield break;

            List<Card> candidates = new();
            for (int i = 0; i < hand.Count; i++)
            {
                Card c = hand[i];
                if (c != null && !c.IsXCost && !c.HasKeyword(CardKeywords.Unplayable) && GetEffectiveManaCost(c) > 0)
                {
                    candidates.Add(c);
                }
            }

            if (candidates.Count == 0) yield break;

            void ApplyTo(Card c)
            {
                if (ga.ThisTurnOnly) c.TurnManaModifier += ga.Reduction;
                else c.CombatManaModifier += ga.Reduction;
            }

            if (ga.TargetMode == HandCostReductionTarget.AllInHand)
            {
                for (int i = 0; i < candidates.Count; i++) ApplyTo(candidates[i]);
            }
            else if (ga.TargetMode == HandCostReductionTarget.HighestCostInHand)
            {
                Card best = candidates[0];
                int bestCost = GetEffectiveManaCost(best);
                for (int i = 1; i < candidates.Count; i++)
                {
                    int cost = GetEffectiveManaCost(candidates[i]);
                    if (cost > bestCost) { best = candidates[i]; bestCost = cost; }
                }
                ApplyTo(best);
            }
            else
            {
                ApplyTo(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
            }

            FindFirstObjectByType<CombatHandController>()?.RefreshCardStates();
            UpdatePileTexts();
            yield break;
        }

        private IEnumerator ApplyCostModifierPerformer(ApplyCostModifierGA applyCostModifierGA)
        {
            if (applyCostModifierGA == null) yield break;

            if (RunManager.Instance != null && !string.IsNullOrEmpty(applyCostModifierGA.TargetCardName))
            {
                if (!RunManager.Instance.CardCostModifiers.ContainsKey(applyCostModifierGA.TargetCardName))
                {
                    RunManager.Instance.CardCostModifiers[applyCostModifierGA.TargetCardName] = 0;
                }
                RunManager.Instance.CardCostModifiers[applyCostModifierGA.TargetCardName] += applyCostModifierGA.ReductionAmount;
            }

            RefreshHandCardVisuals();
            PilesChanged?.Invoke();
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
            CardsPlayedThisTurn = 0;
            AttacksPlayedThisTurn = 0;
            CardsPlayedThisTurnChanged?.Invoke();

            CardsPlayedThisTurn = 0;
            CardsDrawnThisTurn = 0;

            if (HeroSystem.Instance.HeroView.IsStunned) return;

            if (yourTurnSfx != global::PlushieOdyssey.Audio.SfxID.None) global::PlushieOdyssey.Audio.Tune.SFX(yourTurnSfx);

            DrawCardsGA drawCardsGA = new(enemyDrawCardsAmount);
            ActionSystem.Instance.AddReaction(drawCardsGA);
        }

        private void CombatWonReaction(CombatWonGA combatWonGA)
        {
            drawPile.Clear();
            discardPile.Clear();
            exhaustPile.Clear();
            UpdatePileTexts();
        }
    #endregion

    #region Helpers
        private void LaunchPlayExitAnimation(CardView cardView, Card fallbackCard)
        {
            Card card = cardView != null && cardView.Card != null ? cardView.Card : fallbackCard;
            bool isPersistentPower = card != null && card.IsPersistentPower;
            bool exhaustOnPlay = !isPersistentPower && card != null && card.HasKeyword(CardKeywords.Exhaust);
            if (isPersistentPower)
            {
                // True Persistent Power cards leave the deck cycle completely (neither Discard nor Exhaust!)
            }
            else if (exhaustOnPlay)
                exhaustPile.Add(card);
            else if (card != null)
                discardPile.Add(card);
            UpdatePileTexts();

            if (cardView == null) return;

            Canvas rootCanvas = GetCardRootCanvas(cardView);
            PrepareCardForExitAnimation(cardView, rootCanvas);

            Vector3 playCenter = GetPlayCenterPosition(rootCanvas);
            Vector3 exitPos = isPersistentPower
                ? playCenter + new Vector3(0f, 180f, 0f)
                : (exhaustOnPlay ? playCenter : GetDiscardPosition(rootCanvas, playCenter));
            float playScale = rootCanvas != null ? 1.18f : 1.35f;
            float moveDur = Mathf.Max(doTweenMoveDuration, 0.18f);
            float scaleDur = Mathf.Max(doTweenScaleDuration, 0.18f);

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(cardView.transform.DOMove(playCenter, moveDur).SetEase(Ease.OutQuad));
            seq.Join(cardView.transform.DORotate(Vector3.zero, moveDur));
            seq.Join(cardView.transform.DOScale(Vector3.one * playScale, scaleDur).SetEase(Ease.OutBack));
            seq.AppendInterval(0.09f);
            seq.Append(cardView.transform.DOScale(Vector3.zero, scaleDur).SetEase(Ease.InBack));
            seq.Join(cardView.transform.DOMove(exitPos, moveDur).SetEase(Ease.InQuad));
            seq.OnComplete(() =>
            {
                if (cardView != null)
                {
                    cardView.transform.DOKill();
                    Destroy(cardView.gameObject);
                }
            });
        }

        private void LaunchDiscardAnimation(CardView cardView, Card fallbackCard, float delay)
        {
            Card card = cardView != null && cardView.Card != null ? cardView.Card : fallbackCard;
            if (card != null) discardPile.Add(card);

            if (cardView == null) return;

            Canvas rootCanvas = GetCardRootCanvas(cardView);
            PrepareCardForExitAnimation(cardView, rootCanvas);
            Vector3 discardPos = GetDiscardPosition(rootCanvas, cardView.transform.position);
            float dur = Mathf.Max(discardFlyDuration, 0.18f);

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            if (delay > 0f) seq.AppendInterval(delay);
            seq.Append(cardView.transform.DOMove(discardPos, dur).SetEase(Ease.InOutCubic));
            seq.Join(cardView.transform.DOScale(Vector3.zero, dur).SetEase(Ease.InQuad));
            seq.Join(cardView.transform.DORotate(new Vector3(0f, 0f, -24f), dur).SetEase(Ease.InQuad));
            seq.OnComplete(() =>
            {
                if (cardView != null)
                {
                    cardView.transform.DOKill();
                    Destroy(cardView.gameObject);
                }
            });
        }

        private void LaunchExhaustAnimation(CardView cardView, Card fallbackCard, float delay)
        {
            Card card = cardView != null && cardView.Card != null ? cardView.Card : fallbackCard;
            if (card != null) exhaustPile.Add(card);

            if (cardView == null) return;

            Canvas rootCanvas = GetCardRootCanvas(cardView);
            PrepareCardForExitAnimation(cardView, rootCanvas);
            float dur = Mathf.Max(discardFlyDuration, 0.18f);

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            if (delay > 0f) seq.AppendInterval(delay);
            seq.Append(cardView.transform.DOScale(Vector3.zero, dur).SetEase(Ease.InBack));
            seq.OnComplete(() =>
            {
                if (cardView != null)
                {
                    cardView.transform.DOKill();
                    Destroy(cardView.gameObject);
                }
            });
        }

        private void DrawCardNonBlocking()
        {
            if (hand.Count >= MaxHandSize)
            {
                HandFullRejected?.Invoke();
                return;
            }

            Card card = PopNextCardToDraw();
            if (card == null) return;

            CardsDrawnThisTurn++;
            UpdatePileTexts();
            if (drawCardSfx != global::PlushieOdyssey.Audio.SfxID.None) global::PlushieOdyssey.Audio.Tune.SFX(drawCardSfx);
            GetDrawSpawnPose(out Vector3 spawnPos, out Quaternion spawnRot);
            CardView cardView = CardViewCreator.Instance.CreateCardView(card, spawnPos, spawnRot);
            hand.Add(card);
            UpdatePileTexts();
            handView.AddCardImmediate(cardView);

            // Trigger OnDrawEffects (e.g. Status_Void draining 1 Mana when drawn)
            if (card.OnDrawEffects != null && card.OnDrawEffects.Count > 0 && ActionSystem.Instance != null)
            {
                HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
                for (int i = 0; i < card.OnDrawEffects.Count; i++)
                {
                    AutoTargetEffect wrapper = card.OnDrawEffects[i];
                    if (wrapper?.Effect == null || wrapper.TargetMode == null) continue;
                    List<CombatantView> targets = wrapper.TargetMode.GetTargets(hero);
                    ActionSystem.Instance.AddReaction(new PerformEffectGA(wrapper.Effect, targets, hero, 1));
                }
            }

            // Execute automatic On-Draw Status/Curse effects (e.g. Void draining 1 Mana)
            if (card.OnDrawEffects != null && card.OnDrawEffects.Count > 0 && ActionSystem.Instance != null)
            {
                HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
                for (int i = 0; i < card.OnDrawEffects.Count; i++)
                {
                    AutoTargetEffect wrapper = card.OnDrawEffects[i];
                    if (wrapper?.Effect == null) continue;
                    List<CombatantView> targets = wrapper.TargetMode != null
                        ? wrapper.TargetMode.GetTargets(hero)
                        : (hero != null ? new List<CombatantView> { hero } : new List<CombatantView>());
                    GameAction action = wrapper.Effect.GetGameAction(targets, hero);
                    if (action != null) ActionSystem.Instance.AddReaction(action);
                }
            }
        }

        public List<Card> PeekTopDrawCards(int amount)
        {
            List<Card> result = new();
            if (amount <= 0 || drawPile.Count == 0) return result;
            // ListExtensions.Draw() draws from index 0 (or end); check how Draw() works — we peek from end & front consistently
            int count = Mathf.Min(amount, drawPile.Count);
            for (int i = 0; i < count; i++)
            {
                result.Add(drawPile[drawPile.Count - 1 - i]);
            }
            return result;
        }

        public void DiscardSpecificFromDrawPile(IReadOnlyList<Card> cardsToDiscard)
        {
            if (cardsToDiscard == null || cardsToDiscard.Count == 0) return;
            foreach (Card c in cardsToDiscard)
            {
                if (c != null && drawPile.Remove(c))
                {
                    discardPile.Add(c);
                }
            }
            UpdatePileTexts();
        }

        public void AddRuntimeCardToHand(Card card)
        {
            if (card == null) return;
            if (hand.Count >= MaxHandSize)
            {
                discardPile.Add(card);
                HandFullRejected?.Invoke();
                UpdatePileTexts();
                return;
            }
            if (CardViewCreator.Instance == null || handView == null)
            {
                hand.Add(card);
                UpdatePileTexts();
                return;
            }
            GetDrawSpawnPose(out Vector3 pos, out Quaternion rot);
            CardView cardView = CardViewCreator.Instance.CreateCardView(card, pos, rot);
            hand.Add(card);
            UpdatePileTexts();
            handView.AddCardImmediate(cardView);
        }

        public bool DiscardCardFromHand(Card card)
        {
            if (card == null || !hand.Remove(card)) return false;
            CardView cardView = handView != null ? handView.RemoveCard(card) : null;
            LaunchDiscardAnimation(cardView, card, 0f);
            UpdatePileTexts();
            return true;
        }

        public bool ExhaustCardFromHand(Card card)
        {
            if (card == null || !hand.Remove(card)) return false;
            CardView cardView = handView != null ? handView.RemoveCard(card) : null;
            LaunchExhaustAnimation(cardView, card, 0f);
            UpdatePileTexts();
            return true;
        }

        public bool UpgradeCardInHand(Card card)
        {
            if (card == null || !hand.Contains(card)) return false;
            if (!card.UpgradeInCombat()) return false;
            RefreshHandCardVisuals();
            PilesChanged?.Invoke();
            return true;
        }

        public int UpgradeAllCardsInHand()
        {
            int upgraded = 0;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] != null && hand[i].UpgradeInCombat()) upgraded++;
            }
            if (upgraded > 0)
            {
                RefreshHandCardVisuals();
                PilesChanged?.Invoke();
            }
            return upgraded;
        }

        public void RefreshHandCardVisuals()
        {
            if (handView == null || handView.Cards == null) return;
            foreach (CardView view in handView.Cards)
            {
                if (view != null && view.Card != null)
                {
                    view.RefreshFromCard();
                    view.SetDisplayedCost(GetEffectiveManaCost(view.Card));
                }
            }
            FindFirstObjectByType<CombatHandController>()?.RefreshCardStates();
        }

        private Card PopNextCardToDraw()
        {
            if (drawPile.Count == 0) return null;

            if (prioritizeInnateOnOpeningDraw)
            {
                for (int i = 0; i < drawPile.Count; i++)
                {
                    if (drawPile[i] != null && drawPile[i].HasKeyword(CardKeywords.Innate))
                    {
                        Card innate = drawPile[i];
                        drawPile.RemoveAt(i);
                        return innate;
                    }
                }
            }

            // Pop from top of deck (end of list) so Scry and DrawPileTop are 100% deterministic
            int topIndex = drawPile.Count - 1;
            Card top = drawPile[topIndex];
            drawPile.RemoveAt(topIndex);
            return top;
        }

        /// <summary>
        /// Injects a card directly into the player's active hand during combat without modifying assets on disk.
        /// </summary>
        public void AddCardToHand(CardData cardData)
        {
            if (cardData == null) return;
            AddCardInstanceToHand(new Card(cardData));
        }

        public void AddDiscoveredCardToHand(CardData cardData, bool makeZeroCostThisTurn)
        {
            if (cardData == null) return;
            Card card = new(cardData);
            if (makeZeroCostThisTurn && !card.IsXCost && card.Mana > 0)
            {
                card.TurnManaModifier += card.Mana;
            }
            AddCardInstanceToHand(card);
        }

        private void AddCardInstanceToHand(Card card)
        {
            if (card == null || CardViewCreator.Instance == null || handView == null) return;
            GetDrawSpawnPose(out Vector3 pos, out Quaternion rot);
            CardView cardView = CardViewCreator.Instance.CreateCardView(card, pos, rot);
            hand.Add(card);
            UpdatePileTexts();
            handView.AddCardImmediate(cardView);
            FindFirstObjectByType<CombatHandController>()?.RefreshCardStates();
        }

        private void RefreshSingleHandCardView(Card card)
        {
            if (card == null || handView == null || handView.Cards == null) return;
            foreach (CardView cv in handView.Cards)
            {
                if (cv != null && cv.Card == card)
                {
                    cv.RefreshFromCard();
                    cv.transform.DOKill();
                    cv.transform.DOPunchScale(Vector3.one * 0.22f, 0.30f, 6, 0.6f).SetUpdate(true);
                    break;
                }
            }
        }

        private static List<CardData> ResolveDiscoverPool(List<CardData> explicitPool)
        {
            List<CardData> pool = new();
            HashSet<CardData> seen = new();

            void TryAdd(CardData c)
            {
                if (c == null || !seen.Add(c)) return;
                if ((c.Keywords & (CardKeywords.Status | CardKeywords.Curse | CardKeywords.Unplayable)) != 0) return;
                pool.Add(c);
            }

            if (explicitPool != null)
            {
                for (int i = 0; i < explicitPool.Count; i++) TryAdd(explicitPool[i]);
            }

            if (pool.Count == 0)
            {
                CardData[] loaded = Resources.FindObjectsOfTypeAll<CardData>();
                for (int i = 0; i < loaded.Length; i++) TryAdd(loaded[i]);
            }

            if (pool.Count == 0 && RunDeckManager.Instance != null)
            {
                foreach (CardData c in RunDeckManager.Instance.CurrentDeck) TryAdd(c);
            }

            return pool;
        }

        private Canvas GetHandRootCanvas()
        {
            if (handView == null) return null;
            Canvas canvas = handView.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas : null;
        }

        private Canvas GetCardRootCanvas(CardView cardView)
        {
            if (cardView != null && cardView.transform is RectTransform)
            {
                Canvas canvas = cardView.GetComponentInParent<Canvas>();
                if (canvas != null) return canvas.rootCanvas;
            }
            return GetHandRootCanvas();
        }

        private void GetDrawSpawnPose(out Vector3 spawnPos, out Quaternion spawnRot)
        {
            Canvas rootCanvas = GetHandRootCanvas();
            if (rootCanvas != null && handView != null)
            {
                Vector3 pileWorld = GetCanvasWorldPosition(drawPilePoint, rootCanvas, handView.transform.position);
                Vector3 localInHand = handView.transform.InverseTransformPoint(pileWorld);
                localInHand.z = 0f;
                spawnPos = localInHand;
                spawnRot = Quaternion.identity;
                return;
            }

            spawnPos = drawPilePoint != null ? drawPilePoint.position : Vector3.zero;
            spawnRot = drawPilePoint != null ? drawPilePoint.rotation : Quaternion.identity;
        }

        private Vector3 GetPlayCenterPosition(Canvas rootCanvas)
        {
            if (rootCanvas != null && rootCanvas.transform is RectTransform canvasRect)
            {
                return canvasRect.TransformPoint(canvasRect.rect.center);
            }
            return new Vector3(0f, 0f, -1f);
        }

        private Vector3 GetDiscardPosition(Canvas rootCanvas, Vector3 fallback)
        {
            if (rootCanvas != null)
            {
                return GetCanvasWorldPosition(discardPilePoint, rootCanvas, fallback);
            }
            return discardPilePoint != null ? discardPilePoint.position : fallback;
        }

        private static Vector3 GetCanvasWorldPosition(Transform point, Canvas rootCanvas, Vector3 fallback)
        {
            if (point == null) return fallback;
            if (point is RectTransform pointRect)
            {
                return pointRect.TransformPoint(pointRect.rect.center);
            }

            Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? Camera.main
                : (rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main);
            if (cam == null) return fallback;

            Vector3 screenPoint = cam.WorldToScreenPoint(point.position);
            if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return new Vector3(screenPoint.x, screenPoint.y, 0f);
            }

            if (rootCanvas.transform is RectTransform canvasRect &&
                RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPoint, cam, out Vector3 worldOnCanvas))
            {
                return worldOnCanvas;
            }

            return fallback;
        }

        private static void PrepareCardForExitAnimation(CardView cardView, Canvas rootCanvas)
        {
            if (cardView == null) return;
            cardView.transform.DOKill();

            if (cardView.TryGetComponent(out CardPointerInput pointerInput))
            {
                pointerInput.SetInteractable(false);
            }

            if (rootCanvas != null && cardView.transform is RectTransform cardRect)
            {
                cardRect.SetParent(rootCanvas.transform, true);
                cardRect.SetAsLastSibling();

                if (!cardView.TryGetComponent(out CanvasGroup group))
                {
                    group = cardView.gameObject.AddComponent<CanvasGroup>();
                }
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
#if UNITY_EDITOR
                CardData testCard = UnityEditor.AssetDatabase.LoadAssetAtPath<CardData>("Assets/_Project/ScriptableObjects/Cards/Generated/Zoltraak.asset");
                if (testCard != null)
                {
                    AddCardToHand(testCard);
                    Debug.Log("<color=cyan><b>[CombatDebug] F1 Pressed: Injected 'Zoltraak' into hand for testing.</b></color>");
                }
#endif
            }
        }
#endif

        private IEnumerator RefillDeckWithAnimation()
        {
            int countToShuffle = discardPile.Count;
            if (countToShuffle <= 0) yield break;

            if (shuffleSfx != global::PlushieOdyssey.Audio.SfxID.None) global::PlushieOdyssey.Audio.Tune.SFX(shuffleSfx);

            // Shuffle discard pile into draw pile immediately in data so combat state stays consistent
            for (int i = 0; i < discardPile.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, discardPile.Count);
                Card temp = discardPile[i];
                discardPile[i] = discardPile[randomIndex];
                discardPile[randomIndex] = temp;
            }

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            PilesChanged?.Invoke();

            Canvas rootCanvas = GetHandRootCanvas();
            DeckShuffleAnimatorUI animator = DeckShuffleAnimatorUI.GetOrCreate(rootCanvas);
            if (animator != null && discardPilePoint != null && drawPilePoint != null)
            {
                // Animate the counters ticking live from Discard -> Draw as the cards fly across the screen!
                if (discardPileText != null) discardPileText.text = countToShuffle.ToString();
                if (drawPileText != null) drawPileText.text = "0";
                bool resumedDrawing = false;

                float resumeDrawWait = animator.PlayShuffle(
                    discardPilePoint,
                    drawPilePoint,
                    countToShuffle,
                    onDiscardTick: remaining => { if (!resumedDrawing && discardPileText != null) discardPileText.text = remaining.ToString(); },
                    onDrawTick: arrived => { if (!resumedDrawing && drawPileText != null) drawPileText.text = arrived.ToString(); },
                    onAllLanded: UpdatePileTexts);

                if (resumeDrawWait > 0f)
                {
                    yield return new WaitForSecondsRealtime(resumeDrawWait);
                }

                resumedDrawing = true;
                // Sync live pile texts before the next card immediately draws out of the freshly refilled deck
                UpdatePileTexts();
            }
            else
            {
                UpdatePileTexts();
            }
        }

        private int ResolveManaToSpend(PlayCardGA playCardGA)
        {
            if (playCardGA == null || playCardGA.Card == null) return 0;
            if (playCardGA.HasManaSpentOverride) return playCardGA.ManaSpent;
            if (playCardGA.Card.IsXCost)
            {
                return ManaSystem.Instance != null ? ManaSystem.Instance.AvailableMana : 0;
            }
            return GetEffectiveManaCost(playCardGA.Card);
        }

        private void SpendMana(int cost)
        {
            SpendManaGA spendManaGA = new(Mathf.Max(0, cost));
            ActionSystem.Instance.AddReaction(spendManaGA);
        }

        private void DoManualTargetEffect(PlayCardGA playCardGA, int xMultiplier)
        {
            if (playCardGA.Card.ManualTargetEffect != null)
            {
                PerformEffectGA performEffectGA = new(
                    playCardGA.Card.ManualTargetEffect,
                    new() { playCardGA.ManualTarget },
                    caster: null,
                    xMultiplier: xMultiplier,
                    potencyBonus: playCardGA.Card.CombatUpgradeBonus);
                ActionSystem.Instance.AddReaction(performEffectGA);
            }
        }

        private void DoAutoTargetEffect(PlayCardGA playCardGA, int xMultiplier)
        {
            foreach (AutoTargetEffect effectWrapper in playCardGA.Card.OtherEffects)
            {
                List<CombatantView> targets = effectWrapper.TargetMode.GetTargets(HeroSystem.Instance.HeroView);
                PerformEffectGA performEffectGA = new(
                    effectWrapper.Effect,
                    targets,
                    caster: null,
                    xMultiplier: xMultiplier,
                    potencyBonus: playCardGA.Card.CombatUpgradeBonus);
                ActionSystem.Instance.AddReaction(performEffectGA);
            }
        }

        private void UpdatePileTexts()
        {
            if (drawPileText != null) drawPileText.text = drawPile.Count.ToString();
            if (discardPileText != null) discardPileText.text = discardPile.Count.ToString();
            if (exhaustPileText != null) exhaustPileText.text = exhaustPile.Count.ToString();
            PilesChanged?.Invoke();
        }

        public bool HasPlayableCards()
        {
            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            if (hero != null && hero.CurrentHealth <= 0) return false;
            bool lowWillActive = hero != null && (hero.HealthPenalty > 0 || hero.IsLowWill);
            int availableMana = ManaSystem.Instance != null ? ManaSystem.Instance.AvailableMana : 0;

            foreach (Card card in hand)
            {
                if (card == null) continue;
                if (card.HasKeyword(CardKeywords.Unplayable)) continue;
                if (card.Type == CardType.Attack && lowWillActive && !card.HasKeyword(CardKeywords.PlayableInLowWill)) continue;

                if (card.IsXCost)
                {
                    if (availableMana > 0 || card.XBonus > 0) return true;
                    continue;
                }

                int cost = GetEffectiveManaCost(card);
                if (ManaSystem.Instance == null || ManaSystem.Instance.HasEnoughMana(cost))
                {
                    return true;
                }
            }
            return false;
        }

        private void PlayCardVisuals(PlayCardGA playCardGA)
        {
            HashSet<CombatantView> targets = GetCardVfxTargets(playCardGA);

            if (playCardGA.Card.PlayVfx != null && playCardGA.Card.PlayVfx.HasPrefab)
            {
                playCardGA.Card.PlayVfx.Play(targets);
                return;
            }

            if (playCardGA.Card.PlayParticle == null) return;

            foreach (CombatantView target in targets)
            {
                Instantiate(playCardGA.Card.PlayParticle, target.transform.position, Quaternion.identity);
            }
        }

        private HashSet<CombatantView> GetCardVfxTargets(PlayCardGA playCardGA)
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
                if (HeroSystem.Instance.HeroView.HasAnimator)
                {
                    HeroSystem.Instance.HeroView.SetAnimationTrigger(playCardGA.Card.HeroAnimationTrigger);
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
                    if (target.HasAnimator)
                    {
                        target.SetAnimationTrigger(playCardGA.Card.TargetAnimationTrigger);
                    }
                }
            }

            return targets;
        }
    #endregion
    }
}
