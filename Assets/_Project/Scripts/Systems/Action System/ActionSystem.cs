using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Core;
using PlushieOdyssey.Vfx;
using System;
using System.Collections;
using System.Collections.Generic;

namespace PlushieOdyssey.Combat.Actions
{
    [System.Serializable]
    public class ActionSystem : Singleton<ActionSystem>
    {
        private readonly struct QueuedAction
        {
            public readonly GameAction Action;
            public readonly Action OnFinished;

            public QueuedAction(GameAction action, Action onFinished)
            {
                Action = action;
                OnFinished = onFinished;
            }
        }

        public bool IsPerforming { get; private set; } = false;
        public bool IsEnemyTurnQueuedOrActive { get; private set; } = false;
        public int PendingQueueCount => pendingQueue.Count;

        public event Action<bool> BusyChanged;
        public event Action TurnStateChanged;

        private readonly Queue<QueuedAction> pendingQueue = new();

        protected override void Awake()
        {
            base.Awake();
            IsPerforming = false;
            IsEnemyTurnQueuedOrActive = false;
            pendingQueue.Clear();

            if (DamageOverTimeSystem.Instance == null && GetComponent<DamageOverTimeSystem>() == null)
            {
                gameObject.AddComponent<DamageOverTimeSystem>();
            }
            if (BuffSystem.Instance == null && GetComponent<BuffSystem>() == null)
            {
                gameObject.AddComponent<BuffSystem>();
            }
            if (BeamVfxSystem.Instance == null && GetComponent<BeamVfxSystem>() == null)
            {
                gameObject.AddComponent<BeamVfxSystem>();
            }
            if (FrierenShieldSystem.Instance == null && GetComponent<FrierenShieldSystem>() == null)
            {
                gameObject.AddComponent<FrierenShieldSystem>();
            }
            if (SimultaneousSystem.Instance == null && GetComponent<SimultaneousSystem>() == null)
            {
                gameObject.AddComponent<SimultaneousSystem>();
            }
        }

        private static Dictionary<Type, List<Action<GameAction>>> preSubs = new();
        private static Dictionary<Type, Func<GameAction, IEnumerator>> performers = new();
        private static Dictionary<Type, List<Action<GameAction>>> postSubs = new();

        private static Dictionary<Delegate, Action<GameAction>> delegateMap = new();

        private List<GameAction> reactions = null;

        private void OnDestroy()
        {
            pendingQueue.Clear();
            IsPerforming = false;
            IsEnemyTurnQueuedOrActive = false;
            preSubs.Clear();
            performers.Clear();
            postSubs.Clear();
            delegateMap.Clear();
        }

        public static void AttachPerformer<T>(Func<T, IEnumerator> performer) where T : GameAction
        {
            Type type = typeof(T);
            IEnumerator wrappedPerformer(GameAction action) => performer((T)action);

            if (performers.ContainsKey(type)) performers[type] = wrappedPerformer;
            else performers.Add(type, wrappedPerformer);
        }

        public static void DetachPerformer<T>() where T : GameAction
        {
            Type type = typeof(T);

            if (performers.ContainsKey(type))
                performers.Remove(type);
        }

        public static void SubscribeReaction<T>(Action<T> reaction, ReactionTiming timing) where T : GameAction
        {
            Dictionary<Type, List<Action<GameAction>>> subs = timing == ReactionTiming.PRE ? preSubs : postSubs;

            void wrappedReaction(GameAction action) => reaction((T)action);
            delegateMap[reaction] = wrappedReaction;

            if (subs.ContainsKey(typeof(T)))
                subs[typeof(T)].Add(wrappedReaction);
            else
            {
                subs.Add(typeof(T), new());
                subs[typeof(T)].Add(wrappedReaction);
            }
        }

        public static void UnsubscribeReaction<T>(Action<T> reaction, ReactionTiming timing) where T : GameAction
        {
            Dictionary<Type, List<Action<GameAction>>> subs = timing == ReactionTiming.PRE ? preSubs : postSubs;

            if (subs.ContainsKey(typeof(T)) && delegateMap.TryGetValue(reaction, out var wrappedReaction))
            {
                subs[typeof(T)].Remove(wrappedReaction);
                delegateMap.Remove(reaction);
            }
        }

        /// <summary>
        /// Immediately starts the action if idle, or appends it to the FIFO execution queue so
        /// rapid card plays and End Turn clicks execute cleanly in strict order without stalling.
        /// </summary>
        public bool Perform(GameAction action, Action OnPerformFinished = null)
        {
            return Enqueue(action, OnPerformFinished);
        }

        public bool Enqueue(GameAction action, Action onPerformFinished = null)
        {
            if (action == null) return false;

            // Prevent queueing duplicate EnemyTurnGA while one is already queued or running
            if (action is EnemyTurnGA)
            {
                if (IsEnemyTurnQueuedOrActive) return false;
                IsEnemyTurnQueuedOrActive = true;
                TurnStateChanged?.Invoke();
            }

            if (IsPerforming)
            {
                pendingQueue.Enqueue(new QueuedAction(action, onPerformFinished));
                return true;
            }

            StartActionFlow(action, onPerformFinished);
            return true;
        }

        private void StartActionFlow(GameAction action, Action onPerformFinished)
        {
            if (!IsPerforming)
            {
                IsPerforming = true;
                BusyChanged?.Invoke(true);
            }

            StartCoroutine(Flow(action, () =>
            {
                if (action is EnemyTurnGA)
                {
                    IsEnemyTurnQueuedOrActive = false;
                    TurnStateChanged?.Invoke();
                }

                onPerformFinished?.Invoke();

                if (pendingQueue.Count > 0)
                {
                    QueuedAction next = pendingQueue.Dequeue();
                    StartActionFlow(next.Action, next.OnFinished);
                    return;
                }

                IsPerforming = false;
                BusyChanged?.Invoke(false);
            }));
        }

        public void AddReaction(GameAction gameAction)
        {
            reactions?.Add(gameAction);
        }

        public IEnumerator PerformSubflow(GameAction action)
        {
            List<GameAction> restore = reactions;
            yield return Flow(action);
            reactions = restore;
        }

        private IEnumerator Flow(GameAction action, Action OnFlowFinished = null)
        {
            reactions = action.PreReactions;
            PerformSubscribers(action, preSubs);
            yield return PerformReactions();

            reactions = action.PerformReactions;
            yield return PerformPerformer(action);
            yield return PerformReactions();

            reactions = action.PostReactions;
            PerformSubscribers(action, postSubs);
            yield return PerformReactions();

            OnFlowFinished?.Invoke();
        }

        private IEnumerator PerformPerformer(GameAction action)
        {
            Type type = action.GetType();

            if (performers.ContainsKey(type))
                yield return performers[type](action);
        }

        private IEnumerator PerformReactions()
        {
            foreach (GameAction reaction in reactions)
            {
                yield return Flow(reaction);
            }
        }

        private void PerformSubscribers(GameAction action, Dictionary<Type, List<Action<GameAction>>> subs)
        {
            Type type = action.GetType();

            if (subs.ContainsKey(type))
            {
                foreach (Action<GameAction> sub in subs[type])
                {
                    sub(action);
                }
            }
        }
    }
}
