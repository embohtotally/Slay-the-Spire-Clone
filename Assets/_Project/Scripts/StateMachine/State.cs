using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Core.StateMachines
{
    public abstract class State
    {
        public virtual void Enter() { }
        public virtual void Update() { }
        public virtual void Exit() { }
    }
}
