using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[System.Serializable]
public class EnemySystem : Singleton<EnemySystem>
{
    public List<EnemyView> Enemies => enemyBoardView.EnemyViews;

    [SerializeField] private EnemyBoardView enemyBoardView;
    [SerializeField] private float attackXMoveAmount;
    [SerializeField] private float attackMoveDuration;
    [SerializeField] private float attackReturnDuration;

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<EnemyTurnGA>(EnemyTurnPerformer);
        ActionSystem.AttachPerformer<ExecuteEnemyIntentGA>(ExecuteIntentPerformer);
        ActionSystem.AttachPerformer<KillEnemyGA>(KillEnemyPerformer);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<EnemyTurnGA>();
        ActionSystem.DetachPerformer<ExecuteEnemyIntentGA>();
        ActionSystem.DetachPerformer<KillEnemyGA>();
    }

    public void Setup(List<EnemyData> enemyDataList)
    {
        foreach (EnemyData enemyData in enemyDataList)
        {
            enemyBoardView.AddEnemy(enemyData);
        }
    }

    #region Performers
    private IEnumerator EnemyTurnPerformer(EnemyTurnGA enemyTurnGA)
    {
        foreach (EnemyView enemy in enemyBoardView.EnemyViews)
        {
            enemy.DecreaseTaunt();

            if (enemy.IsStunned)
            {
                enemy.DecreaseStun();
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

        bool isAttackComplete = false;
        attacker.StateMachine.ChangeState(new CombatantAttackState(
            attacker,
            attackXMoveAmount,
            attackMoveDuration,
            attackReturnDuration,
            () => isAttackComplete = true
        ));

        yield return new WaitUntil(() => isAttackComplete);

        if (intent.Effects != null)
        {
            foreach (AutoTargetEffect effect in intent.Effects)
            {
                if (effect.Effect != null && effect.TargetMode != null)
                {
                    List<CombatantView> targets = effect.TargetMode.GetTargets();
                    ActionSystem.Instance.AddReaction(effect.Effect.GetGameAction(targets, attacker));
                }
            }
        }

        attacker.PickNextIntent();
    }

    private IEnumerator KillEnemyPerformer(KillEnemyGA killEnemyGA)
    {
        yield return enemyBoardView.RemoveEnemy(killEnemyGA.EnemyView);

        if (enemyBoardView.EnemyViews.Count == 0)
        {
            ActionSystem.Instance.AddReaction(new CombatWonGA());
        }
    }
    #endregion
}
