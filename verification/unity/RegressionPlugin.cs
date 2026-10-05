using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using BepInEx;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using NWH.WheelController3D;
using PartAdjustment;
using UnityEngine;

[BepInPlugin("com.denis.partadjustment.probe", "Part Adjustment Native Verifier", "1.0.0")]
[BepInDependency(PartAdjustment.Plugin.Guid)]
public sealed class Probe : BaseUnityPlugin
{
    private string Report;
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    readonly List<string> lines = new List<string>(); private int checks;
    GameObject car;
    void Awake()
    {
        var argument = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("-partadjustment-regression-report=", StringComparison.Ordinal));
        if (argument == null) return;
        Report = Path.GetFullPath(argument.Substring(argument.IndexOf('=') + 1));
        var host = new GameObject("SuspensionProbe.Runner") { hideFlags = HideFlags.HideAndDontSave }; DontDestroyOnLoad(host); host.AddComponent<ProbeRunner>().Run = this.Run;
    }
    internal IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(2f);
        try { Exercise(); lines.Add("PASS: " + checks + " native Unity callback/transition checks."); }
        catch (Exception e) { lines.Add("ERROR: " + e); }
        File.WriteAllLines(Report, lines);
        if (car != null) Destroy(car);
        Application.Quit();
    }
    void Exercise()
    {
        car = new GameObject("TinyTyrant.SyntheticProbe"); car.SetActive(false); car.AddComponent<Rigidbody>().isKinematic = true;
        var model = Node(car.transform, "suspension_model", new Vector3(.0057373f, -.5764542f, .0036621f));
        var stock = Node(model, "stock", Vector3.zero); var lifted = Node(model, "buggy", Vector3.zero); lifted.gameObject.SetActive(false);
        Mesh(stock, "front_suspension", -.569492f, .5536215f, 1.0146116f); Mesh(stock, "rear_suspension", -.5684477f, .5525781f, -.8942732f);
        Mesh(lifted, "poloska_suspension_buggy_front", -.7673463f, .7673463f, 1.0081612f);
        Mesh(lifted, "poloska_suspension_buggy_rear", -.7538783f, .7546969f, -.8888416f);
        var hinge = Node(Node(car.transform, "hinge_suspension_parent", Vector3.zero), "hinge_suspension", Vector3.zero);
        Empty(hinge, "vehPart_Attach"); Empty(hinge, "checkSuspension");
        var wheels = new List<PlayMakerFSM>();
        foreach (var key in new[] { "FL", "FR", "RL", "RR" })
        {
            float sign = key.EndsWith("L") ? -1 : 1; bool front = key.StartsWith("F");
            var w = Node(car.transform, "hinge_wheel_" + key, new Vector3(sign * .6088f, -.425f, front ? 1.02f : -1.002f));
            w.gameObject.AddComponent<WheelController>().enabled = false;
            var f = w.gameObject.AddComponent<PlayMakerFSM>();
            var data = new Fsm { Name = "Suspension", StartState = "stock" };
            data.States = new[] { State(data, "stock", new Vector3(sign * .6088f, -.425f, front ? 1.02f : -1.002f)), State(data, "lifted", new Vector3(sign * .8f, front ? -.42f : -.455f, front ? 1.02f : -1.002f)) };
            f.Fsm = data; wheels.Add(f);
        }
        car.SetActive(true);
        foreach (var f in wheels) f.Fsm.SetState("stock");
        lines.Add("Scene valid=" + car.scene.IsValid() + " model=" + model.gameObject.activeSelf + " hinge=" + hinge.gameObject.activeSelf);
        foreach (var f in wheels)
        {
            lines.Add(f.gameObject.name + " fsm=" + f.FsmName + " enabled=" + f.enabled + " state=" + f.ActiveStateName);
            foreach (var s in f.FsmStates) { lines.Add(" state=" + s.Name + " actions=" + s.Actions.Length); foreach (var a in s.Actions) { var p = a as SetPosition; lines.Add(" action=" + a.GetType().Name + " enabled=" + a.Enabled + " target=" + (p == null ? "-" : f.Fsm.GetOwnerDefaultTarget(p.gameObject)?.name) + " space=" + (p == null ? "-" : p.space.ToString())); } }
        }
        foreach (var f in hinge.GetComponents<PlayMakerFSM>()) lines.Add("hinge FSM=" + f.FsmName + " enabled=" + f.enabled);
        var adjustment = (SuspensionAdjustment)typeof(SuspensionAdjustment).GetMethod("Ensure", Hidden).Invoke(null, new object[] { car.transform });
        if (adjustment == null) throw new Exception("Synthetic assembly not supported");
        Log("initial", adjustment, wheels); CheckMounts(adjustment, wheels, false, 1f);
        stock.gameObject.SetActive(false); lifted.gameObject.SetActive(true);
        foreach (var f in wheels) f.Fsm.SetState("lifted");
        Log("after native lift transition", adjustment, wheels); CheckMounts(adjustment, wheels, true, 1f);
        typeof(SuspensionAdjustment).GetMethod("Change", Hidden).Invoke(adjustment, new object[] { .025f, 0f, false });
        Log("after first width press", adjustment, wheels); CheckMounts(adjustment, wheels, true, 1.025f);
        typeof(SuspensionAdjustment).GetMethod("Change", Hidden).Invoke(adjustment, new object[] { .475f, 0f, false });
        CheckMounts(adjustment, wheels, true, 1.5f);
        stock.gameObject.SetActive(true); lifted.gameObject.SetActive(false);
        foreach (var f in wheels) f.Fsm.SetState("stock");
        CheckMounts(adjustment, wheels, false, 1.5f);
        typeof(SuspensionAdjustment).GetMethod("Change", Hidden).Invoke(adjustment, new object[] { 0f, 0f, true });
        CheckMounts(adjustment, wheels, false, 1f);
        stock.gameObject.SetActive(false); lifted.gameObject.SetActive(true);
        foreach (var f in wheels) f.Fsm.SetState("lifted");
        typeof(SuspensionAdjustment).GetMethod("Change", Hidden).Invoke(adjustment, new object[] { .025f, 0f, false });
        CheckMounts(adjustment, wheels, true, 1.025f);
        var variables = new FsmVariables();
        typeof(SuspensionAdjustment).GetMethod("WriteSaveVariables", Hidden).Invoke(adjustment, new object[] { variables });
        for (int load = 0; load < 3; load++)
        {
            foreach (var f in wheels) f.transform.localPosition = Vector3.zero;
            typeof(SuspensionAdjustment).GetMethod("ReadSaveVariables", Hidden).Invoke(adjustment, new object[] { variables });
            CheckMounts(adjustment, wheels, true, 1.025f);
        }
        var other = Node(car.transform, "other target", Vector3.zero);
        var action = new SetPosition { gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = new FsmGameObject { Value = other.gameObject } }, vector = new FsmVector3 { UseVariable = true }, x = new FsmFloat { Value = 99f }, y = new FsmFloat { Value = 0f }, z = new FsmFloat { Value = 0f }, space = Space.Self };
        var unrelated = new FsmState(wheels[0].Fsm) { Name = "other", Actions = new FsmStateAction[] { action } }; action.Init(unrelated); action.OnEnter();
        Check(other.localPosition.x == 99f, "Different target action executed");
        CheckMounts(adjustment, wheels, true, 1.025f);
        action.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.UseOwner }; action.space = Space.World; action.OnEnter();
        Check(wheels[0].transform.position.x == 99f, "World-space action executed");
        typeof(SuspensionAdjustment).GetMethod("Apply", Hidden).Invoke(adjustment, null);
        CheckMounts(adjustment, wheels, true, 1.025f);
        var method = typeof(SetPosition).GetMethod("DoSetPosition", Hidden);
        var patches = Harmony.GetPatchInfo(method); lines.Add("SetPosition patches: " + string.Join(",", patches == null ? new string[0] : patches.Owners.ToArray()));
    }
    void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    void CheckMounts(SuspensionAdjustment adjustment, List<PlayMakerFSM> wheels, bool lifted, float width)
    {
        var mounts = (IEnumerable)typeof(SuspensionAdjustment).GetField("mounts", Hidden).GetValue(adjustment);
        foreach (var mount in mounts)
        {
            var type = mount.GetType(); var t = (Transform)type.GetField("Transform", Hidden).GetValue(mount);
            bool left = t.name.EndsWith("L"), front = t.name.EndsWith("FL") || t.name.EndsWith("FR");
            float x = (left ? -1f : 1f) * (lifted ? .8f : .6088f);
            var baseline = new Vector3(x, lifted ? (front ? -.42f : -.455f) : -.425f, front ? 1.02f : -1.002f);
            var cached = (Vector3)type.GetField("Baseline", Hidden).GetValue(mount);
            Check((baseline - cached).sqrMagnitude < .00000001f, t.name + " updates factory baseline for " + (lifted ? "lifted" : "stock"));
            Check((bool)type.GetField("Lifted", Hidden).GetValue(mount) == lifted, t.name + " updates variant");
            float endpoint = lifted ? (front ? (left ? -.7673463f : .7673463f) : (left ? -.7538783f : .7546969f)) : (front ? (left ? -.569492f : .5536215f) : (left ? -.5684477f : .5525781f));
            var expected = baseline + Vector3.right * endpoint * (width - 1f);
            Check((expected - t.localPosition).sqrMagnitude < .00000001f, t.name + " follows matching endpoint at width " + width);
        }
    }
    void Log(string caption, SuspensionAdjustment adjustment, List<PlayMakerFSM> wheels)
    {
        lines.Add(caption);
        var mounts = (IEnumerable)typeof(SuspensionAdjustment).GetField("mounts", Hidden).GetValue(adjustment);
        foreach (var mount in mounts)
        {
            var type = mount.GetType(); var t = (Transform)type.GetField("Transform", Hidden).GetValue(mount);
            lines.Add(t.name + " current=" + t.localPosition.ToString("F6") + " cached=" + type.GetField("Baseline", Hidden).GetValue(mount) + " lifted=" + type.GetField("Lifted", Hidden).GetValue(mount) + " fsm=" + wheels.First(f => f.transform == t).ActiveStateName);
        }
    }
    static Transform Node(Transform parent, string name, Vector3 position) { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go.transform; }
    static void Mesh(Transform parent, string name, float left, float right, float z)
    {
        var node = Node(parent, name, Vector3.zero); var mesh = new Mesh(); mesh.vertices = new[] { new Vector3(left, -.1f, z - .1f), new Vector3(right, .1f, z + .1f), new Vector3(left, .1f, z + .1f) }; mesh.triangles = new[] { 0, 1, 2 }; mesh.RecalculateBounds();
        node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; node.gameObject.AddComponent<MeshRenderer>();
    }
    static void Empty(Transform owner, string name)
    {
        var f = owner.gameObject.AddComponent<PlayMakerFSM>(); var data = new Fsm { Name = name, StartState = "wait" }; data.States = new[] { new FsmState(data) { Name = "wait", Actions = new FsmStateAction[0] } }; f.Fsm = data;
    }
    static FsmState State(Fsm data, string name, Vector3 position)
    {
        var state = new FsmState(data) { Name = name }; state.Actions = new FsmStateAction[] { new SetPosition { gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.UseOwner }, vector = new FsmVector3 { UseVariable = true }, x = new FsmFloat { Value = position.x }, y = new FsmFloat { Value = position.y }, z = new FsmFloat { Value = position.z }, space = Space.Self } }; state.SaveActions(); return state;
    }
}
public sealed class ProbeRunner : MonoBehaviour
{
    internal Func<IEnumerator> Run;
    IEnumerator Start() { return Run(); }
}
