using UnityEngine;
using UnityEngine.Audio;

namespace Gameseed26
{
    /// <summary>
    /// Audio data for Musics.
    /// </summary>
    [CreateAssetMenu(fileName = "TuneTracks_", menuName = "Tunes/TuneTracks Data")]
    public class TuneTracksSO : ScriptableObject
    {
        [Header("Audio Clips")]
        [Tooltip("Can add multiple music for variation.")]
        public AudioClip[] Clips;
        public AudioMixerGroup MixerGroup;

        [Header("Volume")]
        [Range(0f, 1f)] public float Volume = 1f;
    }
}
