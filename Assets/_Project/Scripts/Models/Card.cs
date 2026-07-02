using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gameseed26;

public class Card
{
    private readonly CardData data;

    public string Title { get; private set; }
    public string Description { get; private set; }
    public Effect ManualTargetEffect { get; private set; }
    public CardType Type { get; private set; }
    public CardTargetType TargetType { get; private set; }
    public List<AutoTargetEffect> OtherEffects { get; private set; }
    public Sprite Image { get; private set; }
    public int Mana { get; private set; }
    public SfxID HoverSfx { get; private set; }
    public SfxID PlaySfx { get; private set; }
    public GameObject PlayParticle { get; private set; }
    public CardVisualType VisualType { get; private set; }
    public string HeroAnimationTrigger { get; private set; }
    public string TargetAnimationTrigger { get; private set; }

    /// <summary>
    /// Initialization of a new generic Card based on its ScriptableObject
    /// </summary>
    /// <param name="cardData"></param>
    public Card(CardData cardData)
    {
        data = cardData;
        Image = data.Image;
        Title = data.Title;
        Description = data.Description;
        Mana = data.Mana;
        Type = data.Type;
        ManualTargetEffect = data.ManualTargetEffect;
        TargetType = data.TargetType;
        OtherEffects = data.OtherEffects;
        HoverSfx = data.HoverSfx;
        PlaySfx = data.PlaySfx;
        PlayParticle = data.PlayParticle;
        VisualType = data.VisualType;
        HeroAnimationTrigger = data.HeroAnimationTrigger;
        TargetAnimationTrigger = data.TargetAnimationTrigger;
    }
}