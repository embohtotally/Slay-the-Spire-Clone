using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public readonly struct BeamImpact
{
    public readonly int Index;
    public readonly CombatantView Target;
    public readonly Vector3 Position;
    public readonly Vector3 Direction;
    public readonly BeamVfxConfig Config;

    public BeamImpact(int index, CombatantView target, Vector3 position, Vector3 direction, BeamVfxConfig config)
    {
        Index = index;
        Target = target;
        Position = position;
        Direction = direction;
        Config = config;
    }
}

public class BeamMaterialSet
{
    public Material Core, Sheath, Circle, Shock;
}

/// <summary>
/// Owns PlayBeamVfxGA execution: rents pooled beams, gates the performer on the impact frame,
/// queues payload GameActions via ActionSystem.AddReaction (same-frame delivery), and broadcasts
/// impact events for decoupled camera FX subscribers.
/// </summary>
[System.Serializable]
public class BeamVfxSystem : Singleton<BeamVfxSystem>
{
    [SerializeField] private int prewarmPerPrefab = 2;

    /// <summary>Decoupled camera FX hook (BeamCameraFx or your own system subscribes).</summary>
    public static event System.Action<BeamImpact> ImpactOccurred;

    private readonly Dictionary<BeamVfxConfig, ObjectPool<ZoltraakBeamController>> pools = new();
    private readonly Dictionary<BeamVfxConfig, BeamMaterialSet> materialSets = new();
    private readonly List<ZoltraakBeamController> activeBeams = new();
    private readonly Queue<GameAction> subflowQueue = new();
    private Coroutine subflowPump;
    private int pendingSubflowCasts;

    private void OnEnable()
    {
        ActionSystem.AttachPerformer<PlayBeamVfxGA>(PlayBeamPerformer);
    }

    private void OnDisable()
    {
        ActionSystem.DetachPerformer<PlayBeamVfxGA>();
        for (int i = activeBeams.Count - 1; i >= 0; i--) activeBeams[i].StopAndReset();
        activeBeams.Clear();
        if (subflowPump != null) { StopCoroutine(subflowPump); subflowPump = null; }
        subflowQueue.Clear();
        pendingSubflowCasts = 0;
        foreach (ObjectPool<ZoltraakBeamController> pool in pools.Values) pool.Clear();
        pools.Clear();
        foreach (BeamMaterialSet set in materialSets.Values)
        {
            if (set.Core != null) Destroy(set.Core);
            if (set.Sheath != null) Destroy(set.Sheath);
            if (set.Circle != null) Destroy(set.Circle);
            if (set.Shock != null) Destroy(set.Shock);
        }
        materialSets.Clear();
    }

    private IEnumerator PlayBeamPerformer(PlayBeamVfxGA ga)
    {
        BeamVfxConfig config = ga.Config;

        if (config == null || config.BeamPrefab == null)
        {
            foreach (GameAction payload in ga.ImpactPayloads) ActionSystem.Instance.AddReaction(payload);
            yield break;
        }

        ZoltraakBeamController beam = Rent(config);
        int gate = ResolveGateCount(ga);

        yield return beam.PlayBeamRoutine(
            ga.Caster != null ? ga.Caster.transform : null,
            ga.Targets,
            config,
            impact => HandleImpact(ga, impact),
            null,
            gate);
    }

    private static int ResolveGateCount(PlayBeamVfxGA ga)
    {
        int n = Mathf.Max(1, ga.Targets?.Count ?? 1);
        if (ga.Config.UsePerTargetSubflows) return 1;
        return ga.Config.GateMode switch
        {
            BeamGateMode.FirstImpact => 1,
            BeamGateMode.LastImpact => n,
            BeamGateMode.FullSequence => 0,
            _ => n > 1 ? n : 1,
        };
    }

    private void HandleImpact(PlayBeamVfxGA ga, BeamImpact impact)
    {
        BeamVfxConfig config = ga.Config;
        bool dead = impact.Target != null && impact.Target.CurrentHealth <= 0;

        if (!(config.SkipDeadTargets && dead))
        {
            if (config.UsePerTargetSubflows)
            {
                subflowQueue.Enqueue(ga.ImpactPayloads[impact.Index]);
                EnsureSubflowPump();
            }
            else
            {
                ActionSystem.Instance.AddReaction(ga.ImpactPayloads[impact.Index]);
            }
        }
        ImpactOccurred?.Invoke(impact);
    }

    private void EnsureSubflowPump()
    {
        if (subflowPump == null) subflowPump = StartCoroutine(SubflowPumpRoutine());
    }

    private IEnumerator SubflowPumpRoutine()
    {
        while (pendingSubflowCasts > 0 || subflowQueue.Count > 0)
        {
            if (subflowQueue.Count > 0)
                yield return ActionSystem.Instance.PerformSubflow(subflowQueue.Dequeue());
            else
                yield return null;
        }
        subflowPump = null;
    }

    private ZoltraakBeamController Rent(BeamVfxConfig config)
    {
        if (!pools.TryGetValue(config, out ObjectPool<ZoltraakBeamController> pool))
        {
            pool = new ObjectPool<ZoltraakBeamController>(
                createFunc: () =>
                {
                    GameObject go = Instantiate(config.BeamPrefab, transform);
                    return go.GetComponent<ZoltraakBeamController>();
                },
                actionOnGet: c => { c.gameObject.SetActive(true); c.Configure(config, GetMaterialSet(config)); },
                actionOnRelease: c => { c.OnSequenceCompleted = null; c.StopAndReset(); c.gameObject.SetActive(false); },
                actionOnDestroy: c => Destroy(c.gameObject),
                collectionCheck: true, defaultCapacity: 4, maxSize: 8);
            for (int i = 0; i < prewarmPerPrefab; i++) { var c = pool.Get(); pool.Release(c); }
            pools.Add(config, pool);
        }

        ZoltraakBeamController beam = pool.Get();
        activeBeams.Add(beam);
        System.Action<ZoltraakBeamController> release = b => { activeBeams.Remove(b); pool.Release(b); };
        beam.OnSequenceCompleted = release;

        if (config.UsePerTargetSubflows)
        {
            pendingSubflowCasts++;
            beam.OnSequenceCompleted = b => { release(b); pendingSubflowCasts--; };
            EnsureSubflowPump();
        }
        return beam;
    }

    private BeamMaterialSet GetMaterialSet(BeamVfxConfig config)
    {
        if (!materialSets.TryGetValue(config, out BeamMaterialSet set))
        {
            set = new BeamMaterialSet
            {
                Core = new Material(config.BaseCoreMaterial),
                Sheath = new Material(config.BaseSheathMaterial),
                Circle = new Material(config.BaseCircleMaterial),
                Shock = new Material(config.BaseShockMaterial)
            };
            set.Core.SetColor("_CoreColor", config.CoreColor);
            set.Core.SetColor("_HotColor", config.HotColor);
            set.Core.SetFloat("_CoreIntensity", config.CoreIntensity);
            set.Sheath.SetColor("_SheathColor", config.SheathColor);
            set.Sheath.SetFloat("_SheathIntensity", config.SheathIntensity);
            set.Circle.SetColor("_CircleColor", config.CircleColor);
            set.Circle.SetFloat("_CircleIntensity", config.CircleIntensity);
            set.Shock.SetColor("_RingColor", config.ImpactRingColor);
            set.Shock.SetColor("_FlashColor", config.ImpactFlashColor);
            set.Shock.SetFloat("_RingIntensity", config.ImpactRingIntensity);
            set.Shock.SetFloat("_FlashIntensity", config.ImpactFlashIntensity);
            materialSets.Add(config, set);
        }
        return set;
    }
}
