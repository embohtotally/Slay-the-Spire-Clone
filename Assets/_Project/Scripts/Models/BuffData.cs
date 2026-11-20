using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Models
{
    [System.Serializable]
    public class BuffData
    {
        public CombatantView Target { get; private set; }
        public BuffType Type { get; private set; }
        public int Value { get; private set; }
        public int RemainingTurns { get; private set; }
        public CombatantView Caster { get; private set; }

        public BuffData(CombatantView target, BuffType type, int value, int remainingTurns, CombatantView caster)
        {
            Target = target;
            Type = type;
            Value = value;
            RemainingTurns = remainingTurns;
            Caster = caster;
        }

        public void DecrementDuration()
        {
            if (RemainingTurns > 0) RemainingTurns--;
        }

        public void SetValue(int value)
        {
            Value = value;
        }

        public void AddValue(int delta)
        {
            Value += delta;
        }

        public void AddDuration(int delta)
        {
            if (RemainingTurns >= 900 || delta >= 900)
            {
                RemainingTurns = 999;
                return;
            }
            RemainingTurns = Mathf.Max(1, RemainingTurns + delta);
        }

        public void DecrementValue(int amount = 1)
        {
            Value = Mathf.Max(0, Value - amount);
        }
    }
}
