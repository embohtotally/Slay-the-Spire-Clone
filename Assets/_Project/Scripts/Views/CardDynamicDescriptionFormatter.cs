using System.Text.RegularExpressions;
using PlushieOdyssey.Combat.Effects;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Data;
using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    /// <summary>
    /// Computes live, buff-adjusted card text directly for CardView in hand:
    /// tints boosted numbers green (#59FF8A) and weakened numbers red (#FF5C5C)
    /// when Strength, Dexterity, Weak, Frail, + High Will, In-Combat Upgrade, Combo stacks, or X Mana change.
    /// </summary>
    public static class CardDynamicDescriptionFormatter
    {
        private const string BoostedHex = "#59FF8A";
        private const string WeakenedHex = "#FF5C5C";
        private const string XAccentHex = "#FFE066";

        public static string FormatForHand(Card card, CombatantView previewTarget = null)
        {
            if (card == null) return string.Empty;
            string raw = card.Description ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            BuffSystem buffs = BuffSystem.Instance;
            int availableMana = ManaSystem.Instance != null ? ManaSystem.Instance.AvailableMana : 0;
            int effectiveX = card.GetEffectiveX(availableMana);
            int upgradeBonus = card.CombatUpgradeBonus;

            // Inspect primary damage & shield effects on the card
            TryExtractPrimaryNumbers(card, out int baseDamage, out bool isMultiHit, out bool multiScaleX, out int baseComboTotal, out int baseShield);

            string result = raw;

            // 1. Live-adjust Damage numbers ("Deal N damage" or "N damage")
            if (baseDamage > 0)
            {
                int rawForCalc = baseComboTotal > 0 ? baseComboTotal : baseDamage;
                rawForCalc += upgradeBonus;
                if (card.IsXCost && !multiScaleX)
                {
                    rawForCalc = effectiveX > 0 ? rawForCalc * effectiveX : 0;
                }

                int liveDamage = buffs != null
                    ? buffs.CalculateExpectedDamage(rawForCalc, hero, previewTarget)
                    : rawForCalc;

                string formatted = FormatAdjustedNumber(liveDamage, baseDamage);
                result = ReplaceDamageToken(result, baseDamage, formatted);
            }

            // 2. Live-adjust Shield numbers ("Gain N Shield" or "N Shield")
            if (baseShield > 0)
            {
                int rawShield = baseShield + upgradeBonus;
                if (card.IsXCost)
                {
                    rawShield = effectiveX > 0 ? rawShield * effectiveX : 0;
                }

                int liveShield = buffs != null
                    ? buffs.CalculateExpectedShield(rawShield, hero)
                    : rawShield;

                string formatted = FormatAdjustedNumber(liveShield, baseShield);
                result = ReplaceShieldToken(result, baseShield, formatted);
            }

            // 3. Live-adjust X-cost indicator so player sees exact current X value right on the card!
            if (card.IsXCost)
            {
                string xColor = effectiveX <= 0 ? WeakenedHex : (effectiveX >= 2 ? BoostedHex : XAccentHex);
                if (result.IndexOf("X times", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result = Regex.Replace(
                        result,
                        @"\bX times\b",
                        $"<color={xColor}><b>{effectiveX}</b></color> times",
                        RegexOptions.IgnoreCase);
                }
                else
                {
                    result += $"\n<color={xColor}><b>[X = {effectiveX}]</b></color>";
                }
            }

            return result;
        }

        private static string FormatAdjustedNumber(int liveValue, int baseValue)
        {
            if (liveValue > baseValue) return $"<color={BoostedHex}><b>{liveValue}</b></color>";
            if (liveValue < baseValue) return $"<color={WeakenedHex}><b>{liveValue}</b></color>";
            return liveValue.ToString();
        }

        private static string ReplaceDamageToken(string text, int baseVal, string replacement)
        {
            string pattern = $@"\b{baseVal}(\s+(?:extra\s+)?damage)\b";
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                return Regex.Replace(text, pattern, $"{replacement}$1", RegexOptions.IgnoreCase);
            }
            string dealPattern = $@"(\bDeal\s+){baseVal}\b";
            if (Regex.IsMatch(text, dealPattern, RegexOptions.IgnoreCase))
            {
                return Regex.Replace(text, dealPattern, $"$1{replacement}", RegexOptions.IgnoreCase);
            }
            return text;
        }

        private static string ReplaceShieldToken(string text, int baseVal, string replacement)
        {
            string pattern = $@"\b{baseVal}(\s+(?:Shield|Block))\b";
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                return Regex.Replace(text, pattern, $"{replacement}$1", RegexOptions.IgnoreCase);
            }
            return text;
        }

        private static void TryExtractPrimaryNumbers(
            Card card,
            out int baseDamage,
            out bool isMultiHit,
            out bool multiScaleX,
            out int comboEvaluatedBase,
            out int baseShield)
        {
            int localDamage = 0;
            bool localMulti = false;
            bool localMultiScaleX = false;
            int localComboBase = 0;
            int localShield = 0;

            void Inspect(Effect e)
            {
                if (e == null) return;
                switch (e)
                {
                    case DealDamageEffect dmg when localDamage == 0:
                        localDamage = dmg.DamageAmount;
                        break;
                    case BeamAttackEffect beam when localDamage == 0:
                        localDamage = beam.DamageAmount;
                        break;
                    case MultiHitDamageEffect multi when localDamage == 0:
                        localDamage = multi.DamagePerHit;
                        localMulti = true;
                        localMultiScaleX = multi.ScaleHitsWithX;
                        break;
                    case ComboDamageEffect combo when localDamage == 0:
                        localDamage = combo.BaseDamage;
                        localComboBase = combo.EvaluateDamage(card);
                        break;
                    case GainShieldEffect shield when localShield == 0:
                        localShield = shield.ShieldAmount;
                        break;
                    case SimultaneousEffect sim when sim.Effects != null:
                        foreach (AutoTargetEffect child in sim.Effects) Inspect(child?.Effect);
                        break;
                }
            }

            Inspect(card.ManualTargetEffect);
            if (card.OtherEffects != null)
            {
                for (int i = 0; i < card.OtherEffects.Count; i++)
                {
                    Inspect(card.OtherEffects[i]?.Effect);
                }
            }

            baseDamage = localDamage;
            isMultiHit = localMulti;
            multiScaleX = localMultiScaleX;
            comboEvaluatedBase = localComboBase;
            baseShield = localShield;
        }
    }
}

