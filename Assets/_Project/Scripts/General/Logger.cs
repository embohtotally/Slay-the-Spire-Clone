using System;
using System.Diagnostics;
using UnityEngine;

namespace Gameseed26
{
    /// <summary>
    /// UnityEngine.Debug Wrapper so it only compiled in Unity Editor.
    /// </summary>
    public static class Logger
    {
        #region Log
        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public static void Log(UnityEngine.Object context, object message)
        {
            UnityEngine.Debug.Log($"[Logger]-[{context.name}] {message}", context);
        }

        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public static void Log(object context, object message)
        {
            UnityEngine.Debug.Log($"[Logger]-[{context.GetType().Name}] {message}");
        }

        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public static void Log(object message)
        {
            UnityEngine.Debug.Log($"[Logger] {message}");
        }
        #endregion

        #region LogWarning
        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public static void LogWarning(UnityEngine.Object context, object message)
        {
            UnityEngine.Debug.LogWarning($"[Logger]-[{context.name}] {message}", context);
        }

        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public static void LogWarning(object context, object message)
        {
            UnityEngine.Debug.LogWarning($"[Logger]-[{context.GetType().Name}] {message}");
        }

        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public static void LogWarning(object message)
        {
            UnityEngine.Debug.LogWarning($"[Logger] {message}");
        }
        #endregion

        #region LogError
        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public static void LogError(UnityEngine.Object context, object message)
        {
            UnityEngine.Debug.LogError($"[Logger]-[{context.name}] {message}", context);
        }

        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public static void LogError(object context, object message)
        {
            UnityEngine.Debug.LogError($"[Logger]-[{context.GetType().Name}] {message}");
        }

        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public static void LogError(object message)
        {
            UnityEngine.Debug.LogError($"[Logger] {message}");
        }
        #endregion

        #region LogException
        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public static void LogException(Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
        }

        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public static void LogException(Exception exception, UnityEngine.Object context)
        {
            UnityEngine.Debug.LogException(exception, context);
        }
        #endregion

    }
}
