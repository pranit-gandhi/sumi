# Sword combat

The controller already owned combat before this revision. Its rigid feel came from disconnected attack/IK/damage clocks, attacks discarding movement input, long link delays, and a separate painted sword with its own arc. The revision keeps script authority and makes attack data the shared source for timing and motion.

## Playing

- Left Mouse: descending cut, rising return, sweeping finisher. Press again slightly before or during contact to chain.
- R / Middle Mouse: a slower overhead heavy. Either opening light can branch into it.
- WASD: footwork during startup, contact and recovery; input can steer the next cut.
- Space: Brush Flash. On a whiff, it cancels after contact finishes; after a confirmed hit it can cancel earlier. Flash can link back into a light or heavy.
- Right Mouse: hold to guard; a fresh press just before an incoming frontal hit gives a perfect parry. Holding through attack recovery grants guard when legal, without a delayed perfect parry.
- F3: optional development overlay for action time, active/link/cancel status and buffered input.

There is still only one player resource: health. Held guard takes chip damage. Perfect parries avoid damage and recoil the enemy. Guard requires the enemy to be in the forward half-plane.

## Attack data

Select assets under `Assets/Sumi/Resources/Sumi/Attacks`. All windows are **seconds from the start of that attack**, not Animator normalized time.

| Move | Duration | Damage window | Link window | Damage |
| --- | ---: | --- | --- | ---: |
| Opening | 0.48 | 0.105?0.255 | 0.255?0.42 | 21 |
| Returning | 0.46 | 0.09?0.24 | 0.24?0.40 | 25 |
| Finisher | 0.62 | 0.16?0.34 | terminal | 40 |
| Heavy | 0.73 | 0.23?0.43 | terminal | 49 |
| Flash | 0.52 | 0.12?0.32 | 0.34?0.46 | 30 |

Opening ? Returning ? Finisher. Opening and Returning can branch to Heavy. Flash ? Opening or Heavy. Terminal moves must recover before starting another chain. Only one pending light/heavy is retained; the latest press replaces it and expires after 0.20 seconds of real time. Dash intent expires after 0.18 seconds. Input is sampled during hit stop, but state transitions wait until simulation resumes. Menu input and input made while stunned/dead are discarded.

Startup permits 65% movement, contact 30%, and recovery smoothly restores up to 85%. Attack movement has a separate smoothed velocity, so a lunge cannot feed itself into the next frame's footwork. Lunge displacement passes through CharacterController collision, and nearby opponents shorten the lunge. Assist only selects visible opponents within 3.1 m and a 40-degree forward cone. Rotation is bounded, including while locked on.

## Animation and sword contact

`SumiPlayerCombat` owns state changes. `SumiHumanoidRonin` uses hashed full Animator state paths and `CrossFadeInFixedTime` with an explicit layer and zero entry offset. `AttackRate` synchronizes the licensed full-body clip to each definition's duration. Root motion is disabled because the character controller owns displacement.

The shared pose curve supplies the two-hand IK targets, real katana placement and collision sweep. Ink and pale trails follow the tip; the disconnected painted duplicate sword and free-standing arc have been removed. The opening and returning strokes share their endpoint, and a short pose blend carries that endpoint into the next action.

Sweeps clip the previous/current interval to the active window, subdivide it at 8 ms, and sample six points along the blade as well as the blade segment. This covers a whole active window skipped by a slow frame. A per-swing target set prevents repeated damage from multiple colliders or samples. Environment occlusion uses the project's existing layer 8. There is no extra invisible forward contact volume.

Confirmed impacts request one short hit stop/camera impulse per swing. Swing camera motion is restrained; blocks, parries and clean hits have different feedback. Existing enemy recoil and brush effects remain.

## Play Mode review

Enter Play Mode in `SumiShrine` and exercise the opening-to-returning-to-finisher chain, both heavy branches, movement during each phase, Brush Flash hit and whiff recovery, held guard, perfect parry, chip damage, low-health finishers and pause/resume. Repeat the chain at different input rhythms and near walls to catch dropped buffers, duplicate hits or collision-driven motion jumps.

The editor menu **Sumi ? Author Combat Clips** creates missing definitions and animation states. Existing definition tuning is preserved on subsequent runs. Do not rebuild the humanoid or character artwork to tune combat.

## Limits and next tuning pass

The project still has one licensed base sword attack clip. Distinct arm/blade curves and timing make the attacks different, but bespoke full-body attack and transition clips would improve hips, foot planting and weight transfer further. Existing enemy attack animation timing remains a separate future polish area. This is an authored melee sweep, not a physical sword-on-sword simulation. A human play session must judge rhythm, spacing and satisfaction. Existing WebGL/itch packages need rebuilding to include these changes.

## Sources reviewed

- [Unity swordfight discussion](https://discussions.unity.com/t/designing-a-swordfight-game/647646/8): the linked post concerns footwork and turn continuity; the broader thread discusses full-body stances and moves/countermoves.
- [Reddit combo discussion](https://www.reddit.com/r/Unity3D/comments/1p6bsca/best_way_to_trigger_melee_attackscombos/): the quoted suggestion to call Play/CrossFade in code is a valid approach, not a requirement to discard Animator blending.
- [Unity CrossFadeInFixedTime](https://docs.unity3d.com/ScriptReference/Animator.CrossFadeInFixedTime.html): blend durations and entry offsets are specified in seconds.
- [Unity OverlapCapsuleNonAlloc](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Physics.OverlapCapsuleNonAlloc.html): overlap results use a caller-provided collider buffer.

The tuning numbers above are project-specific starting values, not claims from those discussions.
