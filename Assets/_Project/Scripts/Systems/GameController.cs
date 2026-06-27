using NaughtyAttributes;
using UnityEngine;

namespace Gameseed26
{
    public class GameController : MonoBehaviour
    {
        [SerializeField, Scene] private string menuScene;

        public void BackToMenu()
        {
            SceneLoader.LoadScene(menuScene);
        }
    }
}
