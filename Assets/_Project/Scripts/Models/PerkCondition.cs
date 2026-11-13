using PlushieOdyssey.Combat.Actions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Models
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "PerkCondition")]
    public abstract class PerkCondition
    {
        [SerializeField] protected ReactionTiming reactionTiming;
        public abstract void SubscribeCondition(Action<GameAction> reaction);
        public abstract void UnsubscribeCondition(Action<GameAction> reaction);

        public abstract bool SubConditionIsMet(GameAction gameAction);
    }
}