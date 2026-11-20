using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.States;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [System.Serializable]
    public class EnemySystem : Singleton<EnemySystem>
    {
        public List<EnemyView> Enemies => enemyBoardView.EnemyViews;
        public bool IsEnemyTurnActive { get; private set; }

        [SerializeField] private EnemyBoardView enemyBoardView;
        [SerializeField] private float attackXMoveAmount;
        [SerializeField] private float attackMoveDuration;
        [SerializeField] private float attackReturnDuration;

        private void OnEnable()
        {
            IsEnemyTurnActive = false;
            ActionSystem.AttachPerformer<EnemyTurnGA>(EnemyTurnPerformer);
            ActionSystem.AttachPerformer<ExecuteEnemyIntentGA>(ExecuteIntentPerformer);
            ActionSystem.AttachPerformer<KillEnemyGA>(KillEnemyPerformer);
            ActionSystem.AttachPerformer<SummonEnemyGA>(SummonEnemyPerformer);
            ActionSystem.AttachPerformer<SimultaneousGA>(SimultaneousPerformer);

            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPre, ReactionTiming.PRE);
            ActionSystem.SubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);
        }

        private void OnDisable()
        {
            IsEnemyTurnActive = false;
            ActionSystem.DetachPerformer<EnemyTurnGA>();
            ActionSystem.DetachPerformer<ExecuteEnemyIntentGA>();
            ActionSystem.DetachPerformer<KillEnemyGA>();
            ActionSystem.DetachPerformer<SummonEnemyGA>();
            ActionSystem.DetachPerformer<SimultaneousGA>();

            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPre, ReactionTiming.PRE);
            ActionSystem.UnsubscribeReaction<EnemyTurnGA>(OnEnemyTurnPost, ReactionTiming.POST);
        }

        public void Setup(List<EnemyData> enemyDataList)
        {
            IsEnemyTurnActive = false;
            foreach (EnemyData enemyData in enemyDataList)
            {
                enemyBoardView.AddEnemy(enemyData);
            }
        }

        #region Turn Reactions
        private void OnEnemyTurnPre(EnemyTurnGA _)
        {
            IsEnemyTurnActive = true;
        }

        private void OnEnemyTurnPost(EnemyTurnGA _)
        {
            IsEnemyTurnActive = false;

            // Do not reveal new intents if the hero has already fallen
            if (HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null && HeroSystem.Instance.HeroView.CurrentHealth <= 0)
            {
                return;
            }

            if (enemyBoardView == null || enemyBoardView.EnemyViews == null) return;

            int revealOrder = 0;
            for (int i = 0; i < enemyBoardView.EnemyViews.Count; i++)
            {
                EnemyView enemy = enemyBoardView.EnemyViews[i];
                if (enemy == null || enemy.CurrentHealth <= 0) continue;

                // Reveal each enemy's next action cleanly at Player Turn start with a staggered pop-up wave
                enemy.PickNextIntent(animateReveal: true, revealDelay: 0.16f + revealOrder * 0.10f);
                revealOrder++;
            }
        }
        #endregion

        #region Performers
        private IEnumerator EnemyTurnPerformer(EnemyTurnGA enemyTurnGA)
        {
            IsEnemyTurnActive = true;

            foreach (EnemyView enemy in enemyBoardView.EnemyViews)
            {
                if (enemy == null || enemy.CurrentHealth <= 0) continue;

                enemy.DecreaseTaunt();

                if (enemy.IsStunned)
                {
                    enemy.DecreaseStun();
                    enemy.ConsumeIntentForEnemyTurn();
                    continue;
                }

                if (enemy.NextIntent != null)
                {
                    ExecuteEnemyIntentGA executeIntentGA = new(enemy, enemy.NextIntent);
                    ActionSystem.Instance.AddReaction(executeIntentGA);
                }
            }

            yield return null;
        }

        private IEnumerator ExecuteIntentPerformer(ExecuteEnemyIntentGA executeIntentGA)
        {
            EnemyView attacker = executeIntentGA.Attacker;
            EnemyIntent intent = executeIntentGA.Intent;

            if (attacker == null || attacker.CurrentHealth <= 0)
            {
                yield break;
            }

            // Consume and animate away the executed intent; the next intent waits until Player Turn begins
            attacker.ConsumeIntentForEnemyTurn();

            bool isAttackComplete = false;
            Vector3? lungePos = HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null
                ? HeroSystem.Instance.HeroView.transform.position
                : (Vector3?)null;
            attacker.StateMachine.ChangeState(new CombatantAttackState(
                attacker,
                Mathf.Max(0.42f, attackMoveDuration + attackReturnDuration),
                () => isAttackComplete = true,
                lungePos
            ));

            yield return new WaitUntil(() => isAttackComplete);

            if (intent != null && intent.Effects != null)
            {
                foreach (AutoTargetEffect effect in intent.Effects)
                {
                    if (effect.Effect != null)
                    {
                        List<CombatantView> targets = effect.TargetMode != null ? effect.TargetMode.GetTargets(attacker) : null;
                        ActionSystem.Instance.AddReaction(effect.Effect.GetGameAction(targets, attacker));
                    }
                }
            }
        }

        private IEnumerator KillEnemyPerformer(KillEnemyGA killEnemyGA)
        {
            if (killEnemyGA.EnemyView != null)
            {
                killEnemyGA.EnemyView.ConsumeIntentForEnemyTurn();
            }

            yield return enemyBoardView.RemoveEnemy(killEnemyGA.EnemyView);

            if (enemyBoardView.EnemyViews.Count == 0)
            {
                ActionSystem.Instance.AddReaction(new CombatWonGA());
            }
        }

        private IEnumerator SummonEnemyPerformer(SummonEnemyGA summonEnemyGA)
        {
            for (int i = 0; i < summonEnemyGA.Count; i++)
            {
                if (enemyBoardView.HasAvailableSlot(out Transform _))
                {
                    yield return enemyBoardView.SummonEnemy(summonEnemyGA.EnemyData);
                }
                else
                {
                    break;
                }
            }
        }

        private IEnumerator SimultaneousPerformer(SimultaneousGA simultaneousGA)
        {
            if (simultaneousGA.Actions != null)
            {
                foreach (GameAction action in simultaneousGA.Actions)
                {
                    ActionSystem.Instance.AddReaction(action);
                }
            }
            yield return null;
        }
        #endregion
    }
}
