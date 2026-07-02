using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;

public enum ManualMapViewportBoundsMode
{
    None,
    ContentRect,
    BoundsRectOverride,
    ManualSize
}

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
    [Tooltip("If off, player mouse wheel/right-drag/keyboard map controls are ignored, but scripts may still call FocusNode/FocusWholeMap. Turn this off in the Levels scene so only the Level Select Controller moves the map during selection preview.")]
    [SerializeField, ShowIf("enableViewportControls")] private bool enablePlayerViewportInput = true;
    [Tooltip("The visible area. Empty = Map Root if it is a RectTransform.")]
    [SerializeField, ShowIf("enableViewportControls")] private RectTransform viewportRect;
    [Tooltip("Default viewport-local offset used by FocusNode. Negative X places the selected node left of center, useful when a detail panel occupies the right side.")]
    [SerializeField, ShowIf("enableViewportControls")] private Vector2 defaultFocusOffset;
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

    [Header("Viewport Bounds")]
    [Tooltip("Clamp pan/zoom so the viewport does not reveal empty space outside the background/content art. Use ContentRect for most cases, BoundsRectOverride for a dedicated background RectTransform, or ManualSize for explicit art dimensions.")]
    [SerializeField, ShowIf("enableViewportControls")] private ManualMapViewportBoundsMode boundsMode = ManualMapViewportBoundsMode.ContentRect;
    [Tooltip("Optional bounds source. Use this when the visual background image is a different RectTransform than the node/content root.")]
    [SerializeField, ShowIf("enableViewportControls")] private RectTransform boundsRectOverride;
    [Tooltip("Used only when Bounds Mode = Manual Size. Should match the safe background/content size in local UI units.")]
    [SerializeField, ShowIf("enableViewportControls")] private Vector2 manualBoundsSize = new(1920f, 1080f);
    [Tooltip("Positive values keep the camera further away from the edges. Negative values intentionally allow a little overscroll beyond the art.")]
    [SerializeField, ShowIf("enableViewportControls")] private Vector2 boundsInset;
    [Tooltip("If on, zoom-out cannot go below the scale needed to cover the viewport, preventing 16:9 backgrounds from showing outside edges.")]
    [SerializeField, ShowIf("enableViewportControls")] private bool preventZoomOutPastBounds = true;

    private ManualMapController spawnedMap;
    private RectTransform contentRect;
    private float targetZoom = 1f;
    private Vector2 targetAnchoredPosition;
    private float homeZoom = 1f;
    private Vector2 homeAnchoredPosition;
    private bool hasHomeView;
    private Vector2 lastPointerPosition;
    private bool isDragging;

    public ManualMapController SpawnedMap => spawnedMap;
    public bool PlayerViewportInputEnabled => enablePlayerViewportInput;

    private void Awake()
    {
        EnsureSelectionManagerExists();
        if (mapRoot == null) mapRoot = transform;
        if (viewportRect == null) viewportRect = mapRoot as RectTransform;
    }

    private void Start()
    {
        if (loadFromPool)
        {
            LoadSelectedOrFallbackLayout();
        }
        else
        {
            InitializeStaticViewportContent();
        }
    }

    private void Update()
    {
        if (!enableViewportControls || contentRect == null) return;

        if (enablePlayerViewportInput)
        {
            HandleKeyboardShortcuts();
            HandleMouseZoom();
            HandleMouseDrag();
        }
        else
        {
            isDragging = false;
        }

        SmoothMoveToTarget();
    }

    public void SetPlayerViewportInputEnabled(bool enabled)
    {
        enablePlayerViewportInput = enabled;
        if (!enabled)
        {
            isDragging = false;
        }
    }

    public void SetFocusPathOnLoadEnabled(bool enabled)
    {
        focusPathOnLoad = enabled;
    }

    public void CaptureHomeView()
    {
        EnsureViewportContentReady();
        if (contentRect == null) return;

        targetZoom = ClampZoom(contentRect.localScale.x);
        targetAnchoredPosition = contentRect.anchoredPosition;
        ConstrainTargetAnchoredPosition();
        StoreCurrentTargetAsHome();
    }

    public void RestoreHomeView()
    {
        EnsureViewportContentReady();
        if (contentRect == null) return;

        if (!hasHomeView)
        {
            CaptureHomeView();
        }

        targetZoom = homeZoom;
        targetAnchoredPosition = homeAnchoredPosition;
        ConstrainTargetAnchoredPosition();
    }

    public void RestoreHomeViewImmediate()
    {
        RestoreHomeView();
        ApplyViewportTargetImmediately();
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

    public void RefreshStaticViewportContent()
    {
        InitializeStaticViewportContent();
    }

    public void FocusNode(ManualMapNode node)
    {
        if (node == null) return;

        EnsureViewportContentReady();
        FocusNodes(new List<ManualMapNode> { node }, focusedZoom, defaultFocusOffset);
    }

    public void FocusNode(ManualMapNode node, Vector2 viewportOffset)
    {
        if (node == null) return;

        EnsureViewportContentReady();
        FocusNodes(new List<ManualMapNode> { node }, focusedZoom, viewportOffset);
    }

    public void FocusNodeImmediate(ManualMapNode node)
    {
        FocusNode(node);
        ApplyViewportTargetImmediately();
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
        EnsureViewportContentReady();
        if (contentRect == null) return;

        List<ManualMapNode> focusNodes = GetFocusNodes();
        if (focusNodes.Count == 0)
        {
            FocusWholeMap();
            return;
        }

        FocusNodes(focusNodes, focusedZoom, Vector2.zero);
    }

    [ShowIf("enableViewportControls")]
    [Button("Zoom Out To Whole Map", EButtonEnableMode.Playmode)]
    public void FocusWholeMap()
    {
        EnsureViewportContentReady();
        if (contentRect == null) return;

        List<ManualMapNode> allNodes = GetAllViewportNodes()
            .Where(node => node != null)
            .ToList();
        if (allNodes.Count == 0) return;

        FocusNodes(allNodes, maxZoom, Vector2.zero);
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
        targetZoom = ClampZoom(focusedZoom);
        targetAnchoredPosition = contentRect != null ? contentRect.anchoredPosition : Vector2.zero;
        ConstrainTargetAnchoredPosition();
        StoreCurrentTargetAsHome();

        if (enableViewportControls && focusPathOnLoad)
        {
            StartCoroutine(FocusPathAfterSpawn());
        }
    }

    private void InitializeStaticViewportContent()
    {
        contentRect = ResolveStaticContentRect();
        if (contentRect == null) return;

        targetZoom = ClampZoom(contentRect.localScale.x);
        targetAnchoredPosition = contentRect.anchoredPosition;
        ConstrainTargetAnchoredPosition();
        StoreCurrentTargetAsHome();

        if (enableViewportControls && focusPathOnLoad)
        {
            StartCoroutine(FocusPathAfterSpawn());
        }
    }

    private void EnsureViewportContentReady()
    {
        if (contentRect != null) return;

        if (spawnedMap != null)
        {
            contentRect = spawnedMap.transform as RectTransform;
            return;
        }

        InitializeStaticViewportContent();
    }

    private RectTransform ResolveStaticContentRect()
    {
        RectTransform viewportAsContent = viewportRect != null && viewportRect.GetComponentInChildren<ManualMapNode>(true) != null
            ? viewportRect
            : null;
        if (viewportAsContent != null) return viewportAsContent;

        RectTransform mapRootRect = mapRoot as RectTransform;
        if (mapRootRect != null && mapRootRect.GetComponentInChildren<ManualMapNode>(true) != null)
        {
            return mapRootRect;
        }

        if (mapRoot != null)
        {
            ManualMapNode childNode = mapRoot.GetComponentInChildren<ManualMapNode>(true);
            if (childNode != null)
            {
                return childNode.transform.parent as RectTransform;
            }
        }

        return mapRootRect;
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
        List<ManualMapNode> nodesToSearch = GetAllViewportNodes();

        List<ManualMapNode> availableNodes = nodesToSearch
            .Where(node => node != null && node.CanSelect)
            .OrderBy(node => node.LayerIndex)
            .ThenBy(node => node.ColumnIndex)
            .ToList();
        if (availableNodes.Count > 0) return availableNodes;

        ManualMapNode latestCompletedNode = nodesToSearch
            .Where(node => node != null && node.IsCompleted)
            .OrderByDescending(node => node.LayerIndex)
            .ThenBy(node => node.ColumnIndex)
            .FirstOrDefault();
        if (latestCompletedNode != null) return new List<ManualMapNode> { latestCompletedNode };

        return nodesToSearch
            .Where(node => node != null)
            .OrderBy(node => node.LayerIndex)
            .ThenBy(node => node.ColumnIndex)
            .Take(1)
            .ToList();
    }

    private List<ManualMapNode> GetAllViewportNodes()
    {
        if (spawnedMap != null)
        {
            return spawnedMap.Nodes
                .Where(node => node != null)
                .ToList();
        }

        if (contentRect != null)
        {
            return contentRect.GetComponentsInChildren<ManualMapNode>(true)
                .Where(node => node != null)
                .Distinct()
                .ToList();
        }

        if (mapRoot != null)
        {
            return mapRoot.GetComponentsInChildren<ManualMapNode>(true)
                .Where(node => node != null)
                .Distinct()
                .ToList();
        }

        return new List<ManualMapNode>();
    }

    private void FocusNodes(IReadOnlyList<ManualMapNode> focusNodes, float preferredZoom, Vector2 viewportOffset)
    {
        if (focusNodes == null || focusNodes.Count == 0 || viewportRect == null || contentRect == null) return;

        Bounds bounds = GetNodeBounds(focusNodes);
        Vector2 viewportSize = viewportRect.rect.size;
        float fitZoom = CalculateFitZoom(bounds, viewportSize);
        targetZoom = ClampZoom(Mathf.Min(preferredZoom, fitZoom));
        targetAnchoredPosition = viewportOffset - (Vector2)bounds.center * targetZoom;
        ConstrainTargetAnchoredPosition();
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
        float newZoom = ClampZoom(targetZoom + scrollDelta * wheelZoomSpeed);
        if (Mathf.Approximately(oldZoom, newZoom)) return;

        Vector2 localPointer = GetPointerLocalToViewport();
        Vector2 contentPointBeforeZoom = (localPointer - targetAnchoredPosition) / oldZoom;
        targetZoom = newZoom;
        targetAnchoredPosition = localPointer - contentPointBeforeZoom * targetZoom;
        ConstrainTargetAnchoredPosition();
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
        ConstrainTargetAnchoredPosition();
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
        ConstrainTargetAnchoredPosition();
        contentRect.anchoredPosition = Vector2.Lerp(contentRect.anchoredPosition, targetAnchoredPosition, panLerp);
    }

    private void ApplyViewportTargetImmediately()
    {
        if (contentRect == null) return;

        targetZoom = ClampZoom(targetZoom);
        ConstrainTargetAnchoredPosition();
        contentRect.localScale = new Vector3(targetZoom, targetZoom, 1f);
        contentRect.anchoredPosition = targetAnchoredPosition;
    }

    private void StoreCurrentTargetAsHome()
    {
        homeZoom = targetZoom;
        homeAnchoredPosition = targetAnchoredPosition;
        hasHomeView = true;
    }

    private float ClampZoom(float requestedZoom)
    {
        float lowerBound = Mathf.Max(0.01f, minZoom);
        if (preventZoomOutPastBounds && HasViewportBounds())
        {
            lowerBound = Mathf.Max(lowerBound, CalculateMinZoomToCoverViewport());
        }

        float upperBound = Mathf.Max(lowerBound, maxZoom);
        return Mathf.Clamp(requestedZoom, lowerBound, upperBound);
    }

    private float CalculateMinZoomToCoverViewport()
    {
        if (viewportRect == null) return minZoom;

        Vector2 boundsSize = GetViewportBoundsSize();
        Vector2 viewportSize = viewportRect.rect.size;
        if (boundsSize.x <= 0f || boundsSize.y <= 0f || viewportSize.x <= 0f || viewportSize.y <= 0f)
        {
            return minZoom;
        }

        return Mathf.Max(viewportSize.x / boundsSize.x, viewportSize.y / boundsSize.y);
    }

    private void ConstrainTargetAnchoredPosition()
    {
        if (!HasViewportBounds() || viewportRect == null) return;

        Vector2 boundsSize = GetViewportBoundsSize();
        Vector2 viewportSize = viewportRect.rect.size;
        if (boundsSize.x <= 0f || boundsSize.y <= 0f || viewportSize.x <= 0f || viewportSize.y <= 0f) return;

        float safeZoom = ClampZoom(targetZoom);
        if (!Mathf.Approximately(targetZoom, safeZoom))
        {
            targetZoom = safeZoom;
        }

        Vector2 scaledBounds = boundsSize * targetZoom;
        float maxX = Mathf.Max(0f, (scaledBounds.x - viewportSize.x) * 0.5f - boundsInset.x);
        float maxY = Mathf.Max(0f, (scaledBounds.y - viewportSize.y) * 0.5f - boundsInset.y);
        targetAnchoredPosition = new Vector2(
            Mathf.Clamp(targetAnchoredPosition.x, -maxX, maxX),
            Mathf.Clamp(targetAnchoredPosition.y, -maxY, maxY));
    }

    private bool HasViewportBounds()
    {
        return boundsMode != ManualMapViewportBoundsMode.None && viewportRect != null && contentRect != null;
    }

    private Vector2 GetViewportBoundsSize()
    {
        switch (boundsMode)
        {
            case ManualMapViewportBoundsMode.ManualSize:
                return new Vector2(Mathf.Max(1f, manualBoundsSize.x), Mathf.Max(1f, manualBoundsSize.y));

            case ManualMapViewportBoundsMode.BoundsRectOverride:
                if (boundsRectOverride != null)
                {
                    return boundsRectOverride.rect.size;
                }
                break;

            case ManualMapViewportBoundsMode.ContentRect:
                break;
        }

        return contentRect != null ? contentRect.rect.size : Vector2.zero;
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