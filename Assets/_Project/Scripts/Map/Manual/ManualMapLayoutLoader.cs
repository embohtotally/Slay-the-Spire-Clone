using UnityEngine;
using UnityEngine.SceneManagement;

public class ManualMapLayoutLoader : MonoBehaviour
{
    [Header("Layouts")]
    [SerializeField] private ManualMapLayoutRegistry layoutRegistry;
    [SerializeField] private Transform mapRoot;

    [Header("Selection")]
    [Tooltip("If empty, uses the saved selection. If no saved selection exists, a random layout is chosen.")]
    [SerializeField] private string fallbackLayoutId;
    [SerializeField] private bool randomizeWhenNoSelection = true;
    [Tooltip("Useful for testing one layout in the editor. This overrides saved selection on Start.")]
    [SerializeField] private bool forceFallbackLayoutOnStart;

    [Header("Spawn")]
    [SerializeField] private bool clearRootBeforeSpawn = true;
    [SerializeField] private bool stretchRectTransformToParent = true;

    [Header("Optional Scene Flow")]
    [SerializeField] private string mapSceneName = "Map";

    private ManualMapController spawnedMap;

    private void Awake()
    {
        EnsureSelectionManagerExists();
        if (mapRoot == null) mapRoot = transform;
    }

    private void Start()
    {
        LoadSelectedOrFallbackLayout();
    }

    public void LoadSelectedOrFallbackLayout()
    {
        ManualMapLayoutEntry layout = ResolveLayoutToLoad();
        if (layout == null)
        {
            Debug.LogWarning("ManualMapLayoutLoader could not load a layout. Assign a registry with at least one valid ManualMapController prefab.");
            return;
        }

        SpawnLayout(layout);
    }

    public void SelectLayoutAndLoad(string layoutId)
    {
        if (layoutRegistry == null)
        {
            Debug.LogWarning("Cannot select manual map layout because registry is missing.");
            return;
        }

        ManualMapLayoutEntry layout = layoutRegistry.GetById(layoutId);
        if (layout == null)
        {
            Debug.LogWarning($"Manual map layout '{layoutId}' was not found in the registry.");
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
            SceneManager.LoadScene(mapSceneName);
        }
    }

    private ManualMapLayoutEntry ResolveLayoutToLoad()
    {
        if (layoutRegistry == null) return null;

        if (forceFallbackLayoutOnStart && !string.IsNullOrWhiteSpace(fallbackLayoutId))
        {
            ManualMapLayoutEntry forcedLayout = layoutRegistry.GetById(fallbackLayoutId);
            if (forcedLayout != null)
            {
                ManualMapRunSelection.Instance.SelectLayout(forcedLayout.SafeId);
                return forcedLayout;
            }

            Debug.LogWarning($"Forced manual map layout '{fallbackLayoutId}' was not found. Falling back to saved/random layout.");
        }

        if (ManualMapRunSelection.Instance.HasSelectedLayout)
        {
            ManualMapLayoutEntry savedLayout = layoutRegistry.GetById(ManualMapRunSelection.Instance.SelectedLayoutId);
            if (savedLayout != null)
            {
                return savedLayout;
            }

            Debug.LogWarning($"Saved manual map layout '{ManualMapRunSelection.Instance.SelectedLayoutId}' was not found. Choosing a fallback layout.");
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
        spawnedMap.SetRuntimeMapId(layout.SafeId);
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
}
