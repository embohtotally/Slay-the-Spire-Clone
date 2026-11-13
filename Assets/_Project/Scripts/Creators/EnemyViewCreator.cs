using PlushieOdyssey.Core;
using PlushieOdyssey.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    public class EnemyViewCreator : Singleton<EnemyViewCreator>
    {
        [SerializeField] private EnemyView enemyViewPrefab;

        public EnemyView CreateEnemyView(EnemyData enemyData, Vector3 position, Quaternion rotation)
        {
            EnemyView enemyView = Instantiate(enemyViewPrefab, position, rotation);
            enemyView.Setup(enemyData);
            return enemyView;
        }
    }
}
