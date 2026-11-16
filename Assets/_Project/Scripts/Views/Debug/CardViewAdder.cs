using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Data;
using UnityEngine;

namespace PlushieOdyssey.UI
{
    [DisallowMultipleComponent]
    public class CardViewAdder : MonoBehaviour
    {
        [SerializeField] private HandView _handView;
        [SerializeField] private CardView _cardViewPrefab;
        [SerializeField] private List<CardData> _cardPool = new();
        [SerializeField, Min(0)] private int _targetCount = 10;
        [SerializeField] private bool _fillOnStart = true;

        [Tooltip("Editor-preview cards to hide during this debug session.")]
        [SerializeField] private List<CardView> _previewCards = new();

        [SerializeField, ReadOnly] private List<CardView> _addedCard = new();

        private readonly Dictionary<CardView, bool> _previewStates = new();
        private bool _isFilling;

        private void Awake()
        {
            _addedCard ??= new List<CardView>();
            _addedCard.Clear();
        }

        private void Start()
        {
            if (!Debug.isDebugBuild) return;
            HidePreviewCards();
            if (_fillOnStart) FillHand();
        }

        [Button("Add Random Card", EButtonEnableMode.Playmode)]
        public void AddCardViewToHand()
        {
            BeginAdding(false);
        }

        [Button("Fill Hand", EButtonEnableMode.Playmode)]
        public void FillHand()
        {
            BeginAdding(true);
        }

        [Button("Clear Spawned Cards", EButtonEnableMode.Playmode)]
        public void RemoveAllCard()
        {
            if (!Application.isPlaying) return;
            StopAllCoroutines();
            _isFilling = false;

            for (int i = _addedCard.Count - 1; i >= 0; i--)
            {
                CardView view = _addedCard[i];
                _addedCard.RemoveAt(i);
                if (view == null) continue;
                if (_handView != null && view.Card != null && IsRegistered(view))
                    _handView.RemoveCard(view.Card);
                Destroy(view.gameObject);
            }
        }

        private void BeginAdding(bool fillToTarget)
        {
            if (!Application.isPlaying || !Debug.isDebugBuild || !isActiveAndEnabled || _isFilling)
                return;
            if (_handView == null || !_handView.isActiveAndEnabled || _cardViewPrefab == null ||
                _cardViewPrefab.transform is not RectTransform)
            {
                Debug.LogWarning("Assign an active HandView and a Canvas CardView prefab.", this);
                return;
            }

            var pool = new List<CardData>();
            if (_cardPool != null)
            {
                foreach (CardData data in _cardPool)
                {
                    if (data != null) pool.Add(data);
                }
            }
            if (pool.Count == 0)
            {
                Debug.LogWarning("Assign at least one non-null CardData to Card Pool.", this);
                return;
            }

            HidePreviewCards();
            _isFilling = true;
            StartCoroutine(AddCards(pool, fillToTarget));
        }

        private IEnumerator AddCards(List<CardData> pool, bool fillToTarget)
        {
            int amount = fillToTarget ? Mathf.Max(0, _targetCount - CountRegisteredCards()) : 1;
            try
            {
                for (int i = 0; i < amount; i++)
                {
                    if (_handView == null || !_handView.isActiveAndEnabled) yield break;
                    if (fillToTarget && CountRegisteredCards() >= _targetCount) yield break;

                    CardData data = pool[Random.Range(0, pool.Count)];
                    CardView view = Instantiate(_cardViewPrefab);
                    view.Setup(new Card(data));
                    view.gameObject.SetActive(true);
                    _addedCard.Add(view);
                    yield return _handView.AddCard(view);

                    if (view == null) continue;
                    if (IsRegistered(view)) continue;
                    _addedCard.Remove(view);
                    Destroy(view.gameObject);
                    yield break;
                }
            }
            finally
            {
                _isFilling = false;
            }
        }

        private int CountRegisteredCards()
        {
            int count = 0;
            foreach (CardView view in _handView.Cards)
            {
                if (view != null) count++;
            }
            return count;
        }

        private bool IsRegistered(CardView view)
        {
            if (_handView == null) return false;
            foreach (CardView registered in _handView.Cards)
            {
                if (registered == view) return true;
            }
            return false;
        }

        private void HidePreviewCards()
        {
            if (_previewCards == null) return;
            foreach (CardView view in _previewCards)
            {
                if (view == null || _previewStates.ContainsKey(view) || IsRegistered(view)) continue;
                if (transform.IsChildOf(view.transform) ||
                    (_handView != null && _handView.transform.IsChildOf(view.transform)))
                {
                    Debug.LogWarning("Preview Cards must not contain the debug component or HandView.", this);
                    continue;
                }

                _previewStates.Add(view, view.gameObject.activeSelf);
                view.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying) RemoveAllCard();
            foreach (KeyValuePair<CardView, bool> state in _previewStates)
            {
                if (state.Key != null) state.Key.gameObject.SetActive(state.Value);
            }
            _previewStates.Clear();
        }
    }
}
