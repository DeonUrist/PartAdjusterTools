using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
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
        var managed = new[] { "Assembly-CSharp", "Assembly-CSharp-firstpass" }
            .Select(name => Assembly.LoadFrom(Path.Combine(game, "Apocalypter_Data/Managed", name + ".dll"))).ToArray();
        var targets = new[] {
            "HutongGames.PlayMaker.Actions.SendEvent:OnEnter",
            "HutongGames.PlayMaker.Actions.SetPosition:DoSetPosition",
            "ES3PlayMaker.SaveAll:Enter", "ES3PlayMaker.LoadAll:Enter",
            "ES3Types.ES3Type_GameObject:GetChildren"
        };
        foreach (var target in targets)
        {
            var split = target.Split(':');
            var type = managed.Select(a => a.GetType(split[0])).FirstOrDefault(t => t != null);
            Check(type != null, "Hook type exists: " + split[0]);
            Check(type.GetMethod(split[1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static) != null, "Hook method exists: " + target);
        }
    }
}
