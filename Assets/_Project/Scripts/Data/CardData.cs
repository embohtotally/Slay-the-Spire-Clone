using System.Collections.Generic;
using SerializeReferenceEditor;
using UnityEngine;
using Gameseed26;

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

[CreateAssetMenu(menuName = "Data/Card")]
public class CardData : ScriptableObject
{
    [field: SerializeField] public Sprite Image { get; private set; }
    [field: SerializeField] public string Title { get; private set; }
    [field: SerializeField] public string Description { get; private set; }
    [field: SerializeField] public Tier Tier { get; private set; }
    [field: SerializeField] public int Mana { get; private set; }
    [field: SerializeField, Tooltip("Attack: Deals damage (blocked during breakdowns).\nBuff: Healing, Shielding, Utility.\nPower: Permanent passive effect.")] public CardType Type { get; private set; } = CardType.Attack;
    [field: SerializeField] public CardTargetType TargetType { get; private set; } = CardTargetType.Enemy;
    [field: SerializeReference, SR] public Effect ManualTargetEffect { get; private set; } = null;
    [field: SerializeField] public List<AutoTargetEffect> OtherEffects { get; private set; }
    [field: SerializeField] public SfxID HoverSfx { get; private set; } = SfxID.hover;
    [field: SerializeField] public SfxID PlaySfx { get; private set; } = SfxID.None;

    [Header("VFX")]
    [field: SerializeField] public GameObject PlayParticle { get; private set; }
    [field: SerializeField, Header("Visuals")] public CardVisualType VisualType { get; private set; } = CardVisualType.Particle;
    [field: SerializeField, Tooltip("Animation trigger name to play on the Hero")] public string HeroAnimationTrigger { get; private set; }
    [field: SerializeField, Tooltip("Animation trigger name to play on the Target(s)")] public string TargetAnimationTrigger { get; private set; }
}
