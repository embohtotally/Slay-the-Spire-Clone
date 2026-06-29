using UnityEngine;
using NaughtyAttributes;


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
        [SerializeField, Scene] private string _gameplaySceneName;
        [SerializeField] private SfxID _onPlaySfx;
        [SerializeField] private MusicID _bgMusic;

        [Header("Campaign Flow")]
        [Tooltip("Enable for a New Game button. Leave off for Continue-style buttons that should keep saved level progress.")]
        [SerializeField] private bool _beginNewCampaignOnStartGame;

        void Start()
        {
            SetExitButton();

            Tune.MusicFade(_bgMusic, fadeDuration: .5f);
        }

        public void StartGame()
        {
            if (_beginNewCampaignOnStartGame)
            {
                BeginNewCampaign();
            }

            SceneLoader.LoadScene(_gameplaySceneName);

            Tune.StopMusicSafe();
        }

        public void StartNewCampaign()
        {
            BeginNewCampaign();
            SceneLoader.LoadScene(_gameplaySceneName);
            Tune.StopMusicSafe();
        }

        public void ContinueGame()
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

        private static void BeginNewCampaign()
        {
            LevelProgressionManager.GetOrCreate().BeginNewCampaign();
        }
    }
}
