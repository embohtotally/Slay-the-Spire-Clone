using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameseed26
{
    public static class SceneLoader
    {
        public const string LoadingSceneName = "Loading";
        public const float UseLoadingSceneDefaultMinimumTime = -1f;

        public static SceneLoadRequest CurrentRequest { get; private set; }
        public static string TargetSceneName => CurrentRequest?.SceneName;

        public static void LoadScene(string sceneName)
        {
            LoadScene(sceneName, UseLoadingSceneDefaultMinimumTime);
        }

        public static void LoadScene(string sceneName, float minimumLoadingTime)
        {
            if (!ValidateScene(sceneName))
            {
                Logger.LogWarning($"Cannot load scene '{sceneName}' because it is not in Build Settings.");
                return;
            }

            CurrentRequest = SceneLoadRequest.Single(sceneName, minimumLoadingTime);

            if (!ValidateScene(LoadingSceneName))
            {
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                return;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartCoroutine(LoadLoadingSceneAsync());
            }
            else
            {
                SceneManager.LoadSceneAsync(LoadingSceneName, LoadSceneMode.Single);
            }
        }

        public static void LoadSceneWithMinimumTime(string sceneName, float minimumLoadingTime)
        {
            LoadScene(sceneName, minimumLoadingTime);
        }

        public static AsyncOperation LoadSceneAdditive(string sceneName)
        {
            return LoadSceneAdditive(sceneName, false);
        }

        public static AsyncOperation LoadSceneAdditive(string sceneName, bool setActiveSceneWhenLoaded)
        {
            if (!ValidateScene(sceneName))
            {
                Logger.LogWarning($"Cannot load additive scene '{sceneName}' because it is not in Build Settings.");
                return null;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (setActiveSceneWhenLoaded && GameManager.Instance != null)
            {
                GameManager.Instance.StartCoroutine(SetActiveWhenLoaded(sceneName, operation));
            }

            return operation;
        }

        public static AsyncOperation UnloadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return null;

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return null;
            if (SceneManager.sceneCount <= 1) return null;

            return SceneManager.UnloadSceneAsync(scene);
        }

        public static bool ValidateScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return SceneUtility.GetBuildIndexByScenePath(sceneName) != -1;
        }

        public static void ClearRequest(SceneLoadRequest request)
        {
            if (ReferenceEquals(CurrentRequest, request)) CurrentRequest = null;
        }

        private static IEnumerator LoadLoadingSceneAsync()
        {
            AsyncOperation loadingOperation = SceneManager.LoadSceneAsync(LoadingSceneName, LoadSceneMode.Single);
            if (loadingOperation == null) yield break;

            while (!loadingOperation.isDone)
            {
                yield return null;
            }
        }

        private static IEnumerator SetActiveWhenLoaded(string sceneName, AsyncOperation operation)
        {
            if (operation == null) yield break;

            while (!operation.isDone)
            {
                yield return null;
            }

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
            }
        }
    }

    public sealed class SceneLoadRequest
    {
        public string SceneName { get; }
        public LoadSceneMode LoadMode { get; }
        public float MinimumLoadingTime { get; }
        public bool SetActiveSceneWhenLoaded { get; }

        private SceneLoadRequest(string sceneName, LoadSceneMode loadMode, float minimumLoadingTime, bool setActiveSceneWhenLoaded)
        {
            SceneName = sceneName;
            LoadMode = loadMode;
            MinimumLoadingTime = minimumLoadingTime;
            SetActiveSceneWhenLoaded = setActiveSceneWhenLoaded;
        }

        public static SceneLoadRequest Single(string sceneName, float minimumLoadingTime)
        {
            return new SceneLoadRequest(sceneName, LoadSceneMode.Single, minimumLoadingTime, true);
        }
    }
}
