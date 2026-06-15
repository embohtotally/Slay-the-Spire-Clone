using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gameseed26.Editor
{
    public class AudioToSO : UnityEditor.Editor
    {
        [MenuItem("Assets/Create/Tunes/Generate TuneClips from Selection", false, 1)]
        public static void GenerateTuneClipsFromAudio()
        {
            Object[] selectedObjects = Selection.objects;
            List<AudioClip> audioClips = new();

            foreach (Object obj in selectedObjects)
            {
                if (obj is AudioClip clip)
                {
                    audioClips.Add(clip);
                }
            }

            if (audioClips.Count == 0)
            {
                EditorUtility.DisplayDialog("No Audio Found", "Please select one or more audio in projects.", "OK");
                return;
            }

            TuneAssetUtils.EnsureFolderExists(TuneAssetUtils.TARGET_FOLDER);
            string finalPath = Path.Combine(TuneAssetUtils.TARGET_FOLDER, $"{audioClips[0].name}.asset");

            finalPath = AssetDatabase.GenerateUniqueAssetPath(finalPath);

            TuneClipsSO asset = CreateInstance<TuneClipsSO>();
            asset.Clips = audioClips.ToArray();

            AssetDatabase.CreateAsset(asset, finalPath);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;

            Debug.Log($"<color=green>Success:</color> TuneClips created with {audioClips.Count} audio clips.");
        }

        [MenuItem("Assets/Create/Tunes/Generate TuneTrack from Selection", false, 2)]
        public static void GenerateTuneTrackFromAudio()
        {
            Object[] selectedObjects = Selection.objects;
            List<AudioClip> audioClips = new();

            foreach (Object obj in selectedObjects)
            {
                if (obj is AudioClip clip)
                {
                    audioClips.Add(clip);
                }
            }

            if (audioClips.Count == 0)
            {
                EditorUtility.DisplayDialog("No Audio Found", "Please select one or more audio in projects.", "OK");
                return;
            }

            TuneAssetUtils.EnsureFolderExists(TuneAssetUtils.TARGET_FOLDER);
            string finalPath = Path.Combine(TuneAssetUtils.TARGET_FOLDER, $"{audioClips[0].name}.asset");

            finalPath = AssetDatabase.GenerateUniqueAssetPath(finalPath);

            TuneTracksSO asset = CreateInstance<TuneTracksSO>();
            asset.Clips = audioClips.ToArray();

            AssetDatabase.CreateAsset(asset, finalPath);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;

            Debug.Log($"<color=green>Success:</color> TuneTrack created with {audioClips.Count} music/s.");
        }
    }
}
