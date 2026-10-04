// Headless adapter for production session/input code. Native game physics, UI and Harmony
// dispatch are not simulated or certified by these checks.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NVector = System.Numerics.Vector3;
using NQuat = System.Numerics.Quaternion;

namespace UnityEngine
{
    public class Object
    {
        public static void DontDestroyOnLoad(Object value) { ((GameObject)value).Persistent = true; }
        public static void Destroy(Object value) { if (value is GameObject go) go.activeSelf = false; }
        public static GameObject Instantiate(GameObject original, Transform parent, bool worldPositionStays)
        {
            var copy=new GameObject(original.name);copy.transform.parent=parent;
            var text=original.GetComponent<UI.Text>();
            if(text!=null) { var target=copy.AddComponent<UI.Text>();target.text=text.text;target.verticalOverflow=text.verticalOverflow; }
            foreach(var fsm in original.GetComponents<PlayMakerFSM>()) { var target=copy.AddComponent<PlayMakerFSM>();target.FsmName=fsm.FsmName; }
            foreach(var child in GameObject.All.Where(g=>g.transform.parent==original.transform).ToArray()) Instantiate(child,copy.transform,false);
            return copy;
        }
    }
    public enum HideFlags { HideAndDontSave }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public string name => gameObject.name;
        public T GetComponent<T>() where T:Component => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component { }
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int value) { } }
    public enum KeyCode { None, E, F, G, R, LeftAlt, LeftShift, RightShift, Keypad0, Keypad1, Keypad2, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Home }
    public enum CursorLockMode { None, Locked }
    public static class Cursor { public static CursorLockMode lockState = CursorLockMode.Locked; }
    public static class Application { public static bool isFocused = true; }
    public static class Time { public static float timeScale = 1f, deltaTime = 0.016f; public static int frameCount; public static float unscaledTime => frameCount * .016f; }
    public static class Input
    {
        public static readonly HashSet<KeyCode> Down = new HashSet<KeyCode>(), Held = new HashSet<KeyCode>();
        public static bool GetKeyDown(KeyCode key) => key != KeyCode.None && Down.Contains(key);
        public static bool GetKey(KeyCode key) => key != KeyCode.None && Held.Contains(key);
    }
    public enum QueryTriggerInteraction { Ignore }
    public struct RaycastHit { public Transform transform; public Collider collider; public float distance; }
    public sealed class Collider : Component { }
    public static class Physics
    {
        public static Transform Hit;
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance, int mask, QueryTriggerInteraction triggers)
        { hit = new RaycastHit { transform = Hit }; return Hit != null; }
    }
    public sealed class GameObject : Object
    {
        internal static readonly List<GameObject> All=new List<GameObject>();
        public SceneManagement.Scene scene => new SceneManagement.Scene();
        public string name, tag = "Untagged";
        public HideFlags hideFlags;
        public bool Persistent;
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public readonly Transform transform;
        private readonly List<Component> components = new List<Component>();
        public GameObject(string value) { name = value; transform = new Transform { gameObject = this }; components.Add(transform); All.Add(this); }
        public T AddComponent<T>() where T : Component, new() { var result = new T { gameObject = this }; components.Add(result); return result; }
        public T GetComponent<T>() where T : Component => components.OfType<T>().FirstOrDefault();
        public T[] GetComponents<T>() where T : Component => components.OfType<T>().ToArray();
        public bool CompareTag(string value) => tag == value;
        public void SetActive(bool value) { activeSelf=value; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T:Component => All.Where(g=>Below(g.transform,transform) && (includeInactive || g.activeInHierarchy)).SelectMany(g=>g.GetComponents<T>()).ToArray();
        private static bool Below(Transform node,Transform root) { for(var t=node;t!=null;t=t.parent) if(t==root) return true;return false; }
    }
    public class Transform : Component
    {
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = new Vector3(1f, 1f, 1f);
        public Vector3 position { get => parent == null ? localPosition : parent.TransformPoint(localPosition); set => localPosition = parent == null ? value : parent.InverseTransformPoint(value); }
        public Quaternion rotation { get => parent == null ? localRotation : parent.rotation * localRotation; set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value; }
        public Vector3 localEulerAngles => localRotation.EulerAngles;
        public Vector3 forward => rotation * Vector3.forward;
        public bool CompareTag(string value) => gameObject.CompareTag(value);
        public Transform Find(string value) => GameObject.All.FirstOrDefault(g=>g.transform.parent==this && g.name==value)?.transform;
        public Vector3 TransformPoint(Vector3 value) => position + rotation * (value * localScale);
        public Vector3 InverseTransformPoint(Vector3 value) => (Quaternion.Inverse(rotation) * (value - position)) / localScale;
    }
    public sealed class RectTransform : Transform { public Vector2 anchorMin,anchorMax,pivot,anchoredPosition,sizeDelta; }
    public struct Vector2 { public float x,y;public Vector2(float a,float b) { x=a;y=b; } }
    public static class Resources { public static T[] FindObjectsOfTypeAll<T>() where T:Component => GameObject.All.SelectMany(g=>g.GetComponents<T>()).ToArray(); }
    public sealed class Camera : Component { public bool enabled = true; public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy; }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
        internal NVector N => new NVector(x, y, z);
        internal static Vector3 From(NVector v) => new Vector3(v.X, v.Y, v.Z);
        public float sqrMagnitude => x*x + y*y + z*z;
        public static Vector3 zero => new Vector3();
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 operator +(Vector3 a, Vector3 b) => From(a.N + b.N);
        public static Vector3 operator -(Vector3 a, Vector3 b) => From(a.N - b.N);
        public static Vector3 operator *(Vector3 a, float b) => From(a.N * b);
        public static Vector3 operator *(Vector3 a, Vector3 b) => From(a.N * b.N);
        public static Vector3 operator /(Vector3 a, Vector3 b) => From(a.N / b.N);
        public override string ToString() => N.ToString();
    }
    public struct Quaternion
    {
        internal NQuat N;
        private Quaternion(NQuat value) { N = value; }
        public static Quaternion identity => new Quaternion(NQuat.Identity);
        public static Quaternion AngleAxis(float degrees, Vector3 axis) => new Quaternion(NQuat.CreateFromAxisAngle(NVector.Normalize(axis.N), degrees * (float)Math.PI / 180f));
        public static Quaternion Inverse(Quaternion q) => new Quaternion(NQuat.Inverse(q.N));
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(a.N * b.N);
        public static Vector3 operator *(Quaternion q, Vector3 v) => Vector3.From(NVector.Transform(v.N, q.N));
        // Only used for checking that the MMB hook captures a changed pose immediately.
        public Vector3 EulerAngles => new Vector3(N.X, N.Y, N.Z);
    }
}
namespace HutongGames.PlayMaker
{
    using UnityEngine;
    public class ES3NonSerializable : Attribute { }
    public class FsmGameObject { public GameObject Value; }
    public class FsmFloat { public float Value; }
    public class FsmString { public string Value; }
    public class FsmVector3 { public string Name; public Vector3 Value; }
    public class FsmVariables
    {
        public static readonly FsmVariables GlobalVariables = new FsmVariables();
        public readonly Dictionary<string,FsmGameObject> Objects = new Dictionary<string,FsmGameObject>();
        public readonly Dictionary<string,FsmFloat> Floats = new Dictionary<string,FsmFloat>();
        public readonly Dictionary<string,FsmVector3> Vectors = new Dictionary<string,FsmVector3>();
        public readonly Dictionary<string,FsmString> Strings = new Dictionary<string,FsmString>();
        public FsmGameObject GetFsmGameObject(string key) => Objects.GetValueOrDefault(key);
        public FsmFloat GetFsmFloat(string key) => Floats.GetValueOrDefault(key);
        public FsmVector3 GetFsmVector3(string key) => Vectors.GetValueOrDefault(key);
        public FsmString GetFsmString(string key) => Strings.GetValueOrDefault(key);
    }
    public static class ActionHelpers
    {
        public static RaycastHit MousePick(float distance, int mask)
        {
            var collider = Physics.Hit == null ? null : Physics.Hit.gameObject.GetComponent<Collider>();
            if (Physics.Hit != null && collider == null) collider = Physics.Hit.gameObject.AddComponent<Collider>();
            return new RaycastHit { transform = Physics.Hit, collider = collider };
        }
    }
    public class FsmOwnerDefault { public GameObject GameObject; }
    public class FsmState { public string Name; }
    public class Fsm
    {
        public string Name;
        public GameObject Owner;
        public readonly FsmVariables Variables = new FsmVariables();
        public GameObject GetOwnerDefaultTarget(FsmOwnerDefault target) => target.GameObject;
    }
}
public sealed class PlayMakerFSM : UnityEngine.Component
{
    public string FsmName;
    public string ActiveStateName = "Idle";
    public bool enabled = true;
    public readonly HutongGames.PlayMaker.FsmVariables FsmVariables = new HutongGames.PlayMaker.FsmVariables();
    public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
}
namespace InsaneSystems.InputManager
{
    using UnityEngine;
    public sealed class KeyAction
    {
        public KeyCode Key, AlternativeKey;
        public bool IsDown() => Input.GetKeyDown(Key) || Input.GetKeyDown(AlternativeKey);
    }
    public static class InputController
    {
        public static readonly Dictionary<string,KeyAction> Actions = new Dictionary<string,KeyAction>();
        public static KeyAction GetKeyAction(string name) => Actions[name];
        public static bool GetKeyActionIsDown(string name) => GetKeyAction(name).IsDown();
        public static bool GetKeyActionIsActive(string name) => false;
        public static bool GetKeyActionIsUp(string name) => false;
    }
}
namespace HutongGames.PlayMaker.Actions
{
    public sealed class Rotate
    {
        public Fsm Fsm;
        public FsmState State;
        public FsmVector3 vector;
        public FsmOwnerDefault gameObject;
        public bool perSecond;
    }
}
namespace HarmonyLib
{
    public sealed class Harmony
    {
        public static bool Installed;
        public Harmony(string id) { }
        public void PatchAll(Assembly assembly) { Installed = true; }
        public void UnpatchSelf() { Installed = false; }
    }
    public class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type value) { }
        public HarmonyPatch(Type value, string name) { }
        public HarmonyPatch(string name) { }
    }
    public class HarmonyPrefix : Attribute { }
}
namespace PartAdjustment
{
    internal class ToolRunner : UnityEngine.MonoBehaviour { internal static void SceneChanged() { } }
}
namespace BepInEx.Configuration
{
    public sealed class ConfigEntry<T> { public T Value; public ConfigEntry(T value) { Value = value; } }
    public sealed class ConfigDescription { public ConfigDescription(string text, object range) { } }
    public sealed class AcceptableValueRange<T> { public AcceptableValueRange(T min, T max) { } }
    public sealed class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T value, string description) => new ConfigEntry<T>(value);
        public ConfigEntry<T> Bind<T>(string section, string key, T value, ConfigDescription description) => new ConfigEntry<T>(value);
    }
}
namespace BepInEx.Logging
{
    public sealed class ManualLogSource { public void LogInfo(object text) { } }
}
namespace BepInEx
{
    public sealed class BepInPlugin : Attribute { public BepInPlugin(string guid, string name, string version) { } }
    public class BaseUnityPlugin : UnityEngine.MonoBehaviour
    {
        public readonly Configuration.ConfigFile Config = new Configuration.ConfigFile();
        public readonly Logging.ManualLogSource Logger = new Logging.ManualLogSource();
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public bool IsValid() => true; }
    public enum LoadSceneMode { Single }
    public static class SceneManager
    {
        public static event Action<Scene,LoadSceneMode> sceneLoaded;
        public static void Load() { sceneLoaded?.Invoke(new Scene(), LoadSceneMode.Single); }
    }
}
namespace UnityEngine.UI
{
    public enum HorizontalWrapMode { Wrap,Overflow }
    public enum VerticalWrapMode { Truncate,Overflow }
    public class Text : UnityEngine.Component
    {
        private string content="";
        public int Writes;
        public string text { get=>content;set { content=value;Writes++; } }
        public bool raycastTarget;
        public HorizontalWrapMode horizontalOverflow;
        public VerticalWrapMode verticalOverflow;
    }
}
