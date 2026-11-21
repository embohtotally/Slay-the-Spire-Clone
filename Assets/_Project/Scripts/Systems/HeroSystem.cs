using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [System.Serializable]
    public class HeroSystem : Singleton<HeroSystem>
    {
        [field: SerializeField] public HeroView HeroView { get; private set; }

        [Header("Will System — Per-Card RNG Gain")]
        [Tooltip("When enabled, every card played grants a random amount of Will between Min and Max (inclusive).")]
        [SerializeField] private bool grantWillOnCardPlay = true;
        [SerializeField, Min(0)] private int minWillGainPerCard = 1;
        [SerializeField, Min(0)] private int maxWillGainPerCard = 3;

        public int MinWillGainPerCard => Mathf.Max(0, minWillGainPerCard);
        public int MaxWillGainPerCard => Mathf.Max(MinWillGainPerCard, maxWillGainPerCard);
        public int LastCardWillGain { get; private set; }
        public event Action<int> CardWillGained;

        private void OnEnable()
        {
            ActionSystem.AttachPerformer<AddStressGA>(AddStressPerformer);
            ActionSystem.AttachPerformer<ModifyStressGA>(ModifyStressPerformer);
            ActionSystem.AttachPerformer<SetStressGA>(SetStressPerformer);
            ActionSystem.SubscribeReaction<PlayCardGA>(OnCardPlayedPost, ReactionTiming.POST);
        }

        private void OnDisable()
        {
            ActionSystem.DetachPerformer<AddStressGA>();
            ActionSystem.DetachPerformer<ModifyStressGA>();
            ActionSystem.DetachPerformer<SetStressGA>();
            ActionSystem.UnsubscribeReaction<PlayCardGA>(OnCardPlayedPost, ReactionTiming.POST);
        }

        private void OnCardPlayedPost(PlayCardGA playCardGA)
        {
            if (!grantWillOnCardPlay || HeroView == null || playCardGA?.Card == null) return;
            int min = MinWillGainPerCard;
            int max = MaxWillGainPerCard;
            if (max <= 0) return;

            int roll = UnityEngine.Random.Range(min, max + 1);
            LastCardWillGain = roll;
            if (roll <= 0) return;

            HeroView.AddWillToAllHeroes(roll);
            GameManager.GenerateFloatingText($"+{roll} Will", HeroView.transform, 1f, 0.92f, "#38D4FF");
            CardWillGained?.Invoke(roll);
        }

        public void Setup(List<HeroData> heroTeam)
        {
            HeroView.Setup(heroTeam);
        }

        private IEnumerator AddStressPerformer(AddStressGA addStressGA)
        {
            foreach (CombatantView target in addStressGA.Targets)
            {
                if (target == null) continue;
                if (addStressGA.Amount > 0 && BuffSystem.Instance != null && BuffSystem.Instance.HasBuff(target, Models.BuffType.FearImmunity))
                {
                    GameManager.GenerateFloatingText("Will Protected!", target.transform, 1f, 1f, "#FFD54F");
                    continue;
                }
                target.AddStress(addStressGA.Amount);
                GameManager.GenerateFloatingText("-" + addStressGA.Amount + " Will", target.transform, 1f, 1f, "#CE93D8");
                target.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.2f, 5, 1f);
            }
            yield break;
        }

        private IEnumerator ModifyStressPerformer(ModifyStressGA modifyStressGA)
        {
            foreach (CombatantView target in modifyStressGA.Targets)
            {
                if (target is HeroView heroView)
                {
                    if (modifyStressGA.Amount > 0)
                    {
                        if (!modifyStressGA.IgnoreImmunity && BuffSystem.Instance != null && BuffSystem.Instance.HasBuff(target, Models.BuffType.FearImmunity))
                        {
                            GameManager.GenerateFloatingText("Will Protected!", target.transform, 1f, 1f, "#FFD54F");
                            continue;
                        }
                        heroView.DrainWillFromAllHeroes(modifyStressGA.Amount);
                        GameManager.GenerateFloatingText("-" + modifyStressGA.Amount + " Will", target.transform, 1f, 1f, "#CE93D8");
                        target.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.2f, 5, 1f);
                    }
                    else if (modifyStressGA.Amount < 0)
                    {
                        int gainedWill = -modifyStressGA.Amount;
                        heroView.AddWillToAllHeroes(gainedWill);
                        GameManager.GenerateFloatingText("+" + gainedWill + " Will", target.transform, 1f, 1f, "#38D4FF");
                        target.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.2f, 5, 1f);
                    }
                }
            }
            yield break;
        }

        private IEnumerator SetStressPerformer(SetStressGA setStressGA)
        {
            int sourceStress = 0;
            if (HeroView != null)
            {
                for (int i = 0; i < HeroView.HeroTeam.Count; i++)
                {
                    if (HeroView.HeroTeam[i] != null && HeroView.HeroTeam[i].name.Contains(setStressGA.SourceAllyName))
                    {
                        sourceStress = HeroView.GetStressStateCopies()[i].CurrentStress;
                        break;
                    }
                }
            }

            foreach (CombatantView target in setStressGA.Targets)
            {
                if (target is HeroView heroView)
                {
                    heroView.SetCurrentStress(sourceStress);
                    GameManager.GenerateFloatingText("Will = " + heroView.CurrentWill, target.transform, 1f, 1f, "#38D4FF");
                    target.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.2f, 5, 1f);
                }
            }
            yield break;
        }
    }
}
