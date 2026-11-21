using PlushieOdyssey.Relics;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Data
{
    public enum RelicRarity
    {
        Common,
        Uncommon,
        Rare,
        Boss,
        Special
    }

    [CreateAssetMenu(menuName = "Data/Relic")]
    public class RelicData : ScriptableObject
    {
        [field: Header("Identity")]
        [field: SerializeField] public string Title { get; private set; }
        [field: TextArea(2, 5)]
        [field: SerializeField] public string Description { get; private set; }
        [field: SerializeField] public Sprite Image { get; private set; }

        [field: Header("Rules")]
        [field: SerializeField] public RelicRarity Rarity { get; private set; } = RelicRarity.Common;
        [field: SerializeField] public bool Unique { get; private set; } = true;
        [field: Min(0)]
        [field: SerializeField] public int GoldValue { get; private set; } = 150;

        [field: Header("Combat Reactions")]
        [field: Tooltip("Optional trigger/effect rules used by RelicCombatTriggerSystem during combat.")]
        [field: SerializeField] public List<RelicReactionDefinition> Reactions { get; private set; } = new();

        [field: Header("Passive Run & Boss Relic Trade-Offs")]
        [field: SerializeField] public int BonusMaxMana { get; private set; }
        [field: SerializeField] public int StartCombatWillDelta { get; private set; }
        [field: SerializeField] public bool DisableRestSiteHeal { get; private set; }
        [field: SerializeField, Range(0, 75)] public int MerchantDiscountPercent { get; private set; }
        [field: SerializeField, Min(0)] public int BonusOpeningDraw { get; private set; }
    }
}
