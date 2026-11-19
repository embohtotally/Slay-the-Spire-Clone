using PlushieOdyssey.Combat;
using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Actions
{
    public class DealDamageGA : GameAction, IHaveCaster
    {
        public List<CombatantView> Targets { get; set; }
        public int Amount { get; set; }
        public bool IsThornsDamage { get; set; }

        public CombatantView Caster{ get; private set; }

        public DealDamageGA(int amount, List<CombatantView> targets, CombatantView caster, bool isThornsDamage = false)
        {
            Amount = amount;
            Targets = targets == null ? new() : new(targets);
            Caster = caster;
            IsThornsDamage = isThornsDamage;
        }
    }
}
