using System.Collections.Generic;
using System.IO;
using System.Text;
using NaughtyAttributes.Editor;
using UnityEditor;

namespace Gameseed26.Editor
{
    [CustomEditor(typeof(Tune))]
    public class TuneEditor : NaughtyInspector
    {
        const string GEN_ENUM_RELATIVE_PATH = "../Runtime/Generated/";

        const string ENUM_SFX = "SfxID";
        const string ENUM_MUSIC = "MusicID";

        public override void OnInspectorGUI()
        {
            Tune ctx = (Tune)target;

            if (UnityEngine.GUILayout.Button("Find All Sfxs"))
            {
                FindAssets<TuneClipsSO>(ctx, ref ctx.Sfxs);
            }
            if (UnityEngine.GUILayout.Button("Find All Musics"))
            {
                FindAssets<TuneTracksSO>(ctx, ref ctx.Musics);
            }
            if (UnityEngine.GUILayout.Button("Generate Audio Enums"))
            {
                GenerateEnum(ctx);
            }

            UnityEngine.GUILayout.Space(20);


            base.OnInspectorGUI();
        }

        void FindAssets<T>(Tune ctx, ref List<T> target) where T : UnityEngine.Object
        {
            Undo.RecordObject(ctx, "Find Assets " + typeof(T).Name);

            target = new();
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T));

            if (guids.Length == 0)
            {
                UnityEngine.Debug.LogWarning($"There is no {typeof(T).Name} in the Project");
                return;
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) target.Add(asset);
            }

            UnityEngine.Debug.Log($"There is {target.Count} of {typeof(T).Name} in the Project");
            EditorUtility.SetDirty(ctx);
        }

        void GenerateEnum(Tune ctx)
        {
            MonoScript self = MonoScript.FromScriptableObject(this);

            string path = AssetDatabase.GetAssetPath(self); // Get path of this script
            string scriptDirectory = Path.GetDirectoryName(path); // Get directory

            string targetDirectory = Path.Combine(scriptDirectory, GEN_ENUM_RELATIVE_PATH);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// This is an auto-generated file via \"Generate Audio Enums\" button in Tune.cs.");
            sb.AppendLine("// Do not modify directly.");

            sb.AppendLine();

            // Generate namespace
            sb.AppendLine("namespace Gameseed26");
            sb.AppendLine("{");

            sb.AppendLine();

            // Generate SFX enum
            sb.AppendLine("\tpublic enum " + ENUM_SFX);
            sb.AppendLine("\t{");
            sb.AppendLine("\t\tNone,");

            foreach (var sfx in ctx.Sfxs)
            {
                string cleanName = sfx.name.Replace(" ", "_").Replace("-", "_");
                sb.AppendLine("\t\t" + cleanName + ",");
            }

            sb.AppendLine("\t}");

            sb.AppendLine();

            // Generate Music enum
            sb.AppendLine("\tpublic enum " + ENUM_MUSIC);
            sb.AppendLine("\t{");
            sb.AppendLine("\t\tNone,");

            foreach (var music in ctx.Musics)
            {
                string cleanName = music.name.Replace(" ", "_").Replace("-", "_");
                sb.AppendLine("\t\t" + cleanName + ",");
            }

            sb.AppendLine("\t}");

            sb.AppendLine();

            sb.AppendLine("}");

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory); // Ensure directory exists
            }

            string finalPath = Path.Combine(targetDirectory, "AudioEnums.cs");
            File.WriteAllText(finalPath, sb.ToString());
            AssetDatabase.Refresh(); // Refresh Assets
            UnityEngine.Debug.Log("Generated Audio Enums at: " + finalPath);
        }
    }
}
