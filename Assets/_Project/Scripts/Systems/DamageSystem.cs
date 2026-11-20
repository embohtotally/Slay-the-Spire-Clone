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
                target.Damage(dealDamageGA.Amount);
                if (damageVFX != null)
                {
                    Instantiate(damageVFX, target.transform.position, Quaternion.identity);
                }

                GameManager.GenerateFloatingText("-" + dealDamageGA.Amount.ToString(), target.transform);

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
    }
}
