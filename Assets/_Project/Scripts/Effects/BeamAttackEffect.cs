using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Beam attack" effect: sequences beam VFX + damage. Drop it into CardData.ManualTargetEffect
/// (single-target cards) or into an AutoTargetEffect.Effect with any TargetMode (pierce/AoE cards).
/// Produces one DealDamageGA payload per target, delivered on each target's impact frame.
/// Also safe as an EnemyIntent effect: EffectSystem passes the attacking EnemyView as caster.
/// </summary>
[System.Serializable]
public class BeamAttackEffect : Effect
{
    [SerializeField] private int damageAmount = 6;
    [SerializeField] private BeamVfxConfig beamConfig;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        List<CombatantView> list = targets ?? new List<CombatantView>();

        // Graceful degradation: no config configured -> plain damage, no beam.
        if (beamConfig == null)
            return new DealDamageGA(damageAmount, list, caster);

        // Manual-target pierce: expand the single clicked enemy into the geometric line.
        if (beamConfig.ExpandSingleTargetToLine && list.Count == 1 && caster != null)
            list = BeamTargetQuery.ExpandToLine(caster, list[0], beamConfig.LineMaxAngle, beamConfig.LineMaxTargets);

        List<GameAction> payloads = new(list.Count);
        foreach (CombatantView target in list)
            payloads.Add(new DealDamageGA(damageAmount, new List<CombatantView> { target }, caster));

        return new PlayBeamVfxGA(caster, list, beamConfig, payloads);
    }
}
