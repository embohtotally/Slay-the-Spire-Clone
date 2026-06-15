using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameseed26
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _exitButton;

        [Header("Settings")]
        [SerializeField] private string _gameplaySceneName = "Map";

        void Start()
        {
            SetExitButton();
        }

        public void StartGame()
        {
            SceneLoader.LoadScene(_gameplaySceneName);
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetExitButton()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _exitButton.SetActive(false);
#endif
        }
    }
}
