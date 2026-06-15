using UnityEngine;

namespace Gameseed26
{
    /*
        Static class to call generated prefab in specific path ("Resource/Tunes/Tune.prefab"),
        if has Tune component, then Instantiate it to scene early.
    */

    public static class TuneInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            Tune tunePrefab = Resources.Load<Tune>("Tunes/Tune");

            if (tunePrefab != null)
            {
                Object.Instantiate(tunePrefab.gameObject);
            }
        }
    }
}
