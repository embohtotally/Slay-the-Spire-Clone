using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CombatantStatusEffectsUI : MonoBehaviour
{
#pragma warning disable 0649
    [Serializable]
    private struct StatusIconOverride
    {
        public string Id;
        public Sprite Icon;
    }
#pragma warning restore 0649

    private sealed class StatusAccumulator
    {
        public string Id;
        public string DisplayName;
        public int Stack;
        public int RemainingTurns;
        public bool IsDebuff;
    }

    [SerializeField] private CombatantView target;
    [SerializeField] private Transform container;
    [SerializeField] private StatusEffectIconView iconPrefab;
    [SerializeField] private bool autoFindTargetInParent = true;
    [SerializeField] private List<StatusIconOverride> iconOverrides = new();

    private readonly List<StatusEffectIconView> iconViews = new();
    private BuffSystem subscribedBuffSystem;
    private DamageOverTimeSystem subscribedDotSystem;

    private void Awake()
    {
        if (container == null) container = transform;
        EnsureTargetReference();
    }

    private void OnEnable()
    {
        EnsureTargetReference();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (BuffSystem.Instance != subscribedBuffSystem || DamageOverTimeSystem.Instance != subscribedDotSystem)
        {
            Unsubscribe();
            Subscribe();
            Refresh();
        }
    }

    public void SetTarget(CombatantView newTarget)
    {
        if (target == newTarget) return;
        if (isActiveAndEnabled && target != null) target.StatusEffectsChanged -= Refresh;
        target = newTarget;
        if (isActiveAndEnabled && target != null) target.StatusEffectsChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        if (target == null || container == null || iconPrefab == null) return;

        List<StatusEffectDisplayData> statusEffects = BuildStatusList();
        EnsureIconCount(statusEffects.Count);

        for (int i = 0; i < iconViews.Count; i++)
        {
            bool hasStatus = i < statusEffects.Count;
            iconViews[i].gameObject.SetActive(hasStatus);
            if (hasStatus) iconViews[i].Bind(statusEffects[i]);
        }
    }

    private List<StatusEffectDisplayData> BuildStatusList()
    {
        Dictionary<string, StatusAccumulator> groupedStatuses = new(StringComparer.OrdinalIgnoreCase);

        if (target.IsStunned)
        {
            AddStatus(groupedStatuses, "stun", "Stun", 1, target.StunDuration, true);
        }

        if (target.IsTaunted)
        {
            AddStatus(groupedStatuses, "taunt", "Taunt", 1, target.TauntDuration, false);
        }

        if (BuffSystem.Instance != null)
        {
            foreach (BuffData buff in BuffSystem.Instance.GetBuffsFor(target))
            {
                string id = buff.Type.ToString().ToLowerInvariant();
                int stack = buff.Value != 0 ? Mathf.Abs(buff.Value) : 1;
                AddStatus(groupedStatuses, id, buff.Type.ToString(), stack, buff.RemainingTurns, IsDebuff(buff.Type));
            }
        }

        if (DamageOverTimeSystem.Instance != null)
        {
            foreach (DamageOverTimeData dot in DamageOverTimeSystem.Instance.GetDoTsFor(target))
            {
                AddStatus(groupedStatuses, "dot", "DoT", dot.DamagePerTurn, dot.RemainingTurns, true);
            }
        }

        List<StatusEffectDisplayData> statuses = new();
        foreach (StatusAccumulator accumulator in groupedStatuses.Values)
        {
            statuses.Add(new StatusEffectDisplayData(
                accumulator.Id,
                accumulator.DisplayName,
                GetIcon(accumulator.Id),
                accumulator.Stack,
                accumulator.RemainingTurns,
                accumulator.IsDebuff
            ));
        }

        return statuses;
    }

    private static void AddStatus(Dictionary<string, StatusAccumulator> statuses, string id, string displayName, int stack, int remainingTurns, bool isDebuff)
    {
        if (!statuses.TryGetValue(id, out StatusAccumulator accumulator))
        {
            accumulator = new StatusAccumulator
            {
                Id = id,
                DisplayName = displayName,
                Stack = 0,
                RemainingTurns = 0,
                IsDebuff = isDebuff
            };
            statuses.Add(id, accumulator);
        }

        accumulator.Stack += Mathf.Max(0, stack);
        accumulator.RemainingTurns = Mathf.Max(accumulator.RemainingTurns, remainingTurns);
        accumulator.IsDebuff |= isDebuff;
    }

    private Sprite GetIcon(string id)
    {
        foreach (StatusIconOverride iconOverride in iconOverrides)
        {
            if (string.Equals(iconOverride.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return iconOverride.Icon;
            }
        }

        return null;
    }

    private static bool IsDebuff(BuffType type)
    {
        return type == BuffType.Vulnerable || type == BuffType.Weak;
    }

    private void EnsureTargetReference()
    {
        if (target != null || !autoFindTargetInParent) return;
        target = GetComponentInParent<CombatantView>();
    }

    private void EnsureIconCount(int targetCount)
    {
        while (iconViews.Count < targetCount)
        {
            StatusEffectIconView iconView = Instantiate(iconPrefab, container);
            iconViews.Add(iconView);
        }
    }

    private void Subscribe()
    {
        if (target != null) target.StatusEffectsChanged += Refresh;

        subscribedBuffSystem = BuffSystem.Instance;
        if (subscribedBuffSystem != null) subscribedBuffSystem.StatusEffectsChanged += Refresh;

        subscribedDotSystem = DamageOverTimeSystem.Instance;
        if (subscribedDotSystem != null) subscribedDotSystem.StatusEffectsChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (target != null) target.StatusEffectsChanged -= Refresh;
        if (subscribedBuffSystem != null) subscribedBuffSystem.StatusEffectsChanged -= Refresh;
        if (subscribedDotSystem != null) subscribedDotSystem.StatusEffectsChanged -= Refresh;

        subscribedBuffSystem = null;
        subscribedDotSystem = null;
    }
}
