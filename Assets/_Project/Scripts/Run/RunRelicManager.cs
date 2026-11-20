using PlushieOdyssey.Core;
using PlushieOdyssey.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Run
{
    public class RunRelicManager : PersistentSingleton<RunRelicManager>
    {
        public const int DefaultMaxEquippedRelics = 5;

        [Header("Relic Capacity")]
        [SerializeField, Min(1)] private int maxEquippedRelics = DefaultMaxEquippedRelics;

        private readonly List<RelicData> equippedSlots = new();
        private readonly List<RelicData> cachedActiveRelics = new();

        public int MaxEquippedRelics => Mathf.Max(1, maxEquippedRelics);
        public IReadOnlyList<RelicData> EquippedSlots
        {
            get
            {
                EnsureSlotCount();
                return equippedSlots;
            }
        }

        public IReadOnlyList<RelicData> CurrentRelics
        {
            get
            {
                RebuildActiveCache();
                return cachedActiveRelics;
            }
        }

        public int EquippedCount
        {
            get
            {
                EnsureSlotCount();
                int count = 0;
                for (int i = 0; i < equippedSlots.Count; i++)
                {
                    if (equippedSlots[i] != null) count++;
                }
                return count;
            }
        }

        public bool HasRelics => EquippedCount > 0;
        public bool IsFull => FindFirstEmptySlot() < 0;
        public bool HasFreeSlot => FindFirstEmptySlot() >= 0;
        public RelicData PendingRelic { get; private set; }

        public event Action<IReadOnlyList<RelicData>> RelicsChanged;
        public event Action<RelicData> RelicSlotsFullRequested;
        public event Action<RelicData> PendingRelicChanged;

        public static event Action<IReadOnlyList<RelicData>> AnyRelicsChanged;
        public static event Action<RelicData> AnyRelicSlotsFullRequested;

        protected override void Awake()
        {
            base.Awake();
            EnsureSlotCount();
        }

        public static RunRelicManager EnsureInstance()
        {
            if (Instance != null) return Instance;

            GameObject host = RunManager.Instance != null
                ? RunManager.Instance.gameObject
                : new GameObject("Run Relic Manager");

            RunRelicManager manager = host.GetComponent<RunRelicManager>();
            if (manager == null)
            {
                manager = host.AddComponent<RunRelicManager>();
            }

            manager.EnsureSlotCount();
            return manager;
        }

        public List<RelicData> GetRelicsCopy()
        {
            RebuildActiveCache();
            return new List<RelicData>(cachedActiveRelics);
        }

        public RelicData GetRelicAt(int slotIndex)
        {
            EnsureSlotCount();
            if (slotIndex < 0 || slotIndex >= equippedSlots.Count) return null;
            return equippedSlots[slotIndex];
        }

        public void ResetRelics(IEnumerable<RelicData> startingRelics)
        {
            EnsureSlotCount();
            for (int i = 0; i < equippedSlots.Count; i++) equippedSlots[i] = null;
            PendingRelic = null;

            if (startingRelics != null)
            {
                foreach (RelicData relic in startingRelics)
                {
                    AddRelicInternal(relic, logFailures: false, raiseFullEvent: false);
                }
            }

            PendingRelicChanged?.Invoke(null);
            NotifyRelicsChanged();
        }

        public void ClearRelics()
        {
            EnsureSlotCount();
            bool hadAny = HasRelics || PendingRelic != null;
            for (int i = 0; i < equippedSlots.Count; i++) equippedSlots[i] = null;
            PendingRelic = null;

            if (hadAny)
            {
                PendingRelicChanged?.Invoke(null);
                NotifyRelicsChanged();
            }
        }

        public bool Contains(RelicData relic)
        {
            if (relic == null) return false;
            EnsureSlotCount();
            for (int i = 0; i < equippedSlots.Count; i++)
            {
                if (equippedSlots[i] == relic) return true;
            }
            return false;
        }

        public int Count(RelicData relic)
        {
            if (relic == null) return 0;
            EnsureSlotCount();

            int count = 0;
            for (int i = 0; i < equippedSlots.Count; i++)
            {
                if (equippedSlots[i] == relic) count++;
            }
            return count;
        }

        /// <summary>
        /// Equips the relic into the first open slot (up to MaxEquippedRelics = 5).
        /// If all 5 slots are full, stores it as PendingRelic and fires RelicSlotsFullRequested
        /// so the player can drag-and-drop to unequip/swap a relic.
        /// </summary>
        public bool AddRelic(RelicData relic)
        {
            bool added = AddRelicInternal(relic, logFailures: true, raiseFullEvent: true);
            if (added) NotifyRelicsChanged();
            return added;
        }

        public bool EquipAtSlot(int slotIndex, RelicData newRelic, out RelicData displacedRelic)
        {
            displacedRelic = null;
            EnsureSlotCount();
            if (slotIndex < 0 || slotIndex >= equippedSlots.Count || newRelic == null) return false;

            if (newRelic.Unique && Contains(newRelic) && equippedSlots[slotIndex] != newRelic)
            {
                global::PlushieOdyssey.Core.Logger.Log(this, $"Relic '{newRelic.Title}' is unique and already equipped in another slot.");
                return false;
            }

            displacedRelic = equippedSlots[slotIndex];
            equippedSlots[slotIndex] = newRelic;

            if (PendingRelic == newRelic)
            {
                PendingRelic = null;
                PendingRelicChanged?.Invoke(null);
            }

            NotifyRelicsChanged();
            return true;
        }

        public bool UnequipAtSlot(int slotIndex, out RelicData removedRelic)
        {
            removedRelic = null;
            EnsureSlotCount();
            if (slotIndex < 0 || slotIndex >= equippedSlots.Count) return false;
            if (equippedSlots[slotIndex] == null) return false;

            removedRelic = equippedSlots[slotIndex];
            equippedSlots[slotIndex] = null;
            NotifyRelicsChanged();
            return true;
        }

        public bool SwapSlots(int indexA, int indexB)
        {
            EnsureSlotCount();
            if (indexA < 0 || indexA >= equippedSlots.Count || indexB < 0 || indexB >= equippedSlots.Count)
                return false;
            if (indexA == indexB) return false;
            if (equippedSlots[indexA] == null && equippedSlots[indexB] == null) return false;

            (equippedSlots[indexA], equippedSlots[indexB]) = (equippedSlots[indexB], equippedSlots[indexA]);
            NotifyRelicsChanged();
            return true;
        }

        public void ApplySlotLoadout(IReadOnlyList<RelicData> desiredSlots)
        {
            EnsureSlotCount();
            if (desiredSlots == null) return;

            var seenUnique = new HashSet<RelicData>();
            for (int i = 0; i < equippedSlots.Count; i++)
            {
                RelicData candidate = i < desiredSlots.Count ? desiredSlots[i] : null;
                if (candidate != null && candidate.Unique && !seenUnique.Add(candidate))
                {
                    candidate = null;
                }
                equippedSlots[i] = candidate;
            }

            if (PendingRelic != null && Contains(PendingRelic))
            {
                PendingRelic = null;
                PendingRelicChanged?.Invoke(null);
            }

            NotifyRelicsChanged();
        }

        public void SetPendingRelic(RelicData relic, bool openSwapPrompt = true)
        {
            PendingRelic = relic;
            PendingRelicChanged?.Invoke(PendingRelic);
            if (openSwapPrompt && relic != null)
            {
                RelicSlotsFullRequested?.Invoke(relic);
                AnyRelicSlotsFullRequested?.Invoke(relic);
            }
        }

        public void ClearPendingRelic()
        {
            if (PendingRelic == null) return;
            PendingRelic = null;
            PendingRelicChanged?.Invoke(null);
        }

        public bool RemoveRelic(RelicData relic)
        {
            if (relic == null) return false;
            EnsureSlotCount();

            for (int i = 0; i < equippedSlots.Count; i++)
            {
                if (equippedSlots[i] == relic)
                {
                    equippedSlots[i] = null;
                    NotifyRelicsChanged();
                    return true;
                }
            }

            return false;
        }

        private bool AddRelicInternal(RelicData relic, bool logFailures, bool raiseFullEvent)
        {
            EnsureSlotCount();

            if (relic == null)
            {
                if (logFailures) global::PlushieOdyssey.Core.Logger.LogWarning("Tried to add a null relic to the run.");
                return false;
            }

            if (relic.Unique && Contains(relic))
            {
                if (logFailures) global::PlushieOdyssey.Core.Logger.Log(this, $"Relic '{relic.Title}' is unique and already owned.");
                return false;
            }

            int emptyIndex = FindFirstEmptySlot();
            if (emptyIndex < 0)
            {
                PendingRelic = relic;
                PendingRelicChanged?.Invoke(PendingRelic);
                if (raiseFullEvent)
                {
                    RelicSlotsFullRequested?.Invoke(relic);
                    AnyRelicSlotsFullRequested?.Invoke(relic);
                }
                if (logFailures)
                {
                    global::PlushieOdyssey.Core.Logger.Log(this, $"All {MaxEquippedRelics} relic slots are full! Prompting player to unequip/swap for '{relic.Title}'.");
                }
                return false;
            }

            equippedSlots[emptyIndex] = relic;
            return true;
        }

        private int FindFirstEmptySlot()
        {
            EnsureSlotCount();
            for (int i = 0; i < equippedSlots.Count; i++)
            {
                if (equippedSlots[i] == null) return i;
            }
            return -1;
        }

        private void EnsureSlotCount()
        {
            int target = MaxEquippedRelics;
            while (equippedSlots.Count < target)
            {
                equippedSlots.Add(null);
            }
            if (equippedSlots.Count > target)
            {
                equippedSlots.RemoveRange(target, equippedSlots.Count - target);
            }
        }

        private void RebuildActiveCache()
        {
            EnsureSlotCount();
            cachedActiveRelics.Clear();
            for (int i = 0; i < equippedSlots.Count; i++)
            {
                if (equippedSlots[i] != null) cachedActiveRelics.Add(equippedSlots[i]);
            }
        }

        public void NotifyRelicsChanged()
        {
            IReadOnlyList<RelicData> current = CurrentRelics;
            RelicsChanged?.Invoke(current);
            AnyRelicsChanged?.Invoke(current);
        }
    }
}
