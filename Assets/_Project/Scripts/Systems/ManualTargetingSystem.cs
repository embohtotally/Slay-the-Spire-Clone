using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ManualTargetingSystem : Singleton<ManualTargetingSystem>
{
    [SerializeField] private ArrowView arrowView;
    [SerializeField] private LayerMask targetLayerMask;
    [SerializeField] private GameObject targetVfxPrefab;

    private List<ArrowView> autoArrows = new();
    private List<GameObject> autoVfxs = new();

    public void StartTargeting(Vector3 startPosition)
    {
        arrowView.gameObject.SetActive(true);
        arrowView.SetupArrow(startPosition);
    }

    public EnemyView EndTargeting(Vector3 endPosition)
    {
        arrowView.gameObject.SetActive(false);

        if (Physics.Raycast(endPosition, Vector3.forward, out RaycastHit hit, 10f, targetLayerMask)
            && hit.collider != null
            && hit.transform.TryGetComponent(out EnemyView enemyView))
        {
            bool anyTaunt = EnemySystem.Instance.Enemies.Exists(e => e.IsTaunted);
            if (anyTaunt && !enemyView.IsTaunted)
            {
                return null;
            }

            return enemyView;
        }

        return null;
    }

    public void StartAutoTargeting(Card card, Vector3 startPosition)
    {
        autoArrows.RemoveAll(a => a == null);
        autoVfxs.RemoveAll(v => v == null);

        List<CombatantView> targets = new();
        if (card.OtherEffects != null)
        {
            foreach (var effect in card.OtherEffects)
            {
                if (effect.TargetMode == null) continue;
                var t = effect.TargetMode.GetTargets();
                if (t != null)
                {
                    foreach (var target in t)
                    {
                        if (!targets.Contains(target) && target is EnemyView)
                        {
                            targets.Add(target);
                        }
                    }
                }
            }
        }

        while (autoArrows.Count < targets.Count)
        {
            var newArrow = Instantiate(arrowView.gameObject, arrowView.transform.parent).GetComponent<ArrowView>();
            autoArrows.Add(newArrow);
        }

        if (targetVfxPrefab != null)
        {
            while (autoVfxs.Count < targets.Count)
            {
                var newVfx = Instantiate(targetVfxPrefab, transform);
                autoVfxs.Add(newVfx);
            }
        }

        for (int i = 0; i < targets.Count; i++)
        {
            autoArrows[i].gameObject.SetActive(true);
            autoArrows[i].SetupArrow(startPosition, targets[i].transform);

            if (targetVfxPrefab != null)
            {
                autoVfxs[i].gameObject.SetActive(true);
                autoVfxs[i].transform.position = targets[i].transform.position;

                var particles = autoVfxs[i].GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particles)
                {
                    ps.Play();
                }
            }
        }

        for (int i = targets.Count; i < autoArrows.Count; i++)
        {
            autoArrows[i].gameObject.SetActive(false);
        }

        for (int i = targets.Count; i < autoVfxs.Count; i++)
        {
            autoVfxs[i].gameObject.SetActive(false);
        }
    }

    public void UpdateAutoTargeting(Vector3 startPosition)
    {
        foreach (var arrow in autoArrows)
        {
            if (arrow != null && arrow.gameObject.activeSelf)
            {
                arrow.SetStartPosition(startPosition);
            }
        }
    }

    public void EndAutoTargeting()
    {
        foreach (var arrow in autoArrows)
        {
            if (arrow != null)
                arrow.gameObject.SetActive(false);
        }

        foreach (var vfx in autoVfxs)
        {
            if (vfx != null)
                vfx.gameObject.SetActive(false);
        }
    }
}
