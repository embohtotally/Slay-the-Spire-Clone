using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Gameseed26
{
    public partial class Tune
    {
        public event Action<TuneSource> OnSFXComplete;

        /// <summary>
        /// Play SFX directly using TuneClips data.
        /// Pass Vector2 to play the SFX in specified world position.
        /// </summary>
        public void PlaySFX(TuneClipsSO tuneClips, Vector2 position = default)
        {
            if (tuneClips == null)
            {
                Logger.LogWarning(transform, "TuneClips is null, cannot play the sfx!");
                return;
            }

            TuneSource tune = GetAvailableSource();
            AudioSource source = tune.Source;

            ResetTuneSource(tune);
            InitializeSource(source, tuneClips);

            if (source.clip == null)
            {
                Logger.LogWarning(transform, $"{tuneClips.name} doesn't have any AudioClip registered, cannot play the sfx!");
                return;
            }

            source.transform.position = position;

            tune.PlayStartTime = Time.time;
            tune.IsLooping = tuneClips.Loop;

            source.Play();

            if (!tune.IsLooping)
            {
                StartCoroutine(WaitForAudioComplete(tune));
            }
        }

        /// <summary>
        /// Play SFX directly using TuneClips data.
        /// Pass Transform ref to bind SFX position to it.
        /// </summary>
        public void PlaySFX(TuneClipsSO tuneClips, Transform targetToFollow)
        {
            if (tuneClips == null || targetToFollow == null)
            {
                Logger.LogWarning(transform, "TuneClips is null, cannot play the sfx!");
                return;
            }

            TuneSource tune = GetAvailableSource();
            AudioSource source = tune.Source;

            ResetTuneSource(tune);
            InitializeSource(source, tuneClips);

            if (source.clip == null)
            {
                Logger.LogWarning(transform, $"{tuneClips.name} doesn't have any AudioClip registered, cannot play the sfx!");
                return;
            }

            tune.Target = targetToFollow;
            tune.transform.position = targetToFollow.position;
            tune.PlayStartTime = Time.time;
            tune.IsLooping = tuneClips.Loop;
            _activeTrackingSources.Add(tune);

            source.Play();
        }

        /// <summary>
        /// Play SFX using enum ID
        /// Pass Vector2 to play the SFX in specified world position.
        /// </summary>
        public void PlaySFX(SfxID sfxID, Vector2 position = default)
        {
            if (sfxID == SfxID.None) return;
            if (_sfxMap.TryGetValue(sfxID, out TuneClipsSO tuneClips))
            {
                PlaySFX(tuneClips, position);
            }
        }

        /// <summary>
        /// Play SFX using enum ID
        /// Pass Transform ref to bind SFX position to it.
        /// </summary>
        public void PlaySFX(SfxID sfxID, Transform targetToFollow)
        {
            if (sfxID == SfxID.None) return;
            if (_sfxMap.TryGetValue(sfxID, out TuneClipsSO tuneClips))
            {
                PlaySFX(tuneClips, targetToFollow);
            }
        }

        /// <summary>
        /// Play Music by TuneClips data name (string).
        /// Pass Vector2 to play the SFX in specified world position.
        /// </summary>
        public void PlaySFX(string sfxName, Vector2 position = default)
        {
            if (string.IsNullOrWhiteSpace(sfxName)) return;
            if (_sfxStringMap.TryGetValue(sfxName, out var sfxId))
            {
                PlaySFX(sfxId, position);
            }
        }

        /// <summary>
        /// Play Music by TuneClips data name (string).
        /// Pass Transform ref to bind SFX position to it.
        /// </summary>
        public void PlaySFX(string sfxName, Transform targetToFollow)
        {
            if (string.IsNullOrWhiteSpace(sfxName)) return;
            if (_sfxStringMap.TryGetValue(sfxName, out var sfxId))
            {
                PlaySFX(sfxId, targetToFollow);
            }
        }

        /// <summary>
        /// Stop specific SFX by enum ID.
        /// </summary>
        public void StopSFX(SfxID sfxID)
        {
            if (sfxID == SfxID.None) return;
            if (_sfxMap.TryGetValue(sfxID, out TuneClipsSO tuneClips))
            {
                if (tuneClips.Clips == null || tuneClips.Clips.Length == 0) return;

                foreach (var tune in _sfxSources)
                {
                    if (tune.Source.isPlaying && tuneClips.Clips.Contains(tune.Source.clip))
                    {
                        tune.Source.Stop();
                        ResetTuneSource(tune);
                    }
                }
            }
        }

        /// <summary>
        /// Stop all running SFX.
        /// </summary>
        public void StopAllSFX()
        {
            foreach (var tune in _sfxSources)
            {
                tune.Source.Stop();
                ResetTuneSource(tune);
            }

            _activeTrackingSources.Clear();
        }

        void InitializeSFXPool()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                GameObject obj = new GameObject("SFX_Source_" + i);
                obj.transform.SetParent(transform);
                AudioSource source = obj.AddComponent<AudioSource>();
                source.playOnAwake = false;

                TuneSource tuneSource = obj.AddComponent<TuneSource>();
                tuneSource.Source = source;
                _sfxSources.Add(tuneSource);
            }
        }

        void InitializeSFXMaps()
        {
            foreach (var entry in Sfxs)
            {
                string entryName = entry.name.Replace(" ", "_").Replace("-", "_");
                if (Enum.TryParse(entryName, true, out SfxID idEnum))
                {
                    if (!_sfxMap.ContainsKey(idEnum))
                    {
                        _sfxMap.Add(idEnum, entry);
                    }

                    if (!_sfxStringMap.ContainsKey(entryName))
                    {
                        _sfxStringMap.Add(entryName, entry);
                    }
                }
                else
                    Logger.LogWarning(transform, $"Something wrong when init the SFXMaps! {entryName}");
            }
        }

        void InitializeSource(AudioSource source, TuneClipsSO tuneClips)
        {
            source.clip = tuneClips.GetRandomAudioClip();
            source.outputAudioMixerGroup = tuneClips.MixerGroup;
            source.loop = tuneClips.Loop;
            source.spatialBlend = tuneClips.SpatialBlend;

            source.volume = tuneClips.UseRandomVolume
                ? Mathf.Clamp01(
                    1 + UnityEngine.Random.Range(tuneClips.RandomVolumeRange.x, tuneClips.RandomVolumeRange.y))
                : tuneClips.Volume;

            source.pitch = tuneClips.UseRandomPitch
                ? Mathf.Clamp(
                    1 + UnityEngine.Random.Range(tuneClips.RandomPitchRange.x, tuneClips.RandomPitchRange.y),
                    -3,
                    3)
                : tuneClips.Pitch;
        }

        void ResetTuneSource(TuneSource tune)
        {
            if (tune == null) return;

            tune.Target = null;
            tune.IsLooping = false;
            tune.PlayStartTime = 0f;
            tune.transform.position = Vector3.zero;
            _activeTrackingSources.Remove(tune);
        }

        /// <summary>
        /// If any source played to target, update the position until its stopped.
        /// </summary>
        void SFXUpdate()
        {
            // Tracking Source
            for (int i = _activeTrackingSources.Count - 1; i >= 0; i--)
            {
                TuneSource tune = _activeTrackingSources[i];

                if (tune.Target != null && tune.Source.isPlaying)
                {
                    tune.transform.position = tune.Target.position;
                }
                else if (!tune.Source.isPlaying || tune.Target == null)
                {
                    tune.Source.Stop();
                    tune.Target = null;
                    tune.transform.position = Vector3.zero;

                    OnSFXComplete?.Invoke(tune);

                    _activeTrackingSources.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Get idle source if there is no active pool.
        /// otherwise, iterate all sources and get the oldest played source to
        /// overwrite.
        /// </summary>
        /// <returns>target TuneSource to play the SFX</returns>
        TuneSource GetAvailableSource()
        {
            // Get idle source
            foreach (var tune in _sfxSources)
            {
                if (!tune.Source.isPlaying) return tune;
            }

            // If there is none, get the oldest play time and overwrite.
            TuneSource oldestTune = null;
            float oldestTime = float.MaxValue;

            foreach (var tune in _sfxSources)
            {
                if (tune.IsLooping) continue; // skip looping sound
                if (tune.PlayStartTime < oldestTime)
                {
                    oldestTime = tune.PlayStartTime;
                    oldestTune = tune;
                }
            }

            if (oldestTune != null)
            {
                if (_activeTrackingSources.Contains(oldestTune))
                    _activeTrackingSources.Remove(oldestTune);

                oldestTune.Source.Stop();
                return oldestTune;
            }

            // If still cannot find available source, fallback to the first source
            return _sfxSources[0];
        }

        IEnumerator WaitForAudioComplete(TuneSource tuneSource)
        {
            if (tuneSource == null || tuneSource.Source == null || tuneSource.Source.clip == null)
                yield break;

            AudioClip playingClip = tuneSource.Source.clip;
            float playingPitch = Mathf.Abs(tuneSource.Source.pitch);
            float duration = playingPitch > 0f ? playingClip.length / playingPitch : playingClip.length;

            yield return new WaitForSeconds(duration);

            if (tuneSource.Source.clip == playingClip && !tuneSource.Source.loop)
            {
                OnSFXComplete?.Invoke(tuneSource);
            }
        }
    }
}
