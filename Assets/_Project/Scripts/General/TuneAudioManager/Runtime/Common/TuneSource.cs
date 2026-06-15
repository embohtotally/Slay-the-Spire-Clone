using System;
using UnityEngine;

namespace Gameseed26
{
    /// <summary>
    /// Runtime data for played Audio.
    /// </summary>
    public class TuneSource : MonoBehaviour
    {
        public AudioSource Source;
        public float PlayStartTime;
        public bool IsLooping;

        public Transform Target;
    }
}
