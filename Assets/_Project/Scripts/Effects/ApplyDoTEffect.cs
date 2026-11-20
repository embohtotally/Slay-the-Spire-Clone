using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ApplyDoTEffect")]
    public class ApplyDoTEffect : Effect
    {
        [SerializeField] private int damagePerTurn;
        [SerializeField] private int duration;
        [SerializeField] private XScalingAxis xScaling = XScalingAxis.Auto;

        public int DamagePerTurn => damagePerTurn;
        public int Duration => duration;
        public XScalingAxis XScaling => xScaling;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            ApplyDoTGA applyDoTGA = new(targets, damagePerTurn, duration, caster);
            return applyDoTGA;
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            ResolveScaled(xMultiplier, out int scaledDmg, out int scaledDur);
            return new ApplyDoTGA(targets, scaledDmg, scaledDur, caster);
        }

        public void ResolveScaled(int xMultiplier, out int scaledDamagePerTurn, out int scaledDuration)
        {
            int mult = Mathf.Max(0, xMultiplier);
            if (mult == 1)
            {
                scaledDamagePerTurn = damagePerTurn;
                scaledDuration = duration;
                return;
            }

            XScalingAxis axis = xScaling == XScalingAxis.Auto ? XScalingAxis.Value : xScaling;
            scaledDamagePerTurn = axis == XScalingAxis.Value || axis == XScalingAxis.Both ? damagePerTurn * mult : damagePerTurn;
            scaledDuration = axis == XScalingAxis.Duration || axis == XScalingAxis.Both ? duration * mult : duration;
        }
    }
}
