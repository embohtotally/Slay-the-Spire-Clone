using System.Collections.Generic;

/// <summary>
/// Awaitable VFX action. Its performer (BeamVfxSystem) drives the beam coroutine and
/// gates the game state: each ImpactPayloads[i] is queued (AddReaction) on the exact
/// frame the beam head passes Targets[i]. Follows the DealDamageGA/IHaveCaster pattern.
/// </summary>
public class PlayBeamVfxGA : GameAction, IHaveCaster
{
    public CombatantView Caster { get; }
    public IReadOnlyList<CombatantView> Targets { get; }
    public IReadOnlyList<GameAction> ImpactPayloads { get; }
    public BeamVfxConfig Config { get; }

    public PlayBeamVfxGA(CombatantView caster,
                         List<CombatantView> targets,
                         BeamVfxConfig config,
                         List<GameAction> impactPayloads)
    {
        Caster = caster;
        Targets = targets ?? new List<CombatantView>();
        Config = config;
        ImpactPayloads = impactPayloads ?? new List<GameAction>();
    }
}
