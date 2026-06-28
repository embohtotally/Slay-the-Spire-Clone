#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RunRewardSceneSetup
{
    private const string RunRewardsScenePath = "Assets/_Project/Scenes/RunRewards.unity";
    private const string CardRewardScenePath = "Assets/_Project/Scenes/CardReward.unity";
    private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

    [MenuItem("Tools/Run Rewards/Rebuild Reward Scene")]
    public static void Build()
    {
        CreateRunRewardsScene();
        AddSkipButtonToCardRewardScene();
        ConfigureGameSceneRunProgress();
        EnsureSceneInBuildSettings(RunRewardsScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Run reward scene setup finished.");
    }

    private static void CreateRunRewardsScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject eventSystemObject = new("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetAsLastSibling();

        GameObject canvasObject = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject panel = CreateUIObject("Reward Panel", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        RunRewardController controller = panel.AddComponent<RunRewardController>();

        TextMeshProUGUI titleText = CreateText("Title", panel.transform, "Rewards", 54, TextAlignmentOptions.Center);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -90f);
        titleRect.sizeDelta = new Vector2(800f, 90f);

        GameObject content = CreateUIObject("Reward Content", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f));
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(760f, 420f);
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < 2; i++)
        {
            CreateRewardOption(content.transform, i == 0 ? "Gold Reward" : "Card Reward");
        }

        Button skipButton = CreateButton(panel.transform, "Skip Button", "Skip / Continue", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(320f, 70f));
        UnityEventTools.AddPersistentListener(skipButton.onClick, controller.SkipRemainingRewards);

        SerializedObject serializedController = new(controller);
        serializedController.FindProperty("rewardRoot").objectReferenceValue = panel;
        serializedController.FindProperty("rewardContainer").objectReferenceValue = content.transform;
        serializedController.FindProperty("autoFindOptionViewsInChildren").boolValue = true;
        serializedController.FindProperty("titleText").objectReferenceValue = titleText;
        serializedController.FindProperty("cardRewardSceneName").stringValue = "CardReward";
        serializedController.FindProperty("mapSceneName").stringValue = "Map";
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, RunRewardsScenePath);
    }

    private static void AddSkipButtonToCardRewardScene()
    {
        var scene = EditorSceneManager.OpenScene(CardRewardScenePath, OpenSceneMode.Single);
        CardRewardController controller = Object.FindFirstObjectByType<CardRewardController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogWarning("CardReward scene has no CardRewardController; skip button was not added.");
            return;
        }

        if (GameObject.Find("Skip Card Reward Button") == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                GameObject canvasObject = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            Button skipButton = CreateButton(canvas.transform, "Skip Card Reward Button", "Skip Card", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-180f, 90f), new Vector2(260f, 64f));
            UnityEventTools.AddPersistentListener(skipButton.onClick, controller.SkipReward);
        }

        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureGameSceneRunProgress()
    {
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        RunProgressSystem runProgress = Object.FindFirstObjectByType<RunProgressSystem>(FindObjectsInactive.Include);
        if (runProgress == null)
        {
            Debug.LogWarning("Game scene has no RunProgressSystem; reward scene was not wired.");
            return;
        }

        SerializedObject serializedRunProgress = new(runProgress);
        serializedRunProgress.FindProperty("openRewardSceneAfterCombatWin").boolValue = true;
        serializedRunProgress.FindProperty("rewardSceneName").stringValue = "RunRewards";
        serializedRunProgress.FindProperty("loadRewardSceneAdditive").boolValue = true;
        serializedRunProgress.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene);
    }

    private static RunRewardOptionView CreateRewardOption(Transform parent, string name)
    {
        GameObject optionObject = CreateUIObject(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        Image background = optionObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.1f, 0.08f, 0.94f);
        Button button = optionObject.AddComponent<Button>();
        LayoutElement layoutElement = optionObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 150f;
        layoutElement.minHeight = 120f;

        TextMeshProUGUI titleText = CreateText("Title", optionObject.transform, name, 30, TextAlignmentOptions.Left);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(28f, -62f);
        titleRect.offsetMax = new Vector2(-28f, -18f);

        TextMeshProUGUI descriptionText = CreateText("Description", optionObject.transform, "Reward description", 22, TextAlignmentOptions.Left);
        RectTransform descriptionRect = descriptionText.rectTransform;
        descriptionRect.anchorMin = new Vector2(0f, 0f);
        descriptionRect.anchorMax = new Vector2(1f, 1f);
        descriptionRect.offsetMin = new Vector2(28f, 22f);
        descriptionRect.offsetMax = new Vector2(-180f, -62f);

        TextMeshProUGUI actionText = CreateText("Action", optionObject.transform, "Claim", 24, TextAlignmentOptions.Right);
        RectTransform actionRect = actionText.rectTransform;
        actionRect.anchorMin = new Vector2(1f, 0.5f);
        actionRect.anchorMax = new Vector2(1f, 0.5f);
        actionRect.pivot = new Vector2(1f, 0.5f);
        actionRect.anchoredPosition = new Vector2(-28f, 0f);
        actionRect.sizeDelta = new Vector2(140f, 48f);

        RunRewardOptionView view = optionObject.AddComponent<RunRewardOptionView>();
        SerializedObject serializedView = new(view);
        serializedView.FindProperty("button").objectReferenceValue = button;
        serializedView.FindProperty("titleText").objectReferenceValue = titleText;
        serializedView.FindProperty("descriptionText").objectReferenceValue = descriptionText;
        serializedView.FindProperty("actionText").objectReferenceValue = actionText;
        serializedView.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject buttonObject = CreateUIObject(name, parent, anchorMin, anchorMax, pivot, anchoredPosition);
        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = sizeDelta;
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.72f, 0.54f, 0.24f, 1f);
        Button button = buttonObject.AddComponent<Button>();

        TextMeshProUGUI text = CreateText("Label", buttonObject.transform, label, 26, TextAlignmentOptions.Center);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        TextMeshProUGUI tmpText = textObject.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.alignment = alignment;
        tmpText.color = Color.white;
        return tmpText;
    }

    private static GameObject CreateUIObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        return gameObject;
    }

    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == scenePath) return;
        }

        scenes.Insert(Mathf.Max(0, scenes.Count - 1), new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
