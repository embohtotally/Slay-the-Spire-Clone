using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.Core.Extensions;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
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
        [SerializeField] private float doTweenScaleDuration = 0.15f;
        [SerializeField] private float doTweenMoveDuration = 0.15f;
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
        private bool prioritizeInnateOnOpeningDraw;

        public IReadOnlyList<Card> DrawPile => drawPile;
        public IReadOnlyList<Card> DiscardPile => discardPile;
        public IReadOnlyList<Card> ExhaustPile => exhaustPile;
        public IReadOnlyList<Card> Hand => hand;
        public int MaxHandSize => Mathf.Max(1, maxHandSize);

        public event Action PilesChanged;
        public event Action HandFullRejected;

        public bool ContainsCard(Card card) => card != null && hand.Contains(card);

        public int GetEffectiveManaCost(Card card)
        {
            if (card == null) return 0;
            int cost = card.Mana;
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
            exhaustPile.Clear();
            hand.Clear();
            prioritizeInnateOnOpeningDraw = true;

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
            int remainingToDraw = drawCardsGA.Amount;

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
                    RefillDeck();
                }

                if (drawPile.Count == 0) break;
                yield return DrawCard();
            }

            prioritizeInnateOnOpeningDraw = false;
        }

        private IEnumerator DiscardAllCardsPerformer(DiscardAllCardsGA discardAllCardsGA)
        {
            List<Card> retained = new();
            List<Card> toProcess = new(hand);

            foreach (Card card in toProcess)
            {
                if (card == null) continue;

                if (card.HasKeyword(CardKeywords.Retain))
                {
                    retained.Add(card);
                    continue;
                }

                CardView cardView = handView.RemoveCard(card);
                if (card.HasKeyword(CardKeywords.Ethereal))
                {
                    yield return ExhaustCard(cardView, card);
                }
                else
                {
                    yield return DiscardCard(cardView, card);
                }
            }

            hand.Clear();
            hand.AddRange(retained);
            UpdatePileTexts();
        }

        private IEnumerator PlayCardPerformer(PlayCardGA playCardGA)
        {
            hand.Remove(playCardGA.Card);
            CardView cardView = handView.RemoveCard(playCardGA.Card);

            if (playCardSfx != global::PlushieOdyssey.Audio.SfxID.None) global::PlushieOdyssey.Audio.Tune.SFX(playCardSfx);

            if (playCardGA.Card.PlaySfx != global::PlushieOdyssey.Audio.SfxID.None)
            {
                global::PlushieOdyssey.Audio.Tune.SFX(playCardGA.Card.PlaySfx);
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
        private IEnumerator PlayAndDiscardCard(CardView cardView)
        {
            if (cardView == null) yield break;

            bool exhaustOnPlay = cardView.Card != null && cardView.Card.HasKeyword(CardKeywords.Exhaust);
            if (exhaustOnPlay)
                exhaustPile.Add(cardView.Card);
            else if (cardView.Card != null)
                discardPile.Add(cardView.Card);
            UpdatePileTexts();

            Canvas rootCanvas = GetCardRootCanvas(cardView);
            PrepareCardForExitAnimation(cardView, rootCanvas);

            Vector3 playCenter = GetPlayCenterPosition(rootCanvas);
            Vector3 exitPos = exhaustOnPlay ? playCenter : GetDiscardPosition(rootCanvas, playCenter);
            float playScale = rootCanvas != null ? 1.25f : 1.5f;

            cardView.transform.DOMove(playCenter, doTweenMoveDuration);
            cardView.transform.DORotate(Vector3.zero, doTweenMoveDuration);
            Tween scaleTween = cardView.transform.DOScale(Vector3.one * playScale, doTweenScaleDuration);
            yield return scaleTween.WaitForCompletion();

            yield return new WaitForSeconds(0.18f);

            cardView.transform.DOScale(Vector3.zero, doTweenScaleDuration);
            Tween moveTween = cardView.transform.DOMove(exitPos, doTweenMoveDuration);
            yield return moveTween.WaitForCompletion();

            cardView.transform.DOKill();
            Destroy(cardView.gameObject);
        }

        private IEnumerator DiscardCard(CardView cardView, Card fallbackCard = null)
        {
            Card card = cardView != null ? cardView.Card : fallbackCard;
            if (card != null) discardPile.Add(card);
            UpdatePileTexts();

            if (cardView == null) yield break;

            Canvas rootCanvas = GetCardRootCanvas(cardView);
            PrepareCardForExitAnimation(cardView, rootCanvas);
            Vector3 discardPos = GetDiscardPosition(rootCanvas, cardView.transform.position);

            cardView.transform.DOScale(Vector3.zero, doTweenScaleDuration);
            Tween tween = cardView.transform.DOMove(discardPos, doTweenMoveDuration);
            yield return tween.WaitForCompletion();

            cardView.transform.DOKill();
            Destroy(cardView.gameObject);
        }

        private IEnumerator ExhaustCard(CardView cardView, Card fallbackCard = null)
        {
            Card card = cardView != null ? cardView.Card : fallbackCard;
            if (card != null) exhaustPile.Add(card);
            UpdatePileTexts();

            if (cardView == null) yield break;

            Canvas rootCanvas = GetCardRootCanvas(cardView);
            PrepareCardForExitAnimation(cardView, rootCanvas);

            Tween scaleTween = cardView.transform.DOScale(Vector3.zero, doTweenScaleDuration).SetEase(Ease.InBack);
            yield return scaleTween.WaitForCompletion();

            cardView.transform.DOKill();
            Destroy(cardView.gameObject);
        }

        private IEnumerator DrawCard()
        {
            if (hand.Count >= MaxHandSize)
            {
                HandFullRejected?.Invoke();
                yield break;
            }

            Card card = PopNextCardToDraw();
            if (card == null) yield break;

            UpdatePileTexts();
            if (drawCardSfx != global::PlushieOdyssey.Audio.SfxID.None) global::PlushieOdyssey.Audio.Tune.SFX(drawCardSfx);
            GetDrawSpawnPose(out Vector3 spawnPos, out Quaternion spawnRot);
            CardView cardView = CardViewCreator.Instance.CreateCardView(card, spawnPos, spawnRot);
            hand.Add(card);
            UpdatePileTexts();
            yield return handView.AddCard(cardView);
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

            return drawPile.Draw();
        }

        /// <summary>
        /// Injects a card directly into the player's active hand during combat without modifying assets on disk.
        /// </summary>
        public void AddCardToHand(CardData cardData)
        {
            if (cardData == null || CardViewCreator.Instance == null || handView == null) return;
            Card card = new(cardData);
            GetDrawSpawnPose(out Vector3 pos, out Quaternion rot);
            CardView cardView = CardViewCreator.Instance.CreateCardView(card, pos, rot);
            hand.Add(card);
            StartCoroutine(handView.AddCard(cardView));
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

        private void RefillDeck()
        {
            if (shuffleSfx != global::PlushieOdyssey.Audio.SfxID.None) global::PlushieOdyssey.Audio.Tune.SFX(shuffleSfx);
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
            int cost = GetEffectiveManaCost(playCardGA.Card);
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
            if (exhaustPileText != null) exhaustPileText.text = exhaustPile.Count.ToString();
            PilesChanged?.Invoke();
        }

        public bool HasPlayableCards()
        {
            foreach (Card card in hand)
            {
                int cost = GetEffectiveManaCost(card);

                if (ManaSystem.Instance.HasEnoughMana(cost))
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
