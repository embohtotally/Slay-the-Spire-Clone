using UnityEngine;

namespace Gameseed26
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Execute()
        {
            if (GameManager.Instance == null)
            {
                var managerPrefab = Resources.Load<GameObject>("GameManager");

                if (managerPrefab != null)
                {
                    Object.Instantiate(managerPrefab);
                    Logger.Log("Bootstrap loaded!");
                }
                else
                {
                    Logger.Log("Something wrong when load bootstrap!");
                }
            }
        }
    }
}
