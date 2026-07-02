using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VfxCue
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private Vector3 offset;
    [SerializeField] private Vector3 rotationEuler;
    [SerializeField] private Vector3 scaleMultiplier = Vector3.one;
    [SerializeField] private bool parentToTarget;
    [SerializeField, Min(0f)] private float destroyAfter = 3f;

    public GameObject Prefab => prefab;
    public bool HasPrefab => prefab != null;

    public GameObject Play(Transform target)
    {
        if (prefab == null) return null;

        Vector3 position = target != null ? target.position : Vector3.zero;
        Quaternion rotation = Quaternion.Euler(rotationEuler);
        GameObject instance = Object.Instantiate(prefab, position + offset, rotation);

        if (parentToTarget && target != null)
        {
            instance.transform.SetParent(target, true);
        }

        if (scaleMultiplier != Vector3.zero)
        {
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scaleMultiplier);
        }

        if (destroyAfter > 0f)
        {
            Object.Destroy(instance, destroyAfter);
        }

        return instance;
    }

    public void Play(CombatantView target)
    {
        Play(target != null ? target.transform : null);
    }

    public void Play(IEnumerable<CombatantView> targets)
    {
        if (targets == null) return;

        foreach (CombatantView target in targets)
        {
            Play(target);
        }
    }
}
