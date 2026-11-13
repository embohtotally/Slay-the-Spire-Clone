using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Effects
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "DrawCardsEffect")]
    public class DrawCardsEffect : Effect
    {
        [SerializeField] private int drawAmount;

        public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
        {
            DrawCardsGA drawCardsGA = new(drawAmount);
            return drawCardsGA;
        }
    }
}