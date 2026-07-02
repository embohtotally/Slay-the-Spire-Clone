using System.Collections.Generic;
using SerializeReferenceEditor;
using UnityEngine;

public enum PotionRarity
{
    Common,
    Uncommon,
    Rare,
    Special
}

[CreateAssetMenu(menuName = "Data/Potion")]
public class PotionData : ScriptableObject
{
    [field: Header("Identity")]
    [field: SerializeField] public string Title { get; private set; }
    [field: TextArea(2, 5)]
    [field: SerializeField] public string Description { get; private set; }
    [field: SerializeField] public Sprite Image { get; private set; }

    [field: Header("Rules")]
    [field: SerializeField] public PotionRarity Rarity { get; private set; } = PotionRarity.Common;
    [field: SerializeField] public bool Unique { get; private set; }
    [field: SerializeField] public bool Consumable { get; private set; } = true;
    [field: SerializeField] public bool UsableInCombat { get; private set; } = true;
    [field: SerializeField] public bool UsableOutsideCombat { get; private set; }
    [field: Min(0)]
    [field: SerializeField] public int GoldValue { get; private set; } = 50;

    [field: Header("Combat Effects (Optional)")]
    [field: Tooltip("Optional manual-target effect for a future potion use UI. Uses the same Effect system as cards.")]
    [field: SerializeReference, SR] public Effect ManualTargetEffect { get; private set; }
    [field: Tooltip("Optional auto-target effects for a future potion use UI. Uses the same target/effect wrappers as cards.")]
    [field: SerializeField] public List<AutoTargetEffect> OtherEffects { get; private set; } = new();
}
