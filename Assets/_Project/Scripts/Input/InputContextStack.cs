using System;
using System.Collections.Generic;

namespace PlushieOdyssey.Inputs
{
    // Pure C# lifetime/priority policy; Unity ownership is supplied as a predicate by the router.
    internal sealed class InputContextStack
    {
        internal sealed class Entry : IDisposable
        {
            private InputContextStack _stack;
            private readonly Func<bool> _ownerActive;

            internal InputContext Context { get; }
            internal IBackHandler Handler { get; }
            internal int Priority { get; }
            internal bool IsActive => _stack != null && _ownerActive();

            internal Entry(InputContextStack stack, Func<bool> ownerActive, InputContext context,
                IBackHandler handler, int priority)
            {
                _stack = stack;
                _ownerActive = ownerActive;
                Context = context;
                Handler = handler;
                Priority = priority;
            }

            public void Dispose()
            {
                if (_stack == null) return;
                InputContextStack stack = _stack;
                _stack = null;
                stack.Remove(this);
            }
        }

        private readonly List<Entry> _entries = new();
        internal event Action Changed;

        internal Entry Active
        {
            get
            {
                Entry active = null;
                // For equal priority, the most recently registered live context wins.
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    Entry entry = _entries[i];
                    if (entry.IsActive && (active == null || entry.Priority > active.Priority)) active = entry;
                }
                return active;
            }
        }

        internal Entry Enter(Func<bool> ownerActive, InputContext context, IBackHandler handler, int priority)
        {
            if (ownerActive == null) throw new ArgumentNullException(nameof(ownerActive));
            var entry = new Entry(this, ownerActive, context, handler, priority);
            _entries.Add(entry);
            Changed?.Invoke();
            return entry;
        }

        internal void PruneInactive()
        {
            // Dispose one at a time so each outstanding lease is invalidated too.
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].IsActive) continue;
                _entries[i].Dispose();
                // Changed callbacks may dispose other leases too; restart against the live list.
                i = _entries.Count;
            }
        }

        internal void Clear()
        {
            while (_entries.Count > 0) _entries[_entries.Count - 1].Dispose();
        }

        private void Remove(Entry entry)
        {
            if (_entries.Remove(entry)) Changed?.Invoke();
        }
    }
}
