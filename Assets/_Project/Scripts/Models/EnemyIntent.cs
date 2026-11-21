using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Models
{
    [System.Serializable]
    public class EnemyIntent
    {
        [field: SerializeField] public string IntentName { get; private set; }
        [field: SerializeField] public List<AutoTargetEffect> Effects { get; private set; }

        [field: Header("Conditional Weighted AI")]
        [field: SerializeField, Min(0.05f)] public float Weight { get; private set; } = 1f;
        [field: SerializeField] public bool TurnOneOpener { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float MinHpPercent { get; private set; } = 0f;
        [field: SerializeField, Range(0f, 1f)] public float MaxHpPercent { get; private set; } = 1f;
        [field: SerializeField, Min(0)] public int CooldownTurns { get; private set; } = 0;
        [field: SerializeField, Min(0)] public int MaxConsecutiveUses { get; private set; } = 0;
    }
}
