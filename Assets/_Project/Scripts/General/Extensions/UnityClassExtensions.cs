using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Gameseed26
{
    public static class UnityClassExtensions
    {
        #region GameObject Extensions
        /// <summary>
        /// Get the component, if cannot find it, add the component to the GameObject.
        /// </summary>
        public static T GetOrAdd<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        /// <summary>
        /// Add Trigger Component to GameObject with specified Type and Action.
        /// </summary>
        public static void RegisterTrigger(this GameObject target, EventTriggerType eventTriggerType, UnityAction<BaseEventData> action)
        {
            if (target == null) return;
            if (!target.TryGetComponent<EventTrigger>(out var eventTrigger))
                eventTrigger = target.AddComponent<EventTrigger>();

            var entry = eventTrigger.triggers.Find(e => e.eventID == eventTriggerType);

            if (entry == null)
            {
                entry = new EventTrigger.Entry
                {
                    eventID = eventTriggerType
                };
                eventTrigger.triggers.Add(entry);
            }

            entry.callback.AddListener(action);
        }

        /// <summary>
        /// Remove Trigger Component to GameObject with specified Type and Action.
        /// </summary>
        public static void DeregisterTrigger(this GameObject target, EventTriggerType eventTriggerType, UnityAction<BaseEventData> action)
        {
            if (target == null) return;
            if (!target.TryGetComponent<EventTrigger>(out var eventTrigger)) return;

            EventTrigger.Entry entry = eventTrigger.triggers.Find(e => e.eventID == eventTriggerType);

            if (entry != null)
            {
                entry.callback.RemoveListener(action);
            }
        }

        /// <summary>
        /// Destroy Trigger Component to GameObject
        /// </summary>
        public static void RemoveAllTriggers(this GameObject target)
        {
            if (target == null) return;
            if (!target.TryGetComponent<EventTrigger>(out var eventTrigger)) return;

            Object.Destroy(eventTrigger);
        }
        #endregion

        #region Vector3 Extensions
        /// <summary>
        /// Sets any values of the Vector3
        /// </summary>
        public static Vector3 Set(this Vector3 vector, float? x = null, float? y = null, float? z = null)
        {
            return new Vector3(x ?? vector.x, y ?? vector.y, z ?? vector.z);
        }

        /// <summary>
        /// Adds to any values of the Vector3
        /// </summary>
        public static Vector3 Add(this Vector3 vector, float? x = null, float? y = null, float? z = null)
        {
            return new Vector3(vector.x + (x ?? 0), vector.y + (y ?? 0), vector.z + (z ?? 0));
        }
        #endregion
    }
}
