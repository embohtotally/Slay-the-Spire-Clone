using UnityEngine;

namespace PlushieOdyssey.Data
{
    public enum WillTier
    {
        Low,
        Neutral,
        High
    }

    /// <summary>
    /// Modular, character-configurable Will profile (assignable per HeroData).
    /// Controls a character's Will pool size, High-Will empowerment bonus, Low-Will Fear penalty,
    /// tooltip copy, and optional signature Will bar colors for future character selection.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Hero Will Profile")]
    public class HeroWillProfile : ScriptableObject
    {
        [Header("Will Pool & Thresholds")]
        [Min(1)] [SerializeField] private int maxWill = 100;
        [Min(0)] [SerializeField] private int startingWill = 50;
        [Range(0.5f, 0.99f)] [SerializeField] private float highWillThreshold = 0.75f;
        [Range(0.01f, 0.49f)] [SerializeField] private float lowWillThreshold = 0.25f;

        [Header("Modular Gameplay Effects")]
        [Tooltip("Outgoing attack damage & shield gain multiplier while Will >= High Will Threshold.")]
        [Min(1f)] [SerializeField] private float highWillBonusMultiplier = 1.25f;
        [Tooltip("Fraction of this character's Max HP penalized while Will < Low Will Threshold (Fear).")]
        [Range(0f, 0.8f)] [SerializeField] private float lowWillMaxHealthPenaltyFraction = 0.20f;

        [Header("Tooltip Display (Supports {threshold}, {bonus}, {penalty} Placeholders)")]
        [SerializeField] private string highWillTitle = "High Will";
        [TextArea(2, 3)]
        [SerializeField] private string highWillDescription =
            "Will ≥ {threshold}%: Grants <color=#64E8FF><b>+{bonus}% attack damage</b></color> and <color=#64E8FF><b>+{bonus}% Shield gain</b></color>.";
        [SerializeField] private string lowWillTitle = "Fear (Low Will)";
        [TextArea(2, 3)]
        [SerializeField] private string lowWillDescription =
            "Will < {threshold}%: Effective Max HP is reduced by <color=#FF6B6B><b>{penalty}% Fear</b></color> until Will recovers.";

        [Header("Optional Signature UI Palette")]
        [SerializeField] private bool useCustomPalette;
        [SerializeField] private Color highWillFillColor = new(0.14f, 0.68f, 1f, 1f);
        [SerializeField] private Color neutralWillFillColor = new(0.15f, 0.45f, 0.92f, 1f);
        [SerializeField] private Color lowWillFillColor = new(0.66f, 0.11f, 0.15f, 1f);
        [SerializeField] private Color normalBorderColor = new(0.78f, 0.65f, 0.36f, 1f);
        [SerializeField] private Color highWillBorderColor = new(0.32f, 0.86f, 1f, 1f);
        [SerializeField] private Color lowWillBorderColor = new(1f, 0.28f, 0.32f, 1f);

        public int MaxWill => Mathf.Max(1, maxWill);
        public int StartingWill => Mathf.Clamp(startingWill, 0, MaxWill);
        public float HighWillThreshold => Mathf.Clamp(highWillThreshold, 0.5f, 0.99f);
        public float LowWillThreshold => Mathf.Clamp(lowWillThreshold, 0.01f, Mathf.Min(0.49f, HighWillThreshold - 0.05f));
        public float HighWillBonusMultiplier => Mathf.Max(1f, highWillBonusMultiplier);
        public float LowWillMaxHealthPenaltyFraction => Mathf.Clamp(lowWillMaxHealthPenaltyFraction, 0f, 0.8f);

        public string HighWillTitle => string.IsNullOrWhiteSpace(highWillTitle) ? "High Will" : highWillTitle;
        public string LowWillTitle => string.IsNullOrWhiteSpace(lowWillTitle) ? "Fear (Low Will)" : lowWillTitle;

        public bool UseCustomPalette => useCustomPalette;
        public Color HighWillFillColor => highWillFillColor;
        public Color NeutralWillFillColor => neutralWillFillColor;
        public Color LowWillFillColor => lowWillFillColor;
        public Color NormalBorderColor => normalBorderColor;
        public Color HighWillBorderColor => highWillBorderColor;
        public Color LowWillBorderColor => lowWillBorderColor;

        public WillTier EvaluateTier(float normalizedWill)
        {
            if (normalizedWill < LowWillThreshold) return WillTier.Low;
            if (normalizedWill >= HighWillThreshold) return WillTier.High;
            return WillTier.Neutral;
        }

        public string FormatHighWillDescription()
        {
            int thresholdPct = Mathf.RoundToInt(HighWillThreshold * 100f);
            int bonusPct = Mathf.RoundToInt((HighWillBonusMultiplier - 1f) * 100f);
            string template = string.IsNullOrWhiteSpace(highWillDescription)
                ? "Will ≥ {threshold}%: Grants +{bonus}% attack damage and Shield gain."
                : highWillDescription;
            return template
                .Replace("{threshold}", thresholdPct.ToString())
                .Replace("{bonus}", bonusPct.ToString());
        }

        public string FormatLowWillDescription()
        {
            int thresholdPct = Mathf.RoundToInt(LowWillThreshold * 100f);
            int penaltyPct = Mathf.RoundToInt(LowWillMaxHealthPenaltyFraction * 100f);
            string template = string.IsNullOrWhiteSpace(lowWillDescription)
                ? "Will < {threshold}%: Effective Max HP is reduced by {penalty}% Fear until Will recovers."
                : lowWillDescription;
            return template
                .Replace("{threshold}", thresholdPct.ToString())
                .Replace("{penalty}", penaltyPct.ToString());
        }
    }
}

