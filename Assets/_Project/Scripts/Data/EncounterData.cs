using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Encounter")]
public class EncounterData : ScriptableObject
{
    [field: SerializeField] public string EncounterName { get; private set; }
    [field: SerializeField] public List<EnemyData> Enemies { get; private set; } = new();
}
