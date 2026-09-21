# Sword combat

The controller already owned combat before this revision. Its rigid feel came from disconnected attack/IK/damage clocks, attacks discarding movement input, long link delays, and a separate painted sword with its own arc. The revision keeps script authority and makes attack data the shared source for timing and motion.

## Playing

- Left Mouse: descending cut, rising return, sweeping finisher. Press again slightly before or during contact to chain.
- R / Middle Mouse: a slower overhead heavy. Either opening light can branch into it.
- WASD: footwork during startup, contact and recovery; input can steer the next cut.
- Space: Brush Flash. On a whiff, it cancels after contact finishes; after a confirmed hit it can cancel earlier. Flash can link back into a light or heavy.
- F: 1.08-second shuriken throw with a deliberate gather, shoulder coil, release and follow-through. Release occurs at 0.58 seconds; Brush Flash can cancel the late recovery.
- Right Mouse: hold to guard. Guard mitigates frontal blockable damage but never creates a perfect parry.
- Q: dedicated parry. Its 0.025–0.16-second perfect window deflects a frontal blockable strike; the committed pose then recovers or flows into held guard.
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

The project still has one licensed base sword attack clip. Player and enemy arm/blade curves and timing make attacks different, but bespoke full-body attack and transition clips would improve hips, foot planting and weight transfer further. Player contacts use swept blade samples; enemy contacts use the warned attack sector during its active window. This is not a physical sword-on-sword simulation. A human play session must judge rhythm, spacing and satisfaction. Existing WebGL/itch packages need rebuilding to include these changes.

## Enemy challenge update — 2026-09-20

Enemies now ask for different responses while retaining the health-only player rules:

- **Ashen Retainer (90 health):** two cuts with a readable return, then an occasional slower braced overhead. A clean light chain deals 86 damage and opens an execution rather than killing outright.
- **Ink Shade (64 health):** faster approach and lateral movement, a narrow lunging attack, then a diagonal retreat. Its low health rewards catching it before it escapes.
- **Painted Oni (340 health):** alternates a cut, crimson sweep and delayed overhead. At 55% health it transitions into a second phase at the next safe opening, adds a return cut, and reduces idle time. Execution becomes available at 12% health so the finale is played out.

Black ground strokes mark ordinary attacks. Gold strokes and the overhead pose mark bracing: light hits still deal full damage, but a heavy cut is needed to interrupt the windup. Perfect deflection interrupts every blockable strike, including a braced strike, deals 12 damage and grants a 0.95-second opening. A held frontal guard instead takes 18% chip damage and does not hurt or recoil the attacker; Steady Heart also reduces chip damage.

The broad crimson fan marks a sweep that must be evaded with Brush Flash or by leaving its reach. It cannot be blocked or deflected. The warning stops tracking before the strike; the locked enemy HUD and first-use hints explain the response in text as well as color. Attack timing, hand IK, the actual katana and trails share `SumiEnemyAttack` definitions. Heavy cuts break braced windups; once released, braced attacks must be defended or evaded. Repeated hits do not restart an existing recoil or recovery window.

Groups still limit melee to one committed attacker, including its return cut. Permission passes at the start of recovery, so another enemy can prepare while the first recovers. Selection favors another ready enemy and checks screen visibility and line of sight. Faster approach, orbiting and separation keep waiting enemies active. Wave-two arrows reserve their own turn in the same threat budget. The Oni encounter focuses on its own patterns. Arrow warnings freeze during pause and hit stop.

Tuning lives in `Assets/Sumi/Scripts/SumiEnemyAttack.cs` (named attacks), `SumiEnemy.cs` (health, reactions and movement), and `SumiRun.cs` (group pressure). Timings are prototype choices, not universal reaction-time targets.

### Repeatable verification

With `SumiShrine` in Play Mode, use **Sumi → Verify Enemy Combat (Play Mode)** or `python Tools/editor.py SumiCombatVerification.Run`. The checks exercise actual runtime objects, timed group attacks, guard/deflection/interrupt rules, hazards, phase changes and run progression. Results go to `Logs/combat-verification.txt`. Stop Play Mode afterwards; this deliberately manipulates the current run.

For human balance review, compare aggressive light-combo play, patient deflection/heavy play and repeated Flash use. Check that an experienced player can identify each hit's cause, earn a full punish after a deflection, and distinguish the return cut from an opening. Watch for enemies spending too long outside the camera, unclear sweep boundaries or a strategy that wins every encounter without adjustment.

## Sources reviewed

- [Unity swordfight discussion](https://discussions.unity.com/t/designing-a-swordfight-game/647646/8): the linked post concerns footwork and turn continuity; the broader thread discusses full-body stances and moves/countermoves.
- [Reddit combo discussion](https://www.reddit.com/r/Unity3D/comments/1p6bsca/best_way_to_trigger_melee_attackscombos/): the quoted suggestion to call Play/CrossFade in code is a valid approach, not a requirement to discard Animator blending.
- [Unity CrossFadeInFixedTime](https://docs.unity3d.com/ScriptReference/Animator.CrossFadeInFixedTime.html): blend durations and entry offsets are specified in seconds.
- [Unity OverlapCapsuleNonAlloc](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Physics.OverlapCapsuleNonAlloc.html): overlap results use a caller-provided collider buffer.

The tuning numbers above are project-specific starting values, not claims from those discussions.
