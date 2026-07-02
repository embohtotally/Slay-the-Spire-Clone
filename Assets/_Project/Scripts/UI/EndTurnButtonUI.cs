using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndTurnButtonUI : MonoBehaviour
{
    [SerializeField] private Gameseed26.SfxID endTurnSfx = Gameseed26.SfxID.EndTurn;

    public void OnClick()
    {
        if (endTurnSfx != Gameseed26.SfxID.None) Gameseed26.Tune.SFX(endTurnSfx);
        EnemyTurnGA enemyTurnGA = new();
        ActionSystem.Instance.Perform(enemyTurnGA);
    }
}