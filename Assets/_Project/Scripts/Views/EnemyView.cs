using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class EnemyView : CombatantView
{
    [SerializeField] private TMP_Text attackText;

    public EnemyData Data { get; private set; }
    public EnemyIntent NextIntent { get; private set; }

    private int currentIntentIndex = -1;

    public void Setup(EnemyData enemyData)
    {
        Data = enemyData;
        currentIntentIndex = -1;
        PickNextIntent();
        SetupBase(enemyData.Health, enemyData.Image);
    }

    public void PickNextIntent()
    {
        if (Data.Intents != null && Data.Intents.Count > 0)
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
            attackText.text = NextIntent.IntentName;
        }
        else
        {
            attackText.text = "";
        }
    }
}