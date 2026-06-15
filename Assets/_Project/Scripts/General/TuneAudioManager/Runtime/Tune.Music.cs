using System;
using System.Collections;
using UnityEngine;

namespace Gameseed26
{
    public partial class Tune
    {
        /// <summary>
        /// Play Music using TuneTrack.
        /// If TuneTrack has variation, pass the index to select.
        /// </summary>
        public void PlayMusic(TuneTracksSO track, int variation = 0)
        {
            if (track.Clips.Length == 0)
            {
                Logger.LogError(transform, $"{track.name} doesn't have any Clip registered, please add one first.");
                return;
            }

            if (variation < 0 || variation >= track.Clips.Length)
            {
                Logger.LogWarning(transform, $"{track.name} only has {track.Clips.Length} variations. Fallback to 0");
                variation = 0;
            }

            StopMusic();

            // Init
            _musicSourceOne.clip = track.Clips[variation];
            _musicSourceOne.time = 0;
            _musicSourceOne.volume = track.Volume;
            _musicSourceOne.outputAudioMixerGroup = track.MixerGroup;
            _musicSourceOne.loop = true;

            _musicSourceOne.Play();
        }

        /// <summary>
        /// Play Music using enum ID.
        /// If TuneTrack has variation, pass the index to select.
        /// </summary>
        public void PlayMusic(MusicID musicID, int variation = 0)
        {
            if (musicID == MusicID.None) return;
            if (_musicMap.TryGetValue(musicID, out TuneTracksSO musicData))
            {
                PlayMusic(musicData, variation);
            }
        }

        /// <summary>
        /// Play Music by TuneTrack name (string).
        /// If TuneTrack has variation, pass the index to select.
        /// </summary>
        public void PlayMusic(string musicName, int variation = 0)
        {
            if (string.IsNullOrWhiteSpace(musicName)) return;
            if (_musicStringMap.TryGetValue(musicName, out var musicId))
            {
                PlayMusic(musicId, variation);
            }
        }

        /// <summary>
        /// Play Music with Fade by TuneTrack data.
        /// If TuneTrack has variation, pass the index to select.
        /// </summary>
        public void PlayMusicWithFade(TuneTracksSO track, int variation = 0, float fadeDuration = 1f)
        {
            if (track.Clips.Length == 0)
            {
                Logger.LogError(transform, $"{track.name} doesn't have any Clip registered, please add one first.");
                return;
            }

            if (variation < 0 || variation >= track.Clips.Length)
            {
                Logger.LogWarning(transform, $"{track.name} only has {track.Clips.Length} variations. Fallback to 0");
                variation = 0;
            }

            if (_musicFadeCoroutine != null) StopCoroutine(_musicFadeCoroutine);
            AudioSource activeMusicSource = _isSourceTwo ? _musicSourceTwo : _musicSourceOne;
            _musicFadeCoroutine = StartCoroutine(MusicFadeSequenceRoutine(track, variation, activeMusicSource, fadeDuration));
        }

        /// <summary>
        /// Play Music with Fade by MusicID enum.
        /// If TuneTrack has variation, pass the index to select.
        /// </summary>
        public void PlayMusicWithFade(MusicID musicID, int variation = 0, float fadeDuration = 1f)
        {
            if (musicID == MusicID.None) return;
            if (_musicMap.TryGetValue(musicID, out TuneTracksSO musicData))
            {
                PlayMusicWithFade(musicData, variation, fadeDuration);
            }
        }

        /// <summary>
        /// Play Music with Fade by TuneTrack data name (string).
        /// If TuneTrack has variation, pass the index to select.
        /// </summary>
        public void PlayMusicWithFade(string musicName, int variation = 0, float fadeDuration = 1f)
        {
            if (string.IsNullOrWhiteSpace(musicName)) return;
            if (_musicStringMap.TryGetValue(musicName, out var musicId))
            {
                PlayMusicWithFade(musicId, variation, fadeDuration);
            }
        }

        /// <summary>
        /// Crossfade music to the new one.
        /// If TuneTrack has variation, pass the index to select.
        /// </summary>
        public void CrossFade(TuneTracksSO track, int variation = 0, float duration = 2.0f)
        {
            if (track.Clips.Length == 0)
            {
                Logger.LogError(transform, $"{track.name} doesn't have any Clip registered, please add one first.");
                return;
            }

            if (variation < 0 || variation >= track.Clips.Length)
            {
                Logger.LogWarning(transform, $"{track.name} only has {track.Clips.Length} variations. Fallback to 0");
                variation = 0;
            }

            if (_musicFadeCoroutine != null) StopCoroutine(_musicFadeCoroutine);

            AudioSource activeSource = _isSourceTwo ? _musicSourceTwo : _musicSourceOne;
            AudioSource newSource = _isSourceTwo ? _musicSourceOne : _musicSourceTwo;

            _isSourceTwo = !_isSourceTwo;

            newSource.clip = track.Clips[variation];
            newSource.time = 0;
            newSource.outputAudioMixerGroup = track.MixerGroup;
            newSource.volume = 0;
            newSource.loop = true;
            newSource.Play();

            _musicFadeCoroutine = StartCoroutine(MusicCrossFadeRoutine(activeSource, newSource, track.Volume, duration));
        }

        /// <summary>
        /// Crossfade music by MusicID enum.
        /// If TuneTrack has variation, pass the index to select.
        /// </summary>
        public void CrossFade(MusicID musicID, int variation = 0, float duration = 2.0f)
        {
            if (musicID == MusicID.None) return;
            if (_musicMap.TryGetValue(musicID, out TuneTracksSO track))
            {
                CrossFade(track, variation, duration);
            }
        }

        /// <summary>
        /// Go outro immediately, without waiting for loop end
        /// </summary>
        public void StopMusic()
        {
            if (_musicSourceOne == null || _musicSourceTwo == null) return;
            _musicSourceOne.Stop();
            _musicSourceTwo.Stop();
            _isSourceTwo = false;
        }

        IEnumerator MusicFadeSequenceRoutine(TuneTracksSO track, int variation, AudioSource activeSource, float duration)
        {
            if (activeSource.isPlaying)
            {
                float startVol = activeSource.volume;
                for (float t = 0; t < duration; t += Time.deltaTime)
                {
                    activeSource.volume = Mathf.Lerp(startVol, 0, t / duration);
                    yield return null;
                }
            }

            activeSource.clip = track.Clips[variation];
            activeSource.time = 0;
            activeSource.outputAudioMixerGroup = track.MixerGroup;
            activeSource.loop = true;
            activeSource.Play();

            float targetVol = track.Volume;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                activeSource.volume = Mathf.Lerp(0, targetVol, t / duration);
                yield return null;
            }
            activeSource.volume = targetVol;
        }

        IEnumerator MusicCrossFadeRoutine(AudioSource oldSource, AudioSource newSource, float targetVolume, float duration)
        {
            float startVolOld = oldSource.volume;

            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float normalizedTime = t / duration;
                oldSource.volume = Mathf.Lerp(startVolOld, 0, normalizedTime);
                newSource.volume = Mathf.Lerp(0, targetVolume, normalizedTime);
                yield return null;
            }

            newSource.volume = targetVolume;
            oldSource.volume = 0;
            oldSource.Stop();
        }

        void InitializeMusicMaps()
        {
            foreach (var entry in Musics)
            {
                string entryName = entry.name.Replace(" ", "_").Replace("-", "_");
                if (Enum.TryParse(entryName, true, out MusicID idEnum))
                {
                    if (!_musicMap.ContainsKey(idEnum))
                    {
                        _musicMap.Add(idEnum, entry);
                    }

                    if (!_musicStringMap.ContainsKey(entryName))
                    {
                        _musicStringMap.Add(entryName, entry);
                    }
                }
                else
                    Logger.LogWarning(transform, $"Something wrong when init the MusicMaps! {entryName}");
            }
        }

        void InitializeMusic()
        {
            _musicSourceOne = CreateMusicSource("Music_Source_1");
            _musicSourceTwo = CreateMusicSource("Music_Source_2");
        }

        AudioSource CreateMusicSource(string sourceName)
        {
            var obj = new GameObject(sourceName);
            obj.transform.SetParent(transform);
            var src = obj.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            return src;
        }
    }
}
