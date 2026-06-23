using BrunoMikoski.AnimationSequencer;
using UnityEngine;

namespace Gameseed26
{
    public class SceneHandler : Singleton<SceneHandler>
    {
        [SerializeField] private AnimationSequencerController _sequencer;

        public void LoadScene(string sceneName)
        {
            if (_sequencer == null)
            {
                SceneLoader.LoadScene(sceneName);
                return;
            }

            _sequencer.Play(() => SceneLoader.LoadScene(sceneName));
        }
    }
}
