# Sumi handoff

Updated 2026-09-20. Enemy combat challenge work follows the PR #2 combat/interface baseline.

## Current enemy combat update

Enemy behavior now lives in `Assets/Sumi/Scripts/SumiEnemy.cs`, with shared attack timing in `SumiEnemyAttack.cs` and hand/katana posing in `SumiEnemyBlade.cs`. Retainers have 90 health and two-cut/braced attacks; Shades have 64 health and lunge/retreat movement. The 340-health Oni has three patterns, awakens at 55% health and becomes executable at 12%.

Held guard takes 18% chip and does not stagger enemies. Perfect deflection stops blockable strikes, deals 12 damage and grants a 0.95-second opening. Heavy cuts interrupt braced windups; crimson sweeps must be evaded. Groups pass attack permission during recovery and account for screen visibility; wave-two arrows reserve their own turn. Arrow warnings and enemies freeze immediately during hit stop.

Unity compilation and 34 runtime checks passed, including real blade contact, enemy sequences, group attacks, phase rules and the entire run state flow through restart. Run **Sumi → Verify Enemy Combat (Play Mode)** from `SumiShrine`; inspect `Logs/combat-verification.txt`, then stop Play Mode. The checks intentionally manipulate the run. Human balance judgment is still needed. Existing WebGL packages predate this update. Details are in `COMBAT.md`.

Earlier handoff sections below record the previous release and may describe superseded rules.

## Current PR #2 refinement

PR #2 remains the current implementation: its data-driven three-cut chain, heavy attack, Brush Flash, swept-blade contact, health-only rules, shuriken throw, spell wheel, HUD, Japanese-style font, run structure, and visuals are intact. Three narrowly selected ideas from PR #1 were manually adapted: eased lock-on with mouse authority and target cycling, a shared live-enemy registry, and rig-measured reach-limited two-hand IK. PR #1 was not merged or cherry-picked.

Use Tab to acquire/release lock and the mouse wheel to cycle visible targets while locked. Q is the dedicated parry; Right Mouse is guard-only. The camera releases a dead or distant target automatically. `SumiEnemy.Active` is maintained by `OnEnable`/`OnDisable`; use it for live-enemy queries instead of scene-wide searches. `SumiGuardIK` in `Assets/Sumi/Scripts/SumiHumanoidRonin.cs` clamps hand goals to the measured arm envelope and shares that resolved grip with the katana root.

Latest focused check: Unity 6000.2.14f1 compiled, Play Mode started, the active arena/player/enemy rendered, and the active Editor log contained no new Sumi exception. The fresh WebGL build succeeded; regenerated `Builds/Sumi-itch.zip` is 30,740,030 compressed bytes and 31,200,656 extracted bytes across 17 files, with root `index.html` and a 20,710,151-byte largest file. Unity AI account/licensing 404 warnings remain external.

## Open and run

- Root: the folder containing this `HANDOFF.md`
- Unity: 6000.2.14f1, URP 17.2.0
- Scene: `Assets/Scenes/SumiShrine.unity`
- Editor mailbox: `python Tools/editor.py state|play|stop|refresh|capture|save`
- Final itch upload: `Builds/Sumi-itch.zip`

The active scene is a rounded four-gate ink courtyard. Preserve its paper/calligraphy direction and the real skinned ronin. The old billboard and primitive rigs are historical. Do not call `SumiHumanoidBuild.Build`; it recreates an older outfit.

## Complete run

`SumiRunDirector` in `Assets/Sumi/Scripts/SumiRun.cs` owns Intro → Wave One → Upgrade One → Wave Two → Upgrade Two → Boss Intro → Boss → Victory/Death. Wave 1 spawns two masked retainers from opposite gates. Wave 2 spawns an Ink Shade and two retainers from three sides and enables announced arrows. The finale spawns the horned/vermilion Painted Oni.

Enemy groups share one attack token, orbit at staggered radii and apply local separation. The arena center intentionally has no decorative collision. Minor enemies have 54–66 health and 56–70 posture; the Oni has 340/175. A 0.52 s damage grace prevents chain stun.

Controls:

- WASD move; Shift jog
- Left Mouse descending shoulder cut
- Right Mouse hold guard; Q timed perfect deflection
- Space Brush Flash dash cut
- Tab target lock
- E execute a posture-broken enemy
- Esc pause; R restart after death/victory

Mastery comes from defense, falls on damage, drives gold shader accents and triggers 3.6 s Golden Silence. Red stays separate through ronin shader staining and the screen wash. `SumiTime` is the sole time-scale owner.

## Recorded verification

The completed release passed checks covering both waves, both upgrades, Oni, victory, restart, death, control lock, red takeover, combat contact, guard outcomes, Brush Flash and hit-stop. Motion sampling measured maximum 0.105 m enemy travel per 50 ms and no aligned facing snap. The disposable verification utilities and captures were removed before source sharing.

## WebGL release

```powershell
python Tools/editor.py SumiBuild.WebGL
python Tools/package_itch.py
```

Latest build succeeded with zero errors. `Builds/Sumi-itch.zip` is 30,706,743 bytes compressed, 31,169,024 bytes extracted, 17 files, with `index.html` at the ZIP root. Largest file is 20,677,775 bytes. Headless Chrome rendered the arena and HUD at 960×600 with no Unity warning text.

## Preservation and limitations

- Preserve `Assets/Sumi/Resources/Ronin/Humanoid.prefab`, its skeleton, Animator, weapon attachment and player scale.
- Regenerate the scene only with `SumiBuild.CreateScene`; it authors the four-gate arena and gold accent surfaces.
- Runtime synthesized sounds have no network dependency. No new external asset was added in this pass.
- Persistent Unity AI licensing/account 404 warnings are unrelated to the game.
- Remaining risk is subjective balance. Run one human 4–5 minute playthrough before public upload and adjust exposed combat timing/damage only if necessary.
