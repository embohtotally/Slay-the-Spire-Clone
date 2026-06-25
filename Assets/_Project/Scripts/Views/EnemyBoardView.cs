using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBoardView : MonoBehaviour
{
    public List<EnemyView> EnemyViews { get; private set; } = new();

    [SerializeField] private List<Transform> slots;
    [SerializeField] private float removeEnemyScaleDuration = 0.25f;
    [SerializeField] private float summonRiseDuration = 0.5f;
    [SerializeField] private float summonStartingYOffset = -5f;

    public bool HasAvailableSlot(out Transform availableSlot)
    {
        availableSlot = null;
        foreach (Transform slot in slots)
        {
            if (!EnemyViews.Exists(e => e.transform.parent == slot))
            {
                availableSlot = slot;
                return true;
            }
        }
        return false;
    }

    public void AddEnemy(EnemyData enemyData)
    {
        if (!HasAvailableSlot(out Transform slot)) return;
        EnemyView enemyView = EnemyViewCreator.Instance.CreateEnemyView(enemyData, slot.position, slot.rotation);
        enemyView.transform.parent = slot;
        EnemyViews.Add(enemyView);
    }

    public IEnumerator SummonEnemy(EnemyData enemyData)
    {
        if (!HasAvailableSlot(out Transform slot)) yield break;

        Vector3 targetPosition = slot.position;
        Vector3 startPosition = targetPosition + new Vector3(0, summonStartingYOffset, 0);

        EnemyView enemyView = EnemyViewCreator.Instance.CreateEnemyView(enemyData, startPosition, slot.rotation);
        enemyView.transform.parent = slot;
        EnemyViews.Add(enemyView);

        Tween tween = enemyView.transform.DOMoveY(targetPosition.y, summonRiseDuration).SetEase(Ease.OutBack);
        yield return tween.WaitForCompletion();
    }

    public IEnumerator RemoveEnemy(EnemyView enemyView)
    {
        EnemyViews.Remove(enemyView);
        Tween tween = enemyView.transform.DOScale(Vector3.zero, removeEnemyScaleDuration);
        yield return tween.WaitForCompletion();
        Destroy(enemyView.gameObject);
    }
}