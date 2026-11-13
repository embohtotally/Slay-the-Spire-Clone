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
                target.Damage(dealDamageGA.Amount);
                if (damageVFX != null)
                {
                    Instantiate(damageVFX, target.transform.position, Quaternion.identity);
                }

                GameManager.GenerateFloatingText("-" + dealDamageGA.Amount.ToString(), target.transform);

                yield return damageWaitForSeconds;

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
        }
    }
}
