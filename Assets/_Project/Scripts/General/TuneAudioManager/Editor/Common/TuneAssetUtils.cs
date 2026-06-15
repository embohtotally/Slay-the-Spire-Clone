using UnityEditor;

namespace Gameseed26.Editor
{
    public static class TuneAssetUtils
    {
        public const string TARGET_FOLDER = "Assets/Resources/Tunes";
        public const string ASSET_NAME = "Tune";

        public static void EnsureFolderExists(string path)
        {
            string[] folders = path.Split('/');
            string currentPath = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = nextPath;
            }
        }
    }
}
