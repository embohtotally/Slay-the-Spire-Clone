using UnityEngine;

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
}
