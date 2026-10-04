# Part Adjuster Tools

Standalone BepInEx 5 / Harmony plugin for Apocalypter. Extends the existing Part Adjustment tool to select the visible suspension and adjust both axles together. Vanilla engine, exhaust and radiator controls continue through their original FSMs.

Activate the Part Adjustment toolbox as usual, aim at the visible front or rear suspension within the tool's normal two-metre reach, then press the game's Use key to select it. Press Use again or put away the tool to finish.

Version 1.0.4 adds the actual floating wrench indicators at the front and rear suspension. They reuse vanilla's wrench mesh, material, size and tool visibility group, face the camera, and follow the active stock/lifted axle as its width/height changes. Aim at either wrench to select the suspension. These indicators remain the same size while the suspension stretches and are recreated on load.

| Numpad key | Suspension adjustment |
| --- | --- |
| 4 | Narrow both axles toward their standard width |
| 6 | Widen both axles, up to 1.5 times their standard width |
| 2 | Lower the suspension assembly and physical wheel mounts relative to the body |
| 8 | Raise the suspension assembly and physical wheel mounts relative to the body |
| 0 | Reset width and height to standard |

The suspension hint shows only `numpad - adjust wheel spacing`. The standard controls return when you finish or select an engine/exhaust/radiator.

Version 1.0.1 adds a horizontal cylinder connecting the upper suspension rods on each axle. It uses the original suspension's `rusted_black_metal` texture/material, appears when width exceeds 1.0×, stretches with the assembly, and disappears at stock width/reset. Its four-centimetre diameter stays constant. Stock and lifted variants have their own rod-cap placements and follow the game's normal variant visibility. These visual bars are recreated on load and have no collision.

Version 1.0.2 fixes bars disappearing in third-person driving: they now use their axle's rendering layer. The driving camera excludes layer 2, which the original brace implementation used. Their colliders remain disabled/removed, keeping part selection unchanged.

Version 1.0.3 keeps the game's native on-screen adjustment cursor visible while hovering over or adjusting suspension, even if another interaction changes the cursor. This was a cursor change only; the floating wrench indicators were added in 1.0.4. The normal cursor resumes when you leave the suspension. It also includes a transparent axle-and-wrench icon for the Apocasetter mod list.

Width changes stretch the suspension model on its local X axis and move the wheel controllers apart around each axle's original midpoint. Wheel scale, tyre radius and tyre width stay unchanged. Height changes move the model, lift-kit attachment hinge and physical wheel mounts together. NWH uses those mount transforms for suspension raycasts, force application, wheel visuals and wheel colliders on the next physics tick. Factory spring and damper settings continue to come from the game's stock/lifted suspension states. Installing or removing a lift kit preserves the custom width and height offsets.

The plugin discovers vehicles with `suspension_model`, `hinge_suspension_parent/hinge_suspension` and at least four direct `hinge_wheel_*` NWH mounts. This hierarchy was inspected in the installed Poloska, Rustcargo and Rustchief prefabs. Width and height apply to both axles together. Two-wheel trailers without that assembly are excluded.

Each car's adjustments are included in the game's existing `saveItemVar` save data. Older saves default to standard width/height. The mod adds named variables to the existing per-vehicle save wrapper and does not create a separate save file or guess the selected slot. Selection proxies are transient and excluded from ES3 child enumeration.

## Installation and settings

Download `PartAdjusterTools-1.0.4.zip` from [Releases](https://github.com/DeonUrist/PartAdjusterTools/releases/latest) and extract it into your game's `BepInEx/plugins` folder. It creates `BepInEx/plugins/PartAdjustment/PartAdjustment.dll` and `icon.png`. When updating, replace the existing DLL; keep only one installed copy. The release also includes the DLL and PNG separately for manual installation.

Restart the game after installing. BepInEx creates `BepInEx/config/com.denis.apocalypter.partadjustment.cfg` on first load. Apocasetter discovers the plugin through its normal `Apocasetter = true` opt-in; no other mod is required.

Keep `icon.png` beside `PartAdjustment.dll` in the mod's own folder; Apocasetter automatically discovers it there, with no extra registration. For a DLL installed directly in `BepInEx/plugins`, rename the PNG to `PartAdjustment.png` beside that DLL. The icon is copied to `bin/Release` when building. Restart after adding it because Apocasetter caches icons for the session.

Defaults: width step 0.025, height step 0.025 metres and height limit ±0.5 metres. Settings can be changed through Apocasetter or the config file. Turning Enabled off stops selection/input and preserves existing vehicle adjustments. Input is ignored while paused, unfocused or with the cursor unlocked.

## Build and verification

From this directory:

```powershell
$gameDir = 'E:\SteamLibrary\steamapps\common\Apocalypter' # Change to your installation.
dotnet build PartAdjustment.csproj -c Release "-p:GameDir=$gameDir"
dotnet build verification/Verifier.csproj -c Release "-p:GameDir=$gameDir"
& verification/bin/Release/Verifier.exe $gameDir "$PWD\bin\Release\PartAdjustment.dll"
```

Requires a .NET SDK supporting .NET Framework 4.7.2 and an Apocalypter installation with BepInEx 5. Pass `-p:GameDir=...` to both builds or set the `APOCALYPTER_GAME_DIR` environment variable. The verifier takes that installation as its first argument. The plugin DLL contains no game libraries and no hard dependency on Gunplay, GunplayHUD, NPCAI, WomenOfWasteland, Apocaraider or Apocasetter. Build output is `bin/Release/PartAdjustment.dll`; building does not install the mod.

Verification on 2026-10-04: release build with zero warnings/errors; 35 managed checks passed covering width/height bounds, invalid saved numbers, asymmetric axle centering, repeated adjustment limits, PlayMaker variable-cache invalidation, vanilla and custom save-wrapper round trips, legacy saves, independent assembly loading and patch-target existence.

All eight brace endpoint definitions were compared against original mesh rod-cap geometry; source coordinates agree within one micrometre. Offline mesh projections at 1.0×/1.5× were inspected. The user reported the suspension adjustments and bars working in game. The v1.0.2 third-person fix is confirmed against the installed camera masks, but has not been observed in game by the agent.

The game has not been executed for this task. Managed checks do not verify rendering, target selection, driving physics, full save-file reloads or runtime coexistence with other mods. In-game checks still needed:

1. Activate/put away the tool and verify that the two floating suspension wrenches appear/hide alongside the original part wrenches. Select stock and lifted suspension via either wrench; verify the native adjustment cursor, single hint and 4/6/2/8/0 behavior, with the markers following the axles. Aim away and at another adjustable part to check that the cursor returns to normal. Open Apocasetter and check the mod's icon.
2. Drive after adjusting; check that both axles, their visible wheels and physical contacts move together.
3. Attach/remove a lift kit and verify that offsets persist and factory suspension behavior remains intact.
4. Save/load two adjusted cars and switch slots; verify per-car persistence, bounds and reset without accumulating changes.
5. Check the original engine/exhaust/radiator controls, Apocasetter pause/settings, and the installed mod combinations.
