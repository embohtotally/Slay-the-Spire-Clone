using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fully automated, idempotent asset generator for the Frieren Shield (Ordinary Defense Magic).
/// Generates:
/// 1. Materials (M_FrierenHexShield, M_FrierenShieldCircle, M_FrierenShatterFlash - HDR gold/amber)
/// 2. Mesh assets (Mesh_CurvedHexDisc, Mesh_ShieldQuad) via FrierenShieldMeshFactory
/// 3. Prefab (Assets/_Project/Prefabs/VFX/FrierenShield.prefab) with the full hierarchy:
///    Barrier (curved hex disc) + Circle (backing magic circle) + ShatterFlash + Shards,
///    all wired into FrierenShieldController.
/// 4. Registers the generated assets in PlayerSettings preloaded assets so the procedural
///    runtime fallback (Shader.Find) also works in player builds.
/// 5. Verifies the CombatantView integration hooks are present and reminds otherwise.
/// </summary>
[InitializeOnLoad]
public static class FrierenShieldSetupAutomation
{
    private const string HexShaderName = "Gameseed/FrierenShield/HexBarrier";
    private const string CircleShaderName = "Gameseed/FrierenShield/Circle";
    private const string FlashShaderName = "Gameseed/Spellbeam/Shockwave";   // Zoltraak suite reuse

    private const string MaterialDir = "Assets/_Project/Materials/FrierenShield";
    private const string PrefabDir = "Assets/_Project/Prefabs/VFX";

    private const string HexMatPath = MaterialDir + "/M_FrierenHexShield.mat";
    private const string CircleMatPath = MaterialDir + "/M_FrierenShieldCircle.mat";
    private const string FlashMatPath = MaterialDir + "/M_FrierenShatterFlash.mat";

    private const string DiscMeshPath = MaterialDir + "/Mesh_CurvedHexDisc.asset";
    private const string QuadMeshPath = MaterialDir + "/Mesh_ShieldQuad.asset";

    private const string PrefabPath = PrefabDir + "/FrierenShield.prefab";

    // Must match FrierenShieldController's serialized fallback values.
    private const int DiscSegments = 6;
    private const float DiscCurvature = 0.10f;

    static FrierenShieldSetupAutomation()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(PrefabPath) || !File.Exists(HexMatPath) || !File.Exists(CircleMatPath))
            {
                Debug.Log("[FrierenShieldSetupAutomation] Automated setup triggered (missing assets detected).");
                SetupAll();
            }
        };
    }

    [MenuItem("Tools/VFX/Setup Frieren Shield Completely", priority = 105)]
    public static void SetupAll()
    {
        try
        {
            EnsureDirectories();

            Material hexMat = SetupHexMaterial();
            Material circleMat = SetupCircleMaterial();
            Material flashMat = SetupFlashMaterial();
            (Mesh discMesh, Mesh quadMesh) = SetupMeshAssets();

            AssetDatabase.SaveAssets();

            GameObject prefab = SetupPrefab(hexMat, circleMat, flashMat, discMesh, quadMesh);
            TryRegisterPreloadedAssets(hexMat, circleMat, flashMat, prefab);
            TryWireSceneSystems(prefab);
            VerifyCombatantViewIntegration();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green><b>[FrierenShieldSetupAutomation] Frieren Shield setup completed successfully!</b></color>\n" +
                      $"Materials: {HexMatPath}, {CircleMatPath}, {FlashMatPath}\n" +
                      $"Prefab: {PrefabPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FrierenShieldSetupAutomation] Setup failed: {ex}");
        }
    }

    // ------------------------------------------------------------------ directories

    private static void EnsureDirectories()
    {
        EnsureDirectory(MaterialDir);
        EnsureDirectory(PrefabDir);
    }

    private static void EnsureDirectory(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    // ------------------------------------------------------------------ materials

    private static Material GetOrCreateMaterial(string path, string shaderName)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"[FrierenShieldSetupAutomation] Shader not found: {shaderName}");
            return mat;
        }

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    private static Material SetupHexMaterial()
    {
        Material mat = GetOrCreateMaterial(HexMatPath, HexShaderName);
        if (mat == null) return null;

        // Frieren gold/amber palette: #FFE066 core, #FFB300 rim, HDR bloom band 3-5.
        mat.SetColor("_BaseColor", new Color(1f, 0.878f, 0.40f, 1f));
        mat.SetColor("_EdgeColor", new Color(1f, 0.70f, 0f, 1f));
        mat.SetFloat("_Intensity", 3.2f);
        mat.SetFloat("_HexScale", 8f);
        mat.SetFloat("_EdgeThickness", 0.10f);
        mat.SetFloat("_FresnelPower", 3f);
        mat.SetFloat("_FresnelBias", 0.06f);
        mat.SetFloat("_RippleSpeed", 1.6f);
        mat.SetFloat("_RippleSharpness", 14f);
        mat.SetFloat("_RippleDamping", 2.6f);
        mat.SetFloat("_DeployT", 1f);
        mat.SetFloat("_DissolveT", 0f);
        mat.SetFloat("_ShatterT", 0f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material SetupCircleMaterial()
    {
        Material mat = GetOrCreateMaterial(CircleMatPath, CircleShaderName);
        if (mat == null) return null;

        mat.SetColor("_CircleColor", new Color(1f, 0.85f, 0.45f, 1f));
        mat.SetFloat("_CircleIntensity", 3.5f);
        mat.SetFloat("_OuterSpeed", 0.45f);
        mat.SetFloat("_InnerSpeed", -0.70f);
        mat.SetFloat("_TickCount", 24f);
        mat.SetFloat("_RuneCount", 9f);
        mat.SetFloat("_OuterRadius", 0.86f);
        mat.SetFloat("_InnerRadius", 0.64f);
        mat.SetFloat("_OuterWidth", 0.045f);
        mat.SetFloat("_InnerWidth", 0.06f);
        mat.SetFloat("_DeployT", 1f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material SetupFlashMaterial()
    {
        // Gold instance of the existing Zoltraak Shockwave shader (shatter flash + ring).
        // Optional: if the Zoltraak suite is absent, shatter simply skips the flash layer.
        Shader shader = Shader.Find(FlashShaderName);
        if (shader == null)
        {
            Debug.LogWarning("[FrierenShieldSetupAutomation] Shockwave shader not found - shatter flash layer skipped.");
            return null;
        }

        Material mat = GetOrCreateMaterial(FlashMatPath, FlashShaderName);
        if (mat == null) return null;

        mat.SetColor("_RingColor", new Color(1f, 0.75f, 0.30f, 1f));
        mat.SetFloat("_RingIntensity", 8f);
        mat.SetColor("_FlashColor", new Color(1f, 0.95f, 0.80f, 1f));
        mat.SetFloat("_FlashIntensity", 12f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ------------------------------------------------------------------ meshes

    private static (Mesh disc, Mesh quad) SetupMeshAssets()
    {
        Mesh disc = AssetDatabase.LoadAssetAtPath<Mesh>(DiscMeshPath);
        if (disc == null)
        {
            disc = FrierenShieldMeshFactory.GetCurvedHexDisc(DiscSegments, DiscCurvature);
            AssetDatabase.CreateAsset(disc, DiscMeshPath);
        }

        Mesh quad = AssetDatabase.LoadAssetAtPath<Mesh>(QuadMeshPath);
        if (quad == null)
        {
            quad = FrierenShieldMeshFactory.GetUnitQuad();
            AssetDatabase.CreateAsset(quad, QuadMeshPath);
        }

        return (disc, quad);
    }

    // ------------------------------------------------------------------ prefab

    private static GameObject SetupPrefab(Material hexMat, Material circleMat, Material flashMat, Mesh discMesh, Mesh quadMesh)
    {
        if (hexMat == null || circleMat == null || discMesh == null || quadMesh == null)
        {
            Debug.LogError("[FrierenShieldSetupAutomation] Missing shader/mesh prerequisites - prefab not rebuilt.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        GameObject root = FrierenShieldSystem.BuildSkeleton(null, false);
        FrierenShieldController controller = root.AddComponent<FrierenShieldController>();

        Transform barrier = root.transform.Find("Barrier");
        Transform circle = root.transform.Find("Circle");
        Transform flash = root.transform.Find("ShatterFlash");

        MeshFilter barrierFilter = barrier.GetComponent<MeshFilter>();
        MeshRenderer barrierRenderer = barrier.GetComponent<MeshRenderer>();
        MeshFilter circleFilter = circle.GetComponent<MeshFilter>();
        MeshRenderer circleRenderer = circle.GetComponent<MeshRenderer>();
        MeshFilter flashFilter = flash.GetComponent<MeshFilter>();
        MeshRenderer flashRenderer = flash.GetComponent<MeshRenderer>();
        ParticleSystem shardSystem = root.transform.Find("Shards").GetComponent<ParticleSystem>();

        barrierFilter.sharedMesh = discMesh;
        barrierRenderer.sharedMaterial = hexMat;
        circleFilter.sharedMesh = quadMesh;
        circleRenderer.sharedMaterial = circleMat;

        if (flashMat != null)
        {
            flashFilter.sharedMesh = quadMesh;
            flashRenderer.sharedMaterial = flashMat;
        }
        else
        {
            flash.gameObject.SetActive(false);
        }

        // Editor-preview placement values (runtime re-resolves toward the nearest threat).
        barrier.localPosition = Vector3.zero;
        barrier.localScale = Vector3.one * 1.24f;                       // radius 0.62 * 2
        circle.localPosition = new Vector3(0f, 0f, -0.07f);
        circle.localScale = Vector3.one * 1.55f;                        // radius * 2.5

        // Wire the controller's serialized references (names must match FrierenShieldController).
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("barrier").objectReferenceValue = barrier;
        so.FindProperty("barrierFilter").objectReferenceValue = barrierFilter;
        so.FindProperty("barrierRenderer").objectReferenceValue = barrierRenderer;
        so.FindProperty("circle").objectReferenceValue = circle;
        so.FindProperty("circleFilter").objectReferenceValue = circleFilter;
        so.FindProperty("circleRenderer").objectReferenceValue = circleRenderer;
        so.FindProperty("shatterFlash").objectReferenceValue = flash;
        so.FindProperty("shatterFlashRenderer").objectReferenceValue = flashMat != null ? flashRenderer : null;
        so.FindProperty("shards").objectReferenceValue = shardSystem;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return savedPrefab;
    }

    // ------------------------------------------------------------------ build safety & scene wiring

    /// <summary>Keeps the generated materials + prefab alive in player builds so the runtime
    /// procedural fallback (Shader.Find) resolves even without any content chain.</summary>
    private static void TryRegisterPreloadedAssets(params UnityEngine.Object[] assets)
    {
        List<UnityEngine.Object> current = new List<UnityEngine.Object>(PlayerSettings.GetPreloadedAssets());
        current.RemoveAll(a => a == null);

        bool changed = false;
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset == null || current.Contains(asset)) continue;
            current.Add(asset);
            changed = true;
        }

        if (changed)
        {
            PlayerSettings.SetPreloadedAssets(current.ToArray());
            Debug.Log("[FrierenShieldSetupAutomation] Registered shield assets in PlayerSettings preloaded assets.");
        }
    }

    /// <summary>If a FrierenShieldSystem was placed manually in an open scene, wire its prefab
    /// reference so it prefers the saved prefab over the procedural build.</summary>
    private static void TryWireSceneSystems(GameObject prefab)
    {
        if (prefab == null) return;

        FrierenShieldSystem[] systems = UnityEngine.Object.FindObjectsByType<FrierenShieldSystem>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (FrierenShieldSystem system in systems)
        {
            SerializedObject so = new SerializedObject(system);
            SerializedProperty prefabProp = so.FindProperty("shieldPrefab");
            if (prefabProp != null && prefabProp.objectReferenceValue == null)
            {
                prefabProp.objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(system);
            }
        }
    }

    private static void VerifyCombatantViewIntegration()
    {
        MethodInfo hook = typeof(CombatantView).GetMethod("NotifyShieldDeployed",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (hook == null)
        {
            Debug.LogWarning("[FrierenShieldSetupAutomation] CombatantView does not contain the " +
                             "NotifyShieldDeployed hook yet - apply the CombatantView.cs integration diff " +
                             "or shields will not react to AddShield/Damage/ClearShield.");
        }
    }
}
