using UnityEngine;

namespace Gameseed26
{
    public partial class Tune
    {
        /// <summary>
        /// Safe access point for code that wants to play audio without checking Tune.Instance manually.
        /// Example: Tune.SFX(SfxID.click); Tune.Music(MusicID.Space_Cadet);
        /// </summary>
        public static bool IsReady => Instance != null;

        static Tune Current
        {
            get
            {
                if (Instance == null)
                {
                    Logger.LogWarning("Tune is not ready yet. Make sure the Tune prefab exists in the first loaded scene.");
                    return null;
                }

                return Instance;
            }
        }

        // ---------- SFX ----------

        public static void SFX(SfxID sfxID, Vector2 position = default)
        {
            if (sfxID == SfxID.None) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlaySFX(sfxID, position);
        }

        public static void SFX(string sfxName, Vector2 position = default)
        {
            if (string.IsNullOrWhiteSpace(sfxName)) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlaySFX(sfxName, position);
        }

        public static void SFX(TuneClipsSO tuneClips, Vector2 position = default)
        {
            if (tuneClips == null) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlaySFX(tuneClips, position);
        }

        public static void SFXFollow(SfxID sfxID, Transform targetToFollow)
        {
            if (sfxID == SfxID.None || targetToFollow == null) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlaySFX(sfxID, targetToFollow);
        }

        public static void SFXFollow(string sfxName, Transform targetToFollow)
        {
            if (string.IsNullOrWhiteSpace(sfxName) || targetToFollow == null) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlaySFX(sfxName, targetToFollow);
        }

        public static void SFXFollow(TuneClipsSO tuneClips, Transform targetToFollow)
        {
            if (tuneClips == null || targetToFollow == null) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlaySFX(tuneClips, targetToFollow);
        }

        public static void StopSFXSafe(SfxID sfxID)
        {
            if (sfxID == SfxID.None) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.StopSFX(sfxID);
        }

        public static void StopAllSFXSafe()
        {
            Tune tune = Current;
            if (tune == null) return;

            tune.StopAllSFX();
        }

        // ---------- Music ----------

        public static void Music(MusicID musicID, int variation = 0)
        {
            if (musicID == MusicID.None) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlayMusic(musicID, variation);
        }

        public static void Music(string musicName, int variation = 0)
        {
            if (string.IsNullOrWhiteSpace(musicName)) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlayMusic(musicName, variation);
        }

        public static void Music(TuneTracksSO track, int variation = 0)
        {
            if (track == null) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlayMusic(track, variation);
        }

        public static void MusicFade(MusicID musicID, int variation = 0, float fadeDuration = 1f)
        {
            if (musicID == MusicID.None) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlayMusicWithFade(musicID, variation, fadeDuration);
        }

        public static void MusicFade(string musicName, int variation = 0, float fadeDuration = 1f)
        {
            if (string.IsNullOrWhiteSpace(musicName)) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlayMusicWithFade(musicName, variation, fadeDuration);
        }

        public static void MusicFade(TuneTracksSO track, int variation = 0, float fadeDuration = 1f)
        {
            if (track == null) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.PlayMusicWithFade(track, variation, fadeDuration);
        }

        public static void MusicCrossFade(MusicID musicID, int variation = 0, float duration = 2f)
        {
            if (musicID == MusicID.None) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.CrossFade(musicID, variation, duration);
        }

        public static void MusicCrossFade(TuneTracksSO track, int variation = 0, float duration = 2f)
        {
            if (track == null) return;

            Tune tune = Current;
            if (tune == null) return;

            tune.CrossFade(track, variation, duration);
        }

        public static void StopMusicSafe()
        {
            Tune tune = Current;
            if (tune == null) return;

            tune.StopMusic();
        }

        public static void StopAllAudioSafe()
        {
            Tune tune = Current;
            if (tune == null) return;

            tune.StopAllAudio();
        }
    }
}
