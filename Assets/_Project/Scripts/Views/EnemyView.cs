using System;
using System.Collections.Generic;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Data;
using TMPro;
using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    public class EnemyView : CombatantView
    {
        [SerializeField] private TMP_Text attackText;

        public EnemyData Data { get; private set; }
        public EnemyIntent NextIntent { get; private set; }
        public EnemyIntentSnapshot CurrentIntentSnapshot { get; private set; }
        public bool IsIntentVisible { get; private set; }

        /// <summary>
        /// Set by EnemyIntentHUD when the modern screen-space icon HUD is active so legacy 3D text does not overlap.
        /// </summary>
        public static bool SuppressLegacyIntentText { get; set; }

        public event Action<EnemyView, float> IntentRevealed;
        public event Action<EnemyView> IntentUpdated;
        public event Action<EnemyView> IntentConsumed;

        public static event Action<EnemyView, float> AnyIntentRevealed;
        public static event Action<EnemyView> AnyIntentUpdated;
        public static event Action<EnemyView> AnyIntentConsumed;

        private int currentIntentIndex = -1;
        private int turnCounter = 0;
        private int consecutiveIntentUses = 0;
        private readonly System.Collections.Generic.Dictionary<int, int> intentCooldowns = new();
        private BuffSystem subscribedBuffSystem;

        // Live runtime state for visible enemy passive traits
        public int ModeShiftDamageRemaining { get; private set; }
        public int ModeShiftShieldReward { get; private set; }
        public int ModeShiftCurrentThreshold { get; private set; }
        public int TimeWarpCount { get; private set; }
        public int TimeWarpLimit { get; private set; }

        private void OnEnable()
        {
            StatusEffectsChanged += RefreshIntentDisplay;
            TrySubscribeBuffSystem();
        }

        private void OnDisable()
        {
            StatusEffectsChanged -= RefreshIntentDisplay;
            if (subscribedBuffSystem != null)
            {
                subscribedBuffSystem.StatusEffectsChanged -= RefreshIntentDisplay;
                subscribedBuffSystem = null;
            }
        }

        public void Setup(EnemyData enemyData)
        {
            Data = enemyData;
            currentIntentIndex = -1;
            turnCounter = 0;
            consecutiveIntentUses = 0;
            intentCooldowns.Clear();
            NextIntent = null;
            CurrentIntentSnapshot = null;
            IsIntentVisible = false;
            InitializePassiveState(enemyData);

            SetupBase(Mathf.Max(1, enemyData.Health), enemyData.Image);

            if (spriteRenderer != null)
            {
                spriteRenderer.transform.localPosition = enemyData.SpriteOffset;
            }
            CaptureProceduralVisualHomeIfNeeded(forceRecapture: true);

            TrySubscribeBuffSystem();

            // If summoned in the middle of the Enemy Turn, defer intent reveal until Player Turn starts.
            if (EnemySystem.Instance != null && EnemySystem.Instance.IsEnemyTurnActive)
            {
                RefreshLegacyText();
                return;
            }

            // If the battle entrance choreography is staging enemies off-screen, hold the intent reveal
            // until CombatSceneEntranceDirector cues each enemy's staggered pop-in after landing.
            if (PlushieOdyssey.Vfx.CombatSceneEntranceDirector.Instance != null &&
                !PlushieOdyssey.Vfx.CombatSceneEntranceDirector.Instance.HasCompletedEntrance)
            {
                RefreshLegacyText();
                return;
            }

            int staggerIndex = EnemySystem.Instance != null ? EnemySystem.Instance.Enemies.Count : 0;
            PickNextIntent(animateReveal: true, revealDelay: 0.14f + staggerIndex * 0.10f);
        }

        public void RevealEntranceIntent(float revealDelay)
        {
            if (IsIntentVisible && NextIntent != null) return;
            PickNextIntent(animateReveal: true, revealDelay: revealDelay);
        }

        public void PickNextIntent()
        {
            PickNextIntent(animateReveal: true, revealDelay: 0f);
        }

        public void PickNextIntent(bool animateReveal, float revealDelay = 0f)
        {
            if (Data != null && Data.Intents != null && Data.Intents.Count > 0)
            {
                turnCounter++;
                TickCooldowns();

                if (Data.AttackPattern == AIAttackPattern.SequentialLoop)
                {
                    currentIntentIndex = (currentIntentIndex + 1) % Data.Intents.Count;
                }
                else if (Data.AttackPattern == AIAttackPattern.SequentialStop)
                {
                    if (currentIntentIndex < Data.Intents.Count - 1)
                    {
                        currentIntentIndex++;
                    }
                }
                else if (Data.AttackPattern == AIAttackPattern.RandomNoRepeat && Data.Intents.Count > 1)
                {
                    int newIndex = currentIntentIndex;
                    while (newIndex == currentIntentIndex)
                    {
                        newIndex = UnityEngine.Random.Range(0, Data.Intents.Count);
                    }
                    currentIntentIndex = newIndex;
                }
                else if (Data.AttackPattern == AIAttackPattern.ConditionalWeighted)
                {
                    currentIntentIndex = PickConditionalWeightedIndex();
                }
                else // Random
                {
                    currentIntentIndex = UnityEngine.Random.Range(0, Data.Intents.Count);
                }

                NextIntent = Data.Intents[currentIntentIndex];
                if (NextIntent != null && NextIntent.CooldownTurns > 0)
                {
                    intentCooldowns[currentIntentIndex] = NextIntent.CooldownTurns;
                }
            }
            else
            {
                NextIntent = null;
            }

            IsIntentVisible = NextIntent != null || IsStunned;
            CurrentIntentSnapshot = IsIntentVisible ? EnemyIntentSnapshot.Build(this, NextIntent) : null;
            RefreshLegacyText();

            if (IsIntentVisible)
            {
                if (animateReveal)
                {
                    IntentRevealed?.Invoke(this, revealDelay);
                    AnyIntentRevealed?.Invoke(this, revealDelay);
                }
                else
                {
                    IntentUpdated?.Invoke(this);
                    AnyIntentUpdated?.Invoke(this);
                }
            }
        }

        /// <summary>
        /// Clears and hides the enemy's current intent as it executes during the Enemy Turn.
        /// The next intent will not be picked until the next Player Turn begins.
        /// </summary>
        public void ConsumeIntentForEnemyTurn()
        {
            NextIntent = null;
            CurrentIntentSnapshot = null;
            IsIntentVisible = false;
            RefreshLegacyText();

            IntentConsumed?.Invoke(this);
            AnyIntentConsumed?.Invoke(this);
        }

        public void RefreshIntentDisplay()
        {
            TrySubscribeBuffSystem();

            if (!IsIntentVisible && !IsStunned)
            {
                CurrentIntentSnapshot = null;
                RefreshLegacyText();
                return;
            }

            if (IsIntentVisible || IsStunned)
            {
                CurrentIntentSnapshot = EnemyIntentSnapshot.Build(this, NextIntent);
                RefreshLegacyText();
                IntentUpdated?.Invoke(this);
                AnyIntentUpdated?.Invoke(this);
            }
        }

        public Vector3 GetIntentAnchorWorldPosition()
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Bounds b = spriteRenderer.bounds;
                if (b.size.y > 0.05f)
                {
                    return new Vector3(transform.position.x, b.max.y + 0.16f, transform.position.z);
                }
            }

            return transform.position + new Vector3(0f, 1.55f, 0f);
        }

        private void RefreshLegacyText()
        {
            if (attackText == null) return;

            if (SuppressLegacyIntentText)
            {
                attackText.text = string.Empty;
                return;
            }

            if (!IsIntentVisible || CurrentIntentSnapshot == null)
            {
                attackText.text = string.Empty;
                return;
            }

            string hex = ColorUtility.ToHtmlStringRGB(CurrentIntentSnapshot.AccentColor);
            string valuePart = string.IsNullOrEmpty(CurrentIntentSnapshot.ValueLabel)
                ? string.Empty
                : $" {CurrentIntentSnapshot.ValueLabel}";
            attackText.text = $"{CurrentIntentSnapshot.EnemyDisplayName}\n<color=#{hex}><b>[{CurrentIntentSnapshot.ActionLabel}{valuePart}]</b></color>";
        }

        private void TrySubscribeBuffSystem()
        {
            if (BuffSystem.Instance == null || subscribedBuffSystem == BuffSystem.Instance) return;
            if (subscribedBuffSystem != null)
                subscribedBuffSystem.StatusEffectsChanged -= RefreshIntentDisplay;

            subscribedBuffSystem = BuffSystem.Instance;
            subscribedBuffSystem.StatusEffectsChanged += RefreshIntentDisplay;
        }

        private void InitializePassiveState(EnemyData enemyData)
        {
            ModeShiftDamageRemaining = 0;
            ModeShiftShieldReward = 0;
            ModeShiftCurrentThreshold = 0;
            TimeWarpCount = 0;
            TimeWarpLimit = 0;

            if (enemyData?.PassiveTraits == null) return;
            for (int i = 0; i < enemyData.PassiveTraits.Count; i++)
            {
                EnemyPassiveTrait trait = enemyData.PassiveTraits[i];
                if (trait == null) continue;
                if (trait.PassiveType == EnemyPassiveType.ModeShift)
                {
                    ModeShiftCurrentThreshold = Mathf.Max(1, trait.Value);
                    ModeShiftDamageRemaining = ModeShiftCurrentThreshold;
                    ModeShiftShieldReward = Mathf.Max(1, trait.SecondaryValue > 0 ? trait.SecondaryValue : 12);
                }
                else if (trait.PassiveType == EnemyPassiveType.TimeWarp)
                {
                    TimeWarpLimit = Mathf.Max(2, trait.Value);
                    TimeWarpCount = 0;
                }
            }
        }

        public bool RecordModeShiftDamage(int damage, out int shieldReward)
        {
            shieldReward = 0;
            if (ModeShiftCurrentThreshold <= 0 || damage <= 0) return false;
            ModeShiftDamageRemaining -= damage;
            if (ModeShiftDamageRemaining <= 0)
            {
                shieldReward = ModeShiftShieldReward;
                ModeShiftCurrentThreshold += 10;
                ModeShiftDamageRemaining = ModeShiftCurrentThreshold;
                return true;
            }
            return false;
        }

        public bool IncrementTimeWarp(int limit)
        {
            TimeWarpLimit = Mathf.Max(2, limit);
            TimeWarpCount++;
            if (TimeWarpCount >= TimeWarpLimit)
            {
                TimeWarpCount = 0;
                return true;
            }
            return false;
        }

        private void TickCooldowns()
        {
            if (intentCooldowns.Count == 0) return;
            var keys = new System.Collections.Generic.List<int>(intentCooldowns.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int k = keys[i];
                intentCooldowns[k] = Mathf.Max(0, intentCooldowns[k] - 1);
            }
        }

        private int PickConditionalWeightedIndex()
        {
            int count = Data.Intents.Count;
            if (count <= 1) return 0;

            float hpPct = MaxHealth > 0 ? Mathf.Clamp01((float)CurrentHealth / MaxHealth) : 1f;

            // Turn 1 opener priority
            if (turnCounter == 1)
            {
                for (int i = 0; i < count; i++)
                {
                    EnemyIntent intent = Data.Intents[i];
                    if (intent != null && intent.TurnOneOpener)
                    {
                        UpdateConsecutiveTracker(i);
                        return i;
                    }
                }
            }

            var eligible = new System.Collections.Generic.List<int>();
            var hpFallback = new System.Collections.Generic.List<int>();

            for (int i = 0; i < count; i++)
            {
                EnemyIntent intent = Data.Intents[i];
                if (intent == null) continue;

                float minHp = Mathf.Clamp01(intent.MinHpPercent);
                float maxHp = intent.MaxHpPercent <= 0f ? 1f : Mathf.Clamp01(intent.MaxHpPercent);
                if (hpPct < minHp || hpPct > maxHp) continue;

                hpFallback.Add(i);

                if (intentCooldowns.TryGetValue(i, out int cd) && cd > 0) continue;
                if (intent.MaxConsecutiveUses > 0 && i == currentIntentIndex && consecutiveIntentUses >= intent.MaxConsecutiveUses) continue;

                eligible.Add(i);
            }

            System.Collections.Generic.List<int> pool = eligible.Count > 0 ? eligible : hpFallback;
            if (pool.Count == 0)
            {
                int fallbackIdx = UnityEngine.Random.Range(0, count);
                UpdateConsecutiveTracker(fallbackIdx);
                return fallbackIdx;
            }

            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                totalWeight += Mathf.Max(0.05f, Data.Intents[pool[i]].Weight);
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float accum = 0f;
            int chosen = pool[pool.Count - 1];
            for (int i = 0; i < pool.Count; i++)
            {
                accum += Mathf.Max(0.05f, Data.Intents[pool[i]].Weight);
                if (roll <= accum)
                {
                    chosen = pool[i];
                    break;
                }
            }

            UpdateConsecutiveTracker(chosen);
            return chosen;
        }

        private void UpdateConsecutiveTracker(int chosenIndex)
        {
            if (chosenIndex == currentIntentIndex)
                consecutiveIntentUses++;
            else
                consecutiveIntentUses = 1;
        }
    }
}
