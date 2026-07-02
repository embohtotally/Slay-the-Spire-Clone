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
            Tune.SFX(_onPlaySfx);

            if (_beginNewCampaignOnStartGame)
            {
                BeginNewCampaign();
            }

            LoadGameplaySceneOrTutorial();

            Tune.StopMusicSafe();
        }

        public void StartNewCampaign()
        {
            Tune.SFX(_onPlaySfx);
            BeginNewCampaign();
            LoadGameplaySceneOrTutorial();
            Tune.StopMusicSafe();
        }

        public void ContinueGame()
        {
            LoadGameplaySceneOrTutorial();
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

        private void LoadGameplaySceneOrTutorial()
        {
            if (PlayerPrefs.GetInt("HasPlayedTutorial", 0) == 0)
            {
                // Set the flag so they never see the tutorial again
                PlayerPrefs.SetInt("HasPlayedTutorial", 1);
                PlayerPrefs.Save();
                
                SceneLoader.LoadScene("DialogueTutorial");
            }
            else
            {
                SceneLoader.LoadScene(_gameplaySceneName);
            }
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
