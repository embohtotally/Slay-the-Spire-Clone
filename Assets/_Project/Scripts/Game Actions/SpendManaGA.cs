using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Actions
{
    public class SpendManaGA : GameAction
    {
        public int Amount { get; set; }

        public SpendManaGA(int amount)
        {
            Amount = amount;
        }
    }
}
