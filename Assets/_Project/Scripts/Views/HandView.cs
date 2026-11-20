using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using PlushieOdyssey.Combat.Models;
using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    public class HandView : MonoBehaviour
    {
        [SerializeField] private CardFanLayout _fanLayout;
        [Tooltip("Card count that fills the fan width; this is not a hand limit.")]
        [SerializeField, Min(1)] private int maxHandSize = 10;
        [SerializeField, Min(0f)] private float doTweenUpdatePositionDuration = 0.18f;
        [Tooltip("Smooth glide duration when a newly drawn card flies from the draw pile into the hand fan.")]
        [SerializeField, Min(0.05f)] private float drawCardFlyDuration = 0.28f;

        [Header("Modern Hover Spread")]
        [Tooltip("Horizontal spread applied to immediate neighbors of the hovered card.")]
        [SerializeField, Min(0f)] private float hoverNeighborSpread = 86f;
        [Tooltip("Decay factor per card distance away from the hovered card (0-1).")]
        [SerializeField, Range(0.1f, 0.95f)] private float hoverSpreadFalloff = 0.58f;
        [SerializeField, Min(0f)] private float hoverLayoutDuration = 0.14f;

        private Sequence _layoutTween;
        private CardView _suspendedCard;
        private CardView _hoveredCard;
        private bool _refreshOnEnable;

        [SerializeField, ReadOnly]
        private List<CardView> cards = new();

        public IReadOnlyList<CardView> Cards => cards;
        public event Action<CardView> CardAdded;
        public event Action<CardView> CardRemoved;

        private void Awake()
        {
            if (_fanLayout == null) _fanLayout = GetComponent<CardFanLayout>();
        }

        private void OnEnable()
        {
            _refreshOnEnable = true;
        }

        private void LateUpdate()
        {
            if (!_refreshOnEnable) return;
            _refreshOnEnable = false;
            if (cards.Count > 0) RefreshCardPositions();
        }

        private void OnDisable()
        {
            _layoutTween?.Kill();
            _layoutTween = null;
            _suspendedCard = null;
            _hoveredCard = null;
            _refreshOnEnable = false;
        }

        public CardView RemoveCard(Card card)
        {
            CardView cardView = GetCardView(card);

            if (cardView == null) return null;

            cards.Remove(cardView);
            if (_suspendedCard == cardView) _suspendedCard = null;
            if (_hoveredCard == cardView) _hoveredCard = null;
            CardRemoved?.Invoke(cardView);
            RefreshCardPositions();
            return cardView;
        }

        public IEnumerator AddCard(CardView cardView)
        {
            AddCardImmediate(cardView);
            if (_layoutTween != null) yield return _layoutTween.WaitForCompletion();
        }

        /// <summary>
        /// Adds a card to the hand immediately and triggers the fan layout tween without blocking callers.
        /// </summary>
        public void AddCardImmediate(CardView cardView)
        {
            if (cardView == null || cards.Contains(cardView)) return;
            if (!TryGetLayout()) return;

            if (cardView.transform is not RectTransform cardRect)
            {
                Debug.LogWarning("HandView requires a Canvas card prefab with a RectTransform.", this);
                return;
            }

            cardRect.SetParent(_fanLayout.RectTransform, false);
            Vector3 localPosition = cardRect.localPosition;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.localPosition = localPosition;
            cardRect.localScale = Vector3.one * 0.45f;
            cardRect.DOScale(Vector3.one, drawCardFlyDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            cards.Add(cardView);
            CardAdded?.Invoke(cardView);
            UpdateCardPositions(drawCardFlyDuration);
        }

        private Tween UpdateCardPositions(float duration)
        {
            _layoutTween?.Kill();
            _layoutTween = null;
            cards.RemoveAll(card => card == null);

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy ||
                cards.Count == 0 || !TryGetLayout() || !_fanLayout.gameObject.activeInHierarchy)
                return null;

            duration = Mathf.Max(0f, duration);
            int hoveredIndex = _hoveredCard != null ? cards.IndexOf(_hoveredCard) : -1;

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == _suspendedCard) continue;
                if (cards[i].transform is not RectTransform cardRect) continue;

                _fanLayout.GetCardPose(i, cards.Count, maxHandSize, out Vector2 position, out float angle);

                if (hoveredIndex >= 0)
                {
                    if (i == hoveredIndex)
                    {
                        angle = 0f;
                    }
                    else
                    {
                        int delta = i - hoveredIndex;
                        int distance = Mathf.Abs(delta);
                        float spread = hoverNeighborSpread * Mathf.Pow(hoverSpreadFalloff, distance - 1);
                        position.x += Mathf.Sign(delta) * spread;
                    }
                }

                Vector3 anchoredPosition = new Vector3(position.x, position.y, 0f);
                Vector3 rotation = new Vector3(0f, 0f, angle);
                cardRect.SetSiblingIndex(i);

                if (duration == 0f)
                {
                    cardRect.anchoredPosition3D = anchoredPosition;
                    cardRect.localRotation = Quaternion.Euler(rotation);
                    continue;
                }

                if (_layoutTween == null)
                {
                    _layoutTween = DOTween.Sequence()
                        .SetAutoKill(true)
                        .SetRecyclable(false)
                        .SetUpdate(true)
                        .OnKill(() => _layoutTween = null);
                }
                _layoutTween.Join(cardRect.DOAnchorPos3D(anchoredPosition, duration).SetEase(Ease.OutCubic));
                _layoutTween.Join(cardRect.DOLocalRotate(rotation, duration).SetEase(Ease.OutCubic));
            }

            if (hoveredIndex >= 0 && cards[hoveredIndex] != null && cards[hoveredIndex] != _suspendedCard)
            {
                cards[hoveredIndex].transform.SetAsLastSibling();
            }

            return _layoutTween;
        }

        public void SetHoveredCard(CardView cardView)
        {
            CardView next = cardView != null && cards.Contains(cardView) ? cardView : null;
            if (_hoveredCard == next) return;
            _hoveredCard = next;
            UpdateCardPositions(hoverLayoutDuration);
        }

        [Button(enabledMode: EButtonEnableMode.Playmode)]
        public void RefreshCardPositions()
        {
            UpdateCardPositions(doTweenUpdatePositionDuration);
        }

        public void SuspendCardLayout(CardView cardView)
        {
            if (!cards.Contains(cardView)) return;
            _suspendedCard = cardView;
            if (_hoveredCard == cardView) _hoveredCard = null;
            RefreshCardPositions();
        }

        public void ResumeCardLayout(CardView cardView)
        {
            if (_suspendedCard == cardView) _suspendedCard = null;
            RefreshCardPositions();
        }

        public void RefreshCardOrder()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy ||
                _fanLayout == null || !_fanLayout.gameObject.activeInHierarchy) return;
            for (int i = 0; i < cards.Count; i++)
            {
                CardView cardView = cards[i];
                if (cardView != null && cardView != _suspendedCard &&
                    cardView.transform.parent == _fanLayout.transform)
                {
                    cardView.transform.SetSiblingIndex(i);
                }
            }
        }

#if UNITY_EDITOR
        [Button("Preview Child Cards", EButtonEnableMode.Editor)]
        private void PreviewChildCards()
        {
            if (Application.isPlaying) return;

            CardFanLayout layout = _fanLayout != null ? _fanLayout : GetComponent<CardFanLayout>();
            if (layout == null)
            {
                Debug.LogWarning("Assign a CardFanLayout to HandView.", this);
                return;
            }

            var previewCards = new List<RectTransform>();
            foreach (Transform child in layout.RectTransform)
            {
                if (child is RectTransform cardRect && child.TryGetComponent<CardView>(out _))
                {
                    previewCards.Add(cardRect);
                }
            }

            if (previewCards.Count == 0) return;

            UnityEditor.Undo.RecordObjects(previewCards.ToArray(), "Preview Child Cards");
            for (int i = 0; i < previewCards.Count; i++)
            {
                RectTransform cardRect = previewCards[i];
                layout.GetCardPose(i, previewCards.Count, maxHandSize, out Vector2 position, out float angle);
                cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition3D = new Vector3(position.x, position.y, 0f);
                cardRect.localRotation = Quaternion.Euler(0f, 0f, angle);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(cardRect);
            }

            if (layout.gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            }
            UnityEditor.SceneView.RepaintAll();
        }
#endif

        private bool TryGetLayout()
        {
            if (_fanLayout == null) _fanLayout = GetComponent<CardFanLayout>();
            if (_fanLayout == null)
            {
                Debug.LogWarning("Assign a CardFanLayout to HandView.", this);
                return false;
            }

            return true;
        }

        private CardView GetCardView(Card card)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].Card == card)
                {
                    return cards[i];
                }
            }
            return null;
        }
    }
}
