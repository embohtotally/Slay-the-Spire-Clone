using System;
using System.Collections.Generic;
using System.IO;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Data;
using PlushieOdyssey.Map;
using UnityEngine;

namespace PlushieOdyssey.Run
{
    /// <summary>
    /// Mid-Run JSON Save &amp; Continue system.
    /// Automatically snapshots active run state (seed, Ascension, chapter, floor, MapGraph, party,
    /// HP/Will, Gold/Crystals, Deck, Relics, Potions, CostModifiers) to persistentDataPath + PlayerPrefs.
    /// </summary>
    public static class RunSaveSystem
    {
        private const string SaveFileName = "plushie_active_run.json";
        private const string SavePrefFallbackKey = "PlushieOdyssey.ActiveRunSave.v1";

        [Serializable]
        public class SavedNodeData
        {
            public string Id;
            public int Layer;
            public int Column;
            public MapNodeType Type;
            public float PosX;
            public float PosY;
            public string EncounterName;
            public List<string> NextNodeIds = new();
            public bool IsVisited;
            public bool IsAvailable;
        }

        [Serializable]
        public class SavedStressData
        {
            public string HeroId;
            public int CurrentStress;
            public int MaxStress;
        }

        [Serializable]
        public class SavedKeyValue
        {
            public string Key;
            public int Value;
        }

        [Serializable]
        public class ActiveRunSaveData
        {
            public string SavedAtUtc;
            public int ChapterNumber = 1;
            public string ChapterName = string.Empty;
            public int AscensionLevel;
            public string CustomSeedLabel = string.Empty;
            public int MapSeed;
            public int MapLayerCount;
            public string CurrentMapNodeId = string.Empty;
            public List<string> VisitedOrder = new();
            public List<SavedNodeData> MapNodes = new();

            public List<string> PartyHeroIds = new();
            public int HeroCurrentHealth;
            public int HeroMaxHealth;
            public List<SavedStressData> HeroStressStates = new();
            public int Gold;
            public int Crystals;

            public List<string> DeckCardIds = new();
            public List<string> RelicIds = new();
            public List<string> PotionIds = new();
            public List<SavedKeyValue> CostModifiers = new();
            public List<SavedKeyValue> Counters = new();
        }

        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static bool HasSavedRun()
        {
            if (File.Exists(SaveFilePath))
            {
                try
                {
                    string raw = File.ReadAllText(SaveFilePath);
                    if (!string.IsNullOrWhiteSpace(raw) && raw.Contains("\"MapNodes\"")) return true;
                }
                catch { }
            }

            string pref = PlayerPrefs.GetString(SavePrefFallbackKey, string.Empty);
            return !string.IsNullOrWhiteSpace(pref) && pref.Contains("\"MapNodes\"");
        }

        public static ActiveRunSaveData CaptureCurrentRunData()
        {
            RunManager rm = RunManager.Instance;
            if (rm == null || !rm.HasActiveRun || rm.CurrentMap == null) return null;

            ActiveRunSaveData save = new()
            {
                SavedAtUtc = DateTime.UtcNow.ToString("O"),
                ChapterNumber = rm.CurrentChapterNumber,
                ChapterName = rm.CurrentChapterName ?? string.Empty,
                AscensionLevel = 0,
                CustomSeedLabel = string.Empty,
                MapSeed = rm.CurrentMap.Seed,
                MapLayerCount = rm.CurrentMap.LayerCount,
                CurrentMapNodeId = rm.CurrentMapNodeId ?? string.Empty,
                VisitedOrder = new List<string>(rm.CurrentMap.VisitedOrder ?? new List<string>()),
                HeroCurrentHealth = rm.HeroCurrentHealth,
                HeroMaxHealth = rm.HeroMaxHealth,
                Gold = rm.Gold,
                Crystals = rm.Crystals
            };

            if (rm.CurrentMap.Nodes != null)
            {
                foreach (MapNode node in rm.CurrentMap.Nodes)
                {
                    if (node == null) continue;
                    save.MapNodes.Add(new SavedNodeData
                    {
                        Id = node.Id,
                        Layer = node.Layer,
                        Column = node.Column,
                        Type = node.Type,
                        PosX = node.Position.x,
                        PosY = node.Position.y,
                        EncounterName = node.Encounter != null ? node.Encounter.name : string.Empty,
                        NextNodeIds = new List<string>(node.NextNodeIds ?? new List<string>()),
                        IsVisited = node.IsVisited,
                        IsAvailable = node.IsAvailable
                    });
                }
            }

            if (rm.SelectedParty != null)
            {
                foreach (HeroData h in rm.SelectedParty)
                {
                    if (h != null) save.PartyHeroIds.Add(h.CharacterId);
                }
            }

            if (rm.HeroStressStates != null)
            {
                foreach (RunHeroStressState s in rm.HeroStressStates)
                {
                    if (s == null) continue;
                    save.HeroStressStates.Add(new SavedStressData
                    {
                        HeroId = s.HeroData != null ? s.HeroData.CharacterId : string.Empty,
                        CurrentStress = s.CurrentStress,
                        MaxStress = s.MaxStress
                    });
                }
            }

            if (RunDeckManager.Instance != null)
            {
                foreach (CardData c in RunDeckManager.Instance.CurrentDeck)
                {
                    if (c != null) save.DeckCardIds.Add(c.name);
                }
            }

            if (RunRelicManager.Instance != null)
            {
                foreach (RelicData r in RunRelicManager.Instance.EquippedSlots)
                {
                    save.RelicIds.Add(r != null ? r.name : string.Empty);
                }
            }

            if (RunPotionManager.Instance != null)
            {
                foreach (PotionData p in RunPotionManager.Instance.CurrentPotions)
                {
                    if (p != null) save.PotionIds.Add(p.name);
                }
            }

            foreach (var kv in rm.CardCostModifiers)
                save.CostModifiers.Add(new SavedKeyValue { Key = kv.Key, Value = kv.Value });

            foreach (var kv in rm.Counters)
                save.Counters.Add(new SavedKeyValue { Key = kv.Key, Value = kv.Value });

            return save;
        }

        public static string SerializeToJson(ActiveRunSaveData data)
        {
            return data != null ? JsonUtility.ToJson(data, true) : string.Empty;
        }

        public static ActiveRunSaveData DeserializeFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonUtility.FromJson<ActiveRunSaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunSaveSystem] JSON parse error: {ex.Message}");
                return null;
            }
        }

        public static bool SaveCurrentRun()
        {
            ActiveRunSaveData save = CaptureCurrentRunData();
            if (save == null) return false;

            string json = SerializeToJson(save);
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                File.WriteAllText(SaveFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunSaveSystem] File write fallback to PlayerPrefs: {ex.Message}");
            }

            PlayerPrefs.SetString(SavePrefFallbackKey, json);
            PlayerPrefs.Save();
            return true;
        }

        public static ActiveRunSaveData LoadSavedRunData()
        {
            string json = string.Empty;
            if (File.Exists(SaveFilePath))
            {
                try { json = File.ReadAllText(SaveFilePath); } catch { }
            }
            if (string.IsNullOrWhiteSpace(json))
            {
                json = PlayerPrefs.GetString(SavePrefFallbackKey, string.Empty);
            }
            return DeserializeFromJson(json);
        }

        public static bool TryRestoreSavedRun(bool loadMapScene = true)
        {
            ActiveRunSaveData save = LoadSavedRunData();
            if (save == null || save.MapNodes == null || save.MapNodes.Count == 0) return false;

            if (RunManager.Instance == null)
            {
                new GameObject("Run Manager").AddComponent<RunManager>();
            }
            RunManager rm = RunManager.Instance;

            // Rebuild MapGraph
            Dictionary<string, EncounterData> encountersByName = IndexByName(Resources.FindObjectsOfTypeAll<EncounterData>());
            MapGraph graph = new()
            {
                Seed = save.MapSeed,
                LayerCount = save.MapLayerCount,
                VisitedOrder = new List<string>(save.VisitedOrder ?? new List<string>())
            };

            foreach (SavedNodeData sn in save.MapNodes)
            {
                MapNode node = new(sn.Id, sn.Layer, sn.Column, sn.Type, new Vector2(sn.PosX, sn.PosY))
                {
                    NextNodeIds = new List<string>(sn.NextNodeIds ?? new List<string>()),
                    IsVisited = sn.IsVisited,
                    IsAvailable = sn.IsAvailable
                };
                if (!string.IsNullOrWhiteSpace(sn.EncounterName) && encountersByName.TryGetValue(sn.EncounterName, out EncounterData enc))
                {
                    node.Encounter = enc;
                }
                graph.Nodes.Add(node);
            }
            graph.RefreshAvailability(save.CurrentMapNodeId);

            // Resolve Party
            HeroData[] allHeroes = Resources.FindObjectsOfTypeAll<HeroData>();
            List<HeroData> restoredParty = new();
            foreach (string id in save.PartyHeroIds)
            {
                HeroData match = FindHero(allHeroes, id);
                if (match != null) restoredParty.Add(match);
            }
            if (restoredParty.Count > 0) rm.SetSelectedParty(restoredParty);

            rm.AdvanceToNextChapterMap(graph, save.ChapterNumber, save.ChapterName, resetPlayerState: false);
            if (!string.IsNullOrWhiteSpace(save.CurrentMapNodeId))
            {
                MapNode currentNode = graph.GetNode(save.CurrentMapNodeId);
                if (currentNode != null) rm.SelectMapNode(currentNode);
            }

            // Restore HP / Will / Gold / Crystals
            List<RunHeroStressState> stressList = new();
            for (int i = 0; i < save.HeroStressStates.Count; i++)
            {
                SavedStressData sd = save.HeroStressStates[i];
                HeroData hd = i < restoredParty.Count ? restoredParty[i] : null;
                int will = Mathf.Clamp(sd.MaxStress - sd.CurrentStress, 0, Mathf.Max(1, sd.MaxStress));
                stressList.Add(new RunHeroStressState(hd, Mathf.Max(1, sd.MaxStress), will));
            }
            rm.RestorePlayerState(save.HeroCurrentHealth, Mathf.Max(1, save.HeroMaxHealth), stressList, save.Gold, save.Crystals);

            rm.CardCostModifiers.Clear();
            foreach (SavedKeyValue kv in save.CostModifiers) rm.CardCostModifiers[kv.Key] = kv.Value;
            rm.Counters.Clear();
            foreach (SavedKeyValue kv in save.Counters) rm.Counters[kv.Key] = kv.Value;

            // Restore Deck
            Dictionary<string, CardData> cardsByName = IndexByName(Resources.FindObjectsOfTypeAll<CardData>());
            List<CardData> restoredDeck = new();
            foreach (string cid in save.DeckCardIds)
            {
                if (cardsByName.TryGetValue(cid, out CardData cd)) restoredDeck.Add(cd);
            }
            if (restoredDeck.Count > 0)
            {
                if (RunDeckManager.Instance == null) new GameObject("Run Deck Manager").AddComponent<RunDeckManager>();
                RunDeckManager.Instance.ResetDeck(restoredDeck);
            }

            // Restore Relics
            Dictionary<string, RelicData> relicsByName = IndexByName(Resources.FindObjectsOfTypeAll<RelicData>());
            List<RelicData> relicSlots = new();
            foreach (string rid in save.RelicIds)
            {
                relicSlots.Add(!string.IsNullOrWhiteSpace(rid) && relicsByName.TryGetValue(rid, out RelicData rd) ? rd : null);
            }
            RunRelicManager.EnsureInstance().ApplySlotLoadout(relicSlots);

            // Restore Potions
            Dictionary<string, PotionData> potionsByName = IndexByName(Resources.FindObjectsOfTypeAll<PotionData>());
            RunPotionManager pm = RunPotionManager.EnsureInstance();
            pm.ClearPotions();
            foreach (string pid in save.PotionIds)
            {
                if (potionsByName.TryGetValue(pid, out PotionData pd)) pm.AddPotion(pd);
            }

            if (loadMapScene)
            {
                SceneLoader.LoadScene("Map");
            }
            return true;
        }

        public static void ClearSavedRun()
        {
            try
            {
                if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
            }
            catch { }

            PlayerPrefs.DeleteKey(SavePrefFallbackKey);
            PlayerPrefs.Save();
        }

        private static Dictionary<string, T> IndexByName<T>(T[] items) where T : UnityEngine.Object
        {
            Dictionary<string, T> map = new(StringComparer.OrdinalIgnoreCase);
            if (items == null) return map;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && !string.IsNullOrWhiteSpace(items[i].name) && !map.ContainsKey(items[i].name))
                {
                    map[items[i].name] = items[i];
                }
            }
            return map;
        }

        private static HeroData FindHero(HeroData[] heroes, string id)
        {
            if (heroes == null || string.IsNullOrWhiteSpace(id)) return null;
            foreach (HeroData h in heroes)
            {
                if (h == null) continue;
                if (string.Equals(h.CharacterId, id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(h.name, id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(h.EffectiveDisplayName, id, StringComparison.OrdinalIgnoreCase))
                {
                    return h;
                }
            }
            return null;
        }
    }
}