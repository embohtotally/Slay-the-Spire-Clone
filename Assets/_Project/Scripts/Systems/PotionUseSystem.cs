using System.Collections;
using System.Collections.Generic;
using Gameseed26;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PotionUseSystem : Singleton<PotionUseSystem>
{
    [Header("Rules")]
    [SerializeField] private bool consumePotionsOnUse = true;
    [Tooltip("If true, potions with Manual Target Effect need a target from code/UI. Auto-target effects can still run without a manual target.")]
    [SerializeField] private bool requireManualTargetForManualEffect = true;
    [SerializeField] private bool logUseFailures = true;

    [Header("Events")]
    public PotionDataUnityEvent OnPotionUseStarted;
    public PotionDataUnityEvent OnPotionUseCompleted;
    public UnityEvent OnPotionUseFailed;

    [Header("Debug")]
    [ReadOnly][SerializeField] private string lastFailureReason;

    protected override void Awake()
    {
        base.Awake();
    }

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<UsePotionGA>(UsePotionPerformer);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<UsePotionGA>();
    }

    public bool TryUsePotionAtIndex(int potionIndex)
    {
        return TryUsePotionAtIndex(potionIndex, null);
    }

    public bool TryUsePotionAtIndex(int potionIndex, CombatantView manualTarget)
    {
        if (!CanRequestPotionUse(potionIndex, out PotionData potion)) return false;

        UsePotionGA usePotionGA = new(potionIndex, potion, manualTarget, consumePotionsOnUse);
        ActionSystem.Instance.Perform(usePotionGA);
        return true;
    }

    public bool TryUsePotion(PotionData potion)
    {
        return TryUsePotion(potion, null);
    }

    public bool TryUsePotion(PotionData potion, CombatantView manualTarget)
    {
        if (potion == null)
        {
            LogFailure("Cannot use a null potion.");
            return false;
        }

        RunPotionManager potionManager = RunPotionManager.Instance;
        if (potionManager == null)
        {
            LogFailure("Cannot use potion because RunPotionManager is missing.");
            return false;
        }

        IReadOnlyList<PotionData> potions = potionManager.CurrentPotions;
        for (int i = 0; i < potions.Count; i++)
        {
            if (potions[i] == potion)
            {
                return TryUsePotionAtIndex(i, manualTarget);
            }
        }

        LogFailure($"Cannot use potion '{potion.Title}' because it is not in the current run potion list.");
        return false;
    }

    [Button("Use First Potion", EButtonEnableMode.Playmode)]
    public void UseFirstPotionForTesting()
    {
        TryUsePotionAtIndex(0);
    }

    private IEnumerator UsePotionPerformer(UsePotionGA usePotionGA)
    {
        if (!ValidatePotionUse(usePotionGA, out RunPotionManager potionManager, out CombatantView caster))
        {
            OnPotionUseFailed?.Invoke();
            yield return null;
            yield break;
        }

        List<GameAction> effectActions = BuildPotionEffectActions(usePotionGA, caster);
        if (effectActions.Count == 0)
        {
            string reason = $"Potion '{usePotionGA.Potion.Title}' has no supported effect to execute. Auto-target effects need Effect + TargetMode; manual effect needs a target.";
            usePotionGA.MarkFailed(reason);
            LogFailure(reason);
            OnPotionUseFailed?.Invoke();
            yield return null;
            yield break;
        }

        if (usePotionGA.ConsumeOnUse && usePotionGA.Potion.Consumable && !potionManager.RemoveAt(usePotionGA.PotionIndex))
        {
            string reason = $"Potion '{usePotionGA.Potion.Title}' effect was cancelled because the potion could not be removed from slot {usePotionGA.PotionIndex}.";
            usePotionGA.MarkFailed(reason);
            LogFailure(reason);
            OnPotionUseFailed?.Invoke();
            yield return null;
            yield break;
        }

        OnPotionUseStarted?.Invoke(usePotionGA.Potion);
        foreach (GameAction effectAction in effectActions)
        {
            ActionSystem.Instance.AddReaction(effectAction);
        }

        usePotionGA.MarkSuccessful();
        OnPotionUseCompleted?.Invoke(usePotionGA.Potion);
        yield return null;
    }

    private bool CanRequestPotionUse(int potionIndex, out PotionData potion)
    {
        potion = null;

        if (ActionSystem.Instance == null)
        {
            LogFailure("Cannot use potion because ActionSystem is missing.");
            return false;
        }

        if (ActionSystem.Instance.IsPerforming)
        {
            LogFailure("Cannot use potion while another action is still being performed.");
            return false;
        }

        RunPotionManager potionManager = RunPotionManager.Instance;
        if (potionManager == null)
        {
            LogFailure("Cannot use potion because RunPotionManager is missing.");
            return false;
        }

        if (!potionManager.HasIndex(potionIndex))
        {
            LogFailure($"Cannot use potion at index {potionIndex}; no potion exists there.");
            return false;
        }

        potion = potionManager.CurrentPotions[potionIndex];
        if (potion == null)
        {
            LogFailure($"Cannot use potion at index {potionIndex}; potion data is missing.");
            return false;
        }

        return true;
    }

    private bool ValidatePotionUse(UsePotionGA usePotionGA, out RunPotionManager potionManager, out CombatantView caster)
    {
        potionManager = RunPotionManager.Instance;
        caster = HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null;

        if (usePotionGA == null)
        {
            LogFailure("Cannot use potion because UsePotionGA is missing.");
            return false;
        }

        if (usePotionGA.Potion == null)
        {
            usePotionGA.MarkFailed("Cannot use potion because PotionData is missing.");
            LogFailure(usePotionGA.FailureReason);
            return false;
        }

        if (!usePotionGA.Potion.UsableInCombat)
        {
            usePotionGA.MarkFailed($"Potion '{usePotionGA.Potion.Title}' is not usable in combat.");
            LogFailure(usePotionGA.FailureReason);
            return false;
        }

        if (potionManager == null)
        {
            usePotionGA.MarkFailed("Cannot use potion because RunPotionManager is missing.");
            LogFailure(usePotionGA.FailureReason);
            return false;
        }

        if (!potionManager.HasIndex(usePotionGA.PotionIndex) || potionManager.CurrentPotions[usePotionGA.PotionIndex] != usePotionGA.Potion)
        {
            usePotionGA.MarkFailed($"Cannot use potion '{usePotionGA.Potion.Title}' because its run slot is no longer valid.");
            LogFailure(usePotionGA.FailureReason);
            return false;
        }

        if (caster == null)
        {
            usePotionGA.MarkFailed("Cannot use potion because HeroSystem/HeroView is missing.");
            LogFailure(usePotionGA.FailureReason);
            return false;
        }

        return true;
    }

    private List<GameAction> BuildPotionEffectActions(UsePotionGA usePotionGA, CombatantView caster)
    {
        List<GameAction> effectActions = new();
        PotionData potion = usePotionGA.Potion;

        if (potion.ManualTargetEffect != null)
        {
            if (usePotionGA.ManualTarget != null)
            {
                effectActions.Add(new PerformEffectGA(potion.ManualTargetEffect, new List<CombatantView> { usePotionGA.ManualTarget }));
            }
            else if (requireManualTargetForManualEffect)
            {
                LogFailure($"Potion '{potion.Title}' has a manual target effect but no manual target was provided. Auto-target effects, if any, will still run.");
            }
        }

        if (potion.OtherEffects != null)
        {
            foreach (AutoTargetEffect effectWrapper in potion.OtherEffects)
            {
                if (effectWrapper == null || effectWrapper.Effect == null)
                {
                    continue;
                }

                if (effectWrapper.TargetMode == null)
                {
                    LogFailure($"Potion '{potion.Title}' has an auto effect without TargetMode.");
                    continue;
                }

                List<CombatantView> targets = effectWrapper.TargetMode.GetTargets(caster);
                effectActions.Add(new PerformEffectGA(effectWrapper.Effect, targets));
            }
        }

        return effectActions;
    }

    private void LogFailure(string message)
    {
        lastFailureReason = message;
        if (!logUseFailures || string.IsNullOrWhiteSpace(message)) return;
        Gameseed26.Logger.Log(this, message);
    }
}
