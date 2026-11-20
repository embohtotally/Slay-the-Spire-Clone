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
        public CardTargetType TargetType { get; private set; }
        public List<AutoTargetEffect> OtherEffects { get; private set; }
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
            ManualTargetEffect = data.ManualTargetEffect;
            TargetType = data.TargetType;
            OtherEffects = data.OtherEffects;
            HoverSfx = data.HoverSfx;
            PlaySfx = data.PlaySfx;
            PlayParticle = data.PlayParticle;
            PlayVfx = data.PlayVfx;
            VisualType = data.VisualType;
            HeroAnimationTrigger = data.HeroAnimationTrigger;
            TargetAnimationTrigger = data.TargetAnimationTrigger;
        }

        public bool HasKeyword(CardKeywords keyword) => (Keywords & keyword) != 0;
        public bool IsXCost => HasKeyword(CardKeywords.XCost) || Mana < 0;
        public string CostLabel => IsXCost ? "X" : Mathf.Max(0, Mana).ToString();
        public int GetEffectiveX(int manaSpent) => IsXCost ? Mathf.Max(0, manaSpent + XBonus) : 1;
    }
}
