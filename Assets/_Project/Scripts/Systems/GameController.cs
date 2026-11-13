using PlushieOdyssey.Core.Scenes;
using NaughtyAttributes;
using UnityEngine;

namespace PlushieOdyssey.Core
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
