using Gameseed26;
using UnityEngine;

public enum BeamGateMode
{
    Auto,           // 1 target -> FirstImpact, N targets -> LastImpact
    FirstImpact,    // performer releases at first impact (fastest hand-back)
    LastImpact,     // performer holds until the beam head passed every target
    FullSequence    // performer holds until fade-out completes
}

/// <summary>
/// Optional per-cast duration override passed to PlayBeamRoutine.
/// </summary>
[System.Serializable]
public struct BeamTiming
{
    public float ChargeDuration;
    public float BeamTravelSpeed;
    public float HoldDuration;
    public float DissipateDuration;

    public BeamTiming(BeamVfxConfig c)
    {
        ChargeDuration = c.ChargeDuration;
        BeamTravelSpeed = c.BeamTravelSpeed;
        HoldDuration = c.HoldDuration;
        DissipateDuration = c.DissipateDuration;
    }
}

[CreateAssetMenu(menuName = "VFX/Beam Vfx Config")]
public class BeamVfxConfig : ScriptableObject
{
    [Header("Prefab")]
    [Tooltip("Pooled prefab root carrying ZoltraakBeamController. Meshes/materials are wired at runtime.")]
    public GameObject BeamPrefab;

    [Header("Timing (seconds / world-units-per-second)")]
    public float ChargeDuration = 0.55f;
    public float BeamTravelSpeed = 60f;
    public float HoldDuration = 0.28f;
    public float DissipateDuration = 0.45f;

    [Header("Geometry")]
    [Range(6, 24)] public int CylinderSides = 12;
    public float BeamRadius = 0.22f;
    public float SheathRadiusMultiplier = 1.8f;
    public float CircleRadius = 1.15f;
    [Tooltip("0 = circle plane perpendicular to the beam; 1 = full camera billboard.")]
    [Range(0f, 1f)] public float CircleCameraTilt = 0.55f;
    public float PierceOvershoot = 1.2f;
    [Tooltip("Z offset toward the camera so additive FX layer cleanly over sprites.")]
    public float DepthOffset = -0.35f;

    [Header("Anchors (child-name convention, then local-space fallback offset)")]
    public string[] CasterAnchorNames = { "CastAnchor", "Muzzle", "Staff" };
    public Vector3 CasterAnchorOffset = new(0.55f, 0.35f, 0f);
    public string[] TargetAnchorNames = { "ImpactAnchor", "HitPoint" };
    public Vector3 TargetAnchorOffset = new(0f, 0.15f, 0f);

    [Header("Base Materials (palette below is baked into per-config instances)")]
    public Material BaseCoreMaterial;
    public Material BaseSheathMaterial;
    public Material BaseCircleMaterial;
    public Material BaseShockMaterial;

    [Header("Palette (HDR — tuned for URP Bloom, threshold ~1)")]
    [ColorUsage(false, true)] public Color CoreColor = new(1f, 0.35f, 0.75f, 1f);   // Zoltraak magenta
    [ColorUsage(false, true)] public Color HotColor = Color.white;                   // blinding center
    [ColorUsage(false, true)] public Color SheathColor = new(0.72f, 0.30f, 1f, 1f); // violet sheath
    [ColorUsage(false, true)] public Color CircleColor = new(1f, 0.50f, 0.85f, 1f);
    [ColorUsage(false, true)] public Color ImpactRingColor = new(1f, 0.6f, 0.9f, 1f);
    [ColorUsage(false, true)] public Color ImpactFlashColor = Color.white;
    [Range(0.5f, 16f)] public float CoreIntensity = 6f;
    [Range(0.5f, 8f)]  public float SheathIntensity = 2.5f;
    [Range(0.5f, 8f)]  public float CircleIntensity = 4f;
    [Range(0.5f, 16f)] public float ImpactRingIntensity = 7f;
    [Range(0.5f, 16f)] public float ImpactFlashIntensity = 10f;

    [Header("Damage Gating")]
    public BeamGateMode GateMode = BeamGateMode.Auto;
    [Tooltip("TRUE: each target's payload runs as a nested sub-flow exactly on ITS impact frame. " +
             "Requires ActionSystem.PerformSubflow. Default FALSE keeps " +
             "the engine untouched (multi-target damage staggers via DamageSystem.damageDuration).")]
    public bool UsePerTargetSubflows = false;
    [Tooltip("Skip queueing a payload if its target is already dead (pierce overkill protection).")]
    public bool SkipDeadTargets = true;

    [Header("Pierce (manual-target cards that expand to a line)")]
    public bool ExpandSingleTargetToLine = false;
    public float LineMaxAngle = 18f;
    public int LineMaxTargets = 4;

    [Header("Particle Burst Counts (Shuriken accents)")]
    public int MuzzleBurstCount = 16;
    public int SparkBurstCount = 28;
    public int IonLingerCount = 12;
    public int SmokePuffCount = 4;

    [Header("Camera FX (consumed by BeamCameraFx via BeamVfxSystem.ImpactOccurred)")]
    public float ShakeAmplitude = 0.22f;
    public float ShakeDuration = 0.32f;
    public float FlashStrength = 0.55f;

    [Header("Audio")]
    public SfxID ChargeSfx = SfxID.None;
    public SfxID FireSfx = SfxID.None;
    public SfxID ImpactSfx = SfxID.None;

    [Header("Sorting")]
    public string SortingLayerName = "VFX";
    public int SortingOrder = 50;
}
