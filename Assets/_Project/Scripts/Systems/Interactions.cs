using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [System.Serializable]
    public class Interactions : Singleton<Interactions>
    {
        public bool PlayerIsDragging { get; set; } = false;

        public bool PlayerCanInteract()
        {
            if (!ActionSystem.Instance.IsPerforming) return true;

            return false;
        }

        public bool PlayerCanHover()
        {
            if (PlayerIsDragging) return false;

            return true;
        }
    }
}
