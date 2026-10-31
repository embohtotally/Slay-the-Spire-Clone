using System;
using System.Collections;
using Gameseed26;
using UnityEngine;

public enum FrierenShieldState
{
    Inactive,
    Deploying,
    Active,
    HitReaction,
    Shattering,
    Dissolving
}

/// <summary>
/// Frieren's Ordinary Defense Magic: golden hexagonal honeycomb barrier with a backing
/// magic circle. Persistent attachment on a CombatantView (spawned/owned by FrierenShieldSystem).
/// Lifecycle: Inactive -> Deploying -> Active <-> HitReaction -> Shattering/Dissolving -> Inactive.
/// Fully counters the Zoltraak beam: FrierenShieldSystem feeds exact beam impact positions
/// into OnHit so the ripple wave starts on the exact damage frame, at the exact contact point.
/// Zero external dependencies (no DOTween): pure coroutines + analytic easing.
/// All per-instance uniforms go through MaterialPropertyBlock: zero allocations on hot paths.
/// </summary>
[DisallowMultipleComponent]
public class FrierenShieldController : MonoBehaviour
{
    private static readonly int DeployTId       = Shader.PropertyToID("_DeployT");
    private static readonly int DissolveTId     = Shader.PropertyToID("_DissolveT");
    private static readonly int ShatterTId      = Shader.PropertyToID("_ShatterT");
    private static readonly int HitPosOSId      = Shader.PropertyToID("_HitPosOS");
    private static readonly int HitTimeId       = Shader.PropertyToID("_HitTime");
    private static readonly int HitIntensityId  = Shader.PropertyToID("_HitIntensity");
    private static readonly int SeedId          = Shader.PropertyToID("_Seed");
    private static readonly int FlashTId        = Shader.PropertyToID("_FlashT");
    private static readonly int RingTId         = Shader.PropertyToID("_RingT");

    [Header("Wiring (auto-resolved by child name if left empty)")]
    [SerializeField] private Transform barrier;
    [SerializeField] private MeshFilter barrierFilter;
    [SerializeField] private MeshRenderer barrierRenderer;
    [SerializeField] private Transform circle;
    [SerializeField] private MeshFilter circleFilter;
    [SerializeField] private MeshRenderer circleRenderer;
    [SerializeField] private Transform shatterFlash;
    [SerializeField] private MeshRenderer shatterFlashRenderer;
    [SerializeField] private ParticleSystem shards;

    [Header("Placement")]
    [SerializeField] private float radius = 0.62f;
    [Tooltip("World-space standoff in front of the owner, along the threat direction.")]
    [SerializeField] private float standOff = 0.72f;
    [SerializeField] private float heightOffset = 0.18f;
    [Tooltip("Z offset toward the camera so additive layers sit above the owner sprite.")]
    [SerializeField] private float depthOffset = -0.35f;
    [Tooltip("Local -Z distance of the backing magic circle behind the barrier.")]
    [SerializeField] private float circleBackOffset = 0.07f;
    [Tooltip("0 = barrier faces the threat exactly; 1 = full camera billboard.")]
    [SerializeField, Range(0f, 1f)] private float cameraTilt = 0.55f;

    [Header("Timing (seconds)")]
    [SerializeField] private float deployDuration = 0.25f;
    [SerializeField] private float reinforceDuration = 0.28f;
    [SerializeField] private float dissolveDuration = 0.35f;
    [SerializeField] private float shatterDuration = 0.45f;
    [Tooltip("Keeps the GameObject alive after shatter so world-space shard embers finish.")]
    [SerializeField] private float shardLinger = 1.30f;

    [Header("Hit Response")]
    [SerializeField] private float hitShakeDuration = 0.16f;
    [SerializeField] private float hitShakeAmplitude = 0.05f;
    [SerializeField] private float hitIntensityBase = 1.1f;
    [SerializeField] private float hitIntensityPerDamage = 0.12f;
    [SerializeField] private float hitIntensityMax = 3.2f;
    [Tooltip("Stop advancing _HitTime after this many seconds (ripple fully damped).")]
    [SerializeField] private float rippleCutoff = 1.1f;

    [Header("Shatter FX")]
    [SerializeField] private int shardCount = 42;
    [SerializeField] private SfxID shatterSfx = SfxID.Shield;

    [Header("Mesh (runtime fallback when the prefab has no mesh assets)")]
    [SerializeField, Range(3, 12)] private int meshSegments = 6;
    [SerializeField, Range(-0.3f, 0.3f)] private float meshCurvature = 0.10f;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 42;

    public FrierenShieldState State { get; private set; } = FrierenShieldState.Inactive;
    public CombatantView Owner { get; private set; }

    /// <summary>Raised once when Shatter() ignites (position = barrier world position).
    /// FrierenShieldSystem re-broadcasts it as a static event for decoupled camera FX.</summary>
    public event Action<FrierenShieldController, Vector3> Shattered;

    private MaterialPropertyBlock barrierMpb, circleMpb, flashMpb;
    private Vector3 barrierLocalPos, circleLocalPos;
    private float barrierBaseScale, circleBaseScale;
    private Vector3 hitPosOS;
    private float hitTime = 999f;
    private float hitIntensity;
    private bool hitActive;
    private float seed;
    private int generation;

    private void Awake() => AutoWire();

    // ------------------------------------------------------------------ public API

    /// <summary>Deploys the barrier (scale punch + _DeployT 0 -> 1 over deployDuration).
    /// If a barrier is already up, reinforces it instead (punch + center energy surge).</summary>
    public void Deploy(int shieldAmount)
    {
        generation++;
        StopAllCoroutines();
        gameObject.SetActive(true);

        if (State == FrierenShieldState.Active || State == FrierenShieldState.HitReaction)
        {
            StartCoroutine(ReinforceRoutine(generation, shieldAmount));
            return;
        }
        StartCoroutine(DeployRoutine(generation));
    }

    /// <summary>Localized hit reaction: converts the world hit point to object space,
    /// starts the ripple wave at that point and shakes the barrier. No-ops when no
    /// barrier is visible.</summary>
    public void OnHit(int damageAbsorbed, Vector3 worldHitPosition)
    {
        if (State == FrierenShieldState.Inactive ||
            State == FrierenShieldState.Shattering ||
            State == FrierenShieldState.Dissolving) return;

        Transform space = barrier != null ? barrier : transform;
        hitPosOS = space.InverseTransformPoint(worldHitPosition);
        hitTime = 0f;
        hitIntensity = Mathf.Clamp(hitIntensityBase + hitIntensityPerDamage * Mathf.Max(0, damageAbsorbed),
                                   0f, hitIntensityMax);
        hitActive = true;
        PushHit();

        if (State == FrierenShieldState.Active)
        {
            State = FrierenShieldState.HitReaction;
            StartCoroutine(ShakeRoutine(generation));
        }
    }

    /// <summary>Crystalline fracture: hex tiles scatter and fade, shard embers burst,
    /// gold shatter flash ignites, then the GameObject deactivates once embers finish.</summary>
    public void Shatter()
    {
        if (State == FrierenShieldState.Inactive || State == FrierenShieldState.Shattering) return;
        generation++;
        StopAllCoroutines();
        StartCoroutine(ShatterRoutine(generation));
    }

    /// <summary>Tile-by-tile dissolve when the shield naturally expires at turn start.</summary>
    public void Dissolve()
    {
        if (State == FrierenShieldState.Inactive ||
            State == FrierenShieldState.Shattering ||
            State == FrierenShieldState.Dissolving) return;
        generation++;
        StopAllCoroutines();
        StartCoroutine(DissolveRoutine(generation));
    }

    // ------------------------------------------------------------------ lifecycle

    private void Update()
    {
        // Hot path guarantee: single bool check while idle, zero allocations while active.
        if (!hitActive) return;

        hitTime += Time.deltaTime;
        if (hitTime >= rippleCutoff)
        {
            hitActive = false;
            hitTime = 999f;              // fully damped in-shader (exp(-999 * damping) == 0)
        }
        PushHit();
    }

    public void Initialize(CombatantView owner)
    {
        Owner = owner;
        AutoWire();
        EnsureMeshesAndMaterials();
        ApplySorting();
        CacheBaseTransforms();
        State = FrierenShieldState.Inactive;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ wiring

    private void AutoWire()
    {
        if (barrierRenderer != null) return;

        barrier = DeepFind(transform, "Barrier");
        circle = DeepFind(transform, "Circle");
        shatterFlash = DeepFind(transform, "ShatterFlash");

        if (barrier != null)
        {
            barrierFilter = barrier.GetComponent<MeshFilter>();
            barrierRenderer = barrier.GetComponent<MeshRenderer>();
        }
        if (circle != null)
        {
            circleFilter = circle.GetComponent<MeshFilter>();
            circleRenderer = circle.GetComponent<MeshRenderer>();
        }
        if (shatterFlash != null)
        {
            shatterFlashRenderer = shatterFlash.GetComponent<MeshRenderer>();
        }
        Transform shardsTransform = DeepFind(transform, "Shards");
        shards = shardsTransform != null ? shardsTransform.GetComponent<ParticleSystem>() : null;
    }

    private void EnsureMeshesAndMaterials()
    {
        if (barrierFilter != null && barrierFilter.sharedMesh == null)
            barrierFilter.sharedMesh = FrierenShieldMeshFactory.GetCurvedHexDisc(meshSegments, meshCurvature);
        if (circleFilter != null && circleFilter.sharedMesh == null)
            circleFilter.sharedMesh = FrierenShieldMeshFactory.GetUnitQuad();

        if (barrierRenderer != null && barrierRenderer.sharedMaterial == null)
            barrierRenderer.sharedMaterial = FrierenShieldSystem.GetSharedBarrierMaterial();
        if (circleRenderer != null && circleRenderer.sharedMaterial == null)
            circleRenderer.sharedMaterial = FrierenShieldSystem.GetSharedCircleMaterial();

        if (shatterFlashRenderer != null && shatterFlashRenderer.sharedMaterial == null)
        {
            Material flash = FrierenShieldSystem.GetSharedFlashMaterial();
            if (flash == null)
            {
                shatterFlashRenderer = null;
                shatterFlash = null;
            }
            else
            {
                shatterFlashRenderer.sharedMaterial = flash;
                MeshFilter flashFilter = shatterFlash.GetComponent<MeshFilter>();
                if (flashFilter != null && flashFilter.sharedMesh == null)
                    flashFilter.sharedMesh = FrierenShieldMeshFactory.GetUnitQuad();
            }
        }
        if (shatterFlashRenderer != null) shatterFlashRenderer.enabled = false;
    }

    private void ApplySorting()
    {
        int layer = SortingLayer.NameToID(sortingLayerName);
        ApplySorting(barrierRenderer, layer, sortingOrder);
        ApplySorting(circleRenderer, layer, sortingOrder - 1);
        ApplySorting(shatterFlashRenderer, layer, sortingOrder + 2);

        void ApplySorting(MeshRenderer renderer, int layerId, int order)
        {
            if (renderer == null) return;
            renderer.sortingLayerID = layerId;
            renderer.sortingOrder = order;
        }
    }

    private void CacheBaseTransforms()
    {
        // Canonical locals (EnsurePlacement enforces the same values each deploy),
        // so ShakeRoutine always restores to the correct base regardless of prefab state.
        barrierLocalPos = Vector3.zero;
        circleLocalPos = new Vector3(0f, 0f, -circleBackOffset);
        barrierBaseScale = radius * 2f;
        circleBaseScale = radius * 2.5f;
    }

    /// <summary>World-space placement: stands off in front of the owner toward the nearest
    /// threat, tilted toward the camera by cameraTilt so the disc reads from the 2D view.
    /// Computed in world space so flipped/scaled parents cannot mirror the barrier.</summary>
    private void EnsurePlacement()
    {
        if (Owner == null) return;

        Transform ownerTransform = Owner.transform;
        Vector3 ownerPos = ownerTransform.position;

        Vector3 threat = FrierenShieldSystem.GetThreatPosition(Owner);
        Vector3 attackDir = threat - ownerPos;
        attackDir.z = 0f;
        attackDir = attackDir.sqrMagnitude > 0.0001f
            ? attackDir.normalized
            : (Owner is HeroView ? Vector3.right : Vector3.left);

        Camera cam = Camera.main;
        Vector3 toCam = cam != null ? (cam.transform.position - ownerPos).normalized : Vector3.back;
        Vector3 normal = Vector3.Slerp(attackDir, toCam, cameraTilt).normalized;

        Vector3 pos = ownerPos + attackDir * standOff + Vector3.up * heightOffset;
        pos.z += depthOffset;
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(normal, Vector3.up));

        if (barrier != null)
        {
            barrier.localPosition = Vector3.zero;
            barrier.localScale = Vector3.one * barrierBaseScale;
        }
        if (circle != null)
        {
            circle.localPosition = new Vector3(0f, 0f, -circleBackOffset);
            circle.localScale = Vector3.one * circleBaseScale;
        }
    }

    // ------------------------------------------------------------------ routines

    private IEnumerator DeployRoutine(int gen)
    {
        State = FrierenShieldState.Deploying;
        EnsurePlacement();
        SetRenderersVisible(true);
        ResetUniforms();

        float t = 0f;
        while (t < deployDuration && gen == generation)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, deployDuration));
            float deployT = EaseOutCubic(k);

            if (barrier != null)
                barrier.localScale = Vector3.one * (barrierBaseScale * (0.55f + 0.45f * EaseOutBack(k)));
            if (circle != null)
                circle.localScale = Vector3.one * (circleBaseScale * (0.40f + 0.60f * EaseOutCubic(Mathf.Clamp01(k * 1.2f))));

            PushPhase(deployT, 0f, 0f);
            yield return null;
        }
        if (gen != generation) yield break;

        if (barrier != null) barrier.localScale = Vector3.one * barrierBaseScale;
        if (circle != null) circle.localScale = Vector3.one * circleBaseScale;
        PushPhase(1f, 0f, 0f);
        State = FrierenShieldState.Active;
    }

    private IEnumerator ReinforceRoutine(int gen, int shieldAmount)
    {
        State = FrierenShieldState.HitReaction;

        // Center energy surge: reuses the ripple machinery for a pulse blooming from the middle.
        hitPosOS = Vector3.zero;
        hitTime = 0f;
        hitIntensity = Mathf.Clamp(0.45f + shieldAmount * 0.035f, 0.45f, 1.4f);
        hitActive = true;
        PushHit();

        float t = 0f;
        while (t < reinforceDuration && gen == generation)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, reinforceDuration));
            float punch = 1f + 0.16f * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
            if (barrier != null) barrier.localScale = Vector3.one * (barrierBaseScale * punch);
            if (circle != null) circle.localScale = Vector3.one * (circleBaseScale * punch);
            yield return null;
        }
        if (gen != generation) yield break;

        if (barrier != null) barrier.localScale = Vector3.one * barrierBaseScale;
        if (circle != null) circle.localScale = Vector3.one * circleBaseScale;
        if (State == FrierenShieldState.HitReaction) State = FrierenShieldState.Active;
    }

    private IEnumerator ShakeRoutine(int gen)
    {
        float t = 0f;
        while (t < hitShakeDuration && gen == generation)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, hitShakeDuration));
            float amp = hitShakeAmplitude * (1f - k) * (1f - k);
            Vector2 offset = amp * new Vector2(Mathf.Sin(t * 95f), Mathf.Cos(t * 71f));
            if (barrier != null) barrier.localPosition = barrierLocalPos + (Vector3)offset;
            if (circle != null) circle.localPosition = circleLocalPos + (Vector3)offset * 0.6f;
            yield return null;
        }
        if (gen != generation) yield break;

        if (barrier != null) barrier.localPosition = barrierLocalPos;
        if (circle != null) circle.localPosition = circleLocalPos;
        if (State == FrierenShieldState.HitReaction) State = FrierenShieldState.Active;
    }

    private IEnumerator ShatterRoutine(int gen)
    {
        State = FrierenShieldState.Shattering;

        if (shatterSfx != SfxID.None) Tune.SFX(shatterSfx);
        if (shards != null) shards.Emit(shardCount);
        Shattered?.Invoke(this, barrier != null ? barrier.position : transform.position);
        if (shatterFlashRenderer != null && shatterFlash != null)
            StartCoroutine(ShatterFlashRoutine(gen));

        float t = 0f;
        while (t < shatterDuration && gen == generation)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, shatterDuration));
            PushPhase(1f, 0f, k * k);          // accelerating crystalline collapse
            yield return null;
        }
        if (gen != generation) yield break;

        PushPhase(1f, 0f, 1f);
        SetRenderersVisible(false);
        State = FrierenShieldState.Inactive;

        // Keep the object alive so world-space shard embers can finish before deactivation.
        t = 0f;
        while (t < shardLinger && gen == generation)
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (gen != generation) yield break;
        gameObject.SetActive(false);
    }

    private IEnumerator DissolveRoutine(int gen)
    {
        State = FrierenShieldState.Dissolving;

        float t = 0f;
        while (t < dissolveDuration && gen == generation)
        {
            t += Time.deltaTime;
            PushPhase(1f, Mathf.Clamp01(t / Mathf.Max(0.0001f, dissolveDuration)), 0f);
            yield return null;
        }
        if (gen != generation) yield break;

        PushPhase(1f, 1f, 0f);
        SetRenderersVisible(false);
        State = FrierenShieldState.Inactive;
        gameObject.SetActive(false);
    }

    private IEnumerator ShatterFlashRoutine(int gen)
    {
        Camera cam = Camera.main;
        shatterFlash.localScale = Vector3.one * (radius * 3.6f);
        shatterFlashRenderer.enabled = true;
        flashMpb ??= new MaterialPropertyBlock();

        float t = 0f;
        while (t < 1f && gen == generation)
        {
            t += Time.deltaTime / 0.18f;
            Billboard(cam);
            shatterFlashRenderer.GetPropertyBlock(flashMpb);
            flashMpb.SetFloat(FlashTId, Mathf.Clamp01(t));
            shatterFlashRenderer.SetPropertyBlock(flashMpb);
            yield return null;
        }
        t = 0f;
        while (t < 1f && gen == generation)
        {
            t += Time.deltaTime / 0.30f;
            Billboard(cam);
            shatterFlashRenderer.GetPropertyBlock(flashMpb);
            flashMpb.SetFloat(RingTId, EaseOutExpo(Mathf.Clamp01(t)));
            shatterFlashRenderer.SetPropertyBlock(flashMpb);
            yield return null;
        }
        shatterFlashRenderer.enabled = false;

        void Billboard(Camera camera)
        {
            if (camera != null)
                shatterFlash.rotation = Quaternion.LookRotation(
                    (camera.transform.position - shatterFlash.position).normalized, Vector3.up);
        }
    }

    // ------------------------------------------------------------------ uniform pushes (MPB, zero alloc)

    private void PushPhase(float deployT, float dissolveT, float shatterT)
    {
        if (barrierRenderer != null)
        {
            barrierMpb ??= new MaterialPropertyBlock();
            barrierRenderer.GetPropertyBlock(barrierMpb);
            barrierMpb.SetFloat(DeployTId, deployT);
            barrierMpb.SetFloat(DissolveTId, dissolveT);
            barrierMpb.SetFloat(ShatterTId, shatterT);
            barrierRenderer.SetPropertyBlock(barrierMpb);
        }
        if (circleRenderer != null)
        {
            circleMpb ??= new MaterialPropertyBlock();
            circleRenderer.GetPropertyBlock(circleMpb);
            circleMpb.SetFloat(DeployTId, deployT);
            circleMpb.SetFloat(DissolveTId, dissolveT);
            circleMpb.SetFloat(ShatterTId, shatterT);
            circleMpb.SetFloat(SeedId, seed);
            circleRenderer.SetPropertyBlock(circleMpb);
        }
    }

    private void PushHit()
    {
        if (barrierRenderer == null) return;
        barrierMpb ??= new MaterialPropertyBlock();
        barrierRenderer.GetPropertyBlock(barrierMpb);
        barrierMpb.SetVector(HitPosOSId, hitPosOS);
        barrierMpb.SetFloat(HitTimeId, hitTime);
        barrierMpb.SetFloat(HitIntensityId, hitIntensity);
        barrierRenderer.SetPropertyBlock(barrierMpb);
    }

    private void ResetUniforms()
    {
        seed = UnityEngine.Random.value * 100f;
        hitActive = false;
        hitTime = 999f;
        hitIntensity = 0f;
        PushPhase(0f, 0f, 0f);
        PushHit();
        if (shatterFlashRenderer != null) shatterFlashRenderer.enabled = false;
    }

    private void SetRenderersVisible(bool visible)
    {
        if (barrierRenderer != null) barrierRenderer.enabled = visible;
        if (circleRenderer != null) circleRenderer.enabled = visible;
    }

    // ------------------------------------------------------------------ helpers

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

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseOutExpo(float t) => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
    private static float EaseOutBack(float t)
    {
        const float c = 1.70158f;
        float u = t - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }
}
