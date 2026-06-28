using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameseed26
{
    public static class SceneLoader
    {
        public static string TargetSceneName { get; private set; }

        const string LOADING_SCENE_NAME = "Loading";

        public static void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("Cannot load scene because the scene name is empty.");
                return;
            }

            if (SceneUtility.GetBuildIndexByScenePath(LOADING_SCENE_NAME) == -1)
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                TargetSceneName = sceneName;
                SceneManager.LoadScene(LOADING_SCENE_NAME);
            }
        }

        public static AsyncOperation LoadSceneAdditive(string sceneName)
        {
            if (!ValidateScene(sceneName))
            {
                Debug.LogWarning($"Cannot load additive scene '{sceneName}' because it is not in Build Settings.");
                return null;
            }

            return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
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
    }
}
