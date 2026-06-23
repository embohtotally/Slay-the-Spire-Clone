using NaughtyAttributes;
using UnityEngine;

namespace Gameseed26
{
    public class GameoverController : MonoBehaviour
    {
        [SerializeField, Scene] private string _mainMenuScene;

        public void GoToMainMenu()
        {
            SceneLoader.LoadScene(_mainMenuScene);
        }
    }
}
