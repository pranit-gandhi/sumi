# Sumi progress

Updated 2026-09-20. Current milestone: enemy combat challenge iteration.

## 2026-09-20 enemy combat challenge

- Retainers now have 90 health, a two-cut sequence and a braced overhead. Shades have 64 health, faster approach/orbit movement, a narrow lunge and a diagonal retreat. The Oni retains 340 health and gains cut/sweep/overhead patterns, an awakening at 55%, a second-phase return cut, and a 12% execution threshold.
- Held guard now takes 18% chip damage without harming or staggering enemies. Perfect deflection deals 12 damage and grants a 0.95-second punish window. Heavy cuts interrupt braced windups; light hits still deal full damage. Existing recoil and recovery cannot be restarted by repeated hits.
- Ground fans, attack labels and first-use hints communicate threats. Gold indicates bracing; the crimson sweep requires evasion. Enemy timing drives blade posing, two-hand IK, contact and trails. Scarves have bounded segment lengths during lunges.
- The attack token passes at the beginning of recovery, prioritizes another ready enemy, and checks visibility/line of sight. Wave-two arrows reserve a separate attack turn and freeze during pause/hit stop. The Oni encounter uses its own moves instead of independent arrows.
- Verification: Unity 6000.2.14f1 compiled; **34 Play Mode checks passed**, including an actual player heavy blade hit, timed Retainer/Shade/Oni attacks, group handoffs, guard/deflection/interrupt rules, pause/hit-stop hazards, both waves and spell selections, Oni phase/finisher rules, victory, death and restart. No runtime exceptions or compiler errors appeared during the successful run. `git diff --check` passed. The enemy sweep warning and pose were visually reviewed in a local capture.
- `Assets/Editor/SumiCombatVerification.cs` keeps the checks reproducible. Results/captures are local under `Logs/`. Full run flow checks accelerate enemy deaths; they are not a human balance playthrough. Subjective feel still needs human playtesting. WebGL/itch packages have not been rebuilt for this iteration.

See `COMBAT.md` for combat rules, tuning locations and the verification command. Earlier milestones below describe their historical state.

## 2026-09-19 focused PR #1 highlights

- PR #2 remains the source of truth. No PR #1 combat state machine, posture/mastery rules, duplicate blade rendering, UI, or visual treatment was merged.
- Lock-on now eases into and out of targets, gives immediate authority to mouse input, retains targets through a wider release radius, releases dead targets, and cycles targets with the mouse wheel.
- Enemies register in a shared live list. Targeting, attack assist, executions, ink darts, Brush Flash range checks, and local separation no longer scan the full scene every frame.
- Two-hand sword IK now measures the current rig, keeps both goals inside arm reach, lets the off hand slide along the hilt, and eases its release during extreme cuts.
- Unity 6000.2.14f1 recompiled successfully. Play Mode initialized the current arena, player, and enemy with no new runtime error in the active Editor log. A fresh WebGL build succeeded and the itch ZIP was regenerated: 30,740,030 compressed bytes, 31,200,656 extracted bytes, 17 files, largest file 20,710,151 bytes, with root `index.html` confirmed. Comparison captures are local under `Review/` and are intentionally excluded from source control.

## 2026-09-19 combo and interface update

- Left Mouse now chains three distinct cuts with a heavier, wider finisher; the three circles show combo progress.
- Health is a separate bottom bar. Enemy posture was removed; only a locked enemy's health appears at the upper right, and E finishes a nearby enemy at 35% health or less.
- The spell wheel is dark, with a red hovered segment and click-only selection.

## Earlier 2026-09-19 interface and rules update

- The player now has one resource: Health. Mastery, Golden Silence and guard resolve were removed. Held guard takes a small amount of health damage; perfect deflection still avoids the hit.
- `F` throws an ink dart on a short cooldown. Six simple health/throwable spells replace the old eight upgrades, selected from a white three-part spell wheel.
- The HUD uses Jiayou Akira, ten ivory health pips and a red bar with a delayed damage trail. The supplied font is non-commercial; see `ATTRIBUTIONS.md`.

## Previous playable state (2026-09-18)

- Unity 6000.2.14f1, URP 17.2.0, main scene `Assets/Scenes/SumiShrine.unity`.
- Complete arena run: introduction, two enemy waves, a randomized three-of-eight upgrade choice after each wave, Painted Oni boss, victory/death, pause and immediate restart.
- The old corridor is now a rounded four-gate paper courtyard. Its clear center is surrounded by repeated ink huts, torii, lanterns, dry-brush shrubs, pines, shrine geometry and distant mountain washes. Enemies enter from the four cardinal sides.
- The active player remains the real skinned Humanoid ronin with reduced kasa, shadowed face, layered kimono/hakama, moving haori and one katana. Do not run `SumiHumanoidBuild.Build`; use `SumiCharacterArt.Apply` only for intentional character regeneration.
- Controls: WASD walk, Shift jog, Left Mouse shoulder cut, Right Mouse hold/timed deflection, Space Brush Flash, Q lock-on, E execution, Esc pause, R restart after death/victory.

## Combat and run systems

- Shoulder cut: fixed initial strike line with limited early aim help, swept blade traces, one hit per enemy, ink/pale trails, hit-stop, audio and camera response.
- Brush Flash: 3.95 m evasive waist cut, 0.98 s cooldown and authored invulnerability/contact windows. Lethal minor-enemy contact uses the ink-clipped split.
- Right Mouse has a 0.025–0.145 s perfect window and a held resolve guard. Deflections damage posture and add mastery. Damage removes mastery and grants 0.52 s anti-chain-hit grace.
- Broken posture exposes an enemy for 2.5 s. E performs a decisive execution.
- Group AI uses one attack token, orbit direction, local separation, damped CharacterController movement, locked strike direction, readable windup and recovery.
- Enemy roster: masked Retainer, faster Ink Shade and 1.32× Painted Oni with horned mask, broad ink mantle, vermilion corruption, 340 health and 175 posture.
- Wave 2 and the boss add announced arrow strikes: a crimson brush ring warns for 0.82 s. Strikes are dodgeable and perfect-deflectable.
- Gold mastery drives ronin shader accents and eighteen shrine/lantern/torii surfaces. Damage drives shader red and a separate screen wash. Maximum mastery triggers 3.6 s Golden Silence, then mastery returns to 55.
- Eight no-repeat run upgrades: Gilded Edge, Third Bell, Red Reversal, Brush Step, Unbroken Line, Quiet Moon, Falling Petal and Ink Guard.
- UI includes life, mastery, focused-enemy health/posture, boss display, execution prompt, wave titles, upgrade seals, pause, death and victory.
- `SumiTime` centrally owns hit-stop, Golden Silence and menu pause.

## Smoothness and performance

- Enemy recovery crossfades to locomotion instead of holding the final attack pose. Normal attacks no longer continuously home, and combat startup retains some approach momentum.
- Camera impulse is applied after a separate smoothed pose, preventing shake feedback; impulse motion uses stable 19–23 Hz frequencies.
- The rebuilt arena has about 701 MeshRenderers versus roughly 3,731 before. Static windless drawings stop updating property blocks, and the contour shader includes GPU-instancing variants.

## Recorded verification

- Full run: **PASS** — Wave 1, upgrade 1, Wave 2, upgrade 2, Oni identity/stats, victory, restart and time restoration.
- Death flow: **PASS** — death state, control lock, red takeover hook and time restoration.
- Combat sandbox: **PASS** — one-hit tracing, strike completion, perfect/held guard, Brush Flash split and hit-stop restoration.
- Combat motion: **PASS** — maximum sampled 50 ms enemy step 0.105 m; no facing snap in the aligned sample; locomotion and spacing passed.
- Fresh WebGL build: **Succeeded**, zero errors, 31,169,024 extracted bytes, duration 5:03.95.
- Fresh local HTTP/headless-Chrome smoke: loading hidden, blank warning, 960×600 canvas rendered with gameplay HUD.
- Fresh itch ZIP: 30,706,743 compressed bytes, 31,169,024 extracted bytes, 17 files; largest `Build/WebGL.data.unityweb` 20,677,775 bytes; root `index.html` confirmed; longest path 41 characters.

Persistent Unity AI account/licensing 404 warnings are external. The final project compile/build has no Sumi errors.

## Main files

- Run, waves, upgrades, UI, arrows, time: `Assets/Sumi/Scripts/SumiRun.cs`
- Combat and feedback: `Assets/Sumi/Scripts/SumiCombat.cs`
- Arena: `Assets/Sumi/Scripts/SumiSketchWorld.cs`
- Player/camera: `Assets/Sumi/Scripts/SumiPlayer.cs`, `Assets/Sumi/Scripts/SumiCamera.cs`
- Release: `Builds/WebGL/`, `Builds/Sumi-itch.zip`

## Remaining judgment call

The recorded automated and browser checks passed before the disposable checking utilities were removed from the shareable repository. Final difficulty still benefits from one human play session; the next iteration should tune exposed timing, damage and boss health without rebuilding the visual systems.
