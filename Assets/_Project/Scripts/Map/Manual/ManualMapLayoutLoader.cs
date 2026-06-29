using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;

public class ManualMapLayoutLoader : MonoBehaviour
{
    [Header("Layouts")]
    [SerializeField] private bool loadFromPool = true;
    [SerializeField, ShowIf("loadFromPool")] private ManualMapLayoutRegistry layoutRegistry;
    [SerializeField, ShowIf("loadFromPool")] private Transform mapRoot;

    [Header("Selection")]
    [Tooltip("If empty, uses the saved selection. If no saved selection exists, a random layout is chosen.")]
    [SerializeField, ShowIf("loadFromPool")] private string fallbackLayoutId;
    [SerializeField, ShowIf("loadFromPool")] private bool randomizeWhenNoSelection = true;
    [Tooltip("Useful for testing one layout in the editor. This overrides saved selection on Start.")]
    [SerializeField, ShowIf("loadFromPool")] private bool forceFallbackLayoutOnStart;

    [Header("Spawn")]
    [SerializeField, ShowIf("loadFromPool")] private bool clearRootBeforeSpawn = true;
    [SerializeField, ShowIf("loadFromPool")] private bool stretchRectTransformToParent = true;

    [Header("Optional Scene Flow")]
    [SerializeField, Scene] private string mapSceneName;

    [Header("Level Progression")]
    [Tooltip("If a LevelProgressionManager current level exists, use its Manual Map Layout Id instead of random/fallback selection.")]
    [SerializeField] private bool preferCurrentLevelLayout = true;
    [Tooltip("When the spawned map reaches its final node, unlock the next level and return to the Levels scene.")]
    [SerializeField] private bool completeCurrentLevelWhenMapCleared = true;
    [SerializeField, Scene] private string levelSelectSceneName = "Levels";
    [SerializeField] private bool returnToLevelSelectOnMapComplete = true;

    [Header("Viewport / Zoom")]
    [Tooltip("Use scripted UI pan/zoom. Cinemachine will not affect this map because the Map scene uses a Screen Space Overlay Canvas.")]
    [SerializeField] private bool enableViewportControls = true;
    [Tooltip("The visible area. Empty = Map Root if it is a RectTransform.")]
    [SerializeField, ShowIf("enableViewportControls")] private RectTransform viewportRect;
    [Tooltip("Default zoom used when focusing the currently selectable path nodes.")]
    [Min(0.1f)][SerializeField, ShowIf("enableViewportControls")] private float focusedZoom = 1.2f;
    [Tooltip("Lowest allowed zoom. This is usually the zoom-out / overview limit.")]
    [Min(0.1f)][SerializeField, ShowIf("enableViewportControls")] private float minZoom = 0.45f;
    [Tooltip("Highest allowed zoom for close inspection.")]
    [Min(0.1f)][SerializeField, ShowIf("enableViewportControls")] private float maxZoom = 1.6f;
    [Tooltip("Extra space around focused nodes when auto-fitting a row/path.")]
    [Min(0f)][SerializeField, ShowIf("enableViewportControls")] private float focusPadding = 180f;
    [Tooltip("Mouse wheel zoom amount per scroll tick.")]
    [Min(0.01f)][SerializeField, ShowIf("enableViewportControls")] private float wheelZoomSpeed = 0.12f;
    [Tooltip("Right mouse or middle mouse drag speed.")]
    [Min(0.1f)][SerializeField, ShowIf("enableViewportControls")] private float dragPanSpeed = 1f;
    [SerializeField, ShowIf("enableViewportControls")] private float zoomSmoothSpeed = 12f;
    [SerializeField, ShowIf("enableViewportControls")] private float panSmoothSpeed = 12f;
    [SerializeField, ShowIf("enableViewportControls")] private KeyCode focusPathKey = KeyCode.F;
    [SerializeField, ShowIf("enableViewportControls")] private KeyCode overviewKey = KeyCode.Tab;
    [Tooltip("If enabled, map starts zoomed into the currently selectable nodes, then player may scroll/drag/overview.")]
    [SerializeField, ShowIf("enableViewportControls")] private bool focusPathOnLoad = true;

    private ManualMapController spawnedMap;
    private RectTransform contentRect;
    private float targetZoom = 1f;
    private Vector2 targetAnchoredPosition;
    private Vector2 lastPointerPosition;
    private bool isDragging;

    public ManualMapController SpawnedMap => spawnedMap;

    private void Awake()
    {
        EnsureSelectionManagerExists();
        if (mapRoot == null) mapRoot = transform;
        if (viewportRect == null) viewportRect = mapRoot as RectTransform;
    }

    private void Start()
    {
        if (loadFromPool) LoadSelectedOrFallbackLayout();
    }

    private void Update()
    {
        if (!enableViewportControls || contentRect == null) return;

        HandleKeyboardShortcuts();
        HandleMouseZoom();
        HandleMouseDrag();
        SmoothMoveToTarget();
    }

    public void LoadSelectedOrFallbackLayout()
    {
        ManualMapLayoutEntry layout = ResolveLayoutToLoad();
        if (layout == null)
        {
            Gameseed26.Logger.LogWarning("ManualMapLayoutLoader could not load a layout. Assign a registry with at least one valid ManualMapController prefab.");
            return;
        }

        SpawnLayout(layout);
    }

    public void SelectLayoutAndLoad(string layoutId)
    {
        if (layoutRegistry == null)
        {
            Gameseed26.Logger.LogWarning("Cannot select manual map layout because registry is missing.");
            return;
        }

        ManualMapLayoutEntry layout = layoutRegistry.GetById(layoutId);
        if (layout == null)
        {
            Gameseed26.Logger.LogWarning($"Manual map layout '{layoutId}' was not found in the registry.");
            return;
        }

        ManualMapRunSelection.Instance.SelectLayout(layout.SafeId);
        SpawnLayout(layout);
    }

    public void SelectRandomLayoutAndLoad()
    {
        ManualMapLayoutEntry layout = ManualMapRunSelection.Instance.SelectRandomLayout(layoutRegistry);
        if (layout == null) return;

        SpawnLayout(layout);
    }

    public void ClearSelectionAndReloadRandom()
    {
        ManualMapRunSelection.Instance.ClearSelection();
        SelectRandomLayoutAndLoad();
    }

    public void LoadMapSceneWithLayout(string layoutId)
    {
        if (!string.IsNullOrWhiteSpace(layoutId))
        {
            ManualMapRunSelection.Instance.SelectLayout(layoutId);
        }

        if (!string.IsNullOrWhiteSpace(mapSceneName))
        {
            SceneLoader.LoadScene(mapSceneName);
        }
    }

    [ShowIf("enableViewportControls")]
    [Button("Focus Current Path", EButtonEnableMode.Playmode)]
    public void FocusCurrentPath()
    {
        if (spawnedMap == null || contentRect == null) return;

        List<ManualMapNode> focusNodes = GetFocusNodes();
        if (focusNodes.Count == 0)
        {
            FocusWholeMap();
            return;
        }

        FocusNodes(focusNodes, focusedZoom);
    }

    [ShowIf("enableViewportControls")]
    [Button("Zoom Out To Whole Map", EButtonEnableMode.Playmode)]
    public void FocusWholeMap()
    {
        if (spawnedMap == null || contentRect == null) return;

        List<ManualMapNode> allNodes = spawnedMap.Nodes
            .Where(node => node != null)
            .ToList();
        if (allNodes.Count == 0) return;

        FocusNodes(allNodes, maxZoom);
    }

    private ManualMapLayoutEntry ResolveLayoutToLoad()
    {
        if (layoutRegistry == null) return null;

        LevelDefinition currentLevel = GetCurrentLevel();
        if (preferCurrentLevelLayout && currentLevel != null && currentLevel.HasManualMapLayout)
        {
            ManualMapLayoutEntry levelLayout = layoutRegistry.GetById(currentLevel.ManualMapLayoutId);
            if (levelLayout != null)
            {
                ManualMapRunSelection.Instance.SelectLayout(levelLayout.SafeId);
                return levelLayout;
            }

            Gameseed26.Logger.LogWarning($"Current level '{currentLevel.DisplayName}' requested missing manual map layout '{currentLevel.ManualMapLayoutId}'. Falling back to saved/fallback layout.");
        }

        if (forceFallbackLayoutOnStart && !string.IsNullOrWhiteSpace(fallbackLayoutId))
        {
            ManualMapLayoutEntry forcedLayout = layoutRegistry.GetById(fallbackLayoutId);
            if (forcedLayout != null)
            {
                ManualMapRunSelection.Instance.SelectLayout(forcedLayout.SafeId);
                return forcedLayout;
            }

            Gameseed26.Logger.LogWarning($"Forced manual map layout '{fallbackLayoutId}' was not found. Falling back to saved/random layout.");
        }

        if (ManualMapRunSelection.Instance.HasSelectedLayout)
        {
            ManualMapLayoutEntry savedLayout = layoutRegistry.GetById(ManualMapRunSelection.Instance.SelectedLayoutId);
            if (savedLayout != null)
            {
                return savedLayout;
            }

            Gameseed26.Logger.LogWarning($"Saved manual map layout '{ManualMapRunSelection.Instance.SelectedLayoutId}' was not found. Choosing a fallback layout.");
            ManualMapRunSelection.Instance.ClearSelection();
        }

        if (!string.IsNullOrWhiteSpace(fallbackLayoutId))
        {
            ManualMapLayoutEntry fallbackLayout = layoutRegistry.GetById(fallbackLayoutId);
            if (fallbackLayout != null)
            {
                ManualMapRunSelection.Instance.SelectLayout(fallbackLayout.SafeId);
                return fallbackLayout;
            }
        }

        if (randomizeWhenNoSelection)
        {
            return ManualMapRunSelection.Instance.SelectRandomLayout(layoutRegistry);
        }

        ManualMapLayoutEntry firstLayout = layoutRegistry.GetFirstAvailable();
        if (firstLayout != null)
        {
            ManualMapRunSelection.Instance.SelectLayout(firstLayout.SafeId);
        }

        return firstLayout;
    }

    private void SpawnLayout(ManualMapLayoutEntry layout)
    {
        if (layout == null || layout.MapPrefab == null) return;

        if (clearRootBeforeSpawn)
        {
            ClearRoot();
        }

        spawnedMap = Instantiate(layout.MapPrefab, mapRoot);
        LevelDefinition currentLevel = GetCurrentLevel();
        string runtimeMapId = currentLevel != null ? currentLevel.GetProgressMapId() : layout.SafeId;
        spawnedMap.SetRuntimeMapId(runtimeMapId);
        spawnedMap.ConfigureLevelCompletion(
            completeCurrentLevelWhenMapCleared && currentLevel != null,
            levelSelectSceneName,
            returnToLevelSelectOnMapComplete);
        spawnedMap.name = layout.SafeDisplayName;

        if (stretchRectTransformToParent && spawnedMap.transform is RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }
        else
        {
            spawnedMap.transform.localPosition = Vector3.zero;
            spawnedMap.transform.localRotation = Quaternion.identity;
            spawnedMap.transform.localScale = Vector3.one;
        }

        contentRect = spawnedMap.transform as RectTransform;
        targetZoom = Mathf.Clamp(focusedZoom, minZoom, maxZoom);
        targetAnchoredPosition = contentRect != null ? contentRect.anchoredPosition : Vector2.zero;

        if (enableViewportControls && focusPathOnLoad)
        {
            StartCoroutine(FocusPathAfterSpawn());
        }
    }

    private IEnumerator FocusPathAfterSpawn()
    {
        // Wait one frame so ManualMapController.Start has restored saved node states after combat/map reload.
        yield return null;
        FocusCurrentPathImmediate();
    }

    private void FocusCurrentPathImmediate()
    {
        FocusCurrentPath();
        ApplyViewportTargetImmediately();
    }

    private List<ManualMapNode> GetFocusNodes()
    {
        if (spawnedMap == null) return new List<ManualMapNode>();

        List<ManualMapNode> availableNodes = spawnedMap.Nodes
            .Where(node => node != null && node.CanSelect)
            .OrderBy(node => node.LayerIndex)
            .ThenBy(node => node.ColumnIndex)
            .ToList();
        if (availableNodes.Count > 0) return availableNodes;

        ManualMapNode latestCompletedNode = spawnedMap.Nodes
            .Where(node => node != null && node.IsCompleted)
            .OrderByDescending(node => node.LayerIndex)
            .ThenBy(node => node.ColumnIndex)
            .FirstOrDefault();
        if (latestCompletedNode != null) return new List<ManualMapNode> { latestCompletedNode };

        return spawnedMap.Nodes
            .Where(node => node != null)
            .OrderBy(node => node.LayerIndex)
            .ThenBy(node => node.ColumnIndex)
            .Take(1)
            .ToList();
    }

    private void FocusNodes(IReadOnlyList<ManualMapNode> focusNodes, float preferredZoom)
    {
        if (focusNodes == null || focusNodes.Count == 0 || viewportRect == null || contentRect == null) return;

        Bounds bounds = GetNodeBounds(focusNodes);
        Vector2 viewportSize = viewportRect.rect.size;
        float fitZoom = CalculateFitZoom(bounds, viewportSize);
        targetZoom = Mathf.Clamp(Mathf.Min(preferredZoom, fitZoom), minZoom, maxZoom);
        targetAnchoredPosition = -((Vector2)bounds.center * targetZoom);
    }

    private Bounds GetNodeBounds(IReadOnlyList<ManualMapNode> targetNodes)
    {
        Vector2 firstPosition = targetNodes[0].GetDesignerPosition();
        Bounds bounds = new(firstPosition, Vector3.zero);

        foreach (ManualMapNode node in targetNodes)
        {
            if (node == null) continue;
            bounds.Encapsulate(node.GetDesignerPosition());
        }

        return bounds;
    }

    private float CalculateFitZoom(Bounds bounds, Vector2 viewportSize)
    {
        float contentWidth = Mathf.Max(1f, bounds.size.x + focusPadding * 2f);
        float contentHeight = Mathf.Max(1f, bounds.size.y + focusPadding * 2f);
        float widthZoom = viewportSize.x <= 0f ? maxZoom : viewportSize.x / contentWidth;
        float heightZoom = viewportSize.y <= 0f ? maxZoom : viewportSize.y / contentHeight;
        return Mathf.Min(widthZoom, heightZoom, maxZoom);
    }

    private void HandleKeyboardShortcuts()
    {
        if (Input.GetKeyDown(focusPathKey))
        {
            FocusCurrentPath();
        }

        if (Input.GetKeyDown(overviewKey))
        {
            FocusWholeMap();
        }
    }

    private void HandleMouseZoom()
    {
        if (viewportRect == null || !IsPointerInsideViewport()) return;

        float scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scrollDelta, 0f)) return;

        float oldZoom = targetZoom;
        float newZoom = Mathf.Clamp(targetZoom + scrollDelta * wheelZoomSpeed, minZoom, maxZoom);
        if (Mathf.Approximately(oldZoom, newZoom)) return;

        Vector2 localPointer = GetPointerLocalToViewport();
        Vector2 contentPointBeforeZoom = (localPointer - targetAnchoredPosition) / oldZoom;
        targetZoom = newZoom;
        targetAnchoredPosition = localPointer - contentPointBeforeZoom * targetZoom;
    }

    private void HandleMouseDrag()
    {
        bool dragButtonDown = Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        if (dragButtonDown && IsPointerInsideViewport())
        {
            isDragging = true;
            lastPointerPosition = Input.mousePosition;
        }

        if (!Input.GetMouseButton(1) && !Input.GetMouseButton(2))
        {
            isDragging = false;
        }

        if (!isDragging) return;

        Vector2 currentPointerPosition = Input.mousePosition;
        Vector2 pointerDelta = currentPointerPosition - lastPointerPosition;
        targetAnchoredPosition += pointerDelta * dragPanSpeed;
        lastPointerPosition = currentPointerPosition;
    }

    private bool IsPointerInsideViewport()
    {
        if (viewportRect == null) return true;
        return RectTransformUtility.RectangleContainsScreenPoint(viewportRect, Input.mousePosition, null);
    }

    private Vector2 GetPointerLocalToViewport()
    {
        if (viewportRect == null) return Input.mousePosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, Input.mousePosition, null, out Vector2 localPoint);
        return localPoint;
    }

    private void SmoothMoveToTarget()
    {
        if (contentRect == null) return;

        float zoomLerp = 1f - Mathf.Exp(-Mathf.Max(0.01f, zoomSmoothSpeed) * Time.deltaTime);
        float panLerp = 1f - Mathf.Exp(-Mathf.Max(0.01f, panSmoothSpeed) * Time.deltaTime);
        float currentZoom = contentRect.localScale.x;
        float newZoom = Mathf.Lerp(currentZoom, targetZoom, zoomLerp);
        contentRect.localScale = new Vector3(newZoom, newZoom, 1f);
        contentRect.anchoredPosition = Vector2.Lerp(contentRect.anchoredPosition, targetAnchoredPosition, panLerp);
    }

    private void ApplyViewportTargetImmediately()
    {
        if (contentRect == null) return;

        contentRect.localScale = new Vector3(targetZoom, targetZoom, 1f);
        contentRect.anchoredPosition = targetAnchoredPosition;
    }

    private void ClearRoot()
    {
        if (mapRoot == null) return;

        for (int i = mapRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = mapRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void EnsureSelectionManagerExists()
    {
        if (ManualMapRunSelection.Instance != null) return;

        GameObject selectionObject = new("Manual Map Run Selection");
        selectionObject.AddComponent<ManualMapRunSelection>();
    }

    private static LevelDefinition GetCurrentLevel()
    {
        return LevelProgressionManager.Instance != null ? LevelProgressionManager.Instance.CurrentLevel : null;
    }
}