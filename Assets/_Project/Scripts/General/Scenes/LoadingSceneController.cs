using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Gameseed26
{
    public class LoadingSceneController : MonoBehaviour
    {
        [SerializeField] private UnityEvent<float> OnProgress;

        public string fallbackSceneName = "MainMenu";
        public float minimumLoadingTime = 2f;

        private void Start()
        {
            StartCoroutine(LoadSceneAsync());
        }

        IEnumerator LoadSceneAsync()
        {
            float startTime = Time.time;

            string targetScene = SceneLoader.TargetSceneName;
            if (!SceneLoader.ValidateScene(targetScene))
                targetScene = fallbackSceneName;

            var asyncLoad = SceneManager.LoadSceneAsync(targetScene);
            asyncLoad.allowSceneActivation = false;

            while (asyncLoad.progress < 0.9f || Time.time - startTime < minimumLoadingTime)
            {
                float loadingProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                float timeProgress = Mathf.Clamp01((Time.time - startTime) / minimumLoadingTime);

                float progress = Mathf.Min(loadingProgress, timeProgress);

                OnProgress?.Invoke(progress);

                yield return null;
            }
            OnProgress?.Invoke(1f);

            float elapsedTime = Time.time - startTime;
            if (elapsedTime < minimumLoadingTime)
            {
                yield return new WaitForSeconds(minimumLoadingTime - elapsedTime);
            }
            asyncLoad.allowSceneActivation = true;

            Logger.Log($"Loaded scene: {targetScene} with loading time: {Time.time - startTime} seconds");
        }
    }
}
