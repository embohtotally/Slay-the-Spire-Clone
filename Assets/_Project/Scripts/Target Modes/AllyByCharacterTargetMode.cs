using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AllyByCharacterTargetMode : TargetMode
{
    [SerializeField] private string characterName;

    public override List<CombatantView> GetTargets(CombatantView caster = null)
    {
        List<CombatantView> targets = new();
        if (HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null)
        {
            HeroView heroView = HeroSystem.Instance.HeroView;
            foreach (var heroData in heroView.HeroTeam)
            {
                if (heroData != null && heroData.name.Contains(characterName))
                {
                    targets.Add(heroView);
                    break;
                }
            }
        }
        return targets;
    }
}
