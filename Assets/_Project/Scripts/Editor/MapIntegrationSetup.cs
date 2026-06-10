#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MapIntegrationSetup
{
    private const string ProjectRoot = "Assets/_Project";
    private const string ScenesFolder = ProjectRoot + "/Scenes";
    private const string PrefabsFolder = ProjectRoot + "/Prefabs/Map";
    private const string DataFolder = ProjectRoot + "/ScriptableObjects";
    private const string EncountersFolder = DataFolder + "/Encounters";
    private const string MapDataFolder = DataFolder + "/Map";
    private const string MapScenePath = ScenesFolder + "/Map.unity";
    private const string GameScenePath = ScenesFolder + "/Game.unity";

    [MenuItem("Tools/Map Integration/Rebuild Map Integration Assets")]
    public static void RebuildMapIntegrationAssets()
    {
        EnsureFolders();
        AssetDatabase.Refresh();

        EncounterData easyEncounter = CreateEncounter(
            EncountersFolder + "/Encounter - Slime Easy.asset",
            "Slime Easy",
            new[]
            {
                "Assets/_Project/ScriptableObjects/Enemies/Slime.asset",
                "Assets/_Project/ScriptableObjects/Enemies/Slime.asset"
            });

        EncounterData mediumEncounter = CreateEncounter(
            EncountersFolder + "/Encounter - Slime Medium.asset",
            "Slime Medium",
            new[]
            {
                "Assets/_Project/ScriptableObjects/Enemies/Red Slime.asset",
                "Assets/_Project/ScriptableObjects/Enemies/Slime.asset",
                "Assets/_Project/ScriptableObjects/Enemies/Slime.asset"
            });

        EncounterData bossEncounter = CreateEncounter(
            EncountersFolder + "/Encounter - Boss Test.asset",
            "Boss Test",
            new[]
            {
                "Assets/_Project/ScriptableObjects/Enemies/Red Slime.asset",
                "Assets/_Project/ScriptableObjects/Enemies/Red Slime.asset"
            });

        MapGeneratorConfig generatorConfig = CreateOrLoadAsset<MapGeneratorConfig>(MapDataFolder + "/Default Map Generator Config.asset");
        ConfigureGeneratorConfig(generatorConfig);
        MapEncounterPool encounterPool = CreateOrLoadAsset<MapEncounterPool>(MapDataFolder + "/Default Map Encounter Pool.asset");
        ConfigureEncounterPool(encounterPool, easyEncounter, mediumEncounter, bossEncounter);

        MapNodeView nodePrefab = CreateNodePrefab(PrefabsFolder + "/Map Node View.prefab");
        MapLineView linePrefab = CreateLinePrefab(PrefabsFolder + "/Map Line View.prefab");

        CreateMapScene(generatorConfig, encounterPool, nodePrefab, linePrefab);
        EnsureRunProgressSystemInGameScene();
        EnsureBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Map integration assets rebuilt successfully.");
    }

    private static void EnsureFolders()
    {
        EnsureFolder(ProjectRoot, "Scripts");
        EnsureFolder(ProjectRoot, "Prefabs");
        EnsureFolder(ProjectRoot + "/Prefabs", "Map");
        EnsureFolder(ProjectRoot, "ScriptableObjects");
        EnsureFolder(DataFolder, "Encounters");
        EnsureFolder(DataFolder, "Map");
        EnsureFolder(ProjectRoot, "Scenes");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string fullPath = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static EncounterData CreateEncounter(string path, string encounterName, string[] enemyPaths)
    {
        EncounterData encounter = CreateOrLoadAsset<EncounterData>(path);
        SerializedObject serializedObject = new(encounter);

        SerializedProperty nameProperty = serializedObject.FindProperty("<EncounterName>k__BackingField");
        if (nameProperty != null) nameProperty.stringValue = encounterName;

        SerializedProperty enemiesProperty = serializedObject.FindProperty("<Enemies>k__BackingField");
        if (enemiesProperty != null)
        {
            enemiesProperty.ClearArray();
            for (int i = 0; i < enemyPaths.Length; i++)
            {
                enemiesProperty.InsertArrayElementAtIndex(i);
                EnemyData enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(enemyPaths[i]);
                enemiesProperty.GetArrayElementAtIndex(i).objectReferenceValue = enemy;
            }
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(encounter);
        return encounter;
    }

    private static void ConfigureGeneratorConfig(MapGeneratorConfig config)
    {
        config.UseSyntheticStartNode = false;
        config.SyntheticStartDistance = 190f;
        config.NumberOfStartingNodes = new IntRange(2, 3);
        config.NumberOfPreBossNodes = new IntRange(2, 3);
        config.ExtraPaths = 2;
        config.RandomNodeTypes = new List<MapNodeType>
        {
            MapNodeType.Event,
            MapNodeType.Shop,
            MapNodeType.Treasure,
            MapNodeType.Enemy,
            MapNodeType.Rest
        };

        config.Layers = new List<MapLayerConfig>
        {
            new()
            {
                NodeType = MapNodeType.Enemy,
                RandomizeNodes = 0f,
                DistanceFromPreviousLayer = new FloatRange(0f, 0f),
                NodesApartDistance = 220f,
                RandomizePosition = 0.15f
            },
            new()
            {
                NodeType = MapNodeType.Enemy,
                RandomizeNodes = 0.25f,
                DistanceFromPreviousLayer = new FloatRange(140f, 170f),
                NodesApartDistance = 220f,
                RandomizePosition = 0.25f
            },
            new()
            {
                NodeType = MapNodeType.Enemy,
                RandomizeNodes = 0.35f,
                DistanceFromPreviousLayer = new FloatRange(140f, 170f),
                NodesApartDistance = 220f,
                RandomizePosition = 0.30f
            },
            new()
            {
                NodeType = MapNodeType.Enemy,
                RandomizeNodes = 0.40f,
                DistanceFromPreviousLayer = new FloatRange(140f, 170f),
                NodesApartDistance = 220f,
                RandomizePosition = 0.30f
            },
            new()
            {
                NodeType = MapNodeType.Rest,
                RandomizeNodes = 0f,
                DistanceFromPreviousLayer = new FloatRange(140f, 170f),
                NodesApartDistance = 220f,
                RandomizePosition = 0.10f
            },
            new()
            {
                NodeType = MapNodeType.Boss,
                RandomizeNodes = 0f,
                DistanceFromPreviousLayer = new FloatRange(170f, 210f),
                NodesApartDistance = 220f,
                RandomizePosition = 0f
            }
        };

        EditorUtility.SetDirty(config);
    }

    private static void ConfigureEncounterPool(MapEncounterPool pool, EncounterData easy, EncounterData medium, EncounterData boss)
    {
        SerializedObject serializedObject = new(pool);
        SetObjectList(serializedObject.FindProperty("enemyEncounters"), new Object[] { easy, medium });
        SetObjectList(serializedObject.FindProperty("eliteEncounters"), new Object[] { medium });
        SetObjectList(serializedObject.FindProperty("bossEncounters"), new Object[] { boss });
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pool);
    }

    private static void SetObjectList(SerializedProperty property, Object[] values)
    {
        if (property == null) return;

        property.ClearArray();
        for (int i = 0; i < values.Length; i++)
        {
            property.InsertArrayElementAtIndex(i);
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static MapNodeView CreateNodePrefab(string path)
    {
        GameObject root = new("Map Node View", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MapNodeView));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(110f, 64f);

        Image image = root.GetComponent<Image>();
        image.color = new Color(1f, 0.9f, 0.25f);

        Button button = root.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.disabledColor = new Color(0.3f, 0.3f, 0.35f);
        button.colors = colors;

        GameObject labelObject = new("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObject.GetComponent<Text>();
        label.text = "NODE";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.black;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 15;
        label.fontStyle = FontStyle.Bold;

        SerializedObject serializedObject = new(root.GetComponent<MapNodeView>());
        serializedObject.FindProperty("button").objectReferenceValue = button;
        serializedObject.FindProperty("iconImage").objectReferenceValue = image;
        serializedObject.FindProperty("labelText").objectReferenceValue = label;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<MapNodeView>();
    }

    private static MapLineView CreateLinePrefab(string path)
    {
        GameObject root = new("Map Line View", typeof(RectTransform), typeof(Image), typeof(MapLineView));
        RectTransform rectTransform = root.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(100f, 6f);

        Image image = root.GetComponent<Image>();
        image.color = new Color(0.32f, 0.32f, 0.38f, 0.75f);

        SerializedObject serializedObject = new(root.GetComponent<MapLineView>());
        serializedObject.FindProperty("lineImage").objectReferenceValue = image;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<MapLineView>();
    }

    private static void CreateMapScene(MapGeneratorConfig generatorConfig, MapEncounterPool encounterPool, MapNodeView nodePrefab, MapLineView linePrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Map";

        GameObject cameraObject = new("Main Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
        camera.orthographic = true;

        GameObject eventSystemObject = new("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetParent(null);

        GameObject runManagerObject = new("Run Manager", typeof(RunManager));
        runManagerObject.transform.SetParent(null);

        GameObject canvasObject = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panelObject = new("Map Panel", typeof(RectTransform), typeof(Image), typeof(MapView), typeof(MapSystem));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelObject.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.14f, 1f);

        GameObject titleObject = new("Title", typeof(RectTransform), typeof(Text));
        titleObject.transform.SetParent(panelObject.transform, false);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -45f);
        titleRect.sizeDelta = new Vector2(800f, 60f);
        Text titleText = titleObject.GetComponent<Text>();
        titleText.text = "Map";
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 34;
        titleText.fontStyle = FontStyle.Bold;

        GameObject lineParent = new("Lines", typeof(RectTransform));
        lineParent.transform.SetParent(panelObject.transform, false);
        Stretch(lineParent.GetComponent<RectTransform>());

        GameObject nodeParent = new("Nodes", typeof(RectTransform));
        nodeParent.transform.SetParent(panelObject.transform, false);
        Stretch(nodeParent.GetComponent<RectTransform>());

        MapView mapView = panelObject.GetComponent<MapView>();
        SerializedObject mapViewSerialized = new(mapView);
        mapViewSerialized.FindProperty("nodeViewPrefab").objectReferenceValue = nodePrefab;
        mapViewSerialized.FindProperty("lineViewPrefab").objectReferenceValue = linePrefab;
        mapViewSerialized.FindProperty("lineParent").objectReferenceValue = lineParent.transform;
        mapViewSerialized.FindProperty("nodeParent").objectReferenceValue = nodeParent.transform;
        mapViewSerialized.ApplyModifiedPropertiesWithoutUndo();

        MapSystem mapSystem = panelObject.GetComponent<MapSystem>();
        SerializedObject mapSystemSerialized = new(mapSystem);
        mapSystemSerialized.FindProperty("generatorConfig").objectReferenceValue = generatorConfig;
        mapSystemSerialized.FindProperty("encounterPool").objectReferenceValue = encounterPool;
        mapSystemSerialized.FindProperty("mapView").objectReferenceValue = mapView;
        mapSystemSerialized.FindProperty("combatSceneName").stringValue = "Game";
        mapSystemSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, MapScenePath);
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void EnsureRunProgressSystemInGameScene()
    {
        if (!File.Exists(GameScenePath)) return;

        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        if (Object.FindFirstObjectByType<RunProgressSystem>() == null)
        {
            GameObject runProgressObject = new("Run Progress System", typeof(RunProgressSystem));
            EditorSceneManager.MarkSceneDirty(scene);
        }

        EditorSceneManager.SaveScene(scene);
    }

    private static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new()
        {
            new EditorBuildSettingsScene(MapScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
