using PlushieOdyssey.Combat.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Data
{
    public enum EnemyPassiveType
    {
        Spiky = 0,
        Regenerate = 1,
        Ritual = 2,
        Enrage = 3,
        TimeWarp = 4,
        ModeShift = 5,
        WillEaterAura = 6
    }

    [Serializable]
    public class EnemyPassiveTrait
    {
        [SerializeField] private EnemyPassiveType passiveType;
        [SerializeField] private int value = 2;
        [SerializeField] private int secondaryValue = 0;
        [SerializeField] private string customTitle;
        [SerializeField, TextArea(1, 3)] private string customDescription;

        public EnemyPassiveType PassiveType => passiveType;
        public int Value => value;
        public int SecondaryValue => secondaryValue;
        public string CustomTitle => customTitle;
        public string CustomDescription => customDescription;

        public string GetDisplayTitle()
        {
            if (!string.IsNullOrWhiteSpace(customTitle)) return customTitle.Trim();
            return passiveType switch
            {
                EnemyPassiveType.Spiky => "Spiky",
                EnemyPassiveType.Regenerate => "Regenerate",
                EnemyPassiveType.Ritual => "Ritual",
                EnemyPassiveType.Enrage => "Enrage",
                EnemyPassiveType.TimeWarp => "Time Warp",
                EnemyPassiveType.ModeShift => "Mode Shift",
                EnemyPassiveType.WillEaterAura => "Will-Eater Aura",
                _ => passiveType.ToString()
            };
        }
    }

    [CreateAssetMenu(menuName = "Data/Enemy")]
    public class EnemyData : ScriptableObject
    {
        [field: SerializeField] public Sprite Image { get; private set; }
        [field: SerializeField] public int Health { get; private set; }
        [field: SerializeField] public List<EnemyIntent> Intents { get; private set; }
        [field: SerializeField] public AIAttackPattern AttackPattern { get; private set; }
        [field: SerializeField, Tooltip("Offset applied to the sprite position")] public Vector3 SpriteOffset { get; private set; }
        [field: SerializeField] public List<EnemyPassiveTrait> PassiveTraits { get; private set; } = new();
    }
}
