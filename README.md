# Part Adjuster Tools

Standalone BepInEx 5 / Harmony plugin for Apocalypter. Extends the existing Part Adjustment tool to adjust suspension, freely attached items and mounted gauges. Vanilla engine, exhaust and radiator controls continue through their original FSMs.

Version 1.5.8: `[Headlights] TailLightPixel` (on) keeps the tail light's red light per-pixel even with many lights around (forward rendering otherwise lights extra lights per vertex, which looks patchy on the ground); a light report is written to the log when a car with tail lights switches its lights on (investigating darker ground where two tail lights overlap).

Version 1.5.7: the tail light settings are back for tuning - `[Headlights] TailLightBeam` (3, strength vs a headlight beam), `TailLightReach` (4 m), `TailLightShadows` (off), all applied at once. Use (F) during an E adjustment now only removes the light you aim at: putting the tool away at the toolbox no longer drops the light being adjusted; ending an adjustment any way keeps its pose.

Version 1.5.6: the tail light's red light is a short glow on the ground right behind the lamp (4 m, 3x a headlight beam's strength, no shadows, so two crossing tail lights no longer darken the ground); the TailLightBeam / TailLightReach settings are gone (fixed values).

Version 1.5.5: the red light reaches 32 m instead of 80 m (40 %), set by `[Headlights] TailLightReach` (2-200 m, applies at once); strength stays `TailLightBeam`.

Version 1.5.4: the tail light's red beam lights up the ground and objects behind the car - 3x the headlight beam's strength (setting `[Headlights] TailLightBeam`, 0-8, applies at once) up to 80 m.

Version 1.5.3: the tail light's lens glow (a 0.1 m point light inside the lens) keeps its full reach and is a bit stronger - 1.5.2 shortened it to 0.07 m so the lens never lit; the red beam throws 20 % as far as a headlight.

Version 1.5.2: tail lights switch on visibly - the lit part of the light (beam/glow) keeps its brightness and turns red instead of being darkened, the red light keeps the headlight's strength.

Version 1.5.1: every headlight model (Headlight, Headlight 2, Headlight 3) has its tail light (`tail_light`, `tail_light_2`, `tail_light_3`, "Tail Light", "Tail Light 2", "Tail Light 3" in Apocaspawner and world loot); freely mounted lights follow the car's light switch (and match it when mounted); a headlight slot only takes the press when you aim at the slot itself, and a slot that stayed "ready" after the light left it no longer blocks or later steals a mounted light. A refused mount is logged with the reason.

Version 1.5.0 adds free headlight mounting, red tail lights and a put-away key for tools.

- **Headlights anywhere (Part Adjustment tool selected).** Hold a headlight or tail light, aim at any part of a car body within reach and press Use (normally F): it is attached there, flush on the aimed surface (or exactly where you hold it if it already touches the body). Use on an attached headlight / tail light removes it again. Without the tool, headlights only fit the car's headlight slots as before, and a free slot that already offers the held light keeps priority. Freely mounted lights are ordinary attached parts: the utility tool (wrench) removes them, they are saved with the car, they move with the body part they sit on and they switch on with the car's lights.
- **E adjustment for lights.** With the tool selected, secondary Use (normally E) on an attached headlight or tail light - in a slot or mounted freely - starts the same move/rotate session as for plates, crates and gauges.
- **Tail lights.** A red version of the headlight (red lens and housing tint, red light). Headlights spawned in wrecks, caves and merchant stock are tail lights at `TailLightChance` percent (default 8). Apocaspawner lists them as "Tail Light" under Vehicle Parts without any change to Apocaspawner. A tail light is the game's own headlight item named `tail_light(Clone)N`; the save stores it as a headlight and the mod restores the red look when it loads. Without the mod a tail light simply becomes a normal headlight again.
- **Put away tools with X.** `PutAwayKey` (default X) puts away the utility tool, the repair tool or the Part Adjustment tool through the game's own put-away step, as if you used the toolbox again. `PutAwayWithKey` switches it off.

Version 1.4.0 withdraws the 1.3.0 parking hold (it left some cars unable to move) and the 1.2.0 driver/wheel-collider rule: the mod no longer touches a car's physics or wheels. The one stability rule left is the centre of mass (`LiftCenterOfMass`, on/off): a car with a suspension lift kit carries it 0.3 m lower, plus 1.5 x any extra lift of the body, so a tall car on big wheels does not flip in turns. Apocapatrol 2.5.0 applies the same rule to its raider cars.

Version 1.2.1 lowers a lifted car's centre of mass by the lift (`LiftCenterOfMass`, on/off), so a tall car on big wheels does not flip in turns; the Rigidbody's automatic centre is re-read as parts change. Apocapatrol applies the same rule to its template cars without this mod.

Version 1.2.0 requires a suspension lift kit on the vehicle to adjust the suspension: without one the width and height are standard and the hint says so; removing the kit returns an adjusted car to standard. It adds a public `PartAdjustment.SuspensionApi` (`Set`, `Get`, `KitFitted`) so other mods - Apocapatrol applies its car templates' suspension through it - keep their values in this mod's per-car save data. It also stops the car the player drives from vibrating with a raised suspension or big truck wheels: the wheel colliders (NWH's wheel mesh colliders and the game's wheel hub sphere, layer 2) collide with the driver, and once they reach into the cabin PhysX pushes the driver and the car apart every physics step. Collisions between the driver and the wheel colliders of their car are now ignored, re-checked twice a second.

Version 1.1.0 moves the native "ADJUST CAR PARTS" title and its instructions to the top left while adjusting an attached item or gauge with secondary Use (normally E). The detailed item controls retain their existing location, avoiding overlap. The native block follows Canvas scaling with a 20-unit edge margin, and its original position/anchors are restored when the session ends, the tool is put away, gameplay is suspended or the UI runner is removed. The native child layout and styling stay intact; layout writes happen only on entry/exit.

Version 1.0.9 fixes wheels jumping inward on the first width adjustment after installing a lift kit, reproduced with TinyTyrant. A target check introduced in 1.0.6 compared a GameObject to its PlayMaker component, rejecting native stock/lifted mount updates. The check now compares the correct GameObjects, so installing/removing a lift kit updates the factory mount baseline and chooses the matching axle geometry. The disabled-FSM, local-space and target checks remain intact, including the bus/truck exclusions. Existing saved adjustments are recalculated on load; no reset or new save is required.

Version 1.0.8 changes wheel movement to follow the matching stock/lifted mesh end, preserving factory hub clearance instead of multiplying the wheel-center offset. The model's own pivot, child transforms and scale are included; wheel size remains unchanged. Mesh bounds work on non-readable game meshes and are cached once. Its geometry fix did not catch the rejected lift-transition callback; 1.0.9 corrects that regression.

Version 1.0.7 integrates Item Adjustment. Select the Part Adjustment tool, aim within two metres at an attached crate, box, metal/wire plate, spike, trophy or mounted gauge, then press secondary Use (normally E) to begin. Press it again to finish, including while looking away. Speed/RPM/fuel/oil/temperature/boost gauges require the adjustment tool; a wrench is not needed for adjustment. Putting away the tool ends the session immediately. Fixed socket parts such as engines, doors and wheels retain their normal controls.

| Game Controls action | Attached item / gauge adjustment |
| --- | --- |
| Secondary Use (normally E) | Begin / finish adjustment |
| Adjust-change move/rotate mode | Switch position / rotation mode |
| All six Adjust movement/rotation actions | Move or rotate using the native directional controls |
| Adjust-reset | Restore the pose at the beginning of this session |
| Left Alt + an Adjust direction | Faster movement / rotation |

Default steps are 0.01 metres / 1 degree, or 0.1 metres / 10 degrees while holding Left Alt. Configure them and the optional alternate modifier in the new Items settings. Shift is not a default modifier. Directions follow the camera, including pitch and roll; set ReferenceFrame to 1 for world axes. Held-item MMB rotation also uses that reference frame while the adjustment tool is selected, and retains native behavior otherwise. All toggle and directional controls respect the game's primary/alternate bindings.

Item adjustments preserve the attachment parent, tag and physics and leave the final local pose for the game's existing transform saving. Reset restores the session's starting pose rather than a factory mount. Loose/held items and arbitrary locked cargo are excluded. Primary Use remains native and ends our session before its normal interaction. Secondary Use is consumed only during adjustment and its start/stop frames, with the tool selected, to prevent overlapping E actions. Native part adjustment and item adjustment use separate sessions. Pausing or losing focus suspends input; entering a vehicle, detaching/reparenting or losing the camera ends selection.

The item controls reuse native UI styling. Camera/tool FSM references, components and binding labels are cached, repeated UI writes are skipped, and binding/settings changes still update immediately. The second line of the native item hint is allowed to overflow its one-line rectangle while shown, with the original text and overflow restored on exit. Do not run the standalone ItemAdjustment plugin alongside this integrated version.

Activate the Part Adjustment toolbox as usual, aim at the visible front or rear suspension within the tool's normal two-metre reach, then press the game's Use key to select it. Press Use again or put away the tool to finish.

Version 1.0.4 adds the actual floating wrench indicators at the front and rear suspension. They reuse vanilla's wrench mesh, material, size and tool visibility group, face the camera, and follow the active stock/lifted axle as its width/height changes. Aim at either wrench to select the suspension. These indicators remain the same size while the suspension stretches and are recreated on load.

Version 1.0.5 uses the game's existing adjustment bindings, including primary and alternate keys. Rebind them in the game's Controls menu; a numeric keypad is no longer required.

Version 1.0.6 fixes wheels moving inward/backward on load for vehicles without an active suspension attachment system. Rustliner, Rustcargo and Rustchief retain dormant suspension objects and disabled wheel FSMs in their prefabs; earlier versions mistakenly used those scripts' coordinates. These vehicles are now excluded from suspension discovery, adjustment and persistence.

The same update reduces work while the tool is idle: selection colliders change only when tool visibility or the vehicle registry changes, including vehicles spawned while the tool is already active. Vehicle discovery uses deferred wheel-enable notifications with scene/tool-time wheel scans as a fallback; full FSM scans stop once tool/cursor references are found. Floating wrenches are created on demand and continue following moving axles and the camera every active frame, using one shared camera lookup. Hint strings are reused until primary/alternate bindings change, and redundant UI/collider writes, repeated component lookups and axle-pair sorting have been removed. Pressing against an adjustment limit avoids reapplying unchanged geometry; reset still reapplies factory offsets.

| Game Controls action | Suspension adjustment |
| --- | --- |
| Adjust-move left/rotate left | Narrow both axles toward their standard width |
| Adjust-move right/rotate right | Widen both axles, up to 1.5 times their standard width |
| Adjust-move down/rotate backward | Lower the suspension assembly and physical wheel mounts relative to the body |
| Adjust-move up/rotate forward | Raise the suspension assembly and physical wheel mounts relative to the body |
| Adjust-reset | Reset width and height to standard |

The suspension hint shows the current keys for spacing, height and reset, including alternate bindings. It updates when bindings change and shows `Unbound` for an action with no assigned keys. The standard controls return when you finish or select an engine/exhaust/radiator. Suspension always uses the movement directions above; it has no rotation mode.

Version 1.0.1 adds a horizontal cylinder connecting the upper suspension rods on each axle. It uses the original suspension's `rusted_black_metal` texture/material, appears when width exceeds 1.0×, stretches with the assembly, and disappears at stock width/reset. Its four-centimetre diameter stays constant. Stock and lifted variants have their own rod-cap placements and follow the game's normal variant visibility. These visual bars are recreated on load and have no collision.

Version 1.0.2 fixes bars disappearing in third-person driving: they now use their axle's rendering layer. The driving camera excludes layer 2, which the original brace implementation used. Their colliders remain disabled/removed, keeping part selection unchanged.

Version 1.0.3 keeps the game's native on-screen adjustment cursor visible while hovering over or adjusting suspension, even if another interaction changes the cursor. This was a cursor change only; the floating wrench indicators were added in 1.0.4. The normal cursor resumes when you leave the suspension. It also includes a transparent axle-and-wrench icon for the Apocasetter mod list.

Width changes stretch the suspension model on its local X axis and move the wheel controllers by the same displacement as their corresponding axle ends, preserving the original wheel-to-end clearance. Wheel scale, tyre radius and tyre width stay unchanged. Height changes move the model, lift-kit attachment hinge and physical wheel mounts together. NWH uses those mount transforms for suspension raycasts, force application, wheel visuals and wheel colliders on the next physics tick. Factory spring and damper settings continue to come from the game's stock/lifted suspension states. Installing or removing a lift kit preserves the custom width and height offsets. Unrecognized custom axle meshes keep the previous spacing fallback.

The plugin requires active `suspension_model` and `hinge_suspension_parent/hinge_suspension` branches, enabled attachment/check FSMs, and at least four direct `hinge_wheel_*` NWH mounts with enabled suspension FSMs and enabled local position actions targeting those mounts in both stock/lifted states. Activity is checked relative to the vehicle so an inactive vehicle root during loading does not prevent restoration. All 12 candidate prefabs were inspected: Poloska, PipeRat, TinyTyrant, Junker, Vulture, Duke, Outrider, Rustallion and PigPen have active systems; Rustliner, Rustcargo and Rustchief do not. Two-wheel trailers are excluded. Width and height apply to both axles together on supported vehicles.

Each car's adjustments are included in the game's existing `saveItemVar` save data. Older saves default to standard width/height. The mod adds named variables to the existing per-vehicle save wrapper and does not create a separate save file or guess the selected slot. Selection proxies are transient and excluded from ES3 child enumeration.

Existing saves do not require a new game. Unsupported vehicles ignore old adjustment fields, which are omitted on the next save. If a save also persisted displaced wheel transforms, recovery compares them with the old mod's exact width/height calculation and restores matching mounts from the original prefab found by its Easy Save prefab ID. Factory positions and unrelated transform changes are left alone. Recovery requires old mod metadata and the original dormant prefab assembly; it never uses disabled FSM coordinates as factory positions. Supported vehicles keep their existing saved adjustments.

## Installation and settings

Download `PartAdjusterTools-1.1.0.zip` from [Releases](https://github.com/DeonUrist/PartAdjusterTools/releases/latest) and extract it into your game's `BepInEx/plugins` folder. It creates `BepInEx/plugins/PartAdjustment/PartAdjustment.dll` and `icon.png`. When updating, replace the existing DLL; keep only one installed copy. The release also includes the DLL and PNG separately for manual installation.

Restart the game after installing. BepInEx creates `BepInEx/config/com.denis.apocalypter.partadjustment.cfg` on first load. Apocasetter discovers the plugin through its normal `Apocasetter = true` opt-in; no other mod is required.

Keep `icon.png` beside `PartAdjustment.dll` in the mod's own folder; Apocasetter automatically discovers it there, with no extra registration. For a DLL installed directly in `BepInEx/plugins`, rename the PNG to `PartAdjustment.png` beside that DLL. The icon is copied to `bin/Release` when building. Restart after adding it because Apocasetter caches icons for the session.

Settings added in 1.5.0: `[Headlights] AttachAnywhere` (true), `[Headlights] TailLightChance` (8, 0-100), `[Tools] PutAwayWithKey` (true), `[Tools] PutAwayKey` (X). `AttachAnywhere` also needs `Enabled`.

Defaults: width step 0.025, height step 0.025 metres and height limit ±0.5 metres. Settings can be changed through Apocasetter or the config file. Turning Enabled off stops selection/input and preserves existing vehicle adjustments. Input is ignored while paused, unfocused or with the cursor unlocked.

## Build and verification

From this directory:

```powershell
$gameDir = 'E:\SteamLibrary\steamapps\common\Apocalypter' # Change to your installation.
dotnet build PartAdjustment.csproj -c Release "-p:GameDir=$gameDir"
dotnet build verification/Verifier.csproj -c Release "-p:GameDir=$gameDir"
& verification/bin/Release/Verifier.exe $gameDir "$PWD\bin\Release\PartAdjustment.dll"
dotnet run --project verification/items/Verifier.csproj -c Release
```

Requires a .NET SDK supporting .NET Framework 4.7.2 and an Apocalypter installation with BepInEx 5. Pass `-p:GameDir=...` to both builds or set the `APOCALYPTER_GAME_DIR` environment variable. The verifier takes that installation as its first argument. The plugin DLL contains no game libraries and no hard dependency on Gunplay, GunplayHUD, NPCAI, WomenOfWasteland, Apocaraider or Apocasetter. Build output is `bin/Release/PartAdjustment.dll`; building does not install the mod.

Verification on 2026-10-05: release and verifier builds with zero warnings/errors; 459 managed checks passed covering width/height bounds, invalid saved numbers, asymmetric axle centering, repeated adjustment limits, PlayMaker variable-cache invalidation, vanilla and custom save-wrapper round trips, all 12 vehicle eligibility fixtures, disabled/wrong-target position actions, dormant prefab states, old-save displacement recognition at multiple widths/heights, repeated recovery, preservation of unrelated transforms, removal of obsolete fields, tool visibility transitions/new vehicles, hint caching/rebinding, independent assembly loading and patch-target existence. The legacy save-wrapper Harmony patch was installed and removed successfully in the offline verifier. `verification/VehicleFixtures.csv` contains rounded mount coordinates and activity flags measured from the installed prefabs. These are offline checks, not a full Unity save/load execution or an in-game performance benchmark.

All eight brace endpoint definitions were compared against original mesh rod-cap geometry; source coordinates agree within one micrometre. Offline mesh projections at 1.0×/1.5× were inspected. The user reported the suspension adjustments and bars working in game. The v1.0.2 third-person fix is confirmed against the installed camera masks, but has not been observed in game by the agent.

Version 1.0.7 validation: 472 managed checks and 93 headless checks pass. The latter execute the production plugin/session/input/hint code with adapters for Unity, PlayMaker and BepInEx. They cover the tool requirement for items and gauges, wrench-only rejection, mid-frame unequip, native adjustment handoff, Left Alt steps, camera/world axes, reset, rebound bindings, held-item rotation gating, input suppression, persistent runner recreation, native hint ownership/clipping cleanup and unchanged-frame UI reuse. Native input/rotation hook signatures and prefix metadata were checked against the installed game assemblies. UI rendering, native detour execution and full save/load remain live-game checks.

Version 1.0.8 validation: 472 managed checks and 2,544 headless checks pass, with zero build warnings/errors. `verification/SuspensionWidthFixtures.csv` records 72 wheel/mesh measurements from all nine supported prefabs, covering stock/lifted front/rear mounts. The production geometry helper is tested with those fixtures at widths 1.0, 1.025, 1.25 and 1.5, repeated reset and rotated/scaled vehicle parents. Geometry is also initialized from a model already expanded by a restored save; the original local endpoints remain authoritative. Tests reproduce the old gap growth and verify that each wheel displacement equals its mesh-end displacement. Native rendering/physics and full game save/load still require an in-game check.

Version 1.0.9 validation: 472 managed checks, 2,544 headless checks and 146 native Unity checks pass. The native verifier executes the real PlayMaker actions and production Harmony callback on a synthetic TinyTyrant assembly using measured factory mounts/mesh endpoints. It covers installing/removing/reinstalling a lift kit, the first width press, maximum width, reset, three restorations of saved adjustment variables and rejection of world-space/wrong-target actions. The first widening moves the lifted front mounts from ±0.8 m to ±0.819184 m; 1.0.8 instead snapped them inward to about ±0.623 m. The native test runs in the headless menu without loading or writing a user save. This verifies callbacks/transforms, not visual rendering, driving physics or a full save-file reload.

Version 1.1.0 validation: release/native verifier builds pass with zero warnings/errors, alongside 472 managed checks, 2,547 headless checks and 146 native Unity suspension checks. The existing hint checks now cover separating the native panel from item controls and restoring its position on unequip. Layout measurements come from the installed game's RectTransform data; visual HUD rendering has not been checked in an interactive session.

To repeat the native regression test, first build and install the matching production DLL, close the game, then run:

```powershell
& verification/unity/Run.ps1 -GameDir $gameDir
```

The script builds the verifier, launches the menu headlessly, writes its report under `research`, removes the temporary verifier plugin and restores the previous BepInEx log. The verifier stays inert without its explicit regression command-line argument and is not included in release packages.

Visual rendering, target selection, driving physics, full save-file reloads and runtime coexistence with other mods still need in-game checks:

1. Activate/put away the tool and verify that the two floating suspension wrenches appear/hide alongside the original part wrenches. Select stock and lifted suspension via either wrench; verify the native adjustment cursor and spacing/height/reset controls, with the markers following the axles. Rebind all five actions to keys outside the numpad and check both primary and alternate bindings, the updated hint, and that previous keys stop adjusting unless still bound. Aim away and at another adjustable part to check that the cursor returns to normal. Open Apocasetter and check the mod's icon.
2. Drive after adjusting; check that both axles, their visible wheels and physical contacts move together.
3. Attach/remove a lift kit and verify that offsets persist and factory suspension behavior remains intact.
4. Save/load two adjusted cars and switch slots; verify per-car persistence, bounds and reset without accumulating changes.
5. Check the original engine/exhaust/radiator controls, Apocasetter pause/settings, and the installed mod combinations.
6. Spawn Rustliner, Rustcargo and Rustchief, attach wheels, save/load repeatedly and verify their factory wheel spacing/positions remain unchanged. Load an affected save from 1.0.5 or earlier, verify correction, resave and load again. Repeat with a vanilla save and with a supported car's saved stock/lifted adjustments.
7. While the tool is active, spawn another supported car and verify its selection targets and wrenches become available. Toggle the tool/mod repeatedly; check stock/lifted marker visibility, camera following and immediate hint updates after rebinding. Change scenes and check discovery resumes.
8. With the tool selected, adjust an attached crate/plate and a speed gauge without a wrench. Check E start/stop, both modes, Left Alt fast steps, reset, camera/world directions, hints and rebinding. Put away the tool or begin native suspension/engine adjustment and verify item input stops. Drive, save/load and verify placement; with the tool put away, check native E and held-item MMB behavior.
