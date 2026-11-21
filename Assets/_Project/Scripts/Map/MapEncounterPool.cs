using PlushieOdyssey.Data;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Map
{
    [CreateAssetMenu(menuName = "Data/Map Encounter Pool")]
    public class MapEncounterPool : ScriptableObject
    {
        [SerializeField] private List<EncounterData> enemyEncounters = new();
        [SerializeField] private List<EncounterData> eliteEncounters = new();
        [SerializeField] private List<EncounterData> bossEncounters = new();

        public IReadOnlyList<EncounterData> EnemyEncounters => enemyEncounters;
        public IReadOnlyList<EncounterData> EliteEncounters => eliteEncounters;
        public IReadOnlyList<EncounterData> BossEncounters => bossEncounters;

        public EncounterData GetEncounter(MapNodeType nodeType)
        {
            return nodeType switch
            {
                MapNodeType.Enemy => GetRandomCommonEnemyEncounter(),
                MapNodeType.Elite => GetRandom(eliteEncounters.Count > 0 ? eliteEncounters : enemyEncounters),
                MapNodeType.Boss => GetRandom(bossEncounters.Count > 0 ? bossEncounters : eliteEncounters.Count > 0 ? eliteEncounters : enemyEncounters),
                _ => null
            };
        }

        private EncounterData GetRandomCommonEnemyEncounter()
        {
            if (enemyEncounters == null || enemyEncounters.Count == 0) return null;

            List<EncounterData> commonOnly = new(enemyEncounters.Count);
            for (int i = 0; i < enemyEncounters.Count; i++)
            {
                EncounterData candidate = enemyEncounters[i];
                if (candidate == null) continue;
                if (IsEliteOrBossEncounter(candidate)) continue;
                commonOnly.Add(candidate);
            }

            if (commonOnly.Count > 0)
            {
                return commonOnly[Random.Range(0, commonOnly.Count)];
            }

            return GetRandom(enemyEncounters);
        }

        public bool IsEliteOrBossEncounter(EncounterData encounter)
        {
            if (encounter == null) return false;
            if (eliteEncounters != null && eliteEncounters.Contains(encounter)) return true;
            if (bossEncounters != null && bossEncounters.Contains(encounter)) return true;

            // Also guard against duplicate EncounterData assets that wrap an Elite or Boss EnemyData
            if (encounter.Enemies != null)
            {
                for (int i = 0; i < encounter.Enemies.Count; i++)
                {
                    EnemyData enemy = encounter.Enemies[i];
                    if (enemy == null) continue;
                    if (ContainsEnemy(eliteEncounters, enemy) || ContainsEnemy(bossEncounters, enemy))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsEnemy(List<EncounterData> pool, EnemyData target)
        {
            if (pool == null || target == null) return false;
            for (int i = 0; i < pool.Count; i++)
            {
                EncounterData enc = pool[i];
                if (enc == null || enc.Enemies == null) continue;
                if (enc.Enemies.Contains(target)) return true;
            }
            return false;
        }

        private static EncounterData GetRandom(List<EncounterData> encounters)
        {
            if (encounters == null || encounters.Count == 0) return null;
            return encounters[Random.Range(0, encounters.Count)];
        }
    }
}
