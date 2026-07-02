# VFX Assignment Report

## Assigned global GameActionVfxDirector fields

- combatStartedVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab
- enemyTurnVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Eerie/CFXR2 Skull Head Alt.prefab
- playerTurnVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR4 Falling Stars.prefab
- combatWonVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR4 Firework 1 Cyan-Purple (HDR).prefab
- playCardVfx: None (covered by per-card PlayVfx or intentionally empty)
- drawCardsVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR Magic Poof.prefab
- discardHandVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR Smoke Source 3D.prefab
- spendManaVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR Flash.prefab
- refillManaVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Impact Glowing HDR (Blue).prefab
- gainManaVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Impact Glowing HDR (Blue).prefab
- loseManaVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR3 Hit Electric C (Air).prefab
- dealDamageVfx: Assets/Travis Game Assets/Hit Impact Effects/Prefabs/Hits/Hit_01.prefab
- healVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Light/CFXR3 Hit Light B (Air).prefab
- gainShieldVfx: Assets/Travis Game Assets/Hit Impact Effects/Prefabs/Guards/Guard_01.prefab
- applyBuffVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab
- applyStatusVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR2 _CURSED_.prefab
- applyDotVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR2 Poison Cloud.prefab
- removeDebuffVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR4 Falling Stars.prefab
- stunVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR3 Hit Electric C (Air).prefab
- tauntVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Eerie/CFXR2 Skull Head Alt.prefab
- stressGainVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR2 Broken Heart.prefab
- stressReduceVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR4 Falling Stars.prefab
- stressSetVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR2 Broken Heart.prefab
- enemyKilledVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Eerie/CFXR2 Souls Escape.prefab
- summonEnemyVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR Smoke Source 3D.prefab
- enemyIntentVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Eerie/CFXR2 Skull Head Alt.prefab
- usePotionVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Liquids/CFXR Water Splash (Smaller).prefab
- openCardRewardVfx: Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR2 Shiny Item (Loop).prefab

## Card PlayVfx assignment summary
- Card assets assigned: 41
- light_hit: 7 cards -> Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Light/CFXR3 Hit Light B (Air).prefab
- mana_blue: 2 cards -> Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Impact Glowing HDR (Blue).prefab
- poof: 3 cards -> Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR Magic Poof.prefab
- shield: 18 cards -> Assets/Travis Game Assets/Hit Impact Effects/Prefabs/Guards/Guard_01.prefab
- sword_fire: 2 cards -> Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Fire/CFXR4 Sword Hit FIRE (Cross).prefab
- sword_plain: 6 cards -> Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Plain/CFXR4 Sword Hit PLAIN (Cross).prefab
- wind: 3 cards -> Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Nature/CFXR4 Wind Trails.prefab

## Existing scene damageVFX
- Game.unity and GameTutorial.unity DamageSystem.damageVFX set to None to avoid duplicate VFX; damage VFX now comes from GameActionVfxDirector.dealDamageVfx.

## Imported VFX prefabs not used in this first mapping
- Count: 50
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Eerie/CFXR2 WW Enemy Explosion.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR Electrified 3.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR2 Sparks Rain.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion Smoke 2 Solo (HDR).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR2 WW Explosion.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR3 Fire Explosion B.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR4 Firework HDR Shoot Single (Random Color).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/CFXR Fire Breath.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/CFXR Fire.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/CFXR2 Firewall A.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/CFXR3 Hit Fire B (Air).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/CFXR4 Sun.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Ice/CFXR3 Hit Ice B (Air).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit A (Red).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit D 3D (Yellow).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR2 Ground Hit.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Light/CFXR3 LightGlow A (Loop).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Liquids/CFXR Water Ripples.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Liquids/CFXR2 Blood (Directional).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Liquids/CFXR2 Blood Shape Splash.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Liquids/CFXR4 Bubbles Breath Underwater Loop.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR4 Bouncing Glows Bubble (Blue Purple).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR2 Cartoon Fight (Loop).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR3 Ambient Glows.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR3 Hit Misc A.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR3 Hit Misc F Smoke.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Nature/CFXR3 Hit Leaves A (Lit).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Nature/CFXR3 Shield Leaves A (Lit).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Nature/CFXR4 Rain Falling.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Nature/CFXR4 Rain Splashes.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Fire/CFXR4 Sword Trail FIRE (360 Spiral).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Fire/CFXR4 Sword Trail FIRE (360 Thin Spiral).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Ice/CFXR4 Sword Hit ICE (Cross).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Ice/CFXR4 Sword Trail ICE (360 Spiral).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Ice/CFXR4 Sword Trail ICE (360 Thin Spiral).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Plain/CFXR4 Sword Trail PLAIN (360 Spiral).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Plain/CFXR4 Sword Trail PLAIN (360 Thin Spiral).prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR _BOING_.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR _BOOM_.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR _POW_.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR _SLASH_.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR2 _WHAM_ 3.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR3 _WOW_.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR4 _FROZEN_.prefab
- Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Texts/CFXR4 _POISONED_.prefab
- Assets/Travis Game Assets/Hit Impact Effects/Prefabs/Hits/Hit_02.prefab
- Assets/Travis Game Assets/Hit Impact Effects/Prefabs/Hits/Hit_03.prefab
- Assets/Travis Game Assets/Hit Impact Effects/Prefabs/Hits/Hit_04.prefab
- Assets/_Project/Prefabs/Particles/RingEffect.prefab
