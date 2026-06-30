using System;
using System.Collections.Generic;
using Gameseed26;
using NaughtyAttributes;
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

    [Header("Target")]
    [SerializeField] private CombatantView target;
    [SerializeField] private bool autoFindTargetInParent = true;

    [Header("UI References")]
    [Tooltip("Parent used for spawned status icons. Empty = this transform.")]
    [SerializeField] private Transform container;
    [SerializeField] private StatusEffectIconView iconPrefab;
    [SerializeField] private bool hideContainerWhenEmpty = true;
    [SerializeField] private bool warnIfMissingReferences = true;

    [Header("Displayed Statuses")]
    [SerializeField] private bool showStun = true;
    [SerializeField] private bool showTaunt = true;
    [SerializeField] private bool showShieldAsStatus;
    [SerializeField] private bool showBuffs = true;
    [SerializeField] private bool showDamageOverTime = true;

    [Header("Optional Icons")]
    [SerializeField] private Sprite stunIcon;
    [SerializeField] private Sprite tauntIcon;
    [SerializeField] private Sprite shieldIcon;
    [SerializeField] private Sprite dotIcon;
    [SerializeField] private List<StatusIconOverride> iconOverrides = new();

    private readonly List<StatusEffectIconView> iconViews = new();
    private BuffSystem subscribedBuffSystem;
    private DamageOverTimeSystem subscribedDotSystem;
    private bool warnedMissingTarget;
    private bool warnedMissingContainer;
    private bool warnedMissingPrefab;

    private Transform Container => container != null ? container : transform;

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

        if (isActiveAndEnabled && target != null)
        {
            target.StatusEffectsChanged -= Refresh;
        }

        target = newTarget;
        warnedMissingTarget = false;

        if (isActiveAndEnabled && target != null)
        {
            target.StatusEffectsChanged += Refresh;
        }

        Refresh();
    }

    [Button("Auto Find Target In Parent", EButtonEnableMode.Always)]
    public void AutoFindTargetInParent()
    {
        target = GetComponentInParent<CombatantView>();
        warnedMissingTarget = false;
        Refresh();
    }

    [Button("Refresh Status UI", EButtonEnableMode.Always)]
    public void Refresh()
    {
        EnsureTargetReference();

        if (!ValidateReferences())
        {
            ClearIconViews();
            SetContainerVisible(false);
            return;
        }

        List<StatusEffectDisplayData> statusEffects = BuildStatusList();
        SetContainerVisible(!hideContainerWhenEmpty || statusEffects.Count > 0);
        EnsureIconCount(statusEffects.Count);

        for (int i = 0; i < iconViews.Count; i++)
        {
            bool hasStatus = i < statusEffects.Count;
            if (hasStatus)
            {
                iconViews[i].Bind(statusEffects[i]);
            }
            else
            {
                iconViews[i].Clear();
            }
        }
    }

    private List<StatusEffectDisplayData> BuildStatusList()
    {
        Dictionary<string, StatusAccumulator> groupedStatuses = new(StringComparer.OrdinalIgnoreCase);

        if (showStun && target.IsStunned)
        {
            AddStatus(groupedStatuses, "stun", "Stun", 1, target.StunDuration, true);
        }

        if (showTaunt && target.IsTaunted)
        {
            AddStatus(groupedStatuses, "taunt", "Taunt", 1, target.TauntDuration, false);
        }

        if (showShieldAsStatus && target.CurrentShield > 0)
        {
            AddStatus(groupedStatuses, "shield", "Shield", target.CurrentShield, 0, false);
        }

        if (showBuffs && BuffSystem.Instance != null)
        {
            foreach (BuffData buff in BuffSystem.Instance.GetBuffsFor(target))
            {
                string id = buff.Type.ToString().ToLowerInvariant();
                int stack = buff.Value != 0 ? Mathf.Abs(buff.Value) : 1;
                AddStatus(groupedStatuses, id, buff.Type.ToString(), stack, buff.RemainingTurns, IsDebuff(buff.Type));
            }
        }

        if (showDamageOverTime && DamageOverTimeSystem.Instance != null)
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
        if (string.Equals(id, "stun", StringComparison.OrdinalIgnoreCase)) return stunIcon;
        if (string.Equals(id, "taunt", StringComparison.OrdinalIgnoreCase)) return tauntIcon;
        if (string.Equals(id, "shield", StringComparison.OrdinalIgnoreCase)) return shieldIcon;
        if (string.Equals(id, "dot", StringComparison.OrdinalIgnoreCase)) return dotIcon;

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
        return type == BuffType.Vulnerable || type == BuffType.Weak || type == BuffType.Poison;
    }

    private void EnsureTargetReference()
    {
        if (target != null || !autoFindTargetInParent) return;
        target = GetComponentInParent<CombatantView>();
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (target == null)
        {
            WarnOnce(ref warnedMissingTarget, "CombatantStatusEffectsUI has no target. Assign Target or place it under a CombatantView.");
            valid = false;
        }

        if (Container == null)
        {
            WarnOnce(ref warnedMissingContainer, "CombatantStatusEffectsUI has no container.");
            valid = false;
        }

        if (iconPrefab == null)
        {
            WarnOnce(ref warnedMissingPrefab, "CombatantStatusEffectsUI needs an Icon Prefab with StatusEffectIconView.");
            valid = false;
        }

        return valid;
    }

    private void WarnOnce(ref bool warnedFlag, string message)
    {
        if (!warnIfMissingReferences || warnedFlag) return;
        warnedFlag = true;
        Gameseed26.Logger.LogWarning(this, message);
    }

    private void EnsureIconCount(int targetCount)
    {
        while (iconViews.Count < targetCount)
        {
            StatusEffectIconView iconView = Instantiate(iconPrefab, Container);
            iconViews.Add(iconView);
        }
    }

    private void ClearIconViews()
    {
        foreach (StatusEffectIconView iconView in iconViews)
        {
            if (iconView != null)
            {
                iconView.Clear();
            }
        }
    }

    private void SetContainerVisible(bool visible)
    {
        Transform targetContainer = Container;
        if (targetContainer == null) return;

        // If no separate container is assigned, do not disable this component's own GameObject.
        // Otherwise the UI would unsubscribe while empty and could not refresh when a status appears later.
        if (targetContainer == transform) return;

        targetContainer.gameObject.SetActive(visible);
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
