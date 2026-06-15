using UnityEngine.SceneManagement;

namespace Gameseed26
{
    public static class SceneLoader
    {
        public static string TargetSceneName { get; private set; }

        const string LOADING_SCENE_NAME = "Loading";

        public static void LoadScene(string sceneName)
        {
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

        public static bool ValidateScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return SceneUtility.GetBuildIndexByScenePath(sceneName) != -1;
        }
    }
}
