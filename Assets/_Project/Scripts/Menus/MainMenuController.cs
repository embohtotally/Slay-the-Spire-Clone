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
        [SerializeField] private SfxID _onPlaySfx;
        [SerializeField] private MusicID _bgMusic;

        void Start()
        {
            SetExitButton();

            Tune.MusicFade(_bgMusic, fadeDuration: .5f);
        }

        public void StartGame()
        {
            SceneLoader.LoadScene(_gameplaySceneName);

            Tune.StopMusicSafe();
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
