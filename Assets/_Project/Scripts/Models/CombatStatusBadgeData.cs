using System.Collections.Generic;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Run;
using UnityEngine;

namespace PlushieOdyssey.Combat.Models
{
    public enum StatusIconKey
    {
        Buff,
        Attack,
        Defend,
        Weak,
        Vulnerable,
        Poison,
        Burn,
        Thorns,
        Stun,
        WillPositive,
        WillFear,
        Intangible,
        Unknown
    }

    /// <summary>
    /// Deduplicated, stacked representation of a single active buff or debuff on a combatant
    /// (Hero or Enemy), used by both Top Bar portrait status rows and Enemy top-of-intent status rows.
    /// </summary>
    public sealed class CombatStatusBadgeData
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string ShortCode { get; private set; }
        public string StackLabel { get; private set; }
        public string Description { get; private set; }
        public string DurationFooter { get; private set; }
        public int StackValue { get; private set; }
        public int RemainingTurns { get; private set; }
        public bool IsDebuff { get; private set; }
        public StatusIconKey IconKey { get; private set; }
        public Color AccentColor { get; private set; }

        public static List<CombatStatusBadgeData> CollectFor(CombatantView target, bool includeWillState = false)
        {
            var results = new List<CombatStatusBadgeData>();
            if (target == null) return results;

            // 1. Will state badge (Hero only — uses slot 0's modular HeroWillProfile when present)
            if (includeWillState && target is HeroView hero)
            {
                RunHeroStressState primaryState = hero.GetWillState(0);
                if (hero.IsHighWill)
                {
                    results.Add(CreateHighWillBadge(primaryState));
                }
                else if (hero.IsLowWill)
                {
                    results.Add(CreateLowWillBadge(primaryState));
                }
            }

            // 2. Stun & Taunt
            if (target.IsStunned)
            {
                int turns = Mathf.Max(1, target.StunDuration);
                results.Add(new CombatStatusBadgeData
                {
                    Id = "Stun",
                    Title = "Stunned",
                    ShortCode = "ST",
                    StackLabel = turns.ToString(),
                    StackValue = turns,
                    RemainingTurns = turns,
                    IsDebuff = true,
                    IconKey = StatusIconKey.Stun,
                    AccentColor = new Color(0.55f, 0.84f, 1.00f, 1f),
                    Description = "Cannot act and will skip its upcoming turn.",
                    DurationFooter = FormatTurns(turns)
                });
            }

            if (target.IsTaunted)
            {
                int turns = Mathf.Max(1, target.TauntDuration);
                results.Add(new CombatStatusBadgeData
                {
                    Id = "Taunt",
                    Title = "Taunt",
                    ShortCode = "TN",
                    StackLabel = turns.ToString(),
                    StackValue = turns,
                    RemainingTurns = turns,
                    IsDebuff = false,
                    IconKey = StatusIconKey.Defend,
                    AccentColor = new Color(1.00f, 0.78f, 0.28f, 1f),
                    Description = "Forces single-target attacks to target this unit.",
                    DurationFooter = FormatTurns(turns)
                });
            }

            // 3. BuffSystem entries grouped strictly by BuffType so identical buffs NEVER spawn duplicate icons
            if (BuffSystem.Instance != null)
            {
                var orderedTypes = new List<BuffType>();
                var valueSum = new Dictionary<BuffType, int>();
                var durationSum = new Dictionary<BuffType, int>();
                var maxDuration = new Dictionary<BuffType, int>();

                foreach (BuffData buff in BuffSystem.Instance.GetBuffsFor(target))
                {
                    if (buff == null || buff.RemainingTurns <= 0) continue;

                    if (!valueSum.ContainsKey(buff.Type))
                    {
                        orderedTypes.Add(buff.Type);
                        valueSum[buff.Type] = 0;
                        durationSum[buff.Type] = 0;
                        maxDuration[buff.Type] = 0;
                    }

                    valueSum[buff.Type] += Mathf.Max(1, buff.Value);
                    durationSum[buff.Type] = buff.RemainingTurns >= 900 || durationSum[buff.Type] >= 900
                        ? 999
                        : durationSum[buff.Type] + buff.RemainingTurns;
                    if (buff.RemainingTurns > maxDuration[buff.Type])
                    {
                        maxDuration[buff.Type] = buff.RemainingTurns;
                    }
                }

                for (int i = 0; i < orderedTypes.Count; i++)
                {
                    BuffType type = orderedTypes[i];
                    results.Add(BuildBuffBadge(type, valueSum[type], durationSum[type], maxDuration[type]));
                }
            }

            // 4. Damage Over Time entries merged into ONE stacked icon
            if (DamageOverTimeSystem.Instance != null)
            {
                int totalDotDamage = 0;
                int longestDotTurns = 0;

                foreach (DamageOverTimeData dot in DamageOverTimeSystem.Instance.GetDoTsFor(target))
                {
                    if (dot == null || dot.RemainingTurns <= 0) continue;
                    totalDotDamage += Mathf.Max(1, dot.DamagePerTurn);
                    if (dot.RemainingTurns > longestDotTurns) longestDotTurns = dot.RemainingTurns;
                }

                if (totalDotDamage > 0)
                {
                    results.Add(new CombatStatusBadgeData
                    {
                        Id = "DoT",
                        Title = "Affliction (DoT)",
                        ShortCode = "DT",
                        StackLabel = totalDotDamage.ToString(),
                        StackValue = totalDotDamage,
                        RemainingTurns = longestDotTurns,
                        IsDebuff = true,
                        IconKey = StatusIconKey.Burn,
                        AccentColor = new Color(0.84f, 0.42f, 1.00f, 1f),
                        Description = $"Takes <color=#E48BFF><b>{totalDotDamage}</b></color> damage at the start of its turn.",
                        DurationFooter = FormatTurns(longestDotTurns)
                    });
                }
            }

            return results;
        }

        public static List<CombatStatusBadgeData> CollectWillOnly(RunHeroStressState state)
        {
            var results = new List<CombatStatusBadgeData>();
            if (state == null) return results;
            if (state.IsHighWill) results.Add(CreateHighWillBadge(state));
            else if (state.IsLowWill) results.Add(CreateLowWillBadge(state));
            return results;
        }

        private static CombatStatusBadgeData CreateHighWillBadge(RunHeroStressState state = null)
        {
            Data.HeroWillProfile profile = state?.WillProfile;
            int thresholdPct = Mathf.RoundToInt((state != null ? state.HighThreshold : RunHeroStressState.HighWillThreshold) * 100f);
            int bonusPct = Mathf.RoundToInt(((state != null ? state.HighWillBonusMultiplier : 1.25f) - 1f) * 100f);
            string title = profile != null ? profile.HighWillTitle : "High Will";
            string desc = profile != null
                ? profile.FormatHighWillDescription()
                : $"Will ≥ {thresholdPct}%: Grants <color=#64E8FF><b>+{bonusPct}% attack damage</b></color> and <color=#64E8FF><b>+{bonusPct}% Shield gain</b></color>.";

            return new CombatStatusBadgeData
            {
                Id = "HighWill",
                Title = title,
                ShortCode = "W+",
                StackLabel = string.Empty,
                StackValue = bonusPct,
                RemainingTurns = 999,
                IsDebuff = false,
                IconKey = StatusIconKey.WillPositive,
                AccentColor = profile != null && profile.UseCustomPalette
                    ? profile.HighWillBorderColor
                    : new Color(0.24f, 0.84f, 1.00f, 1f),
                Description = desc,
                DurationFooter = $"Active while Will ≥ {thresholdPct}%"
            };
        }

        private static CombatStatusBadgeData CreateLowWillBadge(RunHeroStressState state = null)
        {
            Data.HeroWillProfile profile = state?.WillProfile;
            int thresholdPct = Mathf.RoundToInt((state != null ? state.LowThreshold : RunHeroStressState.LowWillThreshold) * 100f);
            int penaltyPct = Mathf.RoundToInt((state != null ? state.LowWillPenaltyFraction : 0.20f) * 100f);
            string title = profile != null ? profile.LowWillTitle : "Fear (Low Will)";
            string desc = profile != null
                ? profile.FormatLowWillDescription()
                : $"Will < {thresholdPct}%: Effective Max HP is reduced by <color=#FF6B6B><b>{penaltyPct}% Fear</b></color> until Will recovers.";

            return new CombatStatusBadgeData
            {
                Id = "LowWill",
                Title = title,
                ShortCode = "F!",
                StackLabel = string.Empty,
                StackValue = penaltyPct,
                RemainingTurns = 999,
                IsDebuff = true,
                IconKey = StatusIconKey.WillFear,
                AccentColor = profile != null && profile.UseCustomPalette
                    ? profile.LowWillBorderColor
                    : new Color(1.00f, 0.34f, 0.38f, 1f),
                Description = desc,
                DurationFooter = $"Active while Will < {thresholdPct}%"
            };
        }

        private static CombatStatusBadgeData BuildBuffBadge(BuffType type, int totalValue, int summedTurns, int maxTurns)
        {
            bool isDebuff = BuffSystem.IsDebuff(type) || type == BuffType.DamageReductionPercentage;
            bool isDurationStacked = type == BuffType.Weak || type == BuffType.Vulnerable || type == BuffType.Frail ||
                                     type == BuffType.Intangible || type == BuffType.FearImmunity || type == BuffType.Redirect;

            int shownNumber = isDurationStacked ? summedTurns : totalValue;
            int shownTurns = isDurationStacked ? summedTurns : maxTurns;
            string stackLabel = type == BuffType.Barricade || shownNumber >= 900
                ? string.Empty
                : shownNumber.ToString();

            (string title, string code, StatusIconKey icon, Color color, string desc) = type switch
            {
                BuffType.Strength => (
                    "Strength", "ST", StatusIconKey.Attack, new Color(1.00f, 0.62f, 0.24f, 1f),
                    $"Increases outgoing attack damage by <color=#FFD659><b>+{totalValue}</b></color>."),

                BuffType.Dexterity => (
                    "Dexterity", "DX", StatusIconKey.Defend, new Color(0.32f, 0.88f, 0.62f, 1f),
                    $"Increases Shield gained from cards by <color=#59E892><b>+{totalValue}</b></color>."),

                BuffType.DamageReduction => (
                    "Damage Reduction", "DR", StatusIconKey.Defend, new Color(0.32f, 0.78f, 1.00f, 1f),
                    $"Reduces incoming attack damage by <color=#56D0FF><b>{totalValue}</b></color>."),

                BuffType.DamageReductionPercentage => (
                    "Attack Down", "AD", StatusIconKey.Weak, new Color(0.78f, 0.48f, 1.00f, 1f),
                    $"Reduces outgoing attack damage by <color=#D18BFF><b>{totalValue}%</b></color>."),

                BuffType.Weak => (
                    "Weak", "WK", StatusIconKey.Weak, new Color(0.76f, 0.44f, 1.00f, 1f),
                    "Deals <color=#D18BFF><b>25% less</b></color> attack damage."),

                BuffType.Vulnerable => (
                    "Vulnerable", "VN", StatusIconKey.Vulnerable, new Color(1.00f, 0.36f, 0.36f, 1f),
                    "Takes <color=#FF6B6B><b>50% more</b></color> damage from attacks."),

                BuffType.Frail => (
                    "Frail", "FR", StatusIconKey.Vulnerable, new Color(0.86f, 0.42f, 0.72f, 1f),
                    "Gains <color=#FF8CC8><b>25% less</b></color> Shield from cards."),

                BuffType.Poison => (
                    "Poison", "PS", StatusIconKey.Poison, new Color(0.46f, 0.90f, 0.32f, 1f),
                    $"Takes <color=#7BE85F><b>{totalValue}</b></color> Poison damage at turn start, then decreases by 1."),

                BuffType.Thorns => (
                    "Thorns", "TH", StatusIconKey.Thorns, new Color(1.00f, 0.78f, 0.28f, 1f),
                    $"Deals <color=#FFD659><b>{totalValue}</b></color> damage back whenever struck by an attack."),

                BuffType.Metallicize => (
                    "Metallicize", "MT", StatusIconKey.Defend, new Color(0.46f, 0.80f, 1.00f, 1f),
                    $"Gains <color=#56D0FF><b>{totalValue}</b></color> Shield automatically at the end of turn."),

                BuffType.PlatedArmor => (
                    "Plated Armor", "PA", StatusIconKey.Defend, new Color(0.42f, 0.84f, 0.96f, 1f),
                    $"Gains <color=#56D0FF><b>{totalValue}</b></color> Shield at turn end. Decreases by 1 when losing unblocked HP."),

                BuffType.Artifact => (
                    "Artifact", "AR", StatusIconKey.Buff, new Color(1.00f, 0.84f, 0.36f, 1f),
                    $"Negates the next <color=#FFD659><b>{totalValue}</b></color> incoming debuff(s)."),

                BuffType.Intangible => (
                    "Intangible", "IN", StatusIconKey.Intangible, new Color(0.60f, 0.92f, 1.00f, 1f),
                    "Reduces all incoming damage and HP loss to <color=#A0E6FF><b>1</b></color>."),

                BuffType.Barricade => (
                    "Barricade", "BR", StatusIconKey.Defend, new Color(0.36f, 0.82f, 1.00f, 1f),
                    "Shield is no longer removed at the start of the turn."),

                BuffType.FearImmunity => (
                    "Will Shield", "WI", StatusIconKey.WillPositive, new Color(0.36f, 0.88f, 1.00f, 1f),
                    "Immune to incoming Will drain and Fear effects."),

                BuffType.Redirect => (
                    "Redirect", "RD", StatusIconKey.Defend, new Color(0.42f, 0.86f, 0.68f, 1f),
                    "Redirects incoming attacks to this protector."),

                _ => (
                    type.ToString(), "??", StatusIconKey.Unknown, new Color(0.80f, 0.84f, 0.90f, 1f),
                    $"Active status effect ({totalValue}).")
            };

            string footer = type == BuffType.Poison
                ? $"{totalValue} stacks"
                : (shownTurns >= 900 ? "Lasts for combat" : FormatTurns(shownTurns));

            return new CombatStatusBadgeData
            {
                Id = $"Buff_{type}",
                Title = title,
                ShortCode = code,
                StackLabel = stackLabel,
                StackValue = shownNumber,
                RemainingTurns = shownTurns,
                IsDebuff = isDebuff,
                IconKey = icon,
                AccentColor = color,
                Description = desc,
                DurationFooter = footer
            };
        }

        private static string FormatTurns(int turns)
        {
            if (turns <= 0) return string.Empty;
            return turns == 1 ? "1 turn remaining" : $"{turns} turns remaining";
        }
    }
}

