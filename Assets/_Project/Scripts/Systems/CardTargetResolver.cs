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

        public bool TryRaycastCombatant(Vector2 screenPosition, out CombatantView combatant)
        {
            combatant = null;
            if (!isActiveAndEnabled || _camera == null) return false;
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _rayDistance, _targetLayers)) return false;

            combatant = hit.collider.GetComponentInParent<CombatantView>();
            return combatant != null && combatant.isActiveAndEnabled && combatant.CurrentHealth > 0;
        }

        public bool TryResolve(Card card, Vector2 screenPosition, out CombatantView target)
        {
            target = null;
            if (card == null || !TryRaycastCombatant(screenPosition, out CombatantView candidate))
                return false;

            if (!IsValidTarget(card, candidate)) return false;
            target = candidate;
            return true;
        }

        public bool IsBlockedByTaunt(Card card, CombatantView target)
        {
            if (card == null || target is not EnemyView enemy || _enemySystem == null || !_enemySystem.isActiveAndEnabled)
                return false;
            if (card.TargetType == CardTargetType.Hero) return false;

            bool hasTaunt = _enemySystem.Enemies.Exists(candidate =>
                candidate != null && candidate.isActiveAndEnabled &&
                candidate.CurrentHealth > 0 && candidate.IsTaunted);
            return hasTaunt && !enemy.IsTaunted;
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
                if (IsBlockedByTaunt(card, enemy)) return false;
            }

            return true;
        }
    }
}
