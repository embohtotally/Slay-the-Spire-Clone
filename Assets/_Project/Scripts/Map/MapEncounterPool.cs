using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Map Encounter Pool")]
public class MapEncounterPool : ScriptableObject
{
    [SerializeField] private List<EncounterData> enemyEncounters = new();
    [SerializeField] private List<EncounterData> eliteEncounters = new();
    [SerializeField] private List<EncounterData> bossEncounters = new();

    public EncounterData GetEncounter(MapNodeType nodeType)
    {
        return nodeType switch
        {
            MapNodeType.Enemy => GetRandom(enemyEncounters),
            MapNodeType.Elite => GetRandom(eliteEncounters.Count > 0 ? eliteEncounters : enemyEncounters),
            MapNodeType.Boss => GetRandom(bossEncounters.Count > 0 ? bossEncounters : eliteEncounters.Count > 0 ? eliteEncounters : enemyEncounters),
            _ => null
        };
    }

    private static EncounterData GetRandom(List<EncounterData> encounters)
    {
        if (encounters == null || encounters.Count == 0) return null;
        return encounters[Random.Range(0, encounters.Count)];
    }
}
