using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace Gameseed26
{
    /// <summary>
    /// A persistent singleton component that handle Audio Play.    <br />
    /// Play any registered SFXs or Musics using this manager.      <br />
    /// Utilized Resource folder and setup audio entry via prefab.  <br />
    ///                                                             <br />
    /// Basic use:                                                  <br />
    /// - Generate prefab in <c>Tools > Tune Setup</c>              <br />
    /// - Create any TuneClipsSO/TuneTrackSO and register it.       <br />
    /// (or just select any audio and
    /// <c>Create > Tune > Generate (type) from selection</c>)      <br />
    /// - Click "Generate Enum"                                     <br />
    /// - Play in any object by call this singleton PlaySFX/PlayMusic method.
    /// </summary>
    public partial class Tune : PersistentSingleton<Tune>
    {
        /// <summary>
        /// List of SFXs registered in the bank and ready to use.
        /// </summary>
        [Header("Audio Bank")]
        [Tooltip("List of SFXs registered in the bank and ready to use.")]
        public List<TuneClipsSO> Sfxs;
        /// <summary>
        /// List of Musics registered in the bank and ready to use.
        /// </summary>
        [Tooltip("List of Musics registered in the bank and ready to use.")]
        public List<TuneTracksSO> Musics;

        [Header("Settings")]
        [SerializeField] int _poolSize = 10;

        // Pool
        List<TuneSource> _sfxSources = new();
        Dictionary<SfxID, TuneClipsSO> _sfxMap = new();
        Dictionary<string, TuneClipsSO> _sfxStringMap = new();
        List<TuneSource> _activeTrackingSources = new();

        Dictionary<MusicID, TuneTracksSO> _musicMap = new();
        Dictionary<string, TuneTracksSO> _musicStringMap = new();
        AudioSource _musicSourceOne;
        AudioSource _musicSourceTwo;
        Coroutine _musicFadeCoroutine;
        bool _isSourceTwo;

        protected override void Awake()
        {
            base.Awake();
            InitializeSFXPool();
            InitializeSFXMaps();

            InitializeMusic();
            InitializeMusicMaps();
        }

        void Update()
        {
            if (_activeTrackingSources.Count > 0)
            {
                SFXUpdate();
            }
        }

        /// <summary>
        /// Stop all running audio (SFX and Music)
        /// </summary>
        [Button(enabledMode: EButtonEnableMode.Playmode)]
        public void StopAllAudio()
        {
            foreach (var tune in _sfxSources)
            {
                tune.Source.Stop();
            }
            StopMusic();
        }

        protected void OnDestroy()
        {
            StopAllAudio();
        }
    }
}
