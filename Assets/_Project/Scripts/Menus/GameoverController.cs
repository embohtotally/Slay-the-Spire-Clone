using PlushieOdyssey.Core.Scenes;
using NaughtyAttributes;
using UnityEngine;

namespace PlushieOdyssey.UI.Menus
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
