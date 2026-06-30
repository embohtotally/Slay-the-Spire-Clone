using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System;

public static class YamlCardImporter
{
    [MenuItem("Tools/Import Cards from YAML")]
    public static void ImportCards()
    {
        string path = "Assets/_Project/Data/CardsDatabase.yaml";
        if (!File.Exists(path))
        {
            Debug.LogError($"YAML database not found at {path}");
            return;
        }

        string[] lines = File.ReadAllLines(path);
        List<CardDef> cards = ParseCards(lines);

        string baseDirectory = "Assets/_Project/ScriptableObjects/Cards/Generated";
        EnsureDirectory(baseDirectory);
        EnsureDirectory(baseDirectory + "/Aura");
        EnsureDirectory(baseDirectory + "/Ember");

        foreach (var c in cards)
        {
            string dir = c.id.StartsWith("Aura") || c.id.StartsWith("Reward_Aura") ? $"{baseDirectory}/Aura" : $"{baseDirectory}/Ember";
            CreateCard(dir, c);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Successfully imported {cards.Count} cards from YAML!");
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            int lastSlash = path.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                string parent = path.Substring(0, lastSlash);
                string folder = path.Substring(lastSlash + 1);
                EnsureDirectory(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }

    // --- PARSING LOGIC ---
    private class CardDef
    {
        public string id, title, tier, card_type, target_type, description;
        public int mana_cost;
        public EffectDef manual_effect;
        public List<AutoEffectDef> auto_effects = new();
    }
    private class AutoEffectDef
    {
        public string target;
        public EffectDef effect = new();
    }
    private class EffectDef
    {
        public string type;
        public Dictionary<string, string> props = new();
        public EffectDef base_effect;
        public EffectDef scaling_effect;
    }

    private static List<CardDef> ParseCards(string[] lines)
    {
        List<CardDef> cards = new();
        CardDef cCard = null;
        string ctx = "root";
        AutoEffectDef cAuto = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith("#")) continue;

            string trimmed = raw.Trim();
            if (trimmed.StartsWith("- id:"))
            {
                cCard = new CardDef();
                cards.Add(cCard);
                cCard.id = GetVal(trimmed);
                ctx = "root";
                continue;
            }

            if (cCard == null) continue;

            int indent = raw.Length - raw.TrimStart().Length;

            if (indent == 2 && trimmed.StartsWith("manual_effect:")) { ctx = "manual"; cCard.manual_effect = new EffectDef(); continue; }
            if (indent == 2 && trimmed.StartsWith("auto_effects:")) { ctx = "auto"; continue; }

            if (ctx == "root" && indent == 2)
            {
                string key = GetKey(trimmed), val = GetVal(trimmed);
                if (key == "title") cCard.title = val;
                else if (key == "tier") cCard.tier = val;
                else if (key == "mana_cost") cCard.mana_cost = int.Parse(val);
                else if (key == "card_type") cCard.card_type = val;
                else if (key == "target_type") cCard.target_type = val;
                else if (key == "description") cCard.description = val;
            }
            else if (ctx == "manual" && indent == 4)
            {
                string key = GetKey(trimmed), val = GetVal(trimmed);
                if (key == "type") cCard.manual_effect.type = val;
                else cCard.manual_effect.props[key] = val;
            }
            else if (ctx == "auto")
            {
                if (indent == 4 && trimmed.StartsWith("- type:"))
                {
                    cAuto = new AutoEffectDef();
                    cCard.auto_effects.Add(cAuto);
                    cAuto.effect.type = GetVal(trimmed);
                }
                else if (cAuto != null)
                {
                    if (indent == 6 && trimmed.StartsWith("base_effect:"))
                    {
                        cAuto.effect.base_effect = new EffectDef();
                        cAuto.effect.base_effect.type = GetVal(lines[++i]);
                        string amtLine = lines[++i];
                        cAuto.effect.base_effect.props[GetKey(amtLine)] = GetVal(amtLine);
                    }
                    else if (indent == 6 && trimmed.StartsWith("scaling_effect:"))
                    {
                        cAuto.effect.scaling_effect = new EffectDef();
                        cAuto.effect.scaling_effect.type = GetVal(lines[++i]);
                        string amtLine = lines[++i];
                        cAuto.effect.scaling_effect.props[GetKey(amtLine)] = GetVal(amtLine);
                    }
                    else if (indent == 6)
                    {
                        string key = GetKey(trimmed), val = GetVal(trimmed);
                        if (key == "target") cAuto.target = val;
                        else if (key == "type") cAuto.effect.type = val;
                        else cAuto.effect.props[key] = val;
                    }
                }
            }
        }
        return cards;
    }

    private static string GetKey(string s) => s.Split(':')[0].TrimStart('-').Trim();
    private static string GetVal(string s)
    {
        int idx = s.IndexOf(':');
        if (idx < 0) return "";
        string v = s.Substring(idx + 1).Trim();
        if (v.StartsWith("\"") && v.EndsWith("\"")) v = v.Substring(1, v.Length - 2);
        return v;
    }

    // --- GENERATION LOGIC ---
    private static void CreateCard(string dir, CardDef c)
    {
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>($"{dir}/{c.id}.asset");
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<CardData>();
            AssetDatabase.CreateAsset(card, $"{dir}/{c.id}.asset");
        }

        SetPrivateProperty(card, "Title", c.title);
        SetPrivateProperty(card, "Tier", c.tier);
        SetPrivateProperty(card, "Mana", c.mana_cost);
        SetPrivateProperty(card, "Type", (CardType)Enum.Parse(typeof(CardType), c.card_type));
        SetPrivateProperty(card, "TargetType", (CardTargetType)Enum.Parse(typeof(CardTargetType), c.target_type));
        SetPrivateProperty(card, "Description", c.description);

        if (c.manual_effect != null)
        {
            SetPrivateProperty(card, "ManualTargetEffect", BuildEffect(c.manual_effect));
        }

        if (c.auto_effects.Count > 0)
        {
            List<AutoTargetEffect> autos = new();
            foreach (var a in c.auto_effects)
            {
                TargetMode tm = BuildTargetMode(a.target);
                AutoTargetEffect autoEffect = new AutoTargetEffect();
                SetPrivateProperty(autoEffect, "TargetMode", tm);
                SetPrivateProperty(autoEffect, "Effect", BuildEffect(a.effect));
                autos.Add(autoEffect);
            }
            SetPrivateProperty(card, "OtherEffects", autos);
        }

        EditorUtility.SetDirty(card);
    }

    private static TargetMode BuildTargetMode(string t)
    {
        if (t == "Self") return new SelfTM();
        if (t == "SingleEnemy") return new SingleEnemyTargetMode();
        if (t == "AllEnemies") return new AllEnemiesTM();
        return null;
    }

    private static Effect BuildEffect(EffectDef e)
    {
        if (e.type == "Damage")
        {
            DealDamageEffect eff = new DealDamageEffect();
            SetPrivateField(eff, "damageAmount", int.Parse(e.props["amount"]));
            return eff;
        }
        if (e.type == "Heal")
        {
            HealEffect eff = new HealEffect();
            SetPrivateField(eff, "healAmount", int.Parse(e.props["amount"]));
            return eff;
        }
        if (e.type == "Shield")
        {
            GainShieldEffect eff = new GainShieldEffect();
            SetPrivateField(eff, "shieldAmount", int.Parse(e.props["amount"]));
            return eff;
        }
        if (e.type == "Draw")
        {
            DrawEffect eff = new DrawEffect();
            SetPrivateField(eff, "amount", int.Parse(e.props["amount"]));
            return eff;
        }
        if (e.type == "GainAP")
        {
            GainAPEffect eff = new GainAPEffect();
            SetPrivateField(eff, "amount", int.Parse(e.props["amount"]));
            return eff;
        }
        if (e.type == "ReduceFear")
        {
            ReduceFearEffect eff = new ReduceFearEffect();
            SetPrivateField(eff, "amount", int.Parse(e.props["amount"]));
            return eff;
        }
        if (e.type == "CopyFear")
        {
            CopyFearEffect eff = new CopyFearEffect();
            SetPrivateField(eff, "sourceAllyName", e.props["source_ally"]);
            return eff;
        }
        if (e.type == "RedirectDamage")
        {
            RedirectDamageEffect eff = new RedirectDamageEffect();
            SetPrivateField(eff, "duration", int.Parse(e.props["duration"]));
            return eff;
        }
        if (e.type == "RemoveDebuff")
        {
            RemoveDebuffEffect eff = new RemoveDebuffEffect();
            SetPrivateField(eff, "count", int.Parse(e.props["count"]));
            return eff;
        }
        if (e.type == "ConditionalDamage")
        {
            ConditionalDamageEffect eff = new ConditionalDamageEffect();
            SetPrivateField(eff, "baseDamage", int.Parse(e.props["base_amount"]));
            SetPrivateField(eff, "bonusDamage", int.Parse(e.props["bonus_amount"]));
            SetPrivateField(eff, "condition", (ConditionType)Enum.Parse(typeof(ConditionType), e.props["condition"]));
            SetPrivateField(eff, "threshold", int.Parse(e.props["threshold"]));
            return eff;
        }
        if (e.type == "ApplyStatus")
        {
            ApplyStatusEffect eff = new ApplyStatusEffect();
            SetPrivateField(eff, "status", (StatusType)Enum.Parse(typeof(StatusType), e.props["status"]));
            SetPrivateField(eff, "stacks", int.Parse(e.props["stacks"]));
            SetPrivateField(eff, "duration", int.Parse(e.props["duration"]));
            return eff;
        }
        if (e.type == "ApplyBuff")
        {
            ApplyBuffEffect eff = new ApplyBuffEffect();
            SetPrivateField(eff, "buffType", (BuffType)Enum.Parse(typeof(BuffType), e.props["buff_type"]));
            if (e.props.ContainsKey("value")) SetPrivateField(eff, "value", int.Parse(e.props["value"]));
            else SetPrivateField(eff, "value", 1);
            SetPrivateField(eff, "duration", int.Parse(e.props["duration"]));
            return eff;
        }
        if (e.type == "CostReduction")
        {
            CostReductionEffect eff = new CostReductionEffect();
            SetPrivateField(eff, "targetCardName", e.props["target_card_name"]);
            SetPrivateField(eff, "reduction", int.Parse(e.props["reduction"]));
            return eff;
        }
        if (e.type == "ScalingEffect")
        {
            ScalingEffect eff = new ScalingEffect();
            SetPrivateField(eff, "baseEffect", BuildEffect(e.base_effect));
            SetPrivateField(eff, "scalingEffect", BuildEffect(e.scaling_effect));
            SetPrivateField(eff, "step", int.Parse(e.props["step"]));
            SetPrivateField(eff, "counterKey", e.props["counter_key"]);
            return eff;
        }
        return null;
    }

    private static void SetPrivateProperty(object instance, string propertyName, object value)
    {
        PropertyInfo prop = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null) prop.SetValue(instance, value, null);
    }
    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null) field.SetValue(instance, value);
    }
}
