using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ApplyStatusEffect")]
    public class ApplyStatusEffect : Effect
    {
        [SerializeField] private StatusType status;
        [SerializeField] private int stacks;
        [SerializeField] private int duration;
        [SerializeField] private XScalingAxis xScaling = XScalingAxis.Auto;

        public StatusType Status => status;
        public int Stacks => stacks;
        public int Duration => duration;
        public XScalingAxis XScaling => xScaling;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            return new ApplyStatusGA(targets, status, stacks, duration, caster);
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            ResolveScaled(xMultiplier, out int scaledStacks, out int scaledDuration);
            return new ApplyStatusGA(targets, status, scaledStacks, scaledDuration, caster);
        }

        public void ResolveScaled(int xMultiplier, out int scaledStacks, out int scaledDuration)
        {
            int mult = Mathf.Max(0, xMultiplier);
            if (mult == 1)
            {
                scaledStacks = stacks;
                scaledDuration = duration;
                return;
            }

            XScalingAxis axis = xScaling == XScalingAxis.Auto
                ? (status == StatusType.Poison ? XScalingAxis.Value : XScalingAxis.Duration)
                : xScaling;

            scaledStacks = axis == XScalingAxis.Value || axis == XScalingAxis.Both ? stacks * mult : stacks;
            scaledDuration = axis == XScalingAxis.Duration || axis == XScalingAxis.Both ? duration * mult : duration;

            if (status == StatusType.Poison && scaledStacks > 0 && scaledDuration < scaledStacks)
            {
                scaledDuration = scaledStacks;
            }
        }
    }
}
