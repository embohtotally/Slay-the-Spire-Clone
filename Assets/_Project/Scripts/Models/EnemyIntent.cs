using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Models
{
    [System.Serializable]
    public class EnemyIntent
    {
        [field: SerializeField] public string IntentName { get; private set; }
        [field: SerializeField] public List<AutoTargetEffect> Effects { get; private set; }
    }
}
