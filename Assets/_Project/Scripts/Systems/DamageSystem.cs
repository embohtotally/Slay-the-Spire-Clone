using PlushieOdyssey.Combat.Actions;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Core;
using PlushieOdyssey.Core.Scenes;
using PlushieOdyssey.Run;
using System.Collections;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [System.Serializable]
    public class DamageSystem : MonoBehaviour
    {
        [SerializeField] private GameObject damageVFX;
        [SerializeField] private float damageDuration;
        [SerializeField] private string gameOverSceneName = "GameOver";
        [SerializeField] private float gameOverDelay = 0.75f;

        private WaitForSeconds damageWaitForSeconds;
        private bool isLoadingGameOver;

        private void Awake()
        {
            damageWaitForSeconds = new WaitForSeconds(damageDuration);
        }

        private void OnEnable()
        {
            ActionSystem.AttachPerformer<DealDamageGA>(DealDamagePerformer);
        }

        private void OnDisable()
        {
            ActionSystem.DetachPerformer<DealDamageGA>();
        }

        private IEnumerator DealDamagePerformer(DealDamageGA dealDamageGA)
        {
            foreach (CombatantView target in dealDamageGA.Targets)
            {
                if (target == null || target.CurrentHealth <= 0) continue;

                int hpBefore = target.CurrentHealth;
                int shieldBefore = target.CurrentShield;
                target.Damage(dealDamageGA.Amount);
                int hpLost = Mathf.Max(0, hpBefore - target.CurrentHealth);
                int shieldAbsorbed = Mathf.Max(0, shieldBefore - target.CurrentShield);
                bool shieldBroken = shieldBefore > 0 && target.CurrentShield <= 0 && dealDamageGA.Amount > 0;
                bool killed = target.CurrentHealth <= 0;

                if (damageVFX != null)
                {
                    Instantiate(damageVFX, target.transform.position, Quaternion.identity);
                }

                if (hpLost > 0)
                {
                    float popupScale = hpLost >= 14 || killed ? 1.38f : (hpLost >= 8 ? 1.16f : 1.0f);
                    string hex = killed ? "#FF3B3B" : (hpLost >= 12 ? "#FF6B35" : "#FF5252");
                    GameManager.GenerateFloatingText("-" + hpLost, target.transform, 1.05f, 1.15f, hex, popupScale);
                    if (shieldBroken)
                    {
                        GameManager.GenerateFloatingText("SHIELD BREAK!", target.transform, 1.15f, 1.35f, "#5CE1E6", 1.12f);
                    }
                }
                else if (shieldAbsorbed > 0)
                {
                    GameManager.GenerateFloatingText("BLOCKED!", target.transform, 0.95f, 1.05f, "#66E0FF", 1.05f);
                }

                PlushieOdyssey.Vfx.CombatJuiceDirector.Instance?.OnDamageImpact(
                    target, dealDamageGA.Caster, dealDamageGA.Amount, hpLost, shieldAbsorbed, shieldBroken, killed);

                if (target.CurrentHealth < hpBefore && BuffSystem.Instance != null)
                {
                    BuffSystem.Instance.OnCombatantLostUnblockedHealth(target);
                }

                if (!dealDamageGA.IsThornsDamage && dealDamageGA.Amount > 0 &&
                    dealDamageGA.Caster != null && dealDamageGA.Caster != target &&
                    dealDamageGA.Caster.CurrentHealth > 0 && BuffSystem.Instance != null)
                {
                    int thorns = BuffSystem.Instance.GetBuffStack(target, Models.BuffType.Thorns);
                    if (thorns > 0)
                    {
                        ActionSystem.Instance.AddReaction(new DealDamageGA(
                            thorns, new System.Collections.Generic.List<CombatantView> { dealDamageGA.Caster }, target, isThornsDamage: true));
                    }
                }

                if (target.CurrentHealth <= 0)
                {
                    if (target is EnemyView enemyView)
                    {
                        KillEnemyGA killEnemyGA = new(enemyView);
                        ActionSystem.Instance.AddReaction(killEnemyGA);
                    }
                    else if (!isLoadingGameOver)
                    {
                        isLoadingGameOver = true;

                        // Snapshot all run accomplishments BEFORE AbandonRun wipes active run state!
                        string killerName = ResolveDefeatSourceName(dealDamageGA.Caster);
                        string endReason = !string.IsNullOrWhiteSpace(killerName)
                            ? $"Defeated by {killerName}"
                            : null;
                        PlushieOdyssey.UI.Menus.RunEndSummaryUI.CapturePendingSummary(isVictory: false, endReason: endReason);

                        if (RunManager.Instance != null)
                        {
                            RunManager.Instance.AbandonRun();
                        }

                        yield return new WaitForSeconds(gameOverDelay);
                        SceneLoader.LoadScene(gameOverSceneName);
                    }
                }
            }

            yield break;
        }

        private static string ResolveDefeatSourceName(CombatantView caster)
        {
            if (caster is EnemyView enemy && enemy.Data != null && !string.IsNullOrWhiteSpace(enemy.Data.name))
            {
                return enemy.Data.name;
            }
            if (caster != null && !string.IsNullOrWhiteSpace(caster.name))
            {
                return caster.name.Replace("(Clone)", string.Empty).Trim();
            }
            if (RunManager.Instance != null && !string.IsNullOrWhiteSpace(RunManager.Instance.LastEncounterName))
            {
                return RunManager.Instance.LastEncounterName;
            }
            return string.Empty;
        }
    }
}
