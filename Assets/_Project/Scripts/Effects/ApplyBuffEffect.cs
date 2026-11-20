using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ApplyBuffEffect")]
    public class ApplyBuffEffect : Effect
    {
        [SerializeField] private BuffType buffType;
        [SerializeField] private int value;
        [SerializeField] private int duration;
        [SerializeField] private XScalingAxis xScaling = XScalingAxis.Auto;

        public BuffType BuffType => buffType;
        public int Value => value;
        public int Duration => duration;
        public XScalingAxis XScaling => xScaling;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            ApplyBuffGA applyBuffGA = new ApplyBuffGA(targets, buffType, value, duration, caster);
            return applyBuffGA;
        }

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int xMultiplier)
        {
            if (xMultiplier <= 0) return null;
            ResolveScaled(xMultiplier, out int scaledValue, out int scaledDuration);
            return new ApplyBuffGA(targets, buffType, scaledValue, scaledDuration, caster);
        }

        public void ResolveScaled(int xMultiplier, out int scaledValue, out int scaledDuration)
        {
            int mult = Mathf.Max(0, xMultiplier);
            if (mult == 1)
            {
                scaledValue = value;
                scaledDuration = duration;
                return;
            }

            XScalingAxis axis = ResolveEffectiveAxis();
            scaledValue = axis == XScalingAxis.Value || axis == XScalingAxis.Both ? value * mult : value;
            scaledDuration = axis == XScalingAxis.Duration || axis == XScalingAxis.Both ? duration * mult : duration;

            if (buffType == BuffType.Poison && scaledValue > 0 && scaledDuration > 0 && scaledDuration < scaledValue)
            {
                scaledDuration = scaledValue;
            }
        }

        private XScalingAxis ResolveEffectiveAxis()
        {
            if (xScaling != XScalingAxis.Auto) return xScaling;

            bool isDurationDriven = buffType == BuffType.Weak
                || buffType == BuffType.Vulnerable
                || buffType == BuffType.Frail
                || buffType == BuffType.Intangible
                || buffType == BuffType.FearImmunity
                || buffType == BuffType.Redirect;

            if (isDurationDriven && duration > 0) return XScalingAxis.Duration;
            return XScalingAxis.Value;
        }
    }
}
