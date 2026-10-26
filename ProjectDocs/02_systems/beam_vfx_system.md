# Zoltraak Beam VFX & Action Subsystem

## 1. Executive Summary & Purpose

The **Zoltraak Beam VFX Subsystem** is a high-performance, frame-accurate visual effects and combat resolution pipeline designed for beam attacks (such as the Frieren-inspired *Zoltraak* killing magic) within the Unity 6 URP 17 architecture.

It bridges three decoupled layers:
1. **Combat Gameplay Layer (MVS + Action System):** [CardData.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Data/CardData.cs), [BeamAttackEffect.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Effects/BeamAttackEffect.cs), and [PlayBeamVfxGA.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Game%20Actions/PlayBeamVfxGA.cs).
2. **Visual Engine Layer:** [ZoltraakBeamController.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/VFX/ZoltraakBeamController.cs), [BeamMeshFactory.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/VFX/BeamMeshFactory.cs), [BeamCameraFx.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/VFX/BeamCameraFx.cs), and four hand-written HLSL URP shaders.
3. **Subsystem Lifecycle & Pooling:** [BeamVfxSystem.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/VFX/BeamVfxSystem.cs) acting as a self-healing singleton that integrates directly with [ActionSystem.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs).

---

## 2. Root-Cause Analysis & Fix: The Stranded Card Bug

### Problem
Cards dragged into the combat play area occasionally remained floating in the center of the screen, unplayable and unreleasable until a subsequent card was played.

### Root-Cause Diagnostics
Three independent failure modes compounded into the freeze:
1. **Inverted Entry Guard in [CardView.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Views/CardView.cs):**
   `OnPointerUp` opened with `if (!CanStartCombatInteraction()) return;`. When a card was dragged while an action finished or during state jitter, releasing the mouse dropped execution immediately before resetting position or clearing drag state.
2. **Silent Command Rejection in [ActionSystem.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs):**
   `ActionSystem.Perform()` was `void` and silently dropped actions if `IsPerforming == true`. The calling `CardView` had no indication that the command was ignored, leaving its state permanently stranded in limbo.
3. **Missing Hand Layout Re-Anchor:**
   `HandView` lacked a dedicated mechanism to re-anchor and lerp cards back to valid arch positions after rejected or canceled drags.

### Resolution & Zero-Regression Architecture
- **Guard Removal:** Removed the inverted blocking guard from `CardView.OnPointerUp` so pointer release *always* resolves card state.
- **Contract Hardening:** Changed `ActionSystem.Perform(...)` to return `bool` indicating whether the action was scheduled or rejected.
- **Fail-Safe Fallback:** Updated `CardView.PlayCardOrResetPosition()` to test the return value; if rejected, the card immediately resets to its hand slot.
- **Hand Re-anchoring:** Added `HandView.RefreshCardPositions()` and exposed `CardSystem.HandView` so external resets cleanly reposition all cards.

---

## 3. Beam Architecture & Visual Pipeline

The beam visual lifecycle operates across five deterministic phases:

```
[Phase 1: Charge] ---> [Phase 2: Ignition & Travel] ---> [Phase 3: Impact Frame(s)] ---> [Phase 4: Hold] ---> [Phase 5: Dissipate]
  Dual Magic Circles        Mesh Extrusion / Head T        Per-target Damage Sync       Peak Beam Radius       Anisotropic Tail T
  Condensation Particles    Muzzle Shockwave + Burst       Trauma Shake & Screen Flash   Linger Duration       Zero-alloc Pool Return
```

### 1. Phase 1: Charge (Dual Magic Circles)
- Two concentric magic circles project at the caster's muzzle facing the primary target with configurable camera-facing tilt (`CircleCameraTilt`).
- Driven by `SpellbeamMagicCircle.shader` with analytic polar runes, counter-rotating rings, and smooth radial ramp.
- Inward particle condensation gathers at the focal point.

### 2. Phase 2: Ignition & Travel
- Head coordinate `_HeadT` advances along the 3D beam axis at `BeamTravelSpeed` (world-units per second).
- Procedural unit cylinder dynamically scales in real time from muzzle anchor to target endpoint.
- Immediate muzzle shockwave expansion and spark burst.

### 3. Phase 3: Per-Target Impact Gating
- As `_HeadT` intersects each target's projected axial position along the beam, the controller triggers that target's impact slot:
  - Planar impact shockwave ring expansion (`SpellbeamShockwave.shader`).
  - Directional spark spray, ion lingering embers, and smoke puff.
  - Broadcasts `BeamVfxSystem.ImpactOccurred` to trigger `BeamCameraFx` trauma shake and screen flash.
  - Delivers target damage payload on the exact visual contact frame.

### 4. Phase 4: Hold
- Sustains peak beam intensity and width (`BeamRadius`, `SheathRadiusMultiplier`).
- Continuous scrolling noise (`_FlowSpeed`) and high-frequency violet sheath ribbing (`_RibFrequency`, `_RibSpeed`).

### 5. Phase 5: Dissipate
- `_TailT` accelerates forward, pulling the tail of the beam off the caster towards the impact point.
- Beam core and sheath collapse anisotropically into high-energy energy wisps.
- The controller resets its property blocks and triggers `OnSequenceCompleted`, returning the instance to `BeamVfxSystem`'s object pool.

---

## 4. HLSL Shader Suite (Universal Render Pipeline 17)

All shaders are written in pure HLSL for maximum performance on mobile and PC, with zero Shader Graph overhead.

| Shader | Path | Visual Purpose |
|---|---|---|
| `Gameseed/Spellbeam/BeamCore` | [SpellbeamBeamCore.shader](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Shaders/SpellbeamBeamCore.shader) | Additive blazing core cylinder with Fresnel facing power, traveling head/tail cutoffs, and multi-octave axial flow. |
| `Gameseed/Spellbeam/BeamSheath` | [SpellbeamBeamSheath.shader](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Shaders/SpellbeamBeamSheath.shader) | Outer cylindrical sheath with procedural counter-rotating helical ribs, rim falloff, and dissipation jitter. |
| `Gameseed/Spellbeam/MagicCircle` | [SpellbeamMagicCircle.shader](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Shaders/SpellbeamMagicCircle.shader) | Procedural dual-ring magic circle with polar runes, counter-rotation, inner burst ring, and smooth reveal ramp. |
| `Gameseed/Spellbeam/Shockwave` | [SpellbeamShockwave.shader](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Shaders/SpellbeamShockwave.shader) | Expanding impact ring and central flash with exponential falloff and smooth alpha erosion. |

---

## 5. Complete Automation: The Zero-Manual Setup System

To eliminate manual Inspector wiring, drag-and-drop mistakes, and missing GUID references, the system includes [ZoltraakSetupAutomation.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Editor/ZoltraakSetupAutomation.cs).

### Capabilities
- **Menu Item:** `Tools -> VFX -> Setup Zoltraak Attack Completely`
- **Auto-Bootstrapping:** Uses `[InitializeOnLoad]` to automatically detect missing assets on project compilation and build them with zero user intervention.
- **Idempotent Execution:** Can be executed repeatedly without duplicating assets or corrupting scenes.

### Generated Assets Map
1. **Materials (`Assets/_Project/Materials/Spellbeam/`):**
   - `M_BeamCore.mat` (Zoltraak magenta `#FF59BF`, intensity 6.0)
   - `M_BeamSheath.mat` (Violet sheath `#B84DFF`, intensity 2.5)
   - `M_MagicCircle.mat` (Rune circle `#FF80D9`, intensity 4.0)
   - `M_Shockwave.mat` (Impact shockwave `#FF99E6`, intensity 7.0)
2. **Prefab (`Assets/_Project/Prefabs/VFX/ZoltraakBeam.prefab`):**
   - Root GameObject with [ZoltraakBeamController.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/VFX/ZoltraakBeamController.cs).
   - `BeamGroup` child holding procedural Core and Sheath MeshRenderers.
   - Dual Magic Circles (`CircleA`, `CircleB`) with quad meshes.
   - `MuzzleFlash` and particle accents (`Condensation`, `MuzzleBurst`).
   - `Impacts` root with 4 pre-allocated impact slots (`Impact0` to `Impact3`), each with rings and particle emitters (`Sparks`, `Ion`, `Smoke`).
3. **VFX Configuration Asset (`Assets/_Project/ScriptableObjects/VFX/Zoltraak_Config.asset`):**
   - Configured with beam geometry (radius 0.22, sheath multiplier 1.8), timing (0.55s charge, 65 unit/s travel, 0.32s hold, 0.45s dissipate), camera shake (amplitude 0.22, duration 0.32s), and line pierce expansion (22° max angle, up to 4 targets).
4. **Card Data Asset (`Assets/_Project/ScriptableObjects/Cards/Generated/Zoltraak.asset`):**
   - Attack card, Rare Tier, 2 Mana cost.
   - Linked to [BeamAttackEffect.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Effects/BeamAttackEffect.cs) with 16 base damage and `Zoltraak_Config`.
5. **Reward Pool & Testing Cheats (Clean Launch Separation):**
   - **Production Starting Deck Cleanliness:** `Default Hero.asset` contains only standard starting cards; Zoltraak is NOT permanently baked into the starting deck.
   - **Rare Reward Pool:** Registered in `Default Card Reward Pool.asset` as a Rare Tier card obtainable after winning combat.
   - **F1 Playtest Hotkey:** Press `F1` during combat (in Editor/Dev builds) to instantly spawn Zoltraak into your active hand.
   - **Editor Menu Shortcuts:**
     - `Tools -> VFX -> Add Zoltraak to Active Hand (Play Mode)`
     - `Tools -> VFX -> Toggle Zoltraak in Starting Deck (Asset Override)`
   - **Non-Destructive Inspector Overrides:** In [MatchSetupSystem.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Systems/MatchSetupSystem.cs), toggle `enableEditorTestCards` to inject any test cards into Turn 1 without modifying disk assets.

---

## 6. Self-Healing Runtime Hooks

Combat scenes require **zero manual prefab placement or scene modifications**. The runtime auto-wires itself:

1. **Subsystem Auto-Injection in [ActionSystem.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/Systems/Action%20System/ActionSystem.cs):**
   ```csharp
   if (BeamVfxSystem.Instance == null && GetComponent<BeamVfxSystem>() == null)
   {
       gameObject.AddComponent<BeamVfxSystem>();
   }
   ```
   Ensures `BeamVfxSystem` is active in any scene containing `ActionSystem` (`Game.unity`, `GameTutorial.unity`, etc.).
2. **Camera Fallback in [BeamCameraFx.cs](file:///d:/Unity%20Projects/Slay-the-Spire-Clone/Assets/_Project/Scripts/VFX/BeamCameraFx.cs):**
   ```csharp
   private void Awake()
   {
       if (shakeCamera == null)
           shakeCamera = GetComponent<Camera>() ?? Camera.main;
   }
   ```
   Gracefully targets the main camera without needing manual inspector assignments.

---

## 7. How to Create New Beam Attacks

To author a new beam card (e.g. *Holy Smite*, *Prismatic Ray*, or *Infernal Lance*):

1. **Create Config:** In Unity Project view, right-click -> `Create -> VFX -> Beam Vfx Config`.
2. **Assign Base Materials:** Reference `M_BeamCore`, `M_BeamSheath`, `M_MagicCircle`, and `M_Shockwave`.
3. **Customize Palette & Dynamics:**
   - Adjust `CoreColor`, `HotColor`, `SheathColor`, and `CircleColor`.
   - Toggle `ExpandSingleTargetToLine = true` for piercing laser mechanics, or leave `false` for focused single-target beams.
   - Choose `GateMode`: `FirstImpact` (fastest tempo), `LastImpact` (staggered pierce), or `FullSequence` (cinematic holds).
4. **Create Card:** In `CardData`, select `ManualTargetEffect` -> `BeamAttackEffect`, assign your new config, and set damage.
