using PlushieOdyssey.Audio;
using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Data;
using PlushieOdyssey.Run;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [System.Serializable]
    public class MatchSetupSystem : MonoBehaviour
    {
        [SerializeField] private List<HeroData> heroTeam;
        [SerializeField] private List<EnemyData> enemyDataList;
        [SerializeField] private bool forceInspectorEnemies = false;
        [SerializeField] private int startingHandSize = 5;
        [SerializeField] private global::PlushieOdyssey.Audio.SfxID yourTurnSfx = global::PlushieOdyssey.Audio.SfxID.YourTurn;

#if UNITY_EDITOR
        [Header("Developer Playtest Overrides (Editor Only, Zero Launch Impact)")]
        [Tooltip("If enabled, these test cards are prepended to the opening deck only when running in the Unity Editor.")]
        [SerializeField] private bool enableEditorTestCards = false;
        [SerializeField] private List<CardData> editorTestCards = new();
#endif

        private int _bonusOpeningDrawFromTalents;
        private int _bonusStartingShieldFromTalents;

        private void Start()
        {
            List<HeroData> activeParty = ResolveActiveParty();
            HeroSystem.Instance.Setup(activeParty);
            CollectUnlockedTalentBonuses(activeParty);

            List<EnemyData> enemiesForThisMatch = enemyDataList;
            if (!forceInspectorEnemies && RunManager.Instance != null && RunManager.Instance.SelectedEncounter != null)
            {
                enemiesForThisMatch = RunManager.Instance.SelectedEncounter.Enemies;
            }

            EnemySystem.Instance.Setup(enemiesForThisMatch);

            List<CardData> combinedDeck = BuildStartingDeck(activeParty);
            EnsureRunDeckManagerExists();
            RunDeckManager.Instance.InitializeIfEmpty(combinedDeck);
            CardSystem.Instance.Setup(RunDeckManager.Instance.GetDeckCopy());

            foreach (var hero in activeParty)
            {
                if (hero != null && hero.StartingPerks != null)
                {
                    foreach (PerkData perkData in hero.StartingPerks)
                    {
                        PerkSystem.Instance.AddPerk(new Perk(perkData));
                    }
                }
            }
            ActionSystem.Instance.Perform(new CombatStartedGA(), OnCombatStartedFinished);
        }

        private void CollectUnlockedTalentBonuses(List<HeroData> activeParty)
        {
            _bonusOpeningDrawFromTalents = 0;
            _bonusStartingShieldFromTalents = 0;
            if (activeParty == null) return;

            for (int i = 0; i < activeParty.Count; i++)
            {
                HeroData hero = activeParty[i];
                if (hero == null || hero.Talents == null) continue;
                for (int t = 0; t < hero.Talents.Count; t++)
                {
                    HeroTalentDefinition talent = hero.Talents[t];
                    if (talent == null || !talent.IsUnlocked) continue;
                    _bonusOpeningDrawFromTalents += Mathf.Max(0, talent.BonusOpeningDraw);
                    _bonusStartingShieldFromTalents += Mathf.Max(0, talent.BonusStartingShield);
                }
            }
        }

        private void OnCombatStartedFinished()
        {
            if (_bonusStartingShieldFromTalents > 0 && HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null)
            {
                HeroSystem.Instance.HeroView.AddShield(_bonusStartingShieldFromTalents);
            }
            DrawStartingHand();
        }

        private List<HeroData> ResolveActiveParty()
        {
            if (RunManager.Instance != null && RunManager.Instance.HasSelectedParty)
            {
                return new List<HeroData>(RunManager.Instance.SelectedParty);
            }

            return heroTeam ?? new List<HeroData>();
        }

        private void DrawStartingHand()
        {
            if (yourTurnSfx != global::PlushieOdyssey.Audio.SfxID.None) global::PlushieOdyssey.Audio.Tune.SFX(yourTurnSfx);
            int totalDraw = Mathf.Max(1, startingHandSize + _bonusOpeningDrawFromTalents);
            DrawCardsGA drawCardsGA = new(totalDraw);
            ActionSystem.Instance.Perform(drawCardsGA);
        }

        private List<CardData> BuildStartingDeck(List<HeroData> activeParty)
        {
            List<CardData> combinedDeck = new();

#if UNITY_EDITOR
            if (enableEditorTestCards && editorTestCards != null)
            {
                foreach (CardData card in editorTestCards)
                {
                    if (card != null) combinedDeck.Add(card);
                }
            }
#endif

            if (activeParty != null)
            {
                foreach (HeroData hero in activeParty)
                {
                    if (hero != null && hero.Deck != null)
                    {
                        combinedDeck.AddRange(hero.Deck);
                    }
                }
            }

            return combinedDeck;
        }

        private static void EnsureRunDeckManagerExists()
        {
            if (RunDeckManager.Instance != null) return;

            GameObject deckManagerObject = new("Run Deck Manager");
            deckManagerObject.AddComponent<RunDeckManager>();
        }
    }
}
