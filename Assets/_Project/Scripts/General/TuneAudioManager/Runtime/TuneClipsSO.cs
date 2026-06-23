using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Audio;

namespace Gameseed26
{
    /// <summary>
    /// Audio data for SFXs.
    /// </summary>
    [CreateAssetMenu(fileName = "TuneClips_", menuName = "Tunes/TuneClips Data")]
    public class TuneClipsSO : ScriptableObject
    {
        [Header("Audio Clips")]
        [Tooltip("You can add multiple clips for random selection. otherwise, just add one clip.")]
        public AudioClip[] Clips;
        public AudioMixerGroup MixerGroup;

        [Header("Properties")]
        public bool UseRandomVolume;
        [HideIf("UseRandomVolume"), Range(0f, 1f)]
        public float Volume = 1f;
        [ShowIf("UseRandomVolume"), MinMaxSlider(-1f, 1f)]
        public Vector2 RandomVolumeRange = new Vector2(-0.2f, 0.2f);

        [Space(10f)]
        public bool UseRandomPitch = true;
        [HideIf("UseRandomPitch"), Range(-3f, 3f)]
        public float Pitch = 1f;
        [ShowIf("UseRandomPitch"), MinMaxSlider(-3f, 3f)]
        public Vector2 RandomPitchRange = new Vector2(-0.2f, 0.2f);

        [Space(10f)]
        [Range(0f, 1f)] public float SpatialBlend = 0f; // 0 = 2D, 1 = 3D
        public bool Loop;

        public AudioClip GetRandomAudioClip()
        {
            if (Clips == null || Clips.Length == 0) return null;
            return Clips[Random.Range(0, Clips.Length)];
        }
    }
}
