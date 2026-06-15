using UnityEditor;
using UnityEngine;

namespace Gameseed26.Editor
{
    public class TunePrefabGenerator : EditorWindow
    {
        [MenuItem("Tools/Tune Setup")]
        public static void CreateTunePrefabs()
        {
            const string FULL_PATH = TuneAssetUtils.TARGET_FOLDER + "/" + TuneAssetUtils.ASSET_NAME + ".prefab";

            TuneAssetUtils.EnsureFolderExists(TuneAssetUtils.TARGET_FOLDER);

            GameObject go = new(TuneAssetUtils.ASSET_NAME, typeof(Tune));

            try
            {
                PrefabUtility.SaveAsPrefabAsset(go, FULL_PATH, out bool success);

                if (success)
                    Debug.Log($"<color=green>Success:</color> Prefab saved at {FULL_PATH}\nDon't move or change the name!!");
                else
                    Debug.LogError("Failed to save prefab.");
            }
            finally
            {
                DestroyImmediate(go);
                AssetDatabase.Refresh();

                EditorUtility.FocusProjectWindow();
                Selection.activeObject = go;
            }
        }
    }
}
