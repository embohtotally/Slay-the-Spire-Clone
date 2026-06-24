using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ManualMapLayoutEntry
{
    [Tooltip("Unique id saved for this run. Example: Forest_01, Cave_01.")]
    public string Id;

    public string DisplayName;

    [Tooltip("Prefab root should contain a ManualMapController and ManualMapNode children.")]
    public ManualMapController MapPrefab;

    [Min(0)] public int RandomWeight = 1;
    public bool Enabled = true;

    public string SafeId => string.IsNullOrWhiteSpace(Id) ? DisplayName : Id.Trim();
    public string SafeDisplayName => string.IsNullOrWhiteSpace(DisplayName) ? SafeId : DisplayName;
    public bool CanUse => Enabled && MapPrefab != null && !string.IsNullOrWhiteSpace(SafeId);
}

[CreateAssetMenu(menuName = "Data/Manual Map Layout Registry")]
public class ManualMapLayoutRegistry : ScriptableObject
{
    [SerializeField] private List<ManualMapLayoutEntry> layouts = new();

    public IReadOnlyList<ManualMapLayoutEntry> Layouts => layouts;

    public ManualMapLayoutEntry GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        string safeId = id.Trim();

        foreach (ManualMapLayoutEntry layout in layouts)
        {
            if (layout == null || !layout.CanUse) continue;
            if (string.Equals(layout.SafeId, safeId, StringComparison.OrdinalIgnoreCase))
            {
                return layout;
            }
        }

        return null;
    }

    public ManualMapLayoutEntry GetFirstAvailable()
    {
        foreach (ManualMapLayoutEntry layout in layouts)
        {
            if (layout != null && layout.CanUse)
            {
                return layout;
            }
        }

        return null;
    }

    public ManualMapLayoutEntry GetRandomLayout()
    {
        List<ManualMapLayoutEntry> availableLayouts = new();
        int totalWeight = 0;

        foreach (ManualMapLayoutEntry layout in layouts)
        {
            if (layout == null || !layout.CanUse) continue;

            int weight = Mathf.Max(0, layout.RandomWeight);
            if (weight <= 0) continue;

            availableLayouts.Add(layout);
            totalWeight += weight;
        }

        if (availableLayouts.Count == 0) return GetFirstAvailable();

        int roll = UnityEngine.Random.Range(0, totalWeight);
        foreach (ManualMapLayoutEntry layout in availableLayouts)
        {
            roll -= Mathf.Max(0, layout.RandomWeight);
            if (roll < 0)
            {
                return layout;
            }
        }

        return availableLayouts[availableLayouts.Count - 1];
    }
}
