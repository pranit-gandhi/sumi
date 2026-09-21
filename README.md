# Sumi

Sumi is a Unity 6 third-person katana roguelite slice presented as a living monochrome ink drawing. A wandering ronin survives two arena waves, chooses two spells, and confronts a Painted Oni. Health is the only player resource.

## Controls

- `WASD`: move
- `Shift`: jog
- Mouse: camera
- Left Mouse: buffered descending cut, return cut, sweeping finisher
- `R` or Middle Mouse: overhead heavy; branch into it from either opening light cut
- Right Mouse: hold guard
- `Q`: dedicated timed parry; press just before contact for a perfect deflection
- `Space`: buffered Brush Flash; cancel earlier after a confirmed hit
- `Tab`: toggle target lock
- `E`: finish a nearby enemy at 35% health or less (12% for the Oni)
- `F`: perform a drawn-out shuriken throw (3.5-second cooldown)
- `F3`: toggle combat timing debug overlay
- `Esc`: pause
- `R`: restart after victory or death

## Play in Unity

1. Open this folder in Unity 6000.2.14f1.
2. Open `Assets/Scenes/SumiShrine.unity`.
3. Enter Play Mode.

The project uses URP 17.2.0. The run includes two randomized three-choice spell wheels, telegraphed arrow hazards, health and shuriken upgrades, a three-hit sword combo, low-health executions, an Oni boss, pause, death, victory and restart.

The run plays the supplied `Samurai.mp3` as looping background music and uses recorded sword swings, impacts, and parries. Music pauses with the pause screen.

## Collaborating

Create a branch from `main` for each change and open `Assets/Scenes/SumiShrine.unity` for gameplay work. Commit `Assets` together with their `.meta` files, plus intentional changes under `Packages` and `ProjectSettings`. Unity-generated `Library`, `Temp`, `Logs`, `UserSettings`, local review captures and release builds are excluded by `.gitignore` and should remain local.

Before opening a gameplay PR, enter Play Mode from `SumiShrine`, exercise the changed combat or run flow, and confirm the Console has no new errors. Keep the project on Unity 6000.2.14f1 and URP 17.2.0 so serialized scenes and imported assets remain stable between teammates.

## Build and package WebGL

With Unity open at the project root:

```powershell
python Tools/editor.py SumiBuild.WebGL
python Tools/package_itch.py
```

The build is written to `Builds/WebGL`. The itch.io upload is `Builds/Sumi-itch.zip`, with `index.html` at the archive root. The current release loaded successfully through local HTTP in headless Chrome and passed the itch archive checks.

See `PROGRESS.md` for the recorded release results and package facts, and `ATTRIBUTIONS.md` for asset licenses.

## Known limitation

The release passed state, motion, browser and archive checks before the disposable verification utilities were removed from the shareable repository. Final combat difficulty and timing remain subjective and should receive one human balance playthrough before publishing.

Do not run `SumiHumanoidBuild.Build`; it recreates an older outfit. Use `SumiCharacterArt.Apply` only when intentionally regenerating the current character art.

## Sword combat tuning

Moves live in `Assets/Sumi/Resources/Sumi/Attacks/`. Each asset controls timing in seconds, damage, footwork, legal follow-ups and impact feedback. See [COMBAT.md](COMBAT.md) for the design, controls and repeatable Play Mode checks. Existing WebGL packages predate this combat update; build again to distribute it.

Enemies now use distinct patterns: Retainers chain cuts, Shades lunge and retreat, and the Oni alternates cuts, delayed slams and crimson sweeps before awakening into a second phase. Gold warnings indicate braced attacks: use a heavy cut during their windup or perfectly deflect their strike. Crimson sweeps require Brush Flash or moving out of reach. Held guard mitigates damage without staggering enemies. The next opponent can prepare during another's recovery, while arrow hazards take a separate turn in the group attack budget.
