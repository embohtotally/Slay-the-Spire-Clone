using System;
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
        private BuffSystem subscribedBuffSystem;

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
            NextIntent = null;
            CurrentIntentSnapshot = null;
            IsIntentVisible = false;

            SetupBase(enemyData.Health, enemyData.Image);

            if (spriteRenderer != null)
            {
                spriteRenderer.transform.localPosition = enemyData.SpriteOffset;
            }

            TrySubscribeBuffSystem();

            // If summoned in the middle of the Enemy Turn, defer intent reveal until Player Turn starts.
            if (EnemySystem.Instance != null && EnemySystem.Instance.IsEnemyTurnActive)
            {
                RefreshLegacyText();
                return;
            }

            int staggerIndex = EnemySystem.Instance != null ? EnemySystem.Instance.Enemies.Count : 0;
            PickNextIntent(animateReveal: true, revealDelay: 0.14f + staggerIndex * 0.10f);
        }

        public void PickNextIntent()
        {
            PickNextIntent(animateReveal: true, revealDelay: 0f);
        }

        public void PickNextIntent(bool animateReveal, float revealDelay = 0f)
        {
            if (Data != null && Data.Intents != null && Data.Intents.Count > 0)
            {
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
                else // Random
                {
                    currentIntentIndex = UnityEngine.Random.Range(0, Data.Intents.Count);
                }

                NextIntent = Data.Intents[currentIntentIndex];
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
    }
}
