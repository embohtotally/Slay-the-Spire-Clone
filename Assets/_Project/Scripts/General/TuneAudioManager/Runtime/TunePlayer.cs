using NaughtyAttributes;
using UnityEngine;

namespace Gameseed26
{
    public class TunePlayer : MonoBehaviour
    {
        private enum PlayerType { SFX, Music }
        private enum MusicPlayType { Normal, Fade, CrossFade }

        [Header("Player Settings")]
        [SerializeField] private PlayerType _playerType;
        [SerializeField, ShowIf("_playerType", PlayerType.SFX)] private SfxID _sfx;
        [SerializeField, ShowIf("_playerType", PlayerType.Music)] private MusicID _music;
        [SerializeField, ShowIf("_playerType", PlayerType.Music)] private MusicPlayType _musicPlayType;

        public void Play()
        {
            if (_playerType == PlayerType.SFX) Tune.SFX(_sfx);
            else if (_playerType == PlayerType.Music)
            {
                if (_musicPlayType == MusicPlayType.Normal) Tune.Music(_music);
                else if (_musicPlayType == MusicPlayType.Fade) Tune.MusicFade(_music);
                else if (_musicPlayType == MusicPlayType.CrossFade) Tune.MusicCrossFade(_music);
            }
        }

        public void Stop()
        {
            if (_playerType == PlayerType.SFX) Tune.StopSFXSafe(_sfx);
            else if (_playerType == PlayerType.Music)
            {
                if (_musicPlayType == MusicPlayType.Normal) Tune.StopMusicSafe();
                else if (_musicPlayType == MusicPlayType.Fade) Tune.MusicFade((TuneTracksSO)null);
                else if (_musicPlayType == MusicPlayType.CrossFade) Tune.MusicCrossFade(null);
            }
        }

        public void StopAll() => Tune.StopAllAudioSafe();
    }
}
