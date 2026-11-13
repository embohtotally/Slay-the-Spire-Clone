using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Conditions
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "OnEnemyAttackCondition")]
    public class OnEnemyAttackCondition : PerkCondition
    {
        public override bool SubConditionIsMet(GameAction gameAction)
        {
            return true;
        }

        public override void SubscribeCondition(Action<GameAction> reaction)
        {
            ActionSystem.SubscribeReaction<ExecuteEnemyIntentGA>(reaction, reactionTiming);
        }

        public override void UnsubscribeCondition(Action<GameAction> reaction)
        {
            ActionSystem.UnsubscribeReaction<ExecuteEnemyIntentGA>(reaction, reactionTiming);
        }
    }
}