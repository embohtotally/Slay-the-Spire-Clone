using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Data
{
    [CreateAssetMenu(menuName = "Data/Hero")]
    public class HeroData : ScriptableObject
    {
        [Header("Core Combat Data")]
        [field: SerializeField] public List<CardData> Deck { get; private set; }
        [field: SerializeField] public Sprite Image { get; private set; }
        [field: SerializeField] public int Health { get; private set; }
        [field: SerializeField] public List<PerkData> StartingPerks { get; private set; }

        [Header("Modular Character Identity & Portraits")]
        [SerializeField] private string characterId;
        [SerializeField] private string displayName;
        [Tooltip("Primary HUD portrait shown in the top-left party Will strip and character selection.")]
        [SerializeField] private Sprite portrait;
        [Tooltip("Optional portrait override when this character enters High Will (>= threshold).")]
        [SerializeField] private Sprite highWillPortrait;
        [Tooltip("Optional portrait override when this character enters Low Will / Fear (< threshold).")]
        [SerializeField] private Sprite lowWillPortrait;

        [Header("Modular Will Configuration")]
        [Tooltip("Optional ScriptableObject Will profile. Leave empty to use standard Will rules (Max 100, Start 50, >=75% High Will, <25% Fear).")]
        [SerializeField] private HeroWillProfile willProfile;

        public string CharacterId => string.IsNullOrWhiteSpace(characterId) ? name : characterId.Trim();
        public string EffectiveDisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
        public Sprite Portrait => portrait;
        public Sprite HighWillPortrait => highWillPortrait;
        public Sprite LowWillPortrait => lowWillPortrait;
        public HeroWillProfile WillProfile => willProfile;

        public int EffectiveMaxWill => willProfile != null ? willProfile.MaxWill : 100;
        public int EffectiveStartingWill => willProfile != null ? willProfile.StartingWill : 50;
        public float EffectiveHighWillThreshold => willProfile != null ? willProfile.HighWillThreshold : 0.75f;
        public float EffectiveLowWillThreshold => willProfile != null ? willProfile.LowWillThreshold : 0.25f;
        public float EffectiveHighWillBonusMultiplier => willProfile != null ? willProfile.HighWillBonusMultiplier : 1.25f;
        public float EffectiveLowWillPenaltyFraction => willProfile != null ? willProfile.LowWillMaxHealthPenaltyFraction : 0.20f;

        /// <summary>
        /// Resolves the appropriate HUD portrait sprite for this character's current Will state.
        /// Returns null when no HUD portrait is assigned so scene fallback portraits are preserved cleanly.
        /// </summary>
        public Sprite GetPortraitForWill(bool isHighWill, bool isLowWill, bool fallbackToWorldSprite = false)
        {
            if (isLowWill && lowWillPortrait != null) return lowWillPortrait;
            if (isHighWill && highWillPortrait != null) return highWillPortrait;
            if (portrait != null) return portrait;
            return fallbackToWorldSprite ? Image : null;
        }

        /// <summary>
        /// Runtime helper for tests or dynamic character builders.
        /// </summary>
        public void ConfigureRuntimeIdentity(
            string id,
            string label,
            Sprite defaultPortrait,
            Sprite highPortrait = null,
            Sprite lowPortrait = null,
            HeroWillProfile profile = null)
        {
            characterId = id;
            displayName = label;
            portrait = defaultPortrait;
            highWillPortrait = highPortrait;
            lowWillPortrait = lowPortrait;
            willProfile = profile;
        }
    }
}
