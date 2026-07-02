using System.Collections.Generic;
using SerializeReferenceEditor;
using UnityEngine;

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
}
