using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fully automated, idempotent asset generator and scene linker for the Zoltraak Beam VFX system.
/// Generates:
/// 1. Materials (M_BeamCore, M_BeamSheath, M_MagicCircle, M_Shockwave)
/// 2. Prefab (Assets/_Project/Prefabs/VFX/ZoltraakBeam.prefab) with ZoltraakBeamController and impact slots
/// 3. BeamVfxConfig (Assets/_Project/ScriptableObjects/VFX/Zoltraak_Config.asset)
/// 4. CardData (Assets/_Project/ScriptableObjects/Cards/Generated/Zoltraak.asset)
/// 5. Auto-injects Zoltraak into Default Hero starting deck and Reward Pool for zero-friction testing.
/// </summary>
[InitializeOnLoad]
public static class ZoltraakSetupAutomation
{
    private const string BeamCoreShaderName = "Gameseed/Spellbeam/BeamCore";
    private const string BeamSheathShaderName = "Gameseed/Spellbeam/BeamSheath";
    private const string MagicCircleShaderName = "Gameseed/Spellbeam/MagicCircle";
    private const string ShockwaveShaderName = "Gameseed/Spellbeam/Shockwave";

    private const string MaterialDir = "Assets/_Project/Materials/Spellbeam";
    private const string PrefabDir = "Assets/_Project/Prefabs/VFX";
    private const string ConfigDir = "Assets/_Project/ScriptableObjects/VFX";
    private const string CardDir = "Assets/_Project/ScriptableObjects/Cards/Generated";

    private const string CoreMatPath = MaterialDir + "/M_BeamCore.mat";
    private const string SheathMatPath = MaterialDir + "/M_BeamSheath.mat";
    private const string CircleMatPath = MaterialDir + "/M_MagicCircle.mat";
    private const string ShockMatPath = MaterialDir + "/M_Shockwave.mat";

    private const string PrefabPath = PrefabDir + "/ZoltraakBeam.prefab";
    private const string ConfigPath = ConfigDir + "/Zoltraak_Config.asset";
    private const string CardPath = CardDir + "/Zoltraak.asset";
    private const string CylinderMeshPath = MaterialDir + "/Mesh_UnitCylinder.asset";
    private const string QuadMeshPath = MaterialDir + "/Mesh_UnitQuad.asset";

    static ZoltraakSetupAutomation()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(CardPath) || !File.Exists(PrefabPath) || !File.Exists(ConfigPath) || !File.Exists(CylinderMeshPath))
            {
                Debug.Log("[ZoltraakSetupAutomation] Automated setup / mesh generation triggered.");
                SetupAll();
            }
        };
    }

    [MenuItem("Tools/VFX/Setup Zoltraak Attack Completely", priority = 100)]
    public static void SetupAll()
    {
        try
        {
            EnsureDirectories();

            Material coreMat = SetupCoreMaterial();
            Material sheathMat = SetupSheathMaterial();
            Material circleMat = SetupCircleMaterial();
            Material shockMat = SetupShockMaterial();

            AssetDatabase.SaveAssets();

            GameObject prefab = SetupPrefab(coreMat, sheathMat, circleMat, shockMat);
            BeamVfxConfig config = SetupConfig(prefab, coreMat, sheathMat, circleMat, shockMat);
            CardData card = SetupCard(config);
            TryAddToRewardPool(card);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green><b>[ZoltraakSetupAutomation] Zoltraak Attack setup completed successfully!</b></color>");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ZoltraakSetupAutomation] Setup failed: {ex}");
        }
    }

    private static void EnsureDirectories()
    {
        EnsureDirectory(MaterialDir);
        EnsureDirectory(PrefabDir);
        EnsureDirectory(ConfigDir);
        EnsureDirectory(CardDir);
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

    private static Material GetOrCreateMaterial(string path, string shaderName)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"[ZoltraakSetupAutomation] Shader not found: {shaderName}");
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

    private static Material SetupCoreMaterial()
    {
        Material mat = GetOrCreateMaterial(CoreMatPath, BeamCoreShaderName);
        if (mat == null) return null;

        mat.SetColor("_CoreColor", new Color(1f, 0.35f, 0.75f, 1f));
        mat.SetColor("_HotColor", Color.white);
        mat.SetFloat("_CoreIntensity", 6f);
        mat.SetFloat("_CorePower", 3f);
        mat.SetFloat("_NoiseFrequency", 6f);
        mat.SetFloat("_FlowSpeed", 9f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material SetupSheathMaterial()
    {
        Material mat = GetOrCreateMaterial(SheathMatPath, BeamSheathShaderName);
        if (mat == null) return null;

        mat.SetColor("_SheathColor", new Color(0.72f, 0.30f, 1f, 1f));
        mat.SetFloat("_SheathIntensity", 2.5f);
        mat.SetFloat("_NoiseFrequency", 4f);
        mat.SetFloat("_FlowSpeed", 5f);
        mat.SetFloat("_RibFrequency", 18f);
        mat.SetFloat("_RibSpeed", 7f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material SetupCircleMaterial()
    {
        Material mat = GetOrCreateMaterial(CircleMatPath, MagicCircleShaderName);
        if (mat == null) return null;

        mat.SetColor("_CircleColor", new Color(1f, 0.50f, 0.85f, 1f));
        mat.SetFloat("_CircleIntensity", 4f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material SetupShockMaterial()
    {
        Material mat = GetOrCreateMaterial(ShockMatPath, ShockwaveShaderName);
        if (mat == null) return null;

        mat.SetColor("_RingColor", new Color(1f, 0.60f, 0.90f, 1f));
        mat.SetFloat("_RingIntensity", 7f);
        mat.SetColor("_FlashColor", Color.white);
        mat.SetFloat("_FlashIntensity", 10f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static (Mesh cyl, Mesh quad) SetupMeshAssets()
    {
        Mesh cyl = AssetDatabase.LoadAssetAtPath<Mesh>(CylinderMeshPath);
        if (cyl == null)
        {
            cyl = CreateCylinderMesh(12);
            AssetDatabase.CreateAsset(cyl, CylinderMeshPath);
        }

        Mesh quad = AssetDatabase.LoadAssetAtPath<Mesh>(QuadMeshPath);
        if (quad == null)
        {
            quad = CreateQuadMesh();
            AssetDatabase.CreateAsset(quad, QuadMeshPath);
        }

        return (cyl, quad);
    }

    private static Mesh CreateCylinderMesh(int sides)
    {
        sides = Mathf.Clamp(sides, 6, 32);
        int ringVerts = sides + 1;
        var vertices = new Vector3[ringVerts * 2];
        var normals = new Vector3[ringVerts * 2];
        var uvs = new Vector2[ringVerts * 2];

        for (int ring = 0; ring < 2; ring++)
        {
            for (int i = 0; i <= sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                int idx = ring * ringVerts + i;
                vertices[idx] = new Vector3(cs * 0.5f, sn * 0.5f, ring);
                normals[idx] = new Vector3(cs, sn, 0f);
                uvs[idx] = new Vector2(i / (float)sides, ring);
            }
        }

        var triangles = new int[sides * 6];
        int t = 0;
        for (int i = 0; i < sides; i++)
        {
            int b0 = i, b1 = i + 1;
            int t0 = ringVerts + i, t1 = ringVerts + i + 1;
            triangles[t++] = b0; triangles[t++] = b1; triangles[t++] = t0;
            triangles[t++] = b1; triangles[t++] = t1; triangles[t++] = t0;
        }

        Mesh mesh = new Mesh { name = "Mesh_UnitCylinder" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh { name = "Mesh_UnitQuad" };
        mesh.SetVertices(new[] {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f) });
        mesh.SetUVs(0, new[] {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f) });
        mesh.SetTriangles(new[] { 0, 3, 1, 0, 2, 3 }, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static GameObject SetupPrefab(Material coreMat, Material sheathMat, Material circleMat, Material shockMat)
    {
        var (cylMesh, quadMesh) = SetupMeshAssets();

        GameObject root = new GameObject("ZoltraakBeam");
        ZoltraakBeamController controller = root.AddComponent<ZoltraakBeamController>();

        // 1. BeamGroup
        GameObject beamGroup = new GameObject("BeamGroup");
        beamGroup.transform.SetParent(root.transform, false);

        GameObject core = new GameObject("Core");
        core.transform.SetParent(beamGroup.transform, false);
        MeshFilter coreFilter = core.AddComponent<MeshFilter>();
        coreFilter.sharedMesh = cylMesh;
        MeshRenderer coreRenderer = core.AddComponent<MeshRenderer>();
        coreRenderer.sharedMaterial = coreMat;

        GameObject sheath = new GameObject("Sheath");
        sheath.transform.SetParent(beamGroup.transform, false);
        MeshFilter sheathFilter = sheath.AddComponent<MeshFilter>();
        sheathFilter.sharedMesh = cylMesh;
        sheath.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
        MeshRenderer sheathRenderer = sheath.AddComponent<MeshRenderer>();
        sheathRenderer.sharedMaterial = sheathMat;

        // 2. Circles
        GameObject circleA = new GameObject("CircleA");
        circleA.transform.SetParent(root.transform, false);
        MeshFilter circleFilterA = circleA.AddComponent<MeshFilter>();
        circleFilterA.sharedMesh = quadMesh;
        MeshRenderer circleRendererA = circleA.AddComponent<MeshRenderer>();
        circleRendererA.sharedMaterial = circleMat;

        GameObject circleB = new GameObject("CircleB");
        circleB.transform.SetParent(root.transform, false);
        MeshFilter circleFilterB = circleB.AddComponent<MeshFilter>();
        circleFilterB.sharedMesh = quadMesh;
        MeshRenderer circleRendererB = circleB.AddComponent<MeshRenderer>();
        circleRendererB.sharedMaterial = circleMat;

        // 3. MuzzleFlash
        GameObject muzzle = new GameObject("MuzzleFlash");
        muzzle.transform.SetParent(root.transform, false);
        MeshFilter muzzleFilter = muzzle.AddComponent<MeshFilter>();
        muzzleFilter.sharedMesh = quadMesh;
        MeshRenderer muzzleRenderer = muzzle.AddComponent<MeshRenderer>();
        muzzleRenderer.sharedMaterial = shockMat;

        // 4. Muzzle Particles
        GameObject condensation = new GameObject("Condensation");
        condensation.transform.SetParent(root.transform, false);
        ParticleSystem psCondensation = SetupParticleSystem(condensation, new Color(1f, 0.4f, 0.8f, 0.7f), 0.5f, 0.08f);

        GameObject muzzleBurst = new GameObject("MuzzleBurst");
        muzzleBurst.transform.SetParent(root.transform, false);
        ParticleSystem psMuzzleBurst = SetupParticleSystem(muzzleBurst, new Color(1f, 0.8f, 1f, 1f), 0.3f, 0.12f);

        // 5. Impacts Root
        GameObject impacts = new GameObject("Impacts");
        impacts.transform.SetParent(root.transform, false);

        for (int i = 0; i < 4; i++)
        {
            GameObject slotObj = new GameObject($"Impact{i}");
            slotObj.transform.SetParent(impacts.transform, false);

            GameObject ring = new GameObject("Ring");
            ring.transform.SetParent(slotObj.transform, false);
            MeshFilter ringFilter = ring.AddComponent<MeshFilter>();
            ringFilter.sharedMesh = quadMesh;
            MeshRenderer ringRenderer = ring.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = shockMat;

            GameObject sparks = new GameObject("Sparks");
            sparks.transform.SetParent(slotObj.transform, false);
            SetupParticleSystem(sparks, new Color(1f, 0.5f, 0.9f, 1f), 0.4f, 0.06f);

            GameObject ion = new GameObject("Ion");
            ion.transform.SetParent(slotObj.transform, false);
            SetupParticleSystem(ion, new Color(0.8f, 0.4f, 1f, 0.8f), 0.6f, 0.08f);

            GameObject smoke = new GameObject("Smoke");
            smoke.transform.SetParent(slotObj.transform, false);
            SetupParticleSystem(smoke, new Color(0.3f, 0.2f, 0.35f, 0.5f), 0.8f, 0.25f);
        }

        // Wire fields on ZoltraakBeamController via SerializedObject
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("beamGroup").objectReferenceValue = beamGroup.transform;
        so.FindProperty("coreFilter").objectReferenceValue = coreFilter;
        so.FindProperty("sheathFilter").objectReferenceValue = sheathFilter;
        so.FindProperty("coreRenderer").objectReferenceValue = coreRenderer;
        so.FindProperty("sheathRenderer").objectReferenceValue = sheathRenderer;

        SerializedProperty circles = so.FindProperty("circleRenderers");
        circles.arraySize = 2;
        circles.GetArrayElementAtIndex(0).objectReferenceValue = circleRendererA;
        circles.GetArrayElementAtIndex(1).objectReferenceValue = circleRendererB;

        SerializedProperty circleTrans = so.FindProperty("circleTransforms");
        circleTrans.arraySize = 2;
        circleTrans.GetArrayElementAtIndex(0).objectReferenceValue = circleA.transform;
        circleTrans.GetArrayElementAtIndex(1).objectReferenceValue = circleB.transform;

        so.FindProperty("muzzleRenderer").objectReferenceValue = muzzleRenderer;
        so.FindProperty("muzzleTransform").objectReferenceValue = muzzle.transform;
        so.FindProperty("condensation").objectReferenceValue = psCondensation;
        so.FindProperty("muzzleBurst").objectReferenceValue = psMuzzleBurst;
        so.FindProperty("impactsRoot").objectReferenceValue = impacts.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return savedPrefab;
    }

    private static ParticleSystem SetupParticleSystem(GameObject go, Color color, float lifetime, float size)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 8f);
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = 0;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        return ps;
    }

    private static BeamVfxConfig SetupConfig(GameObject prefab, Material coreMat, Material sheathMat, Material circleMat, Material shockMat)
    {
        BeamVfxConfig cfg = AssetDatabase.LoadAssetAtPath<BeamVfxConfig>(ConfigPath);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<BeamVfxConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
        }

        cfg.BeamPrefab = prefab;
        cfg.BaseCoreMaterial = coreMat;
        cfg.BaseSheathMaterial = sheathMat;
        cfg.BaseCircleMaterial = circleMat;
        cfg.BaseShockMaterial = shockMat;

        cfg.ChargeDuration = 0.55f;
        cfg.BeamTravelSpeed = 65f;
        cfg.HoldDuration = 0.32f;
        cfg.DissipateDuration = 0.45f;

        cfg.CylinderSides = 12;
        cfg.BeamRadius = 0.22f;
        cfg.SheathRadiusMultiplier = 1.8f;
        cfg.CircleRadius = 1.15f;
        cfg.CircleCameraTilt = 0.55f;
        cfg.PierceOvershoot = 1.5f;
        cfg.DepthOffset = -0.35f;

        cfg.CoreColor = new Color(1f, 0.35f, 0.75f, 1f);
        cfg.HotColor = Color.white;
        cfg.SheathColor = new Color(0.72f, 0.30f, 1f, 1f);
        cfg.CircleColor = new Color(1f, 0.50f, 0.85f, 1f);
        cfg.ImpactRingColor = new Color(1f, 0.60f, 0.90f, 1f);
        cfg.ImpactFlashColor = Color.white;

        cfg.CoreIntensity = 6f;
        cfg.SheathIntensity = 2.5f;
        cfg.CircleIntensity = 4f;
        cfg.ImpactRingIntensity = 7f;
        cfg.ImpactFlashIntensity = 10f;

        cfg.GateMode = BeamGateMode.Auto;
        cfg.UsePerTargetSubflows = false;
        cfg.SkipDeadTargets = true;
        cfg.ExpandSingleTargetToLine = true;
        cfg.LineMaxAngle = 22f;
        cfg.LineMaxTargets = 4;

        cfg.MuzzleBurstCount = 16;
        cfg.SparkBurstCount = 28;
        cfg.IonLingerCount = 12;
        cfg.SmokePuffCount = 4;

        cfg.ShakeAmplitude = 0.22f;
        cfg.ShakeDuration = 0.32f;
        cfg.FlashStrength = 0.55f;

        cfg.SortingLayerName = "Default";
        cfg.SortingOrder = 50;

        EditorUtility.SetDirty(cfg);
        return cfg;
    }

    private static CardData SetupCard(BeamVfxConfig cfg)
    {
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(CardPath);
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<CardData>();
            AssetDatabase.CreateAsset(card, CardPath);
        }

        SerializedObject so = new SerializedObject(card);

        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Cnaracter/characteraira 1.png");
        if (icon == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/_Project/Art" });
            if (guids.Length > 0)
                icon = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        so.FindProperty("<Image>k__BackingField").objectReferenceValue = icon;
        so.FindProperty("<Title>k__BackingField").stringValue = "Zoltraak";
        so.FindProperty("<Description>k__BackingField").stringValue = "Deal 16 damage with a concentrated piercing beam that cuts through all enemies in its trajectory.";
        so.FindProperty("<Tier>k__BackingField").enumValueIndex = (int)Tier.Rare;
        so.FindProperty("<Mana>k__BackingField").intValue = 2;
        so.FindProperty("<Type>k__BackingField").enumValueIndex = (int)CardType.Attack;
        so.FindProperty("<TargetType>k__BackingField").enumValueIndex = (int)CardTargetType.Enemy;

        BeamAttackEffect effect = new BeamAttackEffect();
        SetPrivateField(effect, "damageAmount", 16);
        SetPrivateField(effect, "beamConfig", cfg);

        SerializedProperty effectProp = so.FindProperty("<ManualTargetEffect>k__BackingField");
        effectProp.managedReferenceValue = effect;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(card);
        return card;
    }

    [MenuItem("Tools/VFX/Toggle Zoltraak in Starting Deck (Asset Override)", priority = 110)]
    public static void ToggleZoltraakInStartingDeck()
    {
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(CardPath);
        if (card == null)
        {
            Debug.LogError("[Zoltraak] Card asset not found. Run 'Setup Zoltraak Attack Completely' first.");
            return;
        }

        HeroData hero = AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_Project/ScriptableObjects/Heroes/Default Hero.asset");
        if (hero == null || hero.Deck == null) return;

        if (hero.Deck.Contains(card))
        {
            hero.Deck.Remove(card);
            EditorUtility.SetDirty(hero);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=yellow><b>[Zoltraak] REMOVED from Default Hero starting deck. Launch deck is clean.</b></color>");
        }
        else
        {
            hero.Deck.Insert(0, card);
            EditorUtility.SetDirty(hero);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green><b>[Zoltraak] ADDED to Default Hero starting deck for testing.</b></color>");
        }
    }

    [MenuItem("Tools/VFX/Add Zoltraak to Active Hand (Play Mode)", priority = 120)]
    public static void AddZoltraakToActiveHand()
    {
        if (!Application.isPlaying || CardSystem.Instance == null)
        {
            Debug.LogWarning("[Zoltraak] Enter Play Mode in a combat scene to add Zoltraak directly to your hand.");
            return;
        }

        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(CardPath);
        if (card != null)
        {
            CardSystem.Instance.AddCardToHand(card);
            Debug.Log("<color=cyan><b>[Zoltraak] Injected Zoltraak into your active hand!</b></color>");
        }
    }

    private static void TryAddToRewardPool(CardData card)
    {
        string poolPath = "Assets/_Project/ScriptableObjects/Cards/Default Card Reward Pool.asset";
        ScriptableObject pool = AssetDatabase.LoadAssetAtPath<ScriptableObject>(poolPath);
        if (pool != null)
        {
            SerializedObject so = new SerializedObject(pool);
            SerializedProperty list = so.FindProperty("newCardRewards");
            if (list != null)
            {
                bool alreadyIn = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == card)
                    {
                        alreadyIn = true;
                        break;
                    }
                }
                if (!alreadyIn)
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = card;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(pool);
                    Debug.Log("[ZoltraakSetupAutomation] Added Zoltraak to Default Card Reward Pool.");
                }
            }
        }
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null) field.SetValue(instance, value);
    }
}
