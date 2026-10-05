using System;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using InsaneSystems.InputManager;
using PartAdjustment;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    private static void Near(Vector3 a, Vector3 b, string message) => Check((a-b).sqrMagnitude < .000001f, message + " " + a + " versus " + b);
    private static readonly MethodInfo update = typeof(AdjustmentRunner).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo rotate = typeof(HeldRotationPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo filter = typeof(SecondaryInputPatch).GetMethod("Filter", BindingFlags.Static | BindingFlags.NonPublic);
    private static GameObject car, item, camera;
    private static AdjustmentRunner runner;
    private static void Frame(params KeyCode[] keys)
    {
        Time.frameCount++; Input.Down.Clear(); foreach (var key in keys) Input.Down.Add(key);
        update.Invoke(runner, null); AdjustmentRunner.Apply();
    }
    private static PlayMakerFSM Fsm(GameObject go, string name) { var f = go.AddComponent<PlayMakerFSM>(); f.FsmName = name; return f; }
    private static void Setup()
    {
        var plugin = new GameObject("plugin host").AddComponent<Plugin>();
        typeof(Plugin).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(plugin, null);
        string[] names = { Controls.Secondary, Controls.Use, Controls.Left, Controls.Right, Controls.Up, Controls.Down, Controls.Forward, Controls.Backward, Controls.Mode, Controls.Reset };
        KeyCode[] keys = { KeyCode.E, KeyCode.F, KeyCode.Keypad4, KeyCode.Keypad6, KeyCode.Keypad8, KeyCode.Keypad2, KeyCode.Keypad7, KeyCode.Keypad1, KeyCode.Keypad5, KeyCode.Keypad0 };
        for (int i=0; i<names.Length; i++) InputController.Actions[names[i]] = new KeyAction { Key = keys[i] };
        car = new GameObject("car"); item = new GameObject("crate") { tag = "vehPart" }; item.transform.parent = car.transform;
        Fsm(item, "Attach"); Fsm(item, "de_Attach");
        camera = new GameObject("PlayerCamera"); camera.AddComponent<Camera>(); Fsm(camera, "DriveUse"); Fsm(camera, "GrabItem"); Fsm(camera, "UseAdjustTool");
        FsmVariables.GlobalVariables.Objects["PlayerCamera"] = new FsmGameObject { Value = camera };
        runner = new GameObject("runner").AddComponent<AdjustmentRunner>();
        Physics.Hit = item.transform;
    }
    private static void PluginLifetime()
    {
        var host = new GameObject("BepInEx host");
        var plugin = host.AddComponent<Plugin>();
        var field = typeof(Plugin).GetField("runner", BindingFlags.Static | BindingFlags.NonPublic);
        var persistent = (GameObject)field.GetValue(null);
        Check(persistent.Persistent && persistent.activeInHierarchy, "Runner has independent persistent lifetime");
        host.activeSelf = false;
        Check(persistent.activeInHierarchy && HarmonyLib.Harmony.Installed, "Destroying plugin host retains runner and hooks");
        Frame(KeyCode.E);
        Check(AdjustmentRunner.Session != null, "E still selects after plugin host destruction");
        UnityEngine.SceneManagement.SceneManager.Load();
        Check(AdjustmentRunner.Session == null && ReferenceEquals(persistent, field.GetValue(null)), "Scene change clears selection and reuses surviving runner");
        persistent.activeSelf = false;
        UnityEngine.SceneManagement.SceneManager.Load();
        var replacement = (GameObject)field.GetValue(null);
        Check(replacement.Persistent && replacement.activeInHierarchy && !ReferenceEquals(persistent,replacement), "Scene callback recreates lost or inactive runner");
    }
    private static void Gauges()
    {
        var dashboard = new GameObject("Dashboard"); dashboard.transform.parent = car.transform;
        var gauge = new GameObject("gauge_speedo") {tag = "vehPart"}; gauge.transform.parent = dashboard.transform;
        Fsm(gauge,"ID").FsmVariables.Strings["ID"] = new FsmString {Value = "gauge"}; Fsm(gauge,"de_Attach");
        var wrench = Fsm(camera,"UseTool"); wrench.enabled = false;
        var tool = AdjustmentRunner.Find(camera,"UseAdjustTool"); tool.enabled = false;
        Physics.Hit = gauge.transform; Frame(KeyCode.E);
        Check(AdjustmentRunner.Hover == null && AdjustmentRunner.Session == null, "Gauges require adjustment tool");
        wrench.enabled = true; Frame();
        Check(AdjustmentRunner.Hover == null, "Wrench alone cannot adjust gauges");
        wrench.enabled = false; tool.enabled = true; Frame();
        Check(AdjustmentRunner.Hover == gauge.transform, "Adjustment tool makes mounted gauge eligible without wrench");
        Frame(KeyCode.E);
        Check(AdjustmentRunner.Session?.Target == gauge.transform, "E begins mounted gauge adjustment without Attach FSM");
        var before = gauge.transform.position; Frame(KeyCode.Keypad6);
        Near(gauge.transform.position-before,Vector3.right*.01f,"Gauges move without detaching");
        Frame(KeyCode.E); Check(AdjustmentRunner.Session == null && gauge.transform.parent == dashboard.transform, "E finishes gauge adjustment with dashboard parent intact");
        gauge.tag = "vehPartRemoved"; Frame(); Check(AdjustmentRunner.Hover == null, "Loose gauges stay excluded with adjustment tool");
        wrench.enabled = false; Physics.Hit = item.transform;
    }
    private static void Session()
    {
        Frame(KeyCode.E);
        Check(AdjustmentRunner.Session != null && !AdjustmentRunner.Session.Rotating, "E begins in movement mode");
        Frame(KeyCode.Keypad6); Near(item.transform.position,new Vector3(.01f,0f,0f),"Normal movement step");
        Frame(KeyCode.Keypad4); Frame(KeyCode.Keypad8); Frame(KeyCode.Keypad2); Frame(KeyCode.Keypad7); Frame(KeyCode.Keypad1);
        Near(item.transform.position, Vector3.zero, "All six movement directions balance");
        Input.Held.Add(KeyCode.LeftAlt); Frame(KeyCode.Keypad6); Input.Held.Clear();
        Near(item.transform.position,new Vector3(.1f,0f,0f),"Left Alt uses the independent fast movement step");
        Frame(KeyCode.Keypad0); Near(item.transform.position,Vector3.zero,"Reset returns to entry position");
        camera.transform.rotation = Quaternion.AngleAxis(90f,Vector3.up) * Quaternion.AngleAxis(60f,Vector3.right);
        Frame(KeyCode.Keypad7); Near(item.transform.position, camera.transform.forward * .01f,"Camera movement includes pitch and yaw");
        Frame(KeyCode.Keypad0); Plugin.ReferenceFrame.Value = 1; Frame(KeyCode.Keypad7);
        Near(item.transform.position,Vector3.forward * .01f,"World movement ignores camera pitch and yaw");
        Frame(KeyCode.Keypad0); Plugin.ReferenceFrame.Value = 0;
        Frame(KeyCode.Keypad5); Check(AdjustmentRunner.Session.Rotating,"Mode key toggles rotation");
        Input.Held.Add(KeyCode.LeftAlt); Frame(KeyCode.Keypad8); Input.Held.Clear();
        var expected=Quaternion.AngleAxis(10f,camera.transform.rotation * Vector3.right);
        Near(item.transform.rotation * Vector3.forward,expected * Vector3.forward,"Fast rotation uses camera right axis");
        Frame(KeyCode.Keypad0); Near(item.transform.rotation * Vector3.forward,Vector3.forward,"Reset also restores rotation");
        var saved=AdjustmentRunner.Session;
        Physics.Hit=null; Frame(KeyCode.Keypad6);
        Check(ReferenceEquals(saved,AdjustmentRunner.Session),"Looking away preserves selection");
        Frame(KeyCode.E); Check(AdjustmentRunner.Session==null,"E again stops even when looking away");
        var pos=item.transform.localPosition;Frame(KeyCode.Keypad6);Near(item.transform.localPosition,pos,"Stopped session ignores Adjust keys");
        Check(item.transform.parent==car.transform && item.tag=="vehPart","Begin/change/stop never detach or change tag");
        Physics.Hit=item.transform;Frame(KeyCode.E);Frame(KeyCode.F);
        Check(AdjustmentRunner.Session==null,"F ends session before native detach");
    }
    private static void Lifecycle()
    {
        var collider=new GameObject("collider");collider.transform.parent=item.transform;
        Check(AdjustmentRunner.FreeAttachment(collider.transform)==item.transform,"Child collider resolves free attachment root");
        var socket=new GameObject("engine") {tag="vehPart"};socket.transform.parent=car.transform;Fsm(socket,"de_Attach");
        Check(AdjustmentRunner.FreeAttachment(socket.transform)==null,"Socket parts excluded");
        item.tag="vehPartRemoved";Check(AdjustmentRunner.FreeAttachment(item.transform)==null,"Loose items excluded");item.tag="vehPart";
        Physics.Hit=item.transform;Frame(KeyCode.E);var start=item.transform.localPosition;
        Time.timeScale=0;Frame(KeyCode.Keypad6);Near(item.transform.localPosition,start,"No adjustment while paused");Check(AdjustmentRunner.Session!=null,"Pause preserves session");Time.timeScale=1;
        Application.isFocused=false;Frame(KeyCode.E);Check(AdjustmentRunner.Session!=null,"Alt-tab ignores toggle");Application.isFocused=true;
        Cursor.lockState=CursorLockMode.None;Frame(KeyCode.Keypad6);Near(item.transform.localPosition,start,"Unlocked cursor blocks input");Cursor.lockState=CursorLockMode.Locked;
        Frame();Near(item.transform.localPosition,start,"No buffered movement on resume");
        item.transform.parent=new GameObject("other parent").transform;Frame();Check(AdjustmentRunner.Session==null,"Reparenting invalidates session without restoring stale pose");item.transform.parent=car.transform;
        Frame(KeyCode.E);item.tag="vehPartRemoved";Frame();Check(AdjustmentRunner.Session==null,"Native detach invalidates session");item.tag="vehPart";
        Frame(KeyCode.E);Plugin.Enabled.Value=false;Frame();Check(AdjustmentRunner.Session==null,"Disabling clears session");Plugin.Enabled.Value=true;
        Frame(KeyCode.E);AdjustmentRunner.Find(camera,"DriveUse").ActiveStateName="inCar";Frame();Check(AdjustmentRunner.Session==null,"Entering vehicle clears selection");AdjustmentRunner.Find(camera,"DriveUse").ActiveStateName="Idle";
        Frame(KeyCode.E);camera.activeSelf=false;Frame();Check(AdjustmentRunner.Session==null,"Scene camera deactivation clears selection");camera.activeSelf=true;
        car.transform.localScale=new Vector3(2f,3f,4f);car.transform.rotation=Quaternion.AngleAxis(35f,Vector3.up);car.transform.position=new Vector3(10f,20f,30f);
        Frame(KeyCode.E);var before=item.transform.position;Frame(KeyCode.Keypad7);
        Near(item.transform.position-before,camera.transform.forward*.01f,"Metre steps stay exact under scaled rotated vehicle parent");
        var local=item.transform.localPosition;Frame(KeyCode.E);car.transform.position+=new Vector3(3f,4f,5f);Near(item.transform.localPosition,local,"Finished placement follows vehicle in local coordinates");
    }
    private static void Rebinding()
    {
        camera.transform.rotation=Quaternion.identity;Physics.Hit=item.transform;
        InputController.Actions[Controls.Secondary].Key=KeyCode.G;InputController.Actions[Controls.Secondary].AlternativeKey=KeyCode.R;
        Frame(KeyCode.E);Check(AdjustmentRunner.Session==null,"Old secondary binding stops toggling");Frame(KeyCode.R);Check(AdjustmentRunner.Session!=null,"Alternate secondary binding works");
        InputController.Actions[Controls.Right].Key=KeyCode.Home;InputController.Actions[Controls.Right].AlternativeKey=KeyCode.Keypad6;
        var before=item.transform.position;Frame(KeyCode.Home);Near(item.transform.position-before,Vector3.right*.01f,"Rebound Adjust key works");
        Check(Controls.Keys(Controls.Secondary)=="G / R", "Hint uses current primary and alternate bindings");
        var label=Controls.Keys(Controls.Secondary);bool reused=true;
        for(int frame=0;frame<144;frame++) reused &= ReferenceEquals(label,Controls.Keys(Controls.Secondary));
        Check(reused,"144 unchanged frames reuse item hint binding strings");
        Frame(KeyCode.R);Check(AdjustmentRunner.Session==null,"Alternate secondary also stops");
        Plugin.MovementStep.Value=float.NaN;Check(Controls.Step(false,false)==.01f,"Invalid movement step has finite fallback");Plugin.MovementStep.Value=.01f;
        Plugin.FastRotationAngle.Value=float.PositiveInfinity;Check(Controls.Step(true,true)==1f,"Invalid fast rotation has finite fallback");Plugin.FastRotationAngle.Value=10f;
    }
    private static void ToolGating()
    {
        Plugin.ReferenceFrame.Value=0;Physics.Hit=item.transform;
        var tool=AdjustmentRunner.Find(camera,"UseAdjustTool");
        tool.enabled=false;Frame(KeyCode.G);
        Check(AdjustmentRunner.Hover==null && AdjustmentRunner.Session==null,"Free items cannot be selected without the adjustment tool");
        Check(AdjustmentRunner.FreeAttachment(item.transform)==null,"Direct eligibility lookup also requires the tool");
        tool.enabled=true;Frame(KeyCode.G);
        Check(AdjustmentRunner.Session!=null,"Tool reactivation permits free item selection");
        var before=item.transform.position;tool.enabled=false;Frame(KeyCode.Home);
        Near(item.transform.position,before,"Putting away tool prevents item movement");
        Check(AdjustmentRunner.Session==null,"Putting away tool ends item session");
        tool.enabled=true;Frame(KeyCode.G);Time.timeScale=0;tool.enabled=false;Frame();
        Check(AdjustmentRunner.Session==null,"Unequipping while paused also clears session");Time.timeScale=1;
        tool.enabled=true;Frame(KeyCode.G);
        Time.frameCount++;Input.Down.Clear();Input.Down.Add(KeyCode.Home);update.Invoke(runner,null);
        before=item.transform.position;tool.enabled=false;AdjustmentRunner.Apply();
        Near(item.transform.position,before,"Late application rechecks tool after earlier input collection");
        Check(AdjustmentRunner.Session==null,"Mid-frame tool switching clears queued movement");
        tool.enabled=true;tool.ActiveStateName="adjust";Frame(KeyCode.G);
        Check(AdjustmentRunner.Session==null && AdjustmentRunner.Hover==null,"Native suspension/engine adjustment owns its input exclusively");
        tool.ActiveStateName="Idle";Frame(KeyCode.G);tool.ActiveStateName="adjust";Frame();
        Check(AdjustmentRunner.Session==null,"Beginning native adjustment ends attached-item adjustment");tool.ActiveStateName="Idle";
        Check(Plugin.ModifierKey.Value==KeyCode.LeftAlt && Plugin.ModifierAlternative.Value==KeyCode.None,"Fast modifier defaults to Left Alt only");
        Input.Held.Add(KeyCode.LeftShift);Check(!Controls.Fast,"Shift no longer changes adjustment speed");Input.Held.Clear();
        Input.Held.Add(KeyCode.LeftAlt);Check(Controls.Fast,"Left Alt activates fast steps");Input.Held.Clear();
        Plugin.ModifierAlternative.Value=KeyCode.R;Input.Held.Add(KeyCode.R);Check(Controls.Fast,"Configured alternate modifier still works");Input.Held.Clear();Plugin.ModifierAlternative.Value=KeyCode.None;
    }
    private static void Mmb()
    {
        camera.transform.rotation=Quaternion.AngleAxis(37f,Vector3.up)*Quaternion.AngleAxis(-52f,Vector3.right)*Quaternion.AngleAxis(21f,Vector3.forward);
        var held=new GameObject("held");held.transform.parent=camera.transform;
        var fsm=new Fsm {Name="GrabItem",Owner=camera};fsm.Variables.Floats["mouse_x"]=new FsmFloat {Value=23f};fsm.Variables.Floats["mouse_y"]=new FsmFloat {Value=17f};fsm.Variables.Vectors["itemRot"]=new FsmVector3();
        var action=new Rotate {Fsm=fsm,State=new FsmState {Name="Rotate"},vector=new FsmVector3 {Name="transformX"},gameObject=new FsmOwnerDefault {GameObject=held}};
        Plugin.ReferenceFrame.Value=0;var before=held.transform.rotation;
        Check(!(bool)rotate.Invoke(null,new object[]{action}),"Only native MMB rotation is replaced");
        Near(held.transform.rotation*Vector3.forward,Quaternion.AngleAxis(23f,camera.transform.rotation*Vector3.right)*before*Vector3.forward,"MMB pitch is exact angle-axis at oblique camera orientation");
        Near(fsm.Variables.Vectors["itemRot"].Value,held.transform.localEulerAngles,"MMB captures final local pose before release");
        before=held.transform.rotation;action.vector.Name="transformY";rotate.Invoke(null,new object[]{action});
        Near(held.transform.rotation*Vector3.forward,Quaternion.AngleAxis(-17f,camera.transform.rotation*Vector3.up)*before*Vector3.forward,"MMB yaw follows camera up with vanilla mouse sign");
        Plugin.ReferenceFrame.Value=1;before=held.transform.rotation;rotate.Invoke(null,new object[]{action});Near(held.transform.rotation*Vector3.forward,Quaternion.AngleAxis(-17f,Vector3.up)*before*Vector3.forward,"World setting also affects MMB");
        fsm.Name="unrelated";Check((bool)rotate.Invoke(null,new object[]{action}),"Other Rotate actions remain native");fsm.Name="GrabItem";
        Time.timeScale=0;Check((bool)rotate.Invoke(null,new object[]{action}),"MMB override inactive when paused");Time.timeScale=1;
        var tool=AdjustmentRunner.Find(camera,"UseAdjustTool");tool.enabled=false;before=held.transform.rotation;
        Check((bool)rotate.Invoke(null,new object[]{action}),"Held rotation remains native with tool put away");
        Near(held.transform.rotation*Vector3.forward,before*Vector3.forward,"Unselected tool hook leaves held pose untouched");tool.enabled=true;
    }
    private static void Suppression()
    {
        Plugin.ReferenceFrame.Value=0;Physics.Hit=item.transform;Frame(KeyCode.G);
        object[] arguments={Controls.Secondary,true};Check(!(bool)filter.Invoke(null,arguments)&&!(bool)arguments[1],"Native E action blocked during selection");
        arguments=new object[]{Controls.Use,true};Check((bool)filter.Invoke(null,arguments),"Primary Use stays native");
        Frame(KeyCode.G);arguments=new object[]{Controls.Secondary,true};Check(!(bool)filter.Invoke(null,arguments),"Exit frame consumes E too");
        Frame();arguments=new object[]{Controls.Secondary,true};Check((bool)filter.Invoke(null,arguments),"Native secondary input resumes after exit");
        Frame(KeyCode.G);var tool=AdjustmentRunner.Find(camera,"UseAdjustTool");tool.enabled=false;
        arguments=new object[]{Controls.Secondary,true};Check((bool)filter.Invoke(null,arguments),"No native input is suppressed after mid-frame tool unequip");
        Frame();tool.enabled=true;
    }
    private static int Main()
    {
        try { Setup();PluginLifetime();Gauges();Session();Lifecycle();Rebinding();ToolGating();Mmb();Suppression();Hints();SuspensionWidthChecks.Run(Check);Console.WriteLine("PASS: "+checks+" headless checks of production code. Game not executed.");return 0; }
        catch(Exception e) { Console.Error.WriteLine(e);return 1; }
    }
    private static void Hints()
    {
        var use=new GameObject("ItemUse").AddComponent<UnityEngine.UI.Text>();use.text="Detach: F";
        FsmVariables.GlobalVariables.Objects["UI_ItemUse"]=new FsmGameObject {Value=use.gameObject};
        var native=new GameObject("AdjustUI");native.AddComponent<UnityEngine.UI.Text>();
        foreach(var name in new[]{"mode","change mode","buttons","reset","PartAdjustment.SuspensionHint"})
        { var child=new GameObject(name);child.transform.parent=native.transform;child.AddComponent<UnityEngine.UI.Text>(); }
        var hint=new AdjustmentHint();Frame();hint.Refresh();
        Check(use.text=="Detach: F\nAdjust: G / R","Item hint preserves native text and uses rebound keys");
        Check(use.verticalOverflow==UnityEngine.UI.VerticalWrapMode.Overflow,"Second hint line is not clipped by native one-line rectangle");
        var writes=use.Writes;for(int frame=0;frame<144;frame++) hint.Refresh();
        Check(use.Writes==writes,"Unchanged hover hint does not write text every frame");
        use.text="Buy: F";hint.Refresh();Check(use.text=="Buy: F\nAdjust: G / R","Native hint rewrite is preserved and suffix appended once");
        InputController.Actions[Controls.Secondary].Key=KeyCode.E;InputController.Actions[Controls.Secondary].AlternativeKey=KeyCode.None;hint.Refresh();
        Check(use.text=="Buy: F\nAdjust: E","Rebinding replaces old suffix without losing native text");
        Frame(KeyCode.E);hint.Refresh();
        Check(use.text=="Buy: F\nStop adjusting: E","Active session shows finish hint");
        var panel=GameObject.All.Find(g=>g.name=="PartAdjustment.ItemControls");
        Check(panel!=null && panel.activeSelf,"Item session creates its own control panel");
        var resetText=panel.transform.Find("reset").gameObject.GetComponent<UnityEngine.UI.Text>();
        Check(resetText.text.Contains("LeftAlt - faster movement"),"Panel shows the Left Alt modifier");
        var clonedSuspension=panel.transform.Find("PartAdjustment.SuspensionHint");
        Check(clonedSuspension!=null && !clonedSuspension.gameObject.activeSelf,"Cloned suspension-specific label is removed");
        Frame(KeyCode.Keypad5);hint.Refresh();Check(resetText.text.Contains("faster rotation"),"Panel updates after switching rotation mode");
        writes=resetText.Writes;for(int frame=0;frame<144;frame++) hint.Refresh();
        Check(resetText.Writes==writes,"Unchanged session panel does not rewrite labels every frame");
        var tool=AdjustmentRunner.Find(camera,"UseAdjustTool");tool.enabled=false;hint.Refresh();
        Check(use.text=="Buy: F" && use.verticalOverflow==UnityEngine.UI.VerticalWrapMode.Truncate,"Unequipping restores native hint text and overflow");
        Check(!panel.activeSelf,"Unequipping hides control panel immediately");
        Frame();tool.enabled=true;Frame();hint.Refresh();use.text="Native replacement";hint.Dispose();
        Check(use.text=="Native replacement","Cleanup preserves a newer native hint");
        Check(!panel.activeSelf,"Cleanup removes generated control panel");
    }
}
