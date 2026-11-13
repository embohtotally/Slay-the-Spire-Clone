using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Systems;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Data;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Targeting
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "AllyByCharacterTargetMode")]
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
}
