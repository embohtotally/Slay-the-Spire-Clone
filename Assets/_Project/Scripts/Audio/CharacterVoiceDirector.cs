using System;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
using UnityEngine;

namespace PlushieOdyssey.Audio
{
    /// <summary>
    /// Connects the project's existing Aira & Ember voicepacks (60 WAV clips already mapped to SfxID)
    /// to live combat events (card casts, taking damage, healing) with polite anti-spam throttling.
    /// Auto-attached by ActionSystem.Awake().
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterVoiceDirector : Singleton<CharacterVoiceDirector>
    {
        [SerializeField] private float voiceCooldownSeconds = 0.72f;
        private float _nextAllowedVoiceTime;

        private void OnEnable()
        {
            ActionSystem.SubscribeReaction<PlayCardGA>(OnPlayCardPre, ReactionTiming.PRE);
            ActionSystem.SubscribeReaction<DealDamageGA>(OnDealDamagePost, ReactionTiming.POST);
            ActionSystem.SubscribeReaction<HealGA>(OnHealPost, ReactionTiming.POST);
        }

        private void OnDisable()
        {
            ActionSystem.UnsubscribeReaction<PlayCardGA>(OnPlayCardPre, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<DealDamageGA>(OnDealDamagePost, ReactionTiming.POST);
            ActionSystem.UnsubscribeReaction<HealGA>(OnHealPost, ReactionTiming.POST);
        }

        private void OnPlayCardPre(PlayCardGA ga)
        {
            if (ga?.Card == null || !CanSpeak()) return;
            bool isEmber = IsActiveHeroEmber();
            string title = ga.Card.Title ?? string.Empty;

            SfxID cue = ResolveCardVoiceCue(isEmber, ga.Card.Type, title);
            if (cue != SfxID.None)
            {
                PlayVoice(cue);
            }
        }

        private void OnDealDamagePost(DealDamageGA ga)
        {
            if (ga == null || ga.Amount <= 0 || ga.Targets == null || !CanSpeak()) return;
            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            if (hero == null) return;

            foreach (CombatantView t in ga.Targets)
            {
                if (t == hero)
                {
                    PlayVoice(IsActiveHeroEmber() ? SfxID.Ember_damaged : SfxID.Aira_damaged);
                    return;
                }
            }
        }

        private void OnHealPost(HealGA ga)
        {
            if (ga == null || ga.Amount <= 0 || ga.Targets == null || !CanSpeak()) return;
            HeroView hero = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;
            if (hero == null) return;

            foreach (CombatantView t in ga.Targets)
            {
                if (t == hero)
                {
                    PlayVoice(IsActiveHeroEmber() ? SfxID.Ember_healed : SfxID.Aira_healed);
                    return;
                }
            }
        }

        private bool CanSpeak() => Time.unscaledTime >= _nextAllowedVoiceTime;

        private void PlayVoice(SfxID id)
        {
            _nextAllowedVoiceTime = Time.unscaledTime + voiceCooldownSeconds;
            Tune.SFX(id);
        }

        private static bool IsActiveHeroEmber()
        {
            if (RunManager.Instance != null && RunManager.Instance.SelectedParty != null && RunManager.Instance.SelectedParty.Count > 0)
            {
                HeroData heroData = RunManager.Instance.SelectedParty[0];
                if (heroData != null)
                {
                    string n = $"{heroData.name} {heroData.EffectiveDisplayName} {heroData.CharacterId}";
                    if (n.IndexOf("Ember", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (n.IndexOf("Aira", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Aura", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                }
            }
            return false; // Default to Aira
        }

        private static SfxID ResolveCardVoiceCue(bool ember, CardType type, string title)
        {
            if (ContainsAny(title, "fire", "flame", "ember", "burn", "flare", "nova"))
                return ember ? SfxID.Ember_fire : SfxID.Aira_fire;
            if (ContainsAny(title, "ice", "frost", "freeze", "chill"))
                return ember ? SfxID.Ember_ice : SfxID.Aira_ice;
            if (ContainsAny(title, "wind", "dash", "rush", "leap", "arrow", "storm", "star"))
                return ember ? SfxID.Ember_wind : SfxID.Aira_wind;
            if (ContainsAny(title, "heal", "comfort", "blessing", "hope", "lantern"))
                return ember ? SfxID.Ember_heal : SfxID.Aira_heal;
            if (ContainsAny(title, "cure", "cleanse", "calm", "fear", "will", "ward", "sigil"))
                return ember ? SfxID.Ember_cure : SfxID.Aira_cure;
            if (ContainsAny(title, "curse", "hex", "wrath", "echo"))
                return ember ? SfxID.Ember_curse : SfxID.Aira_curse;

            return type switch
            {
                CardType.Attack => ember ? SfxID.Ember_attack : SfxID.Aira_attack,
                CardType.Power => ember ? SfxID.Ember_cure : SfxID.Aira_cure,
                _ => ember ? SfxID.Ember_heal : SfxID.Aira_heal
            };
        }

        private static bool ContainsAny(string src, params string[] keys)
        {
            if (string.IsNullOrEmpty(src)) return false;
            foreach (string k in keys)
            {
                if (src.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
