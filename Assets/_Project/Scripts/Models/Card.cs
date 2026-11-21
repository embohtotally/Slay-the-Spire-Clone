using PlushieOdyssey.Audio;
using PlushieOdyssey.Data;
using PlushieOdyssey.Vfx;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Models
{
    public class Card
    {
        private readonly CardData data;

        public CardData SourceData => data;
        public string Title { get; private set; }
        public string Description { get; private set; }
        public Effect ManualTargetEffect { get; private set; }
        public CardType Type { get; private set; }
        public CardKeywords Keywords { get; private set; }
        public string OwnerCharacterId { get; private set; }
        public CardTargetType TargetType { get; private set; }
        public List<AutoTargetEffect> OtherEffects { get; private set; }
        public bool PersistPowerInCombat { get; private set; }
        public List<PowerPassiveDefinition> PowerPassives { get; private set; }
        public List<AutoTargetEffect> OnDrawEffects { get; private set; }
        public List<AutoTargetEffect> EndTurnInHandEffects { get; private set; }
        public Sprite Image { get; private set; }
        public Tier Tier { get; private set; }
        public int Mana { get; private set; }
        public int XBonus { get; private set; }
        public SfxID HoverSfx { get; private set; }
        public SfxID PlaySfx { get; private set; }
        public GameObject PlayParticle { get; private set; }
        public VfxCue PlayVfx { get; private set; }
        public CardVisualType VisualType { get; private set; }
        public string HeroAnimationTrigger { get; private set; }
        public string TargetAnimationTrigger { get; private set; }

        // Combat-scoped & turn-scoped runtime modifiers (never mutate CardData assets)
        public int TurnManaModifier { get; set; }
        public int CombatManaModifier { get; set; }
        public int TurnsRetained { get; set; }
        public bool IsUpgradedInCombat { get; private set; }
        public int CombatUpgradeBonus { get; private set; }

        /// <summary>
        /// Initialization of a new generic Card based on its ScriptableObject
        /// </summary>
        /// <param name="cardData"></param>
        public Card(CardData cardData)
        {
            data = cardData;
            Image = data.Image;
            Tier = data.Tier;
            Title = data.Title;
            Description = data.Description;
            Mana = data.Mana;
            XBonus = Mathf.Max(0, data.XBonus);
            Type = data.Type;
            Keywords = data.Keywords;
            OwnerCharacterId = ResolveOwnerCharacterId(data);
            ManualTargetEffect = data.ManualTargetEffect;
            TargetType = data.TargetType;
            OtherEffects = data.OtherEffects ?? new List<AutoTargetEffect>();
            PersistPowerInCombat = data.PersistPowerInCombat;
            PowerPassives = data.PowerPassives ?? new List<PowerPassiveDefinition>();
            OnDrawEffects = data.OnDrawEffects ?? new List<AutoTargetEffect>();
            EndTurnInHandEffects = data.EndTurnInHandEffects ?? new List<AutoTargetEffect>();
            HoverSfx = data.HoverSfx;
            PlaySfx = data.PlaySfx;
            PlayParticle = data.PlayParticle;
            PlayVfx = data.PlayVfx;
            VisualType = data.VisualType;
            HeroAnimationTrigger = data.HeroAnimationTrigger;
            TargetAnimationTrigger = data.TargetAnimationTrigger;
        }

        private static string ResolveOwnerCharacterId(CardData source)
        {
            if (source == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(source.OwnerCharacterId)) return source.OwnerCharacterId.Trim();
            string assetName = source.name ?? string.Empty;
            if (assetName.IndexOf("Ember", StringComparison.OrdinalIgnoreCase) >= 0) return "Ember";
            if (assetName.IndexOf("Aura", StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetName.IndexOf("Aira", StringComparison.OrdinalIgnoreCase) >= 0) return "Aira";
            return string.Empty;
        }

        public bool HasKeyword(CardKeywords keyword) => (Keywords & keyword) != 0;
        public bool IsXCost => HasKeyword(CardKeywords.XCost) || Mana < 0;
        public bool IsPersistentPower => Type == CardType.Power || PersistPowerInCombat || (PowerPassives != null && PowerPassives.Count > 0);
        public bool IsStatusOrCurse => HasKeyword(CardKeywords.Status) || HasKeyword(CardKeywords.Curse);
        public string CostLabel => IsXCost ? "X" : Mathf.Max(0, Mana - CombatManaModifier - TurnManaModifier).ToString();
        public int GetEffectiveX(int manaSpent) => IsXCost ? Mathf.Max(0, manaSpent + XBonus) : 1;

        public void ResetTurnModifiers()
        {
            TurnManaModifier = 0;
        }

        /// <summary>
        /// Temporarily upgrades this card instance for the remainder of the current battle.
        /// Reduces mana cost by 1 (minimum 0) or grants +1 XBonus for X-cost cards, plus a +3 combat potency bonus.
        /// </summary>
        public bool UpgradeInCombat()
        {
            if (IsUpgradedInCombat || IsStatusOrCurse || HasKeyword(CardKeywords.Unplayable)) return false;

            IsUpgradedInCombat = true;
            CombatUpgradeBonus += 3;
            if (!Title.EndsWith("+", StringComparison.Ordinal))
            {
                Title += "+";
            }

            if (IsXCost)
            {
                XBonus += 1;
            }
            else if (Mana > 0)
            {
                CombatManaModifier += 1;
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                Description += IsXCost
                    ? " <color=#3CFF7A>(+1 X)</color>"
                    : " <color=#3CFF7A>(Upgraded +3)</color>";
            }

            return true;
        }
    }
}
