using UnityEngine;

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
