using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

using Gameseed26;
namespace ModularEvents
{
    // ============================================================
    // 0. SHARED SAFE-INVOKE UTILITY
    // Invokes each subscriber individually so one throwing listener
    // doesn't stop the rest of the invocation list from running.
    // ============================================================
    internal static class EventBusUtility
    {
        public static void SafeInvoke(Action action)
        {
            if (action == null) return;
            foreach (var d in action.GetInvocationList())
            {
                try { ((Action)d)(); }
                catch (Exception e) { Gameseed26.Logger.LogException(e); }
            }
        }

        public static void SafeInvoke<T>(Action<T> action, T arg)
        {
            if (action == null) return;
            foreach (var d in action.GetInvocationList())
            {
                try { ((Action<T>)d)(arg); }
                catch (Exception e) { Gameseed26.Logger.LogException(e); }
            }
        }
    }

    // ============================================================
    // 0b. RESET REGISTRY
    // Every static bus (string-based and each EventBus<T> instantiation)
    // registers a Clear() callback here. A single SubsystemRegistration
    // hook fires on every Play Mode entry — even with Domain Reload
    // disabled in Editor settings — so stale listeners from a previous
    // session can never leak into a new one.
    // ============================================================
    internal static class EventBusRegistry
    {
        private static readonly List<Action> resetCallbacks = new List<Action>();

        public static void Register(Action resetAction)
        {
            if (resetAction != null) resetCallbacks.Add(resetAction);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            foreach (var reset in resetCallbacks) reset?.Invoke();
        }
    }

    // ============================================================
    // 1. STRING-BASED EVENT BUS (parameterless & object payload)
    // Use this for designer-facing / Inspector-wired custom event names.
    // ============================================================
    public static class EventBus
    {
        public static bool DebugLogging = false;

        private static readonly Dictionary<string, Action> parameterless = new Dictionary<string, Action>();
        private static readonly Dictionary<string, Action<object>> withPayload = new Dictionary<string, Action<object>>();

        static EventBus()
        {
            EventBusRegistry.Register(ClearAll);
        }

        public static void Subscribe(string eventName, Action callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;
            parameterless.TryGetValue(eventName, out var existing);
            parameterless[eventName] = existing + callback;
        }

        public static void Unsubscribe(string eventName, Action callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;
            if (parameterless.TryGetValue(eventName, out var existing))
                parameterless[eventName] = existing - callback;
        }

        public static void Subscribe(string eventName, Action<object> callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;
            withPayload.TryGetValue(eventName, out var existing);
            withPayload[eventName] = existing + callback;
        }

        public static void Unsubscribe(string eventName, Action<object> callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;
            if (withPayload.TryGetValue(eventName, out var existing))
                withPayload[eventName] = existing - callback;
        }

        public static void Broadcast(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (DebugLogging) Gameseed26.Logger.Log($"[EventBus] Broadcast: {eventName}");
            if (parameterless.TryGetValue(eventName, out var action))
                EventBusUtility.SafeInvoke(action);
        }

        public static void Broadcast(string eventName, object payload)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (DebugLogging) Gameseed26.Logger.Log($"[EventBus] Broadcast: {eventName} (payload: {payload})");
            if (withPayload.TryGetValue(eventName, out var action))
                EventBusUtility.SafeInvoke(action, payload);
        }

        /// <summary>How many listeners are currently subscribed to a given event name. Useful for debugging "why did this fire twice".</summary>
        public static int GetSubscriberCount(string eventName)
        {
            int count = 0;
            if (parameterless.TryGetValue(eventName, out var a) && a != null) count += a.GetInvocationList().Length;
            if (withPayload.TryGetValue(eventName, out var b) && b != null) count += b.GetInvocationList().Length;
            return count;
        }

        public static void ClearAll()
        {
            parameterless.Clear();
            withPayload.Clear();
        }
    }

    // ============================================================
    // 2. TYPED EVENT BUS — now keyed by NAME too, not just by type.
    // Fixes the collision bug where EventBus<int> was a single global
    // channel: e.g. "ScoreChanged" and "ComboCount" no longer step on
    // each other just because they both carry an int payload.
    // Omit the name to use a single default channel per type, exactly
    // like the old behavior, for simple cases.
    // ============================================================
    public static class EventBus<T>
    {
        private const string DefaultChannel = "__default__";
        private static readonly Dictionary<string, Action<T>> channels = new Dictionary<string, Action<T>>();

        static EventBus()
        {
            EventBusRegistry.Register(Clear);
        }

        public static void Subscribe(Action<T> callback, string eventName = null)
        {
            if (callback == null) return;
            string key = eventName ?? DefaultChannel;
            channels.TryGetValue(key, out var existing);
            channels[key] = existing + callback;
        }

        public static void Unsubscribe(Action<T> callback, string eventName = null)
        {
            if (callback == null) return;
            string key = eventName ?? DefaultChannel;
            if (channels.TryGetValue(key, out var existing))
                channels[key] = existing - callback;
        }

        public static void Broadcast(T payload, string eventName = null)
        {
            string key = eventName ?? DefaultChannel;
            if (channels.TryGetValue(key, out var action))
                EventBusUtility.SafeInvoke(action, payload);
        }

        public static int GetSubscriberCount(string eventName = null)
        {
            string key = eventName ?? DefaultChannel;
            return channels.TryGetValue(key, out var a) && a != null ? a.GetInvocationList().Length : 0;
        }

        public static void Clear() => channels.Clear();
    }

    // ============================================================
    // 3. RECOMMENDED EVENT NAME CONSTANTS (optional, avoids magic strings)
    // Purely a convenience layer — Subscribe/Broadcast accept ANY string,
    // declared here or not. Add your own per project; nothing else
    // in this file needs to change.
    // ============================================================
    public static class EventNames
    {
        public const string PlayerDied = "PlayerDied";
        public const string GamePaused = "GamePaused";
        public const string GameResumed = "GameResumed";
        public const string LevelComplete = "LevelComplete";
        // Add your project's event names here.
    }

    // ============================================================
    // 4. UNITY MONOBEHAVIOUR COMPONENTS
    // ============================================================

    /// <summary>
    /// Listens for string-based events (any name, built-in or custom) and fires local UnityEvents.
    /// </summary>
    [System.Serializable]
    public struct ModularEventListener
    {
        [Tooltip("Event name to listen for. Type any custom name, or use EventNames constants for safety.")]
        public string eventName;
        public UnityEvent onEventTriggered;
    }

    [AddComponentMenu("Modular Events/Event Listener Binding")]
    public class EventListenerBinding : MonoBehaviour
    {
        [SerializeField] private List<ModularEventListener> listeners = new List<ModularEventListener>();

        protected virtual void OnEnable()
        {
            foreach (var listener in listeners)
            {
                if (string.IsNullOrEmpty(listener.eventName)) continue;
                EventBus.Subscribe(listener.eventName, listener.onEventTriggered.Invoke);
            }
        }

        protected virtual void OnDisable()
        {
            foreach (var listener in listeners)
            {
                if (string.IsNullOrEmpty(listener.eventName)) continue;
                EventBus.Unsubscribe(listener.eventName, listener.onEventTriggered.Invoke);
            }
        }
    }

    /// <summary>
    /// Broadcaster that can be used directly from UI/Animation events, or
    /// from code with any custom event name.
    /// </summary>
    [AddComponentMenu("Modular Events/Event Broadcaster Proxy")]
    public class EventBroadcasterProxy : MonoBehaviour
    {
        [SerializeField] private string defaultEventName;

        public void Broadcast(string eventName) => EventBus.Broadcast(eventName);
        public void BroadcastDefault() => EventBus.Broadcast(defaultEventName);
    }


    /// <summary>
    /// Delays then fires a UnityEvent. By default, re-triggering before the
    /// delay elapses cancels the pending invoke instead of stacking a second
    /// one (set allowStacking = true to restore the old stacking behavior).
    /// </summary>
    [AddComponentMenu("Modular Events/Delayed Event Invoker")]
    public class DelayedEventInvoker : MonoBehaviour
    {
        [SerializeField] private bool startAutomatically = false;
        [SerializeField] private float delaySeconds = 2f;
        [SerializeField] private bool allowStacking = false;
        public UnityEvent onDelayElapsed;

        private Coroutine activeRoutine;

        private void OnEnable()
        {
            if (startAutomatically)
                TriggerAfterDelay(delaySeconds);
        }

        private void OnDisable() => CancelPending();

        public void TriggerWithConfiguredDelay() => TriggerAfterDelay(delaySeconds);

        public void TriggerAfterDelay(float seconds)
        {
            if (!allowStacking) CancelPending();
            activeRoutine = StartCoroutine(WaitAndInvoke(seconds));
        }

        public void CancelPending()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }
        }

        private System.Collections.IEnumerator WaitAndInvoke(float delay)
        {
            yield return new WaitForSeconds(delay);
            activeRoutine = null;
            onDelayElapsed?.Invoke();
        }
    }

    /// <summary>
    /// Tag-filtered 2D physics relay, with optional fan-out to EventBus
    /// using any custom event name per collision callback.
    /// </summary>
    [AddComponentMenu("Modular Events/Collision Event Relay 2D")]
    public class CollisionEventRelay2D : MonoBehaviour
    {
        [SerializeField] private string targetTag = "Player";

        [Header("Local UnityEvents")]
        public UnityEvent onTriggerEnter2D, onTriggerExit2D, onCollisionEnter2D, onCollisionExit2D;

        [Header("Optional EventBus broadcast (any custom name)")]
        [SerializeField] private string broadcastNameOnTriggerEnter, broadcastNameOnTriggerExit,
                                       broadcastNameOnCollisionEnter, broadcastNameOnCollisionExit;

        private bool Matches(Collider2D other) => string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag);
        private bool Matches(Collision2D collision) => string.IsNullOrEmpty(targetTag) || collision.gameObject.CompareTag(targetTag);

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!Matches(other)) return;
            onTriggerEnter2D?.Invoke();
            if (!string.IsNullOrEmpty(broadcastNameOnTriggerEnter)) EventBus.Broadcast(broadcastNameOnTriggerEnter);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!Matches(other)) return;
            onTriggerExit2D?.Invoke();
            if (!string.IsNullOrEmpty(broadcastNameOnTriggerExit)) EventBus.Broadcast(broadcastNameOnTriggerExit);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!Matches(collision)) return;
            onCollisionEnter2D?.Invoke();
            if (!string.IsNullOrEmpty(broadcastNameOnCollisionEnter)) EventBus.Broadcast(broadcastNameOnCollisionEnter);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!Matches(collision)) return;
            onCollisionExit2D?.Invoke();
            if (!string.IsNullOrEmpty(broadcastNameOnCollisionExit)) EventBus.Broadcast(broadcastNameOnCollisionExit);
        }
    }

    /// <summary>
    /// Unified wrapper matching the filename so Unity's Add Component menu finds it instantly.
    /// Acts as an EventListenerBinding.
    /// </summary>
    [System.Serializable]
    public class ExternalEventHook
    {
        public UnityEngine.Object target;
        public string eventName;
        public UnityEvent onEventTriggered;

        private Delegate hookedDelegate;
        private UnityEvent hookedUnityEvent;
        private Component hookedComponent;

        public void Subscribe()
        {
            if (target == null || string.IsNullOrEmpty(eventName)) return;
            
            Component[] comps;
            if (target is GameObject go) comps = go.GetComponents<Component>();
            else if (target is Component c) comps = new Component[] { c };
            else return;

            string[] parts = eventName.Split('/');
            string targetEventName = parts[parts.Length - 1];
            string targetCompName = parts.Length > 1 ? parts[0] : null;

            foreach (var comp in comps)
            {
                if (comp == null) continue;
                var type = comp.GetType();
                
                if (targetCompName != null && type.Name != targetCompName) continue;

                var field = type.GetField(targetEventName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && typeof(UnityEventBase).IsAssignableFrom(field.FieldType))
                {
                    hookedUnityEvent = field.GetValue(comp) as UnityEvent;
                    if (hookedUnityEvent != null)
                    {
                        hookedUnityEvent.AddListener(Trigger);
                        hookedComponent = comp;
                        return;
                    }
                }
                
                var prop = type.GetProperty(targetEventName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (prop != null && typeof(UnityEventBase).IsAssignableFrom(prop.PropertyType))
                {
                    hookedUnityEvent = prop.GetValue(comp) as UnityEvent;
                    if (hookedUnityEvent != null)
                    {
                        hookedUnityEvent.AddListener(Trigger);
                        hookedComponent = comp;
                        return;
                    }
                }

                var evt = type.GetEvent(targetEventName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (evt != null && evt.EventHandlerType == typeof(System.Action))
                {
                    hookedDelegate = new System.Action(Trigger);
                    evt.AddEventHandler(comp, hookedDelegate);
                    hookedComponent = comp;
                    return;
                }
            }
        }

        public void Unsubscribe()
        {
            if (hookedUnityEvent != null)
            {
                hookedUnityEvent.RemoveListener(Trigger);
                hookedUnityEvent = null;
            }

            if (hookedDelegate != null && hookedComponent != null && !string.IsNullOrEmpty(eventName))
            {
                string[] parts = eventName.Split('/');
                string targetEventName = parts[parts.Length - 1];

                var evt = hookedComponent.GetType().GetEvent(targetEventName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (evt != null) evt.RemoveEventHandler(hookedComponent, hookedDelegate);
                hookedDelegate = null;
            }
            hookedComponent = null;
        }

        private void Trigger() => onEventTriggered?.Invoke();
    }

    [AddComponentMenu("Modular Events/Universal Event Trigger")]
    public class UniversalEventTrigger : EventListenerBinding
    {
        [Header("Lifecycle Events (Optional)")]
        public UnityEvent onAwake;
        public UnityEvent onStart;
        public UnityEvent onEnableEvent;
        public UnityEvent onDisableEvent;
        public UnityEvent onDestroy;

        [Header("Script Event Hooks (Optional)")]
        public List<ExternalEventHook> externalEventHooks = new List<ExternalEventHook>();

        private void Awake() => onAwake?.Invoke();
        private void Start() => onStart?.Invoke();
        private void OnDestroy() => onDestroy?.Invoke();

        protected override void OnEnable()
        {
            base.OnEnable();
            onEnableEvent?.Invoke();
            foreach(var hook in externalEventHooks) hook.Subscribe();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            onDisableEvent?.Invoke();
            foreach(var hook in externalEventHooks) hook.Unsubscribe();
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ExternalEventHook))]
    public class ExternalEventHookDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var onEventTriggered = property.FindPropertyRelative("onEventTriggered");
            return EditorGUIUtility.singleLineHeight * 2 + 4 + EditorGUI.GetPropertyHeight(onEventTriggered);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            var targetProp = property.FindPropertyRelative("target");
            var eventName = property.FindPropertyRelative("eventName");
            var onEventTriggered = property.FindPropertyRelative("onEventTriggered");

            Rect compRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect eventRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
            Rect unityEventRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight * 2 + 4, position.width, EditorGUI.GetPropertyHeight(onEventTriggered));

            EditorGUI.PropertyField(compRect, targetProp, new GUIContent("Target Object"));

            UnityEngine.Object targetObj = targetProp.objectReferenceValue;
            if (targetObj != null)
            {
                Component[] comps;
                if (targetObj is GameObject go) comps = go.GetComponents<Component>();
                else if (targetObj is Component c) comps = new Component[] { c };
                else comps = new Component[0];

                List<string> options = new List<string>();
                foreach(var comp in comps)
                {
                    if (comp == null) continue;
                    var type = comp.GetType();
                    string prefix = type.Name + "/";
                    
                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                        .Where(f => typeof(UnityEventBase).IsAssignableFrom(f.FieldType))
                        .Select(f => prefix + f.Name);
                    
                    var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                        .Where(p => typeof(UnityEventBase).IsAssignableFrom(p.PropertyType))
                        .Select(p => prefix + p.Name);

                    var events = type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                        .Where(e => e.EventHandlerType == typeof(System.Action))
                        .Select(e => prefix + e.Name);

                    options.AddRange(fields);
                    options.AddRange(props);
                    options.AddRange(events);
                }

                options.Insert(0, "None");

                int currentIndex = Mathf.Max(0, options.IndexOf(eventName.stringValue));
                if (string.IsNullOrEmpty(eventName.stringValue)) currentIndex = 0;

                int selectedIndex = EditorGUI.Popup(eventRect, "Event Name", currentIndex, options.ToArray());
                if (selectedIndex > 0 && selectedIndex < options.Count)
                    eventName.stringValue = options[selectedIndex];
                else
                    eventName.stringValue = "";
            }
            else
            {
                EditorGUI.PropertyField(eventRect, eventName);
            }

            EditorGUI.PropertyField(unityEventRect, onEventTriggered);
            EditorGUI.EndProperty();
        }
    }
#endif
}