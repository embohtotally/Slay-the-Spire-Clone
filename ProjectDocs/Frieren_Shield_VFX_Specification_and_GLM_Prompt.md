# Frieren Defense Magic (Hexagonal Shield) Subsystem: Architecture & GLM 5.3 Prompt

This document provides the complete architectural design, engineering trade-offs, and a turnkey, copy-paste prompt specifically tuned for **GLM 5.3** (or any frontier LLM) to generate the complete procedural VFX and C# pipeline for the Frieren Defensive Magic shield.

---

## 1. Architectural Strategy & Approach

### Comparison of Approaches

| Criteria | 2D Spritesheets (`Pipoya VFX HEXShield`) | Shuriken / VFX Graph Particles | Procedural HLSL + Procedural Mesh (Recommended) |
|---|---|---|---|
| **Visual Style Alignment** | Poor (pixel-art mismatch with 3D URP Zoltraak) | Moderate (soft billowy look, hard to get crisp geometric lines) | **Flawless (identical to Zoltraak beam pipeline, crisp anime geometry, HDR bloom)** |
| **Dynamic Hit Ripples** | Impossible (fixed pre-rendered frames) | Very difficult (requires GPU simulation buffers) | **Trivial & exact (local object-space coordinate `_HitPosOS` propagates wave across hex tiles)** |
| **External Dependencies** | Multiple high-res sprite textures | Texture atlases + particle prefabs | **Zero external assets (100% procedural C# mesh + analytic HLSL math)** |
| **Performance** | High fill-rate overhead | Overdraw spikes on mobile | **SRP Batcher compatible, low vert count (~128 verts), zero per-frame allocations** |
| **Persistence Across Turns** | Rigid animation loops | Continuous emitter load | **Clean FSM states (Deploy -> Active -> Hit -> Shatter / Dissolve)** |

### Recommendation
Adopt the **Procedural HLSL + Procedural Mesh Factory + FSM Controller** pattern established in the Zoltraak implementation:
1. **Procedural Geometry:** A curved faceted hex-disc generated at runtime via `FrierenShieldMeshFactory.cs`.
2. **Analytic Shaders:** `FrierenHexShield.shader` (procedural hexagonal distance function, glowing edges, localized impact ripple wave) and `FrierenShieldCircle.shader` (golden rotating runic magic circle backing).
3. **Decoupled Controller:** `FrierenShieldController.cs` attached to or spawned for `CombatantView`.
4. **Authoritative Integration:** Hooks cleanly into `CombatantView.AddShield()`, `CombatantView.Damage()`, and `CombatantView.ClearShield()` with zero band-aids.

---

## 2. Turnkey Prompt for GLM 5.3

Copy the entire block below and paste it into GLM 5.3 alongside the zipped context package `Frieren_Shield_VFX_Context.zip`:

````markdown
# Role: Elite Real-Time VFX Technical Artist & Unity Systems Architect
# Target Engine: Unity 6 (6000.0.73f1) | Universal Render Pipeline (URP 17.1.0) | C# 12
# Reference Project: Slay-the-Spire-Clone (Command-Performer ActionSystem Architecture)

## Mission
Design and implement the iconic **Ordinary Defense Magic (防御魔法 - Hexagonal Barrier)** from *Sousou no Frieren* (Frieren: Beyond Journey's End) as a complete, production-grade, zero-external-dependency procedural VFX and combat subsystem.

This shield directly opposes and counters our existing *Zoltraak* beam attack. When a combatant plays a shield card or receives shield, the golden hexagonal barrier deploys in front of them, actively protects them across turns, dynamically reacts with localized shockwave ripples when struck, and shatters into crystalline fragments when broken (or cleanly dissolves when expired).

You are provided with `Frieren_Shield_VFX_Context.zip` containing our core architecture (`ActionSystem.cs`, `CombatantView.cs`, `ShieldSystem.cs`, `DamageSystem.cs`) and our existing Zoltraak reference suite (`BeamVfxSystem.cs`, `ZoltraakBeamController.cs`, `BeamMeshFactory.cs`, `SpellbeamMagicCircle.shader`, `ZoltraakSetupAutomation.cs`). Follow our exact naming, architectural, and performance standards.

---

## 1. Visual & Mathematical Specifications (The Frieren Anime Aesthetic)

### A. Visual Composition (Three Composite Layers)
1. **Layer 1: The Hexagonal Honeycomb Barrier (Front):**
   - Translucent amber/gold faceted surface (`#FFE066` core, `#FFB300` rim, HDR bloom intensity 3.0 to 5.0).
   - Crisp procedural hexagonal tessellation with glowing energetic boundaries.
   - Subtle inner-facet gradient and Fresnel rim falloff.
2. **Layer 2: The Concentric Magic Circle (Backing Plane):**
   - Golden counter-rotating geometric rune rings floating slightly behind the barrier.
   - Driven analytically by procedural polar runes, ticking teeth, and concentric circles.
3. **Layer 3: Impact Shimmer & Shatter Embers:**
   - Localized bright flare at the exact impact point when hit.
   - Dynamic crystalline fragmentation on shield destruction.

### B. Mathematical Formulas in HLSL
1. **Hexagonal Distance Function (2D Tiling):**
   To compute seamless regular hexagonal grid coordinates and distance to hex edges without textures:
   ```hlsl
   float2 HexCoords(float2 uv, float scale, out float2 hexIndex)
   {
       float2 r = float2(1.0, 1.7320508);
       float2 h = r * 0.5;
       float2 a = fmod(uv * scale, r) - h;
       float2 b = fmod(uv * scale - h, r) - h;
       float2 gv = dot(a, a) < dot(b, b) ? a : b;
       hexIndex = floor(uv * scale - gv);
       return gv; // Centered local coordinates [-0.5, 0.5]
   }
   
   float HexDist(float2 p)
   {
       p = abs(p);
       return max(dot(p, normalize(float2(1.0, 1.7320508))), p.x);
   }
   ```
2. **Dynamic Localized Hit Ripple Wave:**
   When struck at object-space coordinate `_HitPosOS`, a wave propagates outwards across the hex grid:
   ```hlsl
   float distToHit = length(IN.positionOS.xy - _HitPosOS.xy);
   float waveFront = _HitTime * _RippleSpeed;
   float waveDelta = abs(distToHit - waveFront);
   float ripple = exp(-waveDelta * _RippleSharpness) * exp(-_HitTime * _RippleDamping) * _HitIntensity;
   ```

---

## 2. Required File Deliverables & Architecture

Implement the following complete, production-ready files with zero placeholders or omissions:

### Deliverable 1: `Assets/_Project/Shaders/FrierenHexShield.shader`
- **Pipeline:** Universal Render Pipeline (URP 17), Unlit, Transparent queue (`Queue=Transparent+120`).
- **SRP Batcher Compatibility:** All per-material properties enclosed in `CBUFFER_START(UnityPerMaterial) ... CBUFFER_END`.
- **Properties:**
  - `_BaseColor` (HDR Color, amber/gold), `_EdgeColor` (HDR Color, intense highlight), `_Intensity` (Float).
  - `_HexScale` (Float, grid density), `_EdgeThickness` (Float, boundary width).
  - `_FresnelPower` (Float), `_FresnelBias` (Float).
  - `_HitPosOS` (Vector3, object-space hit origin), `_HitTime` (Float, seconds since hit), `_HitIntensity` (Float).
  - `_RippleSpeed` (Float), `_RippleSharpness` (Float), `_RippleDamping` (Float).
  - `_DeployT` (Range 0..1, geometric reveal), `_DissolveT` (Range 0..1, hex-tile fade-out).
- **Technique:** Additive or Alpha-Blend (`Blend One One` or `Blend SrcAlpha OneMinusSrcAlpha`), `ZWrite Off`, `Cull Off`.

### Deliverable 2: `Assets/_Project/Shaders/FrierenShieldCircle.shader`
- Complementary golden magic circle backing plane.
- Counter-rotating inner/outer rune rings, ticking planetary teeth, and smooth circular alpha mask.

### Deliverable 3: `Assets/_Project/Scripts/VFX/FrierenShieldMeshFactory.cs`
- Static procedural mesh generator generating in-memory shared meshes (zero external `.obj`/`.fbx` files):
  1. `GetCurvedHexDisc(int segments, float curvature)`: A slightly convex, shallow-dished circular shield surface facing +Z with normalized UVs and outward normals.
  2. `GetUnitQuad()`: 1x1 centered quad for the magic circle plane.

### Deliverable 4: `Assets/_Project/Scripts/VFX/FrierenShieldController.cs`
- MonoBehaviour managing the shield visual lifecycle:
  - **States:** `Inactive`, `Deploying`, `Active`, `HitReaction`, `Shattering`, `Dissolving`.
  - **Methods:**
    - `public void Deploy(int shieldAmount)`: Triggers deployment tween (scale punch, `_DeployT` from 0 to 1 over 0.25s).
    - `public void OnHit(int damageAbsorbed, Vector3 worldHitPosition)`: Converts hit position to local object-space, updates `_HitPosOS`, triggers ripple wave, applies small positional shake.
    - `public void Shatter()`: Triggers crystalline fracture burst (spawns shard particles or fast shatter collapse), plays sound, deactivates.
    - `public void Dissolve()`: Smooth tile-by-tile dissolve (`_DissolveT` from 0 to 1 over 0.35s) when shield naturally expires on turn start.
  - **Performance Requirement:** Use `MaterialPropertyBlock` for all per-instance uniform updates to guarantee **zero allocations on hot paths**.

### Deliverable 5: Integration Hooks (`CombatantView.cs` & `ShieldSystem.cs`)
- Provide clean, surgical integration diffs:
  - Add shield lifecycle events or a direct `FrierenShieldController` reference to `CombatantView.cs`.
  - Ensure `CombatantView.AddShield()` calls `Deploy()`.
  - Ensure `CombatantView.Damage()` calls `OnHit()` when damage is absorbed, and `Shatter()` when `CurrentShield` hits 0 from damage.
  - Ensure `CombatantView.ClearShield()` calls `Dissolve()`.

### Deliverable 6: `Assets/_Project/Scripts/Editor/FrierenShieldSetupAutomation.cs`
- Fully automated, idempotent Editor script (`[MenuItem("Tools/VFX/Setup Frieren Shield Completely")]`):
  - Automatically creates Materials with correct HDR gold/amber colors and shader assignments.
  - Builds the prefab `Assets/_Project/Prefabs/VFX/FrierenShield.prefab` with `MeshFilter`, `MeshRenderer`, backing magic circle, and `FrierenShieldController`.
  - Connects default references cleanly.

---

## 3. Strict Engineering Constraints
1. **Compilation Guarantee:** Code must compile with 0 errors and 0 warnings under Unity 6 C# 12.
2. **Zero External Assets:** All meshes and textures must be procedurally generated via code/shaders.
3. **Zero Allocations on Hot Paths:** No `new` allocations, string concatenations, or boxing in `Update()`.
4. **URP 17 Compliance:** Strict adherence to Universal Render Pipeline HLSL macros (`TransformObjectToHClip`, `CBUFFER_START(UnityPerMaterial)`, etc.).
````

---
