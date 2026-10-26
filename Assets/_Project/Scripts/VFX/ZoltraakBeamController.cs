using System;
using System.Collections;
using System.Collections.Generic;
using Gameseed26;
using UnityEngine;

/// <summary>
/// Card-agnostic beam VFX driver. Phases: Charge -> Ignition/Travel -> (per-target Impact)
/// -> Hold -> Dissipate. The awaited PlayBeamRoutine can hand control back to the caller at a
/// configured gate (first/last impact) while the remaining phases continue detached, so
/// BeamVfxSystem can resolve damage on the exact impact frame and still let the beam linger.
/// Zero external dependencies (no DOTween): pure coroutines + analytic easing.
/// </summary>
[DisallowMultipleComponent]
public class ZoltraakBeamController : MonoBehaviour
{
    private const int MaxImpactSlots = 4;
    private static readonly int HeadT  = Shader.PropertyToID("_HeadT");
    private static readonly int TailT  = Shader.PropertyToID("_TailT");
    private static readonly int PowerT = Shader.PropertyToID("_PowerT");
    private static readonly int ChargeT = Shader.PropertyToID("_ChargeT");
    private static readonly int DissipateT = Shader.PropertyToID("_DissipateT");
    private static readonly int RingT  = Shader.PropertyToID("_RingT");
    private static readonly int FlashT = Shader.PropertyToID("_FlashT");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly float[] CircleRoll = { 0f, 23f };

    private class ImpactSlot
    {
        public Transform Root, Ring;
        public MeshRenderer RingRenderer;
        public ParticleSystem Sparks, Ion, Smoke;
        public MaterialPropertyBlock Mpb;
    }

    [SerializeField] private Transform beamGroup;
    [SerializeField] private MeshFilter coreFilter, sheathFilter;
    [SerializeField] private MeshRenderer coreRenderer, sheathRenderer;
    [SerializeField] private MeshRenderer[] circleRenderers = new MeshRenderer[2];
    [SerializeField] private Transform[] circleTransforms = new Transform[2];
    [SerializeField] private MeshRenderer muzzleRenderer;
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private ParticleSystem condensation, muzzleBurst;
    [SerializeField] private Transform impactsRoot;

    public bool IsPlaying { get; private set; }
    /// <summary>Raised once when the full visual sequence ends (or StopAndReset). Used by BeamVfxSystem to pool-release.</summary>
    public Action<ZoltraakBeamController> OnSequenceCompleted;

    private readonly List<ImpactSlot> impactSlots = new();
    private MaterialPropertyBlock coreMpb, sheathMpb, muzzleMpb, circleMpbA, circleMpbB;
    private BeamVfxConfig config;
    private int generation;

    private struct Runtime
    {
        public int Gen;
        public IReadOnlyList<CombatantView> Targets;
        public Action<BeamImpact> OnImpact;
        public BeamTiming Timing;
        public int Gate;                        // 0 = await full sequence; N = await N-th impact
        public Vector3 Muzzle, Dir;
        public float Length;
        public int[] Order;                     // target indices sorted near -> far
        public float[] Proj;                    // axial projection per target
        public bool[] Fired;
        public int FiredCount;
        public float Seed;
    }

    private void Awake() => AutoWire();

    private void AutoWire()
    {
        if (coreRenderer != null && impactSlots.Count > 0) return;

        beamGroup = beamGroup != null ? beamGroup : DeepFind(transform, "BeamGroup");
        coreFilter = Assign("Core", ref coreRenderer);
        sheathFilter = Assign("Sheath", ref sheathRenderer);

        circleRenderers[0] = FindRenderer("CircleA"); circleTransforms[0] = DeepFind(transform, "CircleA");
        circleRenderers[1] = FindRenderer("CircleB"); circleTransforms[1] = DeepFind(transform, "CircleB");
        muzzleRenderer = FindRenderer("MuzzleFlash"); muzzleTransform = DeepFind(transform, "MuzzleFlash");

        condensation = DeepFind(transform, "Condensation")?.GetComponent<ParticleSystem>();
        muzzleBurst = DeepFind(transform, "MuzzleBurst")?.GetComponent<ParticleSystem>();
        impactsRoot = DeepFind(transform, "Impacts");

        if (impactsRoot != null)
        {
            for (int i = 0; i < MaxImpactSlots; i++)
            {
                Transform root = impactsRoot.Find($"Impact{i}");
                if (root == null) continue;
                impactSlots.Add(new ImpactSlot
                {
                    Root = root,
                    Ring = root.Find("Ring"),
                    RingRenderer = root.Find("Ring")?.GetComponent<MeshRenderer>(),
                    Sparks = root.Find("Sparks")?.GetComponent<ParticleSystem>(),
                    Ion = root.Find("Ion")?.GetComponent<ParticleSystem>(),
                    Smoke = root.Find("Smoke")?.GetComponent<ParticleSystem>(),
                    Mpb = new MaterialPropertyBlock()
                });
            }
        }

        MeshFilter Assign(string name, ref MeshRenderer renderer)
        {
            Transform t = DeepFind(transform, name);
            renderer = t != null ? t.GetComponent<MeshRenderer>() : null;
            return t != null ? t.GetComponent<MeshFilter>() : null;
        }
    }

    public void Configure(BeamVfxConfig cfg, BeamMaterialSet mats)
    {
        config = cfg;
        AutoWire();

        Mesh coreMesh = BeamMeshFactory.GetUnitCylinder(cfg.CylinderSides);
        Mesh sheathMesh = BeamMeshFactory.GetUnitCylinder(Mathf.Max(6, cfg.CylinderSides - 4));
        Mesh quad = BeamMeshFactory.GetQuad();

        if (coreFilter != null) coreFilter.sharedMesh = coreMesh;
        if (sheathFilter != null)
        {
            sheathFilter.sharedMesh = sheathMesh;
            sheathFilter.transform.localScale = new Vector3(cfg.SheathRadiusMultiplier, cfg.SheathRadiusMultiplier, 1f);
        }
        if (coreRenderer != null) coreRenderer.sharedMaterial = mats.Core;
        if (sheathRenderer != null) sheathRenderer.sharedMaterial = mats.Sheath;
        for (int i = 0; i < circleRenderers.Length; i++)
        {
            if (circleRenderers[i] != null)
            {
                circleRenderers[i].GetComponent<MeshFilter>().sharedMesh = quad;
                circleRenderers[i].sharedMaterial = mats.Circle;
            }
        }
        if (muzzleRenderer != null)
        {
            muzzleRenderer.GetComponent<MeshFilter>().sharedMesh = quad;
            muzzleRenderer.sharedMaterial = mats.Shock;
        }
        foreach (ImpactSlot slot in impactSlots)
        {
            if (slot.RingRenderer != null)
            {
                slot.RingRenderer.GetComponent<MeshFilter>().sharedMesh = quad;
                slot.RingRenderer.sharedMaterial = mats.Shock;
            }
        }

        coreMpb ??= new MaterialPropertyBlock();
        sheathMpb ??= new MaterialPropertyBlock();
        muzzleMpb ??= new MaterialPropertyBlock();
        circleMpbA ??= new MaterialPropertyBlock();
        circleMpbB ??= new MaterialPropertyBlock();

        int layer = SortingLayer.NameToID(cfg.SortingLayerName);
        ApplySorting(coreRenderer, layer, cfg.SortingOrder);
        ApplySorting(sheathRenderer, layer, cfg.SortingOrder + 1);
        ApplySorting(circleRenderers[0], layer, cfg.SortingOrder + 2);
        ApplySorting(circleRenderers[1], layer, cfg.SortingOrder + 3);
        ApplySorting(muzzleRenderer, layer, cfg.SortingOrder + 4);
        foreach (ImpactSlot slot in impactSlots) ApplySorting(slot.RingRenderer, layer, cfg.SortingOrder + 5);

        void ApplySorting(MeshRenderer r, int layerId, int order)
        {
            if (r == null) return;
            r.sortingLayerID = layerId;
            r.sortingOrder = order;
        }
    }

    public IEnumerator PlayBeamRoutine(Transform casterOrigin, CombatantView target,
        BeamVfxConfig cfg, Action<BeamImpact> onTargetImpact = null, BeamTiming? timingOverride = null)
    {
        yield return PlayBeamRoutine(casterOrigin,
            target != null ? new[] { target } : null, cfg, onTargetImpact, timingOverride, 0);
    }

    public IEnumerator PlayBeamRoutine(Transform casterOrigin, IReadOnlyList<CombatantView> targets,
        BeamVfxConfig cfg, Action<BeamImpact> onTargetImpact = null,
        BeamTiming? timingOverride = null, int gateAfterImpacts = 0)
    {
        Runtime rt = BuildRuntime(casterOrigin, targets, cfg, onTargetImpact, timingOverride, gateAfterImpacts);

        if (rt.Targets == null || rt.Targets.Count == 0 || rt.Order == null)
        {
            FireAllImpactsImmediate(ref rt);
            yield break;
        }

        IsPlaying = true;

        yield return ChargeRoutine(rt);

        // --- PHASE 2: ignition + travel ---
        if (cfg.FireSfx != SfxID.None) Tune.SFX(cfg.FireSfx);
        if (condensation != null) condensation.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (muzzleBurst != null) muzzleBurst.Emit(cfg.MuzzleBurstCount);
        if (muzzleRenderer != null)
            StartCoroutine(QuadFlashRoutine(muzzleRenderer, muzzleMpb, muzzleTransform, 0.10f, 0.22f, cfg.BeamRadius * 5f));

        float head = 0f, power = 0f;
        while (head < rt.Length)
        {
            if (rt.Gen != generation) yield break;
            head += rt.Timing.BeamTravelSpeed * Time.deltaTime;
            power = Mathf.Clamp01(power + Time.deltaTime / 0.08f);   // 80 ms ignition ramp
            PushBeam(rt, Mathf.Clamp01(head / rt.Length), 0f, power);
            TryFireImpacts(ref rt, head);

            if (rt.Gate > 0 && rt.FiredCount >= rt.Gate)
            {
                StartCoroutine(TailRoutine(rt));
                yield break;
            }
            yield return null;
        }

        PushBeam(rt, 1f, 0f, 1f);
        TryFireImpacts(ref rt, float.MaxValue);

        yield return TailRoutine(rt);
    }

    public void StopAndReset()
    {
        generation++;
        StopAllCoroutines();
        StopParticles(condensation, muzzleBurst);
        foreach (ImpactSlot slot in impactSlots) { StopParticles(slot.Sparks, slot.Ion, slot.Smoke); }
        bool wasPlaying = IsPlaying;
        IsPlaying = false;
        if (wasPlaying) OnSequenceCompleted?.Invoke(this);
        OnSequenceCompleted = null;
    }

    private IEnumerator ChargeRoutine(Runtime rt)
    {
        float duration = rt.Timing.ChargeDuration;
        if (duration <= 0f) yield break;

        if (config.ChargeSfx != SfxID.None) Tune.SFX(config.ChargeSfx);
        if (condensation != null) condensation.Play();

        Camera cam = Camera.main;
        Vector3 camPos = cam != null ? cam.transform.position : rt.Muzzle + Vector3.back * 10f;
        Vector3 camDir = (camPos - rt.Muzzle).normalized;
        Vector3 normal = Vector3.Slerp(rt.Dir, camDir, config.CircleCameraTilt).normalized;
        for (int i = 0; i < circleTransforms.Length; i++)
        {
            if (circleTransforms[i] == null) continue;
            circleTransforms[i].localPosition = new Vector3(0f, 0f, 0.05f + 0.12f * i);
            circleTransforms[i].rotation = Quaternion.LookRotation(normal, Vector3.up)
                                           * Quaternion.Euler(0f, 0f, CircleRoll[i % CircleRoll.Length]);
            circleTransforms[i].localScale = Vector3.one * 0.01f;
        }

        float t = 0f;
        while (t < duration)
        {
            if (rt.Gen != generation) yield break;
            t += Time.deltaTime;
            float ct = Mathf.Clamp01(t / duration);
            float reveal = EaseOutCubic(ct);

            PushCircle(circleRenderers[0], circleMpbA, Mathf.Clamp01(ct * 1.25f), 0f, rt.Seed, reveal);
            PushCircle(circleRenderers[1], circleMpbB, Mathf.Clamp01(ct * 1.25f - 0.25f), 0f, rt.Seed, reveal);
            yield return null;
        }
    }

    private IEnumerator TailRoutine(Runtime rt)
    {
        // --- PHASE 3: hold ---
        float hold = rt.Timing.HoldDuration, ht = 0f;
        while (ht < hold)
        {
            if (rt.Gen != generation) yield break;
            ht += Time.deltaTime;
            float flicker = 0.92f + 0.08f * Mathf.PingPong(Time.time * 37f, 1f);
            PushBeam(rt, 1f, 0f, flicker);
            yield return null;
        }

        // --- PHASE 4: dissipate ---
        if (condensation != null) condensation.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        float dis = rt.Timing.DissipateDuration, dt = 0f;
        while (dt < dis)
        {
            if (rt.Gen != generation) yield break;
            dt += Time.deltaTime;
            float k = Mathf.Clamp01(dt / dis);
            float corePower = Mathf.Max(0f, 1f - EaseInCubic(Mathf.Clamp01(k / 0.45f)));
            float tailT = EaseInCubic(k);
            PushBeam(rt, 1f, tailT, corePower);
            PushCircle(circleRenderers[0], circleMpbA, 1f, k, rt.Seed, 1f);
            PushCircle(circleRenderers[1], circleMpbB, 1f, k, rt.Seed, 1f);
            yield return null;
        }

        Finish(rt);
    }

    private void Finish(Runtime rt)
    {
        if (rt.Gen != generation) return;
        StopParticles(condensation, muzzleBurst);
        foreach (ImpactSlot slot in impactSlots) StopParticles(slot.Sparks, slot.Ion, slot.Smoke);
        generation++;
        IsPlaying = false;
        OnSequenceCompleted?.Invoke(this);
        OnSequenceCompleted = null;
    }

    private void TryFireImpacts(ref Runtime rt, float headDist)
    {
        for (int k = 0; k < rt.Order.Length; k++)
        {
            int i = rt.Order[k];
            if (rt.Fired[i] || headDist < rt.Proj[i]) continue;
            rt.Fired[i] = true;
            rt.FiredCount++;

            Vector3 point = rt.Muzzle + rt.Dir * rt.Proj[i] + Vector3.forward * config.DepthOffset;
            TriggerImpactVisuals(i, point, rt.Dir);
            if (config.ImpactSfx != SfxID.None) Tune.SFX(config.ImpactSfx);

            rt.OnImpact?.Invoke(new BeamImpact(i, rt.Targets[i], point, rt.Dir, config));
        }
    }

    private void TriggerImpactVisuals(int targetIndex, Vector3 worldPoint, Vector3 dir)
    {
        if (impactSlots.Count == 0) return;
        ImpactSlot slot = impactSlots[targetIndex % impactSlots.Count];

        if (slot.Root != null)
        {
            slot.Root.position = worldPoint;
            slot.Root.rotation = Quaternion.LookRotation(dir, Vector3.up);
            if (slot.Sparks != null) slot.Sparks.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
        if (slot.Sparks != null) slot.Sparks.Emit(config.SparkBurstCount);
        if (slot.Ion != null) slot.Ion.Emit(config.IonLingerCount);
        if (slot.Smoke != null) slot.Smoke.Emit(config.SmokePuffCount);
        if (slot.RingRenderer != null)
            StartCoroutine(QuadFlashRoutine(slot.RingRenderer, slot.Mpb, slot.Ring, 0.12f, 0.34f, config.BeamRadius * 9f));
    }

    private void FireAllImpactsImmediate(ref Runtime rt)
    {
        if (rt.Order == null) return;
        for (int k = 0; k < rt.Order.Length; k++)
        {
            int i = rt.Order[k];
            rt.OnImpact?.Invoke(new BeamImpact(i, rt.Targets[i], rt.Muzzle + rt.Dir * rt.Proj[i], rt.Dir, config));
        }
    }

    private void PushBeam(in Runtime rt, float headT, float tailT, float powerT)
    {
        if (coreRenderer != null)
        {
            coreRenderer.GetPropertyBlock(coreMpb);
            coreMpb.SetFloat(HeadT, headT);
            coreMpb.SetFloat(TailT, tailT);
            coreMpb.SetFloat(PowerT, powerT);
            coreMpb.SetFloat(SeedId, rt.Seed);
            coreRenderer.SetPropertyBlock(coreMpb);
        }
        if (sheathRenderer != null)
        {
            sheathRenderer.GetPropertyBlock(sheathMpb);
            sheathMpb.SetFloat(HeadT, headT * 0.96f);
            sheathMpb.SetFloat(TailT, tailT);
            sheathMpb.SetFloat(PowerT, powerT);
            sheathMpb.SetFloat(SeedId, rt.Seed * 1.37f);
            sheathRenderer.SetPropertyBlock(sheathMpb);
        }
    }

    private void PushCircle(MeshRenderer renderer, MaterialPropertyBlock mpb, float charge, float dissipate, float seed, float reveal)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(mpb);
        mpb.SetFloat(ChargeT, charge);
        mpb.SetFloat(DissipateT, dissipate);
        mpb.SetFloat(SeedId, seed);
        renderer.SetPropertyBlock(mpb);

        Transform t = renderer.transform;
        float scale = config.CircleRadius * 2f * (0.15f + 0.85f * EaseOutBack(reveal)) * (1f + 0.35f * dissipate);
        t.localScale = Vector3.one * Mathf.Max(0.01f, scale);
    }

    private IEnumerator QuadFlashRoutine(MeshRenderer renderer, MaterialPropertyBlock mpb,
                                         Transform quad, float flashDuration, float ringDuration, float scale)
    {
        Camera cam = Camera.main;
        quad.localScale = Vector3.one * scale;
        float t = 0f;

        renderer.GetPropertyBlock(mpb);
        mpb.SetFloat(RingT, 0f); mpb.SetFloat(FlashT, 0f);
        renderer.SetPropertyBlock(mpb);

        while (t < 1f)
        {
            t += Time.deltaTime / flashDuration;
            if (cam != null) quad.rotation = Quaternion.LookRotation((cam.transform.position - quad.position).normalized, Vector3.up);
            renderer.GetPropertyBlock(mpb);
            mpb.SetFloat(FlashT, Mathf.Clamp01(t));
            renderer.SetPropertyBlock(mpb);
            yield return null;
        }
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / ringDuration;
            if (cam != null) quad.rotation = Quaternion.LookRotation((cam.transform.position - quad.position).normalized, Vector3.up);
            renderer.GetPropertyBlock(mpb);
            mpb.SetFloat(RingT, EaseOutExpo(Mathf.Clamp01(t)));
            renderer.SetPropertyBlock(mpb);
            yield return null;
        }
        renderer.GetPropertyBlock(mpb);
        mpb.SetFloat(RingT, 1f); mpb.SetFloat(FlashT, 1f);
        renderer.SetPropertyBlock(mpb);
    }

    private Runtime BuildRuntime(Transform casterOrigin, IReadOnlyList<CombatantView> targets,
        BeamVfxConfig cfg, Action<BeamImpact> onImpact, BeamTiming? timingOverride, int gate)
    {
        Runtime rt = new()
        {
            Gen = ++generation,
            Targets = targets,
            OnImpact = onImpact,
            Timing = timingOverride ?? new BeamTiming(cfg),
            Gate = gate,
            Seed = UnityEngine.Random.value * 100f
        };
        IsPlaying = true;
        if (casterOrigin == null || targets == null || targets.Count == 0 || cfg == null) { rt.Order = null; return rt; }

        Vector3 muzzle = ResolveAnchor(casterOrigin, cfg.CasterAnchorNames, cfg.CasterAnchorOffset);
        muzzle.z += cfg.DepthOffset;

        int n = targets.Count;
        var valid = new List<(int idx, CombatantView v, float proj)>(n);
        Vector3 far = muzzle;
        for (int i = 0; i < n; i++)
        {
            if (targets[i] == null) continue;
            Vector3 p = ResolveAnchor(targets[i].transform, cfg.TargetAnchorNames, cfg.TargetAnchorOffset);
            p.z += cfg.DepthOffset;
            valid.Add((i, targets[i], 0f));
            if ((p - muzzle).sqrMagnitude > (far - muzzle).sqrMagnitude) far = p;
        }
        if (valid.Count == 0) { rt.Order = null; return rt; }

        Vector3 dir = far - muzzle;
        float baseLen = dir.magnitude;
        dir = baseLen < 0.01f ? Vector3.right : dir / baseLen;

        rt.Muzzle = muzzle;
        rt.Dir = dir;
        rt.Length = baseLen + cfg.PierceOvershoot;

        rt.Order = new int[valid.Count];
        rt.Proj = new float[valid.Count];
        for (int i = 0; i < valid.Count; i++)
        {
            Vector3 p = ResolveAnchor(valid[i].v.transform, cfg.TargetAnchorNames, cfg.TargetAnchorOffset);
            p.z += cfg.DepthOffset;
            valid[i] = (valid[i].idx, valid[i].v, Vector3.Dot(p - muzzle, dir));
            rt.Proj[i] = valid[i].proj;
            rt.Order[i] = i;
        }
        Array.Sort(rt.Order, (a, b) => rt.Proj[a].CompareTo(rt.Proj[b]));

        rt.Fired = new bool[n];
        rt.FiredCount = 0;

        transform.SetPositionAndRotation(muzzle, Quaternion.LookRotation(dir, Vector3.up));
        if (beamGroup != null)
        {
            beamGroup.localPosition = Vector3.zero;
            beamGroup.localScale = new Vector3(cfg.BeamRadius * 2f, cfg.BeamRadius * 2f, rt.Length);
        }
        return rt;
    }

    private static Vector3 ResolveAnchor(Transform root, string[] names, Vector3 localFallback)
    {
        if (root == null) return Vector3.zero;
        for (int i = 0; i < names?.Length; i++)
        {
            Transform t = DeepFind(root, names[i]);
            if (t != null) return t.position;
        }
        return root.TransformPoint(localFallback);
    }

    private static Transform DeepFind(Transform root, string childName)
    {
        if (root.name == childName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = DeepFind(root.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }

    private MeshRenderer FindRenderer(string name)
    {
        Transform t = DeepFind(transform, name);
        return t != null ? t.GetComponent<MeshRenderer>() : null;
    }

    private static void StopParticles(params ParticleSystem[] systems)
    {
        foreach (ParticleSystem ps in systems)
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInCubic(float t) => t * t * t;
    private static float EaseOutExpo(float t) => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
    private static float EaseOutBack(float t)
    {
        const float c = 1.70158f;
        float u = t - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }
}
