using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Geometric line query shared by BeamLineTM and BeamAttackEffect line expansion.</summary>
public static class BeamTargetQuery
{
    public static List<CombatantView> CollectAlongAxis(CombatantView caster, Vector3 axis,
                                                       float maxAngleDeg, int maxTargets,
                                                       CombatantView guaranteed = null)
    {
        List<CombatantView> result = new();
        if (EnemySystem.Instance == null)
        {
            if (guaranteed != null) result.Add(guaranteed);
            return result;
        }

        Vector3 origin = caster != null ? caster.transform.position : Vector3.zero;
        axis.Normalize();

        foreach (EnemyView enemy in EnemySystem.Instance.Enemies)
        {
            if (enemy == null) continue;
            if (guaranteed != null && enemy == guaranteed) continue;
            Vector3 to = enemy.transform.position - origin;
            if (to.sqrMagnitude < 0.0001f) continue;
            if (Vector3.Angle(axis, to) > maxAngleDeg) continue;
            result.Add(enemy);
        }

        result.Sort((a, b) =>
            (a.transform.position - origin).sqrMagnitude
                .CompareTo((b.transform.position - origin).sqrMagnitude));

        if (guaranteed != null && !result.Contains(guaranteed)) result.Insert(0, guaranteed);
        if (maxTargets > 0 && result.Count > maxTargets) result.RemoveRange(maxTargets, result.Count - maxTargets);
        return result;
    }

    public static List<CombatantView> ExpandToLine(CombatantView caster, CombatantView anchor,
                                                   float maxAngleDeg, int maxTargets)
    {
        Vector3 axis = anchor.transform.position - caster.transform.position;
        return CollectAlongAxis(caster, axis, maxAngleDeg, maxTargets, anchor);
    }
}

/// <summary>
/// TargetMode for auto-fire pierce cards: axis = caster -> farthest enemy, then cone filter.
/// </summary>
[Serializable]
public class BeamLineTM : TargetMode
{
    [SerializeField] private float maxAngle = 18f;
    [SerializeField] private int maxTargets = 4;

    public override List<CombatantView> GetTargets(CombatantView caster = null)
    {
        if (EnemySystem.Instance == null || EnemySystem.Instance.Enemies.Count == 0)
            return new List<CombatantView>();

        Vector3 origin = caster != null ? caster.transform.position : Vector3.zero;
        EnemyView deepest = null;
        float best = -1f;
        foreach (EnemyView e in EnemySystem.Instance.Enemies)
        {
            if (e == null) continue;
            float d = (e.transform.position - origin).sqrMagnitude;
            if (d > best) { best = d; deepest = e; }
        }
        if (deepest == null) return new List<CombatantView>();

        return BeamTargetQuery.CollectAlongAxis(caster, deepest.transform.position - origin, maxAngle, maxTargets);
    }
}
