using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using HarmonyLib;
using InsaneSystems.InputManager;
using PartAdjustment;
using UnityEngine;

internal static class Program
{
    private static string game;
    private static int checks;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.00001f;
    private static Assembly Resolve(object sender, ResolveEventArgs e)
    {
        foreach (var directory in new[] { "BepInEx/core", "Apocalypter_Data/Managed" })
        {
            var file = Path.Combine(game, directory, new AssemblyName(e.Name).Name + ".dll");
            if (File.Exists(file)) return Assembly.LoadFrom(file);
        }
        return null;
    }
    private static int Main(string[] args)
    {
        game = Path.GetFullPath(args[0]);
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        try
        {
            Mathematics();
            SaveRoundTrip();
            SuspensionRegression();
            VisibilityRegression();
            BindingRegression();
            Hooks(Path.GetFullPath(args[1]));
            Console.WriteLine("PASS: " + checks + " managed checks. Game not executed.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void Mathematics()
    {
        Check(AdjustmentMath.Width(0) == 1, "Cannot shrink below stock width");
        Check(AdjustmentMath.Width(2) == 1.5f, "Maximum width is 1.5x");
        Check(AdjustmentMath.Width(float.NaN) == 1 && AdjustmentMath.Width(float.PositiveInfinity) == 1, "Invalid saved widths default to stock");
        Check(AdjustmentMath.Height(2, .5f) == .5f && AdjustmentMath.Height(-2, .5f) == -.5f, "Both height bounds");
        Check(AdjustmentMath.Height(float.NaN, .5f) == 0, "Invalid saved height defaults to stock");
        float width = 1;
        for (int i = 0; i < 100; i++) width = AdjustmentMath.Width(width + .025f);
        Check(width == 1.5f, "Repeated widening stays at upper bound");
        for (int i = 0; i < 100; i++) width = AdjustmentMath.Width(width - .025f);
        Check(width == 1f, "Repeated narrowing stays at lower bound");
        foreach (var axle in new[] { new[] { -.5638428f, .5667725f }, new[] { -.9f, .9f }, new[] { -.94f, .94f } })
        {
            float center = (axle[0] + axle[1]) / 2;
            float left = AdjustmentMath.SpacedX(axle[0], center, 1.5f);
            float right = AdjustmentMath.SpacedX(axle[1], center, 1.5f);
            Check(Near((right + left) / 2, center), "Axle center preserved for asymmetric stock mounts");
            Check(Near(right - left, (axle[1] - axle[0]) * 1.5f), "Track width grows by exactly 50 percent");
            Check(Near(AdjustmentMath.SpacedX(axle[0], center, 1), axle[0]), "Reset returns exactly to stock mount");
        }
    }
    private static void SaveRoundTrip()
    {
        var fsm = new Fsm();
        var vars = fsm.Variables;
        var vanilla = new FsmFloat("Liquid") { Value = 12f };
        vars.FloatVariables = new[] { vanilla };
        vars.Reinitialize();
        vars.GetAllNamedVariables(); // Exercise PlayMaker's cache before inserting our variables.
        var width = SaveVariables.Float(vars, SaveVariables.Width, 1);
        var height = SaveVariables.Float(vars, SaveVariables.Height, 0);
        var scale = SaveVariables.Vector(vars, SaveVariables.ModelScale, new Vector3(1, 1, 1));
        width.Value = 1.4f; height.Value = -.2f;
        Check(vars.GetAllNamedVariables().Any(v => v.Name == SaveVariables.Width), "SaveAll sees added variables after cache invalidation");
        Check(ReferenceEquals(width, SaveVariables.Float(vars, SaveVariables.Width, 1)), "Repeated save/load does not duplicate variables");
        Check(vars.FloatVariables.Length == 3 && ReferenceEquals(vars.FloatVariables[0], vanilla), "Vanilla variables and references preserved");
        var wrapper = new ES3PlayMaker.PMDataWrapper(fsm, true, false);
        width.Value = 1; height.Value = 0; vanilla.Value = 0;
        wrapper.ApplyVariables(fsm, true, false);
        Check(Near(width.Value, 1.4f) && Near(height.Value, -.2f), "Game save wrapper round-trips custom width/height");
        Check(vanilla.Value == 12, "Vanilla values still round-trip");
        Check(scale.Value.x == 1 && scale.Value.y == 1 && scale.Value.z == 1, "Original model scale round-trips");
        width.Value = 1; height.Value = 0;
        var legacy = new ES3PlayMaker.PMDataWrapper(new Dictionary<string, object> { { "Liquid", 9f } }, new Dictionary<string, object[]>());
        legacy.ApplyVariables(fsm, true, false);
        Check(width.Value == 1 && height.Value == 0 && vanilla.Value == 9, "Legacy save without mod variables leaves stock defaults");
    }
    private static void Hooks(string dll)
    {
        var plugin = Assembly.LoadFrom(dll);
        Check(plugin.GetTypes().Length > 0, "Plugin types load with game/BepInEx assemblies only");
        Check(!plugin.GetReferencedAssemblies().Any(a => new[] { "Gunplay", "GunplayHUD", "NPCAI", "Apocaraider", "Apocasetter", "WomenOfWasteland" }.Contains(a.Name)), "No hard dependency on another mod");
        var managed = new[] { "Assembly-CSharp", "Assembly-CSharp-firstpass", "NWH.WheelController" }
            .Select(name => Assembly.LoadFrom(Path.Combine(game, "Apocalypter_Data/Managed", name + ".dll"))).ToArray();
        var targets = new[] {
            "HutongGames.PlayMaker.Actions.SendEvent:OnEnter",
            "HutongGames.PlayMaker.Actions.SetPosition:DoSetPosition",
            "ES3PlayMaker.SaveAll:Enter", "ES3PlayMaker.LoadAll:Enter",
            "ES3PlayMaker.PMDataWrapper:ApplyVariables",
            "NWH.WheelController3D.WheelController:OnEnable",
            "ES3Types.ES3Type_GameObject:GetChildren"
        };
        foreach (var target in targets)
        {
            var split = target.Split(':');
            var type = managed.Select(a => a.GetType(split[0])).FirstOrDefault(t => t != null);
            Check(type != null, "Hook type exists: " + split[0]);
            Check(type.GetMethod(split[1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static) != null, "Hook method exists: " + target);
        }
        var harmony = new Harmony("PartAdjustment.OfflineVerifier");
        try
        {
            harmony.PatchAll(plugin.GetType("PartAdjustment.LegacySuspensionSavePatch", true));
            Check(harmony.GetPatchedMethods().Count() == 1, "Legacy wrapper patch installs against actual game assembly");
        }
        finally { harmony.UnpatchSelf(); }
    }

    private static SetPosition PositionAction(Vector3 position) => new SetPosition
    {
        Enabled = true, space = Space.Self,
        vector = new FsmVector3 { UseVariable = true },
        x = new FsmFloat { Value = position.x }, y = new FsmFloat { Value = position.y }, z = new FsmFloat { Value = position.z }
    };

    private static bool Near(Vector3 a, Vector3 b) => Near(a.x, b.x) && Near(a.y, b.y) && Near(a.z, b.z);

    private static void VisibilityRegression()
    {
        var state = new ToolVisibilityState();
        Check(state.Changed(false, 0), "Initial tool visibility is synchronized");
        int updates = 0;
        for (int frame = 0; frame < 144; frame++) if (state.Changed(false, 0)) updates++;
        Check(updates == 0, "144 unchanged idle frames perform no registry visibility updates");
        Check(state.Changed(true, 0), "Tool activation updates all vehicles");
        updates = 0;
        for (int frame = 0; frame < 144; frame++) if (state.Changed(true, 0)) updates++;
        Check(updates == 0, "144 unchanged active frames perform no collider visibility updates");
        Check(state.Changed(true, 1), "Vehicle spawned while tool is active receives current visibility");
        Check(!state.Changed(true, 1), "New vehicle synchronization is performed once");
        Check(state.Changed(true, 2), "Replacement vehicle is detected even when vehicle count is unchanged");
        Check(state.Changed(false, 2), "Tool put away or mod disabled hides vehicles");
        Check(state.Changed(false, 3), "New vehicle spawned while tool is off is synchronized");
        Check(state.Changed(true, 3), "Reactivation after spawning restores visibility");
    }

    private static void BindingRegression()
    {
        var actions = Enumerable.Range(0, 5).Select(i => new KeyAction()).ToArray();
        var primary = new[] { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.DownArrow, KeyCode.UpArrow, KeyCode.Backspace };
        for (int i = 0; i < actions.Length; i++) actions[i].UpdateKey(primary[i], false);
        var text = SuspensionControls.HintFor(actions);
        Check(text == "LeftArrow / RightArrow - narrow / widen wheel spacing\nDownArrow / UpArrow - lower / raise suspension\nBackspace - reset suspension",
            "Cached hint preserves all five action labels");
        bool reused = true;
        for (int frame = 0; frame < 144; frame++) reused &= ReferenceEquals(text, SuspensionControls.HintFor(actions));
        Check(reused, "144 unchanged frames reuse the same hint string");
        actions[0].UpdateKey(KeyCode.A, true);
        Check(SuspensionControls.HintFor(actions).StartsWith("LeftArrow or A /"), "Alternate rebinding updates cached hint immediately");
        actions[0].UpdateKey(KeyCode.None, false);
        Check(SuspensionControls.HintFor(actions).StartsWith("A /"), "Alternate-only binding remains visible");
        actions[0].UpdateKey(KeyCode.None, true);
        Check(SuspensionControls.HintFor(actions).StartsWith("Unbound /"), "Unbinding invalidates cached hint");
        actions[0].UpdateKey(KeyCode.A, false); actions[0].UpdateKey(KeyCode.A, true);
        Check(SuspensionControls.HintFor(actions).StartsWith("A /"), "Duplicate primary/alternate keys are shown once");
        actions[0] = new KeyAction(); actions[0].UpdateKey(KeyCode.B, false);
        Check(SuspensionControls.HintFor(actions).StartsWith("B /"), "Reloaded input action objects update cached hint");
    }

    private static void SuspensionRegression()
    {
        // Measured from the installed game's 12 candidate prefabs on 2026-10-05.
        var excluded = new[] { "Rustliner", "Rustcargo", "Rustchief" };
        var fixtures = File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VehicleFixtures.csv")).Skip(1);
        int accepted = 0, rejected = 0;
        foreach (var line in fixtures)
        {
            var fields = line.Split(',');
            var name = fields[0];
            bool mountEnabled = bool.Parse(fields[3]);
            bool eligible = SuspensionSupport.Eligible(bool.Parse(fields[1]), bool.Parse(fields[2]), true, true,
                Enumerable.Repeat(mountEnabled, 4));
            Check(eligible == !excluded.Contains(name), name + " is classified by actual suspension support");
            if (eligible) accepted++; else rejected++;
            var values = fields.Skip(4).Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            foreach (int side in new[] { -1, 1 })
            foreach (bool front in new[] { true, false })
            {
                var factory = new Vector3(side * values[0], values[1], values[front ? 2 : 3]);
                var stale = new Vector3(side * values[4], values[5], values[front ? 6 : 7]);
                var action = PositionAction(stale);
                var chosen = SuspensionSupport.MountAction(new[] { action }, mountEnabled, a => true);
                Check(Near(SuspensionSupport.Position(chosen, factory), factory), name + " stock load preserves original mount");
                if (eligible) continue;
                foreach (float width in new[] { 1f, 1.4f, 1.5f })
                foreach (float height in new[] { 0f, -.2f, .5f })
                {
                    var corrupted = new Vector3(stale.x * width, stale.y + height, stale.z);
                    Check(LegacySuspensionRecovery.MatchesLegacy(corrupted, factory, stale, 0f, width, height),
                        name + " old saved displacement is recognized");
                    Check(!LegacySuspensionRecovery.MatchesLegacy(factory, factory, stale, 0f, width, height),
                        name + " restored/default mount is left alone on repeated loads");
                    Check(!LegacySuspensionRecovery.MatchesLegacy(new Vector3(factory.x, factory.y, factory.z + 10f),
                        factory, stale, 0f, width, height), name + " unrelated transform changes are left alone");
                }
            }
        }
        Check(accepted == 9 && rejected == 3, "All 12 inspected vehicle fixtures exercised");
        Check(!SuspensionSupport.Eligible(true, true, true, true, new[] { true, true }), "Two-wheel trailers excluded");
        Check(!SuspensionSupport.Eligible(true, true, true, true, new[] { true, false, true, true }), "One disabled mount excludes entire vehicle");
        Check(!SuspensionSupport.Eligible(true, true, false, true, Enumerable.Repeat(true, 4)), "Disabled attachment excludes vehicle");
        Check(!SuspensionSupport.Eligible(true, true, true, false, Enumerable.Repeat(true, 4)), "Disabled suspension check excludes vehicle");
        Check(!SuspensionSupport.Eligible(false, true, true, true, Enumerable.Repeat(true, 4)), "Hidden model excludes vehicle even with enabled mounts");
        Check(!SuspensionSupport.Eligible(true, false, true, true, Enumerable.Repeat(true, 4)), "Hidden hinge branch excludes vehicle");

        var valid = PositionAction(new Vector3(1, 2, 3));
        var prefabState = new FsmState((Fsm)null) { Actions = new FsmStateAction[] { valid } };
        var prefabFsm = new Fsm();
        Check(ReferenceEquals(SuspensionSupport.Actions(prefabFsm, prefabState)[0], valid) && prefabState.IsInitialized,
            "Dormant prefab state can be inspected without running the FSM");
        var disabled = PositionAction(new Vector3(4, 5, 6)); disabled.Enabled = false;
        var world = PositionAction(new Vector3(7, 8, 9)); world.space = Space.World;
        var wrongTarget = PositionAction(new Vector3(10, 11, 12));
        var actions = new[] { disabled, world, wrongTarget, valid };
        Check(ReferenceEquals(SuspensionSupport.MountAction(actions, true, a => !ReferenceEquals(a, wrongTarget)), valid),
            "Mount lookup skips disabled, world-space and other-object actions");
        Check(SuspensionSupport.MountAction(actions, false, a => true) == null, "Disabled FSM never supplies baseline");
        Check(SuspensionSupport.MountAction(new[] { valid }, true, a => false) == null, "Missing targeted mount action rejected");
        valid.x.UseVariable = true;
        Check(Near(SuspensionSupport.Position(valid, new Vector3(42, 0, 0)), new Vector3(42, 2, 3)), "None axis preserves original coordinate");

        float restoredWidth, restoredHeight;
        Check(!LegacySuspensionRecovery.TryOffsets(new Dictionary<string, object>(), out restoredWidth, out restoredHeight),
            "Vanilla saves cannot trigger recovery");
        var fsm = new Fsm();
        fsm.Variables.FloatVariables = new[] { new FsmFloat("Liquid") { Value = 12f } };
        var old = new ES3PlayMaker.PMDataWrapper(new Dictionary<string, object>
        {
            { "Liquid", 9f }, { SaveVariables.Width, 1.4f }, { SaveVariables.Height, -.2f },
            { SaveVariables.ModelPosition, new Vector3(0, 0, 0) }
        }, new Dictionary<string, object[]>());
        old.ApplyVariables(fsm, true, false);
        Check(LegacySuspensionRecovery.TryOffsets(old.objs, out restoredWidth, out restoredHeight)
            && Near(restoredWidth, 1.4f) && Near(restoredHeight, -.2f), "Old mod metadata detected even without injected variables");
        Check(fsm.Variables.GetAllNamedVariables().All(v => v.Name == "Liquid"), "Old unsupported save does not inject adjustment fields");
        SaveVariables.Float(fsm.Variables, SaveVariables.Width, 1f);
        SaveVariables.Float(fsm.Variables, SaveVariables.Height, 0f);
        SaveVariables.Vector(fsm.Variables, SaveVariables.ModelPosition, new Vector3());
        SaveVariables.Vector(fsm.Variables, SaveVariables.ModelScale, new Vector3(1, 1, 1));
        SaveVariables.Vector(fsm.Variables, SaveVariables.HingePosition, new Vector3());
        fsm.Variables.GetAllNamedVariables();
        SaveVariables.Remove(fsm.Variables);
        var clean = new ES3PlayMaker.PMDataWrapper(fsm, true, false);
        Check(clean.objs.Count == 1 && (float)clean.objs["Liquid"] == 9f, "Resaving removes obsolete mod metadata and preserves vanilla data");
        Check(!LegacySuspensionRecovery.TryOffsets(clean.objs, out restoredWidth, out restoredHeight), "Migrated save no longer requests recovery");
        SaveVariables.Remove(fsm.Variables);
        Check(fsm.Variables.GetAllNamedVariables().Length == 1, "Repeated metadata cleanup is harmless");
    }
}
