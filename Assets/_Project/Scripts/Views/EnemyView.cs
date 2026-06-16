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

    public void Setup(EnemyData enemyData)
    {
        Data = enemyData;
        PickNextIntent();
        SetupBase(enemyData.Health, enemyData.Image);
    }

    public void PickNextIntent()
    {
        if (Data.Intents != null && Data.Intents.Count > 0)
        {
            NextIntent = Data.Intents[UnityEngine.Random.Range(0, Data.Intents.Count)];
            attackText.text = NextIntent.IntentName;
        }
        else
        {
            attackText.text = "";
        }
    }
}