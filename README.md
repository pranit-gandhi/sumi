# Sumi

Sumi is a complete Unity 6 third-person katana roguelite slice presented as a living monochrome ink drawing. A wandering ronin survives two four-direction arena waves, chooses two brush vows, and confronts a Painted Oni while defense spreads gold and wounds return the painting to red and grayscale.

## Controls

- `WASD`: move
- `Shift`: jog
- Mouse: camera
- Left Mouse: descending shoulder cut
- Right Mouse: hold guard; press just before contact for a perfect deflection
- `Space`: Brush Flash evasive dash cut
- `Q`: toggle target lock
- `E`: execute a posture-broken enemy
- `Esc`: pause
- `R`: restart after victory or death

## Play in Unity

1. Open this folder in Unity 6000.2.14f1.
2. Open `Assets/Scenes/SumiShrine.unity`.
3. Enter Play Mode.

The project uses URP 17.2.0. The run includes two randomized three-choice upgrade screens, telegraphed arrow hazards, gold mastery/Golden Silence, damage red, executions, an Oni boss, pause, death, victory and restart.

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
