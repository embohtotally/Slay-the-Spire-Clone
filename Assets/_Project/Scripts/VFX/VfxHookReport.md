# VFX Hook Report

## Hook system added

- `VfxCue`: flexible serializable VFX slot for a prefab, offset, rotation, scale multiplier, parent-to-target, and auto-destroy duration.
- `GameActionVfxDirector`: global/persistent action VFX director attached to `Assets/Resources/GameManager.prefab`.
- `CardData.PlayVfx`: per-card flexible VFX cue. Existing `PlayParticle` remains as legacy fallback.

All VFX fields are intentionally empty now because VFX assets are not selected yet.

## Existing VFX found before this pass

- `Assets/_Project/Prefabs/Particles/RingEffect.prefab`
- `DamageSystem.damageVFX` in `Game.unity` and `GameTutorial.unity` already pointed to RingEffect.
- `CardData.PlayParticle` existed but card assets were not using it.

## VFX to find/create, highest priority

### Combat / cards
- Card play generic burst
- Card targeted projectile / impact
- Card hover glow / selectable pulse
- Card pickup / drag trail
- Card invalid release puff or red flash
- Card draw streak / spawn shimmer
- Card discard vanish / dissolve
- Shuffle / reshuffle swirl
- Mana spend small spark
- Mana refill/gain pulse

### Combatant feedback
- Damage impact / slash / hit burst
- Critical/heavy hit impact, optional later
- Heal glow
- Shield/barrier appear
- Shield hit/absorb, optional later
- Buff apply upward aura
- Debuff/status apply dark/purple aura
- Poison/DoT apply cloud
- DoT tick, optional later
- Remove debuff cleanse sparkle
- Stun stars/electric ring
- Taunt/aggro marker
- Stress gain/fear/shadow pulse
- Stress reduce calm/soft glow
- Enemy death dissolve/burst
- Enemy summon portal/smoke
- Enemy intent/wind-up indicator

### Scene / flow
- Combat start screen/world pulse
- Enemy turn warning
- Player turn/your turn glow
- Combat won celebration
- Game over/defeat effect, optional

### Map / nodes / rewards
- Map node select pulse
- Combat node / elite / boss node activation
- Shop/rest/event/treasure node activation
- Map completed path glow
- Treasure open sparkle
- Gold claim sparkle/coins
- Relic claim glow
- Potion claim sparkle
- Reward open/claim/skip
- Card reward open/chosen

### UI / non-gameplay optional
- Button hover/click spark, optional because DOTween/audio already cover it
- Panel open/close puff, optional
- Settings slider sparkle, optional
- Scene transition/fade particle, optional

## Where to assign later

- Global gameplay VFX: `Assets/Resources/GameManager.prefab` -> `GameActionVfxDirector`.
- Per-card VFX: each `CardData` asset -> `Play Vfx`.
- Existing damage fallback: `Game`/`GameTutorial` scene -> `DamageSystem.damageVFX`.

## Notes

- Prefer a compact reusable set first instead of one VFX per action.
- Suggested first reusable set: hit, heal, shield, buff, debuff, poison, cleanse, stress, card-play, card-draw, discard, mana, enemy-death, summon, reward, treasure, node-select.
- Keep UI VFX optional unless playtest feels too static.
