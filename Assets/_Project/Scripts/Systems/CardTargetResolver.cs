using PlushieOdyssey.Combat.Models;
using PlushieOdyssey.Combat.Views;
using PlushieOdyssey.Data;
using UnityEngine;

namespace PlushieOdyssey.Combat.Systems
{
    [DisallowMultipleComponent]
    public class CardTargetResolver : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _targetLayers;
        [SerializeField, Min(0f)] private float _rayDistance = 100f;
        [SerializeField] private EnemySystem _enemySystem;

        public bool TryResolve(Card card, Vector2 screenPosition, out CombatantView target)
        {
            target = null;
            if (!isActiveAndEnabled || _camera == null || card == null) return false;
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _rayDistance, _targetLayers)) return false;

            CombatantView candidate = hit.collider.GetComponentInParent<CombatantView>();
            if (!IsValidTarget(card, candidate)) return false;
            target = candidate;
            return true;
        }

        public bool IsValidTarget(Card card, CombatantView target)
        {
            if (card == null || target == null || !target.isActiveAndEnabled || target.CurrentHealth <= 0)
                return false;
            if (card.TargetType == CardTargetType.Enemy && target is not EnemyView) return false;
            if (card.TargetType == CardTargetType.Hero && target is not HeroView) return false;

            if (target is EnemyView enemy)
            {
                if (_enemySystem == null || !_enemySystem.isActiveAndEnabled) return false;
                bool hasTaunt = _enemySystem.Enemies.Exists(candidate =>
                    candidate != null && candidate.isActiveAndEnabled &&
                    candidate.CurrentHealth > 0 && candidate.IsTaunted);
                if (hasTaunt && !enemy.IsTaunted) return false;
            }

            return true;
        }
    }
}
