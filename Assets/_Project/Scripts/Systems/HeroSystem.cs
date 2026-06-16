using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HeroSystem : Singleton<HeroSystem>
{
    [field: SerializeField] public HeroView HeroView { get; private set; }

    public void Setup(HeroData heroData)
    {
        HeroView.Setup(heroData);
    }
}