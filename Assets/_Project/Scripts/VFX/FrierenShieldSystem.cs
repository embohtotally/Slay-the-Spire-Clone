using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns FrierenShieldController attachment per CombatantView, feeds exact Zoltraak beam
/// impact positions into shield hit ripples (same-frame delivery via the ActionSystem
/// reaction pipeline), and broadcasts shield events for decoupled camera FX subscribers.
/// Auto-injected by ActionSystem.Awake (see integration diff) - mirrors BeamVfxSystem -
/// or can simply be placed on any scene GameObject; both paths work unchanged.
/// Falls back to a fully procedural shield (factory meshes + Shader.Find materials) when
/// no prefab is assigned, so any combat scene works with zero manual wiring.
/// </summary>
[System.Serializable]
public class FrierenShieldSystem : Singleton<FrierenShieldSystem>
{
    private const string BarrierShaderName = "Gameseed/FrierenShield/HexBarrier";
    private const string CircleShaderName = "Gameseed/FrierenShield/Circle";
    private const string FlashShaderName = "Gameseed/Spellbeam/Shockwave";

    [Tooltip("Optional prefab override (Assets/_Project/Prefabs/VFX/FrierenShield.prefab). " +
             "When null, shields are built procedurally at runtime - zero manual wiring.")]
    [SerializeField] private GameObject shieldPrefab;

    /// <summary>Decoupled camera FX hook: raised when a barrier shatters (owner + world position).</summary>
    public static event Action<CombatantView, Vector3> ShieldShattered;

    private readonly Dictionary<CombatantView, FrierenShieldController> shields = new();
    private readonly Dictionary<CombatantView, Vector3> beamImpacts = new();

    private static Material sharedBarrierMaterial, sharedCircleMaterial, sharedFlashMaterial;

    private void OnEnable()
    {
        BeamVfxSystem.ImpactOccurred += OnBeamImpact;
    }

    private void OnDisable()
    {
        BeamVfxSystem.ImpactOccurred -= OnBeamImpact;
        shields.Clear();
        beamImpacts.Clear();
    }

    // ------------------------------------------------------------------ attachment

    /// <summary> lazily attaches (or returns the existing) shield controller on the combatant.</summary>
    public FrierenShieldController Attach(CombatantView view)
    {
        if (view == null) return null;
        if (shields.TryGetValue(view, out FrierenShieldController existing) && existing != null) return existing;

        FrierenShieldController controller = CreateController(view);
        shields[view] = controller;
        return controller;
    }

    private FrierenShieldController CreateController(CombatantView view)
    {
        GameObject go = shieldPrefab != null
            ? Instantiate(shieldPrefab, view.transform, false)
            : BuildSkeleton(view.transform, true);
        go.name = "FrierenShield";

        FrierenShieldController controller = go.GetComponent<FrierenShieldController>();
        if (controller == null) controller = go.AddComponent<FrierenShieldController>();
        controller.Shattered += OnControllerShattered;
        controller.Initialize(view);
        return controller;
    }

    private void OnControllerShattered(FrierenShieldController controller, Vector3 position)
    {
        ShieldShattered?.Invoke(controller.Owner, position);
    }

    // ------------------------------------------------------------------ Zoltraak counter-play (frame sync)

    private void OnBeamImpact(BeamImpact impact)
    {
        // Registered on the beam's exact impact frame; consumed by the DealDamageGA that
        // the beam queues via AddReaction - which the ActionSystem executes the SAME frame.
        if (impact.Target != null) beamImpacts[impact.Target] = impact.Position;
    }

    /// <summary>Hit position for a shield impact: the beam's exact contact point when the
    /// damage came from a Zoltraak impact this frame, otherwise a sensible default point
    /// in front of the combatant toward the nearest threat.</summary>
    public static Vector3 ResolveHitPosition(CombatantView view)
    {
        if (view == null) return Vector3.zero;

        if (Instance != null && Instance.beamImpacts.TryGetValue(view, out Vector3 beamPos))
        {
            Instance.beamImpacts.Remove(view);     // consume-on-use: one exact hit per impact
            return beamPos;
        }

        Vector3 threat = GetThreatPosition(view);
        Vector3 dir = threat - view.transform.position;
        dir.z = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;
        return view.transform.position + dir * 0.72f + (Vector3)UnityEngine.Random.insideUnitCircle * 0.18f;
    }

    /// <summary>Nearest opposing combatant position: enemies aim at the hero, the hero aims
    /// at the nearest living enemy. Static so controllers can resolve placement without an
    /// active system instance.</summary>
    public static Vector3 GetThreatPosition(CombatantView view)
    {
        if (view == null) return Vector3.zero;

        if (view is EnemyView && HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null)
        {
            return HeroSystem.Instance.HeroView.transform.position;
        }

        if (view is HeroView && EnemySystem.Instance != null && EnemySystem.Instance.Enemies != null)
        {
            Vector3 origin = view.transform.position;
            Vector3 nearest = origin + Vector3.right * 3f;
            float bestSqr = float.MaxValue;
            foreach (EnemyView enemy in EnemySystem.Instance.Enemies)
            {
                if (enemy == null) continue;
                float sqr = (enemy.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = enemy.transform.position;
                }
            }
            return nearest;
        }

        return view.transform.position + (view is HeroView ? Vector3.right : Vector3.left) * 3f;
    }

    // ------------------------------------------------------------------ procedural shared assets

    /// <summary>Builds the shield GameObject skeleton (Barrier / Circle / ShatterFlash / Shards).
    /// Used by the runtime procedural path and by FrierenShieldSetupAutomation for the prefab.</summary>
    public static GameObject BuildSkeleton(Transform parent, bool assignRuntimeAssets)
    {
        GameObject root = new GameObject("FrierenShield");
        if (parent != null) root.transform.SetParent(parent, false);

        GameObject barrierGo = new GameObject("Barrier");
        barrierGo.transform.SetParent(root.transform, false);
        MeshFilter barrierFilter = barrierGo.AddComponent<MeshFilter>();
        MeshRenderer barrierRenderer = barrierGo.AddComponent<MeshRenderer>();

        GameObject circleGo = new GameObject("Circle");
        circleGo.transform.SetParent(root.transform, false);
        MeshFilter circleFilter = circleGo.AddComponent<MeshFilter>();
        MeshRenderer circleRenderer = circleGo.AddComponent<MeshRenderer>();

        GameObject flashGo = new GameObject("ShatterFlash");
        flashGo.transform.SetParent(root.transform, false);
        MeshFilter flashFilter = flashGo.AddComponent<MeshFilter>();
        MeshRenderer flashRenderer = flashGo.AddComponent<MeshRenderer>();

        GameObject shardsGo = new GameObject("Shards");
        shardsGo.transform.SetParent(root.transform, false);
        ParticleSystem shardSystem = shardsGo.AddComponent<ParticleSystem>();
        ConfigureShardSystem(shardSystem);

        if (assignRuntimeAssets)
        {
            barrierFilter.sharedMesh = FrierenShieldMeshFactory.GetCurvedHexDisc(6, 0.10f);
            circleFilter.sharedMesh = FrierenShieldMeshFactory.GetUnitQuad();
            barrierRenderer.sharedMaterial = GetSharedBarrierMaterial();
            circleRenderer.sharedMaterial = GetSharedCircleMaterial();

            Material flash = GetSharedFlashMaterial();
            if (flash != null)
            {
                flashFilter.sharedMesh = FrierenShieldMeshFactory.GetUnitQuad();
                flashRenderer.sharedMaterial = flash;
            }
            else
            {
                flashGo.SetActive(false);
            }
            root.AddComponent<FrierenShieldController>();
        }
        return root;
    }

    private static void ConfigureShardSystem(ParticleSystem ps)
    {
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.11f);
        main.startColor = new Color(1f, 0.82f, 0.35f, 1f);
        main.gravityModifier = 1.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 96;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-5.5f, 5.5f);
    }

    public static Material GetSharedBarrierMaterial() => sharedBarrierMaterial ??= CreateMaterial(BarrierShaderName);
    public static Material GetSharedCircleMaterial() => sharedCircleMaterial ??= CreateMaterial(CircleShaderName);

    public static Material GetSharedFlashMaterial()
    {
        if (sharedFlashMaterial != null) return sharedFlashMaterial;
        sharedFlashMaterial = CreateMaterial(FlashShaderName);   // may be null: Zoltraak suite absent -> flash gracefully skipped
        return sharedFlashMaterial;
    }

    private static Material CreateMaterial(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[FrierenShieldSystem] Shader not found: {shaderName}. " +
                             "Run 'Tools/VFX/Setup Frieren Shield Completely' once in the Editor.");
            return null;
        }
        Material material = new Material(shader) { hideFlags = HideFlags.DontSave };
        return material;
    }
}
