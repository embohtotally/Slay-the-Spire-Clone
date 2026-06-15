using NaughtyAttributes;
using UnityEngine;

namespace Gameseed26
{
    public class GameManager : PersistentSingleton<GameManager>
    {
        [field: SerializeField, ReadOnly]
        public bool IsPaused { get; private set; }

        public void SetPaused(bool pause)
        {
            IsPaused = pause;

            if (pause) Time.timeScale = 1f;
            else Time.timeScale = 0f;
        }
    }
}
