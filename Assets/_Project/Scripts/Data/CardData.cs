using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Vfx;
using System;
using System.Collections.Generic;
using SerializeReferenceEditor;
using UnityEngine;

namespace PlushieOdyssey.Data
{
    public enum CardTargetType
    {
        Enemy,
        Hero,
        Any
    }

    public enum CardType
    {
        Attack,
        Buff,
        Power
    }

    public enum Tier
    {
        Basic,
        Rare,
        Special
    }

    public enum CardVisualType
    {
        Particle,
        Animator,
        Both
    }

    [Flags]
    public enum CardKeywords
    {
        None = 0,
        Exhaust = 1 << 0,
        Retain = 1 << 1,
        Ethereal = 1 << 2,
        Innate = 1 << 3,
        Unplayable = 1 << 4,
        XCost = 1 << 5
    }

    [CreateAssetMenu(menuName = "Data/Card")]
    public class CardData : ScriptableObject
    {
        [field: SerializeField] public Sprite Image { get; private set; }
        [field: SerializeField] public string Title { get; private set; }
        [field: SerializeField] public string Description { get; private set; }
        [field: SerializeField] public Tier Tier { get; private set; }
        [field: SerializeField] public int Mana { get; private set; }
        [field: SerializeField, Min(0), Tooltip("Optional bonus added to X when playing an X-cost card (e.g., 1 for an 'X+1' upgraded card).")] public int XBonus { get; private set; } = 0;
        [field: SerializeField, Tooltip("Attack: Deals damage (blocked during - Low Will).\nBuff: Healing, Shielding, Utility.\nPower: Permanent passive effect.")] public CardType Type { get; private set; } = CardType.Attack;
        [field: SerializeField] public CardKeywords Keywords { get; private set; } = CardKeywords.None;
        [field: SerializeField] public CardTargetType TargetType { get; private set; } = CardTargetType.Enemy;
        [field: SerializeReference, SR] public Effect ManualTargetEffect { get; private set; } = null;
        [field: SerializeField] public List<AutoTargetEffect> OtherEffects { get; private set; }
        [field: SerializeField] public SfxID HoverSfx { get; private set; } = SfxID.hover;
        [field: SerializeField] public SfxID PlaySfx { get; private set; } = SfxID.None;

        [Header("VFX")]
        [field: SerializeField] public GameObject PlayParticle { get; private set; }
        [field: SerializeField] public VfxCue PlayVfx { get; private set; } = new();
        [field: SerializeField, Header("Visuals")] public CardVisualType VisualType { get; private set; } = CardVisualType.Particle;
        [field: SerializeField, Tooltip("Animation trigger name to play on the Hero")] public string HeroAnimationTrigger { get; private set; }
        [field: SerializeField, Tooltip("Animation trigger name to play on the Target(s)")] public string TargetAnimationTrigger { get; private set; }

        public bool IsXCost => (Keywords & CardKeywords.XCost) != 0 || Mana < 0;
        public string CostLabel => IsXCost ? "X" : Mathf.Max(0, Mana).ToString();
        public int GetEffectiveX(int manaSpent) => IsXCost ? Mathf.Max(0, manaSpent + Mathf.Max(0, XBonus)) : 1;
    }
}
