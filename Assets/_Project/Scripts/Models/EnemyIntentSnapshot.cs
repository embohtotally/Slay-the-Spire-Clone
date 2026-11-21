using System.Collections.Generic;
using System.Text;
using PlushieOdyssey.Combat.Effects;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using UnityEngine;

namespace PlushieOdyssey.Combat.Models
{
    public enum EnemyIntentCategory
    {
        None,
        Attack,
        AttackDebuff,
        AttackBuff,
        Defend,
        Buff,
        Debuff,
        WillDrain,
        Heal,
        Summon,
        Stunned,
        Unknown
    }

    /// <summary>
    /// Live, buff-adjusted visual and tooltip snapshot of an enemy's upcoming intent.
    /// </summary>
    public sealed class EnemyIntentSnapshot
    {
        public EnemyIntentCategory Category { get; private set; }
        public string EnemyDisplayName { get; private set; }
        public string ActionLabel { get; private set; }
        public string ValueLabel { get; private set; }
        public string SecondaryBadge { get; private set; }
        public string TooltipTitle { get; private set; }
        public string TooltipDescription { get; private set; }
        public Color AccentColor { get; private set; }
        public Color SecondaryColor { get; private set; }

        public int TotalDamage { get; private set; }
        public int HitCount { get; private set; }
        public int TotalShield { get; private set; }
        public int TotalWillDrain { get; private set; }
        public int TotalHeal { get; private set; }
        public int SummonCount { get; private set; }

        public static Color GetCategoryColor(EnemyIntentCategory category)
        {
            return category switch
            {
                EnemyIntentCategory.Attack => new Color(1.00f, 0.34f, 0.34f, 1f),       // Crimson Red
                EnemyIntentCategory.AttackDebuff => new Color(1.00f, 0.33f, 0.56f, 1f), // Crimson-Magenta
                EnemyIntentCategory.AttackBuff => new Color(1.00f, 0.56f, 0.24f, 1f),   // Fiery Orange
                EnemyIntentCategory.Defend => new Color(0.30f, 0.78f, 1.00f, 1f),       // Sky Blue
                EnemyIntentCategory.Buff => new Color(1.00f, 0.82f, 0.28f, 1f),         // Warm Gold
                EnemyIntentCategory.Debuff => new Color(0.78f, 0.44f, 1.00f, 1f),       // Deep Violet
                EnemyIntentCategory.WillDrain => new Color(0.88f, 0.40f, 1.00f, 1f),    // Psychic Magenta
                EnemyIntentCategory.Heal => new Color(0.31f, 0.89f, 0.54f, 1f),         // Emerald Green
                EnemyIntentCategory.Summon => new Color(0.21f, 0.89f, 0.77f, 1f),       // Arcane Teal
                EnemyIntentCategory.Stunned => new Color(0.55f, 0.84f, 1.00f, 1f),      // Ice Blue
                _ => new Color(0.82f, 0.86f, 0.92f, 1f)                                 // Silver
            };
        }

        public static EnemyIntentSnapshot Build(EnemyView enemy, EnemyIntent intent)
        {
            string enemyName = enemy != null && enemy.Data != null ? enemy.Data.name : (enemy != null ? enemy.name : "Enemy");

            if (enemy != null && enemy.IsStunned)
            {
                return new EnemyIntentSnapshot
                {
                    Category = EnemyIntentCategory.Stunned,
                    EnemyDisplayName = enemyName,
                    ActionLabel = "STUNNED",
                    ValueLabel = string.Empty,
                    SecondaryBadge = string.Empty,
                    TooltipTitle = "Stunned",
                    TooltipDescription = "This enemy is <color=#8CD6FF><b>Stunned</b></color> and will skip its upcoming turn.",
                    AccentColor = GetCategoryColor(EnemyIntentCategory.Stunned),
                    SecondaryColor = Color.white
                };
            }

            if (intent == null) return null;

            int totalDamage = 0;
            int hitCount = 0;
            int totalShield = 0;
            int totalWillDrain = 0;
            int totalHeal = 0;
            int dotDamage = 0;
            int dotDuration = 0;
            int summonCount = 0;
            string summonName = null;
            int buffPrimaryValue = 0;
            bool hasBuff = false;
            bool hasDebuff = false;
            var detailClauses = new List<string>();

            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;

            if (intent.Effects != null)
            {
                foreach (AutoTargetEffect wrapper in intent.Effects)
                {
                    if (wrapper?.Effect == null) continue;
                    AccumulateEffect(
                        wrapper.Effect, enemy, hero,
                        ref totalDamage, ref hitCount, ref totalShield, ref totalWillDrain, ref totalHeal,
                        ref dotDamage, ref dotDuration, ref summonCount, ref summonName,
                        ref buffPrimaryValue, ref hasBuff, ref hasDebuff, detailClauses);
                }
            }

            bool hasAttack = hitCount > 0;
            bool hasDefend = totalShield > 0;
            bool hasWill = totalWillDrain > 0;
            bool hasHeal = totalHeal > 0;
            bool hasSummon = summonCount > 0;

            // Classify primary category & color-coded top action label
            EnemyIntentCategory category;
            string actionLabel;
            string tooltipTitle;

            if (hasAttack && (hasWill || hasDebuff))
            {
                category = EnemyIntentCategory.AttackDebuff;
                actionLabel = hasWill && !hasDebuff ? "ATTACK + WILL" : "ATTACK + DEBUFF";
                tooltipTitle = hasWill && !hasDebuff ? "Attack & Will Drain" : "Attack & Debuff";
            }
            else if (hasAttack && (hasDefend || hasBuff))
            {
                category = EnemyIntentCategory.AttackBuff;
                actionLabel = hasDefend ? "ATTACK + DEFEND" : "ATTACK + BUFF";
                tooltipTitle = hasDefend ? "Attack & Defend" : "Attack & Buff";
            }
            else if (hasAttack)
            {
                category = EnemyIntentCategory.Attack;
                actionLabel = hitCount > 1 ? "MULTI-ATTACK" : "ATTACK";
                tooltipTitle = hitCount > 1 ? $"Multi-Hit Attack ({hitCount}x)" : "Attack";
            }
            else if (hasSummon)
            {
                category = EnemyIntentCategory.Summon;
                actionLabel = "SUMMON";
                tooltipTitle = "Summon Reinforcements";
            }
            else if (hasDefend && hasBuff)
            {
                category = EnemyIntentCategory.Defend;
                actionLabel = "DEFEND + BUFF";
                tooltipTitle = "Defend & Buff";
            }
            else if (hasDefend)
            {
                category = EnemyIntentCategory.Defend;
                actionLabel = "DEFEND";
                tooltipTitle = "Defend";
            }
            else if (hasHeal)
            {
                category = EnemyIntentCategory.Heal;
                actionLabel = "HEAL";
                tooltipTitle = "Heal";
            }
            else if (hasWill && !hasDebuff)
            {
                category = EnemyIntentCategory.WillDrain;
                actionLabel = "WILL DRAIN";
                tooltipTitle = "Will Drain";
            }
            else if (hasDebuff)
            {
                category = EnemyIntentCategory.Debuff;
                actionLabel = dotDamage > 0 ? "AFFLICT" : "DEBUFF";
                tooltipTitle = dotDamage > 0 ? "Affliction" : "Debuff";
            }
            else if (hasBuff)
            {
                category = EnemyIntentCategory.Buff;
                actionLabel = "BUFF";
                tooltipTitle = "Strategic Buff";
            }
            else
            {
                category = EnemyIntentCategory.Unknown;
                actionLabel = "SPECIAL";
                tooltipTitle = !string.IsNullOrWhiteSpace(intent.IntentName) && intent.IntentName != enemyName
                    ? intent.IntentName
                    : "Unknown Intent";
            }

            // Primary bold number + optional secondary chip
            string valueLabel = string.Empty;
            string secondaryBadge = string.Empty;
            Color secondaryColor = new Color(0.86f, 0.56f, 1.00f, 1f);

            if (hasAttack)
            {
                valueLabel = hitCount > 1
                    ? $"{Mathf.RoundToInt(totalDamage / (float)hitCount)}×{hitCount}"
                    : totalDamage.ToString();

                if (hasWill)
                {
                    secondaryBadge = $"-{totalWillDrain} WILL";
                    secondaryColor = GetCategoryColor(EnemyIntentCategory.WillDrain);
                }
                else if (hasDefend)
                {
                    secondaryBadge = $"+{totalShield} DEF";
                    secondaryColor = GetCategoryColor(EnemyIntentCategory.Defend);
                }
                else if (hasDebuff)
                {
                    secondaryBadge = "DEBUFF";
                    secondaryColor = GetCategoryColor(EnemyIntentCategory.Debuff);
                }
                else if (hasBuff)
                {
                    secondaryBadge = "BUFF";
                    secondaryColor = GetCategoryColor(EnemyIntentCategory.Buff);
                }
            }
            else if (hasSummon)
            {
                valueLabel = $"×{summonCount}";
            }
            else if (hasDefend)
            {
                valueLabel = $"+{totalShield}";
            }
            else if (hasHeal)
            {
                valueLabel = $"+{totalHeal}";
            }
            else if (hasWill)
            {
                valueLabel = $"-{totalWillDrain}";
            }
            else if (dotDamage > 0)
            {
                valueLabel = $"{dotDamage}/t";
            }
            else if (hasBuff && buffPrimaryValue > 0)
            {
                valueLabel = $"+{buffPrimaryValue}";
            }

            // Natural-language StS / WAJ tooltip description
            string description = BuildDescription(detailClauses, hasAttack, totalDamage, hitCount, totalWillDrain, totalShield, totalHeal);

            return new EnemyIntentSnapshot
            {
                Category = category,
                EnemyDisplayName = enemyName,
                ActionLabel = actionLabel,
                ValueLabel = valueLabel,
                SecondaryBadge = secondaryBadge,
                TooltipTitle = tooltipTitle,
                TooltipDescription = description,
                AccentColor = GetCategoryColor(category),
                SecondaryColor = secondaryColor,
                TotalDamage = totalDamage,
                HitCount = hitCount,
                TotalShield = totalShield,
                TotalWillDrain = totalWillDrain,
                TotalHeal = totalHeal,
                SummonCount = summonCount
            };
        }

        private static string BuildDescription(
            List<string> clauses,
            bool hasAttack, int totalDamage, int hitCount,
            int totalWillDrain, int totalShield, int totalHeal)
        {
            var merged = new List<string>();
            if (hasAttack)
            {
                if (hitCount > 1)
                {
                    int perHit = Mathf.RoundToInt(totalDamage / (float)hitCount);
                    merged.Add($"<color=#FF6B6B><b>attack {hitCount} times for {perHit} damage each</b></color> ({totalDamage} total)");
                }
                else
                {
                    merged.Add($"<color=#FF6B6B><b>attack for {totalDamage} damage</b></color>");
                }
            }

            if (totalShield > 0)
                merged.Add($"<color=#56D0FF><b>gain {totalShield} Shield</b></color>");

            if (totalWillDrain > 0)
                merged.Add($"<color=#E48BFF><b>drain {totalWillDrain} Will</b></color>");

            if (totalHeal > 0)
                merged.Add($"<color=#59E892><b>heal {totalHeal} HP</b></color>");

            merged.AddRange(clauses);

            if (merged.Count == 0)
                return "This enemy is preparing a mysterious action.";

            var sb = new StringBuilder("Intends to ");
            for (int i = 0; i < merged.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(i == merged.Count - 1 ? " and " : ", ");
                }
                sb.Append(merged[i]);
            }
            sb.Append('.');
            return sb.ToString();
        }

        private static void AccumulateEffect(
            Effect effect,
            EnemyView enemy,
            HeroView hero,
            ref int totalDamage,
            ref int hitCount,
            ref int totalShield,
            ref int totalWillDrain,
            ref int totalHeal,
            ref int dotDamage,
            ref int dotDuration,
            ref int summonCount,
            ref string summonName,
            ref int buffPrimaryValue,
            ref bool hasBuff,
            ref bool hasDebuff,
            List<string> detailClauses)
        {
            switch (effect)
            {
                case DealDamageEffect dmg:
                    int calcDmg = BuffSystem.Instance != null
                        ? BuffSystem.Instance.CalculateExpectedDamage(dmg.DamageAmount, enemy, hero)
                        : dmg.DamageAmount;
                    totalDamage += calcDmg;
                    hitCount++;
                    break;

                case MultiHitDamageEffect multi:
                    int perHitDmg = BuffSystem.Instance != null
                        ? BuffSystem.Instance.CalculateExpectedDamage(multi.DamagePerHit, enemy, hero)
                        : multi.DamagePerHit;
                    totalDamage += perHitDmg * multi.HitCount;
                    hitCount += multi.HitCount;
                    break;

                case InjectCardEffect inject:
                    hasDebuff = true;
                    string cardTitle = inject.CardToInject != null ? inject.CardToInject.Title : "Status";
                    detailClauses.Add($"shuffle <color=#FF8A80><b>{inject.Count} {cardTitle}</b></color> into your deck");
                    break;

                case BeamAttackEffect beam:
                    int calcBeam = BuffSystem.Instance != null
                        ? BuffSystem.Instance.CalculateExpectedDamage(beam.DamageAmount, enemy, hero)
                        : beam.DamageAmount;
                    totalDamage += calcBeam;
                    hitCount++;
                    break;

                case GainShieldEffect shield:
                    int calcShield = BuffSystem.Instance != null
                        ? BuffSystem.Instance.CalculateExpectedShield(shield.ShieldAmount, enemy)
                        : shield.ShieldAmount;
                    totalShield += calcShield;
                    break;

                case AddStressEffect stress:
                    totalWillDrain += stress.StressAmount;
                    break;

                case HealEffect heal:
                    totalHeal += heal.HealAmount;
                    break;

                case ApplyBuffEffect buff:
                    string buffLabel = FormatBuffName(buff.BuffType);
                    string durText = buff.Duration > 0 ? $" for {buff.Duration} {(buff.Duration == 1 ? "turn" : "turns")}" : string.Empty;
                    if (BuffSystem.IsDebuff(buff.BuffType))
                    {
                        hasDebuff = true;
                        string valText = buff.Value > 0 ? $"{buff.Value} " : string.Empty;
                        detailClauses.Add($"inflict <color=#D18BFF><b>{valText}{buffLabel}</b></color>{durText}");
                    }
                    else
                    {
                        hasBuff = true;
                        if (buff.Value > buffPrimaryValue) buffPrimaryValue = buff.Value;
                        string valText = buff.Value > 0 ? $"+{buff.Value} " : string.Empty;
                        detailClauses.Add($"grant itself <color=#FFD659><b>{valText}{buffLabel}</b></color>{durText}");
                    }
                    break;

                case ApplyStatusEffect status:
                    hasDebuff = true;
                    string sDur = status.Duration > 0 ? $" for {status.Duration} {(status.Duration == 1 ? "turn" : "turns")}" : string.Empty;
                    detailClauses.Add($"inflict <color=#D18BFF><b>{status.Stacks} {status.Status}</b></color>{sDur}");
                    break;

                case ApplyDoTEffect dot:
                    hasDebuff = true;
                    dotDamage += dot.DamagePerTurn;
                    dotDuration = Mathf.Max(dotDuration, dot.Duration);
                    detailClauses.Add($"inflict <color=#D18BFF><b>{dot.DamagePerTurn} Damage/turn</b></color> for {dot.Duration} {(dot.Duration == 1 ? "turn" : "turns")}");
                    break;

                case ApplyStunEffect stun:
                    hasDebuff = true;
                    detailClauses.Add($"<color=#8CD6FF><b>Stun</b></color> for {Mathf.Max(1, stun.Duration)} {(stun.Duration == 1 ? "turn" : "turns")}");
                    break;

                case ApplyTauntEffect taunt:
                    hasBuff = true;
                    detailClauses.Add($"activate <color=#FFD659><b>Taunt</b></color> for {Mathf.Max(1, taunt.Duration)} {(taunt.Duration == 1 ? "turn" : "turns")}");
                    break;

                case SummonEnemyEffect summon:
                    int count = Mathf.Max(1, summon.SummonCount);
                    summonCount += count;
                    summonName = summon.EnemyToSummon != null ? summon.EnemyToSummon.name : "Enemy";
                    detailClauses.Add($"<color=#4CE8CE><b>summon {count} {summonName}</b></color> {(count == 1 ? "ally" : "allies")}");
                    break;

                case SimultaneousEffect sim when sim.Effects != null:
                    foreach (AutoTargetEffect child in sim.Effects)
                    {
                        if (child?.Effect != null)
                        {
                            AccumulateEffect(
                                child.Effect, enemy, hero,
                                ref totalDamage, ref hitCount, ref totalShield, ref totalWillDrain, ref totalHeal,
                                ref dotDamage, ref dotDuration, ref summonCount, ref summonName,
                                ref buffPrimaryValue, ref hasBuff, ref hasDebuff, detailClauses);
                        }
                    }
                    break;
            }
        }

        private static string FormatBuffName(BuffType type)
        {
            return type switch
            {
                BuffType.DamageReduction => "Damage Reduction",
                BuffType.DamageReductionPercentage => "% Damage Reduction",
                BuffType.FearImmunity => "Will Immunity",
                BuffType.PlatedArmor => "Plated Armor",
                _ => type.ToString()
            };
        }
    }
}

