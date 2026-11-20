using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Data
{
    [Serializable]
    public class HeroUltimateDefinition
    {
        public string Title = "Astral Surge";
        [Min(1)] public int UltimatePointCost = 3;
        [TextArea(2, 5)] public string Description = "Unleash this hero's signature Ultimate technique.";
        public Sprite Icon;
    }

    [Serializable]
    public class HeroTalentDefinition
    {
        public string Title = "Innate Blessing";
        [TextArea(2, 4)] public string Description = "Passive party effect active whenever this hero joins combat.";
        public Sprite Icon;
        public bool IsInnate = true;
        public bool IsUnlocked = true;
        public string UnlockRequirement = "Unlocked by Default";

        [Header("Optional Combat Innate Modifiers")]
        [Min(0)] public int BonusOpeningDraw = 0;
        [Min(0)] public int BonusStartingShield = 0;
    }

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
        [SerializeField] private string roleTitle = "ARCANE SUPPORT";
        [SerializeField] private Color themeColor = new(0.46f, 0.86f, 0.44f, 1f);
        [SerializeField] private RelicData startingRelic;
        [Tooltip("Primary HUD portrait shown in the top-left party Will strip and character selection.")]
        [SerializeField] private Sprite portrait;
        [Tooltip("Optional square roster icon used in Party Setup's bottom selector strip. Falls back to Portrait/Image.")]
        [SerializeField] private Sprite rosterIcon;
        [Tooltip("Optional portrait override when this character enters + High Will (>= threshold).")]
        [SerializeField] private Sprite highWillPortrait;
        [Tooltip("Optional portrait override when this character enters - Low Will (< threshold).")]
        [SerializeField] private Sprite lowWillPortrait;

        [Header("Party Setup Framing")]
        [SerializeField] private Vector2 tarotPortraitOffset = new(0f, -155f);
        [SerializeField, Min(0.25f)] private float tarotPortraitScale = 1.65f;
        [SerializeField] private Vector2 splashPortraitOffset = new(0f, -45f);
        [SerializeField, Min(0.25f)] private float splashPortraitScale = 1.08f;
        [SerializeField] private Vector2 rosterIconOffset = new(0f, -58f);
        [SerializeField, Min(0.25f)] private float rosterIconScale = 2.35f;

        [Header("Ultimate & Unlockable Talents")]
        [SerializeField] private HeroUltimateDefinition ultimate = new();
        [SerializeField] private List<HeroTalentDefinition> talents = new();

        [Header("Party Setup Animated Standing Sprite")]
        [SerializeField] private List<Sprite> previewIdleFrames = new();
        [SerializeField] private List<Sprite> previewActionFrames = new();
        [SerializeField, Min(0.5f)] private float previewIdleFps = 2.4f;
        [SerializeField, Min(1f)] private float previewActionFps = 8.5f;

        [Header("Modular Will Configuration")]
        [Tooltip("Optional ScriptableObject Will profile. Leave empty to use standard Will rules (Max 100, Start 50, >=75% + High Will, <25% - Low Will).")]
        [SerializeField] private HeroWillProfile willProfile;

        public string CharacterId => string.IsNullOrWhiteSpace(characterId) ? name : characterId.Trim();
        public string EffectiveDisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
        public string RoleTitle => string.IsNullOrWhiteSpace(roleTitle) ? "ADVENTURER" : roleTitle.Trim();
        public Color ThemeColor => themeColor.a > 0.01f ? themeColor : new Color(0.92f, 0.78f, 0.42f, 1f);
        public RelicData StartingRelic => startingRelic;
        public Sprite Portrait => portrait;
        public Sprite EffectiveRosterIcon => rosterIcon != null ? rosterIcon : (portrait != null ? portrait : Image);
        public Sprite HighWillPortrait => highWillPortrait;
        public Sprite LowWillPortrait => lowWillPortrait;
        public HeroWillProfile WillProfile => willProfile;

        public Vector2 TarotPortraitOffset => tarotPortraitOffset;
        public float TarotPortraitScale => tarotPortraitScale > 0.01f ? tarotPortraitScale : 1.65f;
        public Vector2 SplashPortraitOffset => splashPortraitOffset;
        public float SplashPortraitScale => splashPortraitScale > 0.01f ? splashPortraitScale : 1.08f;
        public Vector2 RosterIconOffset => rosterIconOffset;
        public float RosterIconScale => rosterIconScale > 0.01f ? rosterIconScale : 2.35f;

        public HeroUltimateDefinition Ultimate => ultimate;
        public IReadOnlyList<HeroTalentDefinition> Talents => talents;
        public IReadOnlyList<Sprite> PreviewIdleFrames => previewIdleFrames;
        public IReadOnlyList<Sprite> PreviewActionFrames => previewActionFrames;
        public float PreviewIdleFps => previewIdleFps > 0.1f ? previewIdleFps : 2.4f;
        public float PreviewActionFps => previewActionFps > 0.1f ? previewActionFps : 8.5f;

        public int EffectiveMaxWill => willProfile != null ? willProfile.MaxWill : 100;
        public int EffectiveStartingWill => willProfile != null ? willProfile.StartingWill : 50;
        public float EffectiveHighWillThreshold => willProfile != null ? willProfile.HighWillThreshold : 0.75f;
        public float EffectiveLowWillThreshold => willProfile != null ? willProfile.LowWillThreshold : 0.25f;
        public float EffectiveHighWillBonusMultiplier => willProfile != null ? willProfile.HighWillBonusMultiplier : 1.25f;
        public float EffectiveLowWillPenaltyFraction => willProfile != null ? willProfile.LowWillMaxHealthPenaltyFraction : 0.20f;

        /// <summary>
        /// Returns the character's primary innate talent (first marked IsInnate, else first entry).
        /// </summary>
        public HeroTalentDefinition GetInnateTalent()
        {
            if (talents == null || talents.Count == 0) return null;
            for (int i = 0; i < talents.Count; i++)
            {
                if (talents[i] != null && talents[i].IsInnate) return talents[i];
            }
            return talents[0];
        }

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
