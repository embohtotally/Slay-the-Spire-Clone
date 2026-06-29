using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Gameseed26
{
    public class LoadingSceneController : MonoBehaviour
    {
        [SerializeField] private UnityEvent<float> OnProgress;

        [Header("Fallback")]
        [SerializeField] private string fallbackSceneName = "MainMenu";
        [SerializeField] private float defaultMinimumLoadingTime = 2f;

        private void Start()
        {
            StartCoroutine(LoadSceneAsync());
        }

        private IEnumerator LoadSceneAsync()
        {
            float startTime = Time.time;
            SceneLoadRequest request = SceneLoader.CurrentRequest;

            string targetScene = request?.SceneName;
            if (!SceneLoader.ValidateScene(targetScene)) targetScene = fallbackSceneName;

            if (!SceneLoader.ValidateScene(targetScene))
            {
                Logger.LogWarning($"LoadingSceneController cannot load target scene '{targetScene}'.");
                yield break;
            }

            float minimumLoadingTime = request != null && request.MinimumLoadingTime >= 0f
                ? request.MinimumLoadingTime
                : defaultMinimumLoadingTime;

            LoadSceneMode loadMode = request?.LoadMode ?? LoadSceneMode.Single;
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene, loadMode);
            if (asyncLoad == null) yield break;

            asyncLoad.allowSceneActivation = false;

            while (asyncLoad.progress < 0.9f || Time.time - startTime < minimumLoadingTime)
            {
                float loadingProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                float timeProgress = minimumLoadingTime > 0f
                    ? Mathf.Clamp01((Time.time - startTime) / minimumLoadingTime)
                    : 1f;

                OnProgress?.Invoke(Mathf.Min(loadingProgress, timeProgress));
                yield return null;
            }

            OnProgress?.Invoke(1f);
            asyncLoad.allowSceneActivation = true;

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            if (request != null && request.SetActiveSceneWhenLoaded)
            {
                Scene loadedScene = SceneManager.GetSceneByName(targetScene);
                if (loadedScene.IsValid() && loadedScene.isLoaded)
                {
                    SceneManager.SetActiveScene(loadedScene);
                }
            }

            SceneLoader.ClearRequest(request);
            Logger.Log($"Loaded scene: {targetScene} with loading time: {Time.time - startTime:0.00} seconds");
        }
    }
}
