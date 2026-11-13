using PlushieOdyssey.Combat.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Actions
{
    public class KillEnemyGA : GameAction
    {
        public EnemyView EnemyView { get; private set; }

        public KillEnemyGA(EnemyView enemyView)
        {
            EnemyView = enemyView;
        }
    }
}
