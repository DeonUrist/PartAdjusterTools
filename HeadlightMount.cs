using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using NWH.WheelController3D;
using UnityEngine;
using UnityEngine.UI;

namespace PartAdjustment
{
    // Free headlight mounting with the Part Adjustment tool selected.
    // Attach = the vanilla hinge Attach recipe (not_Hold, no Rigidbody, tag vehPart, layer 8, SetParent keeping
    // the world pose) onto a car-body collider instead of a hinge. Remove = the vanilla wrench path (de_Attach event).
    // Everything after that (CheckTag, LockPhysics, saving the parent and local pose, the car's HeadlightsON/OFF
    // broadcast) is the game's own logic, exactly as for gauges and metal plates.
    internal static class HeadlightMount
    {
        internal const float Reach = 2f;
        private const int BodyMask = (1 << 0) | (1 << 8);
        internal static bool Ready;
        // Frame on which our Use press attached/removed a light: the game's own Use polls skip it (no door, hinge, etc.).
        internal static int ConsumedUseFrame = -1;
        private static int touchedFrame = -1;
        private static Text uiText;
        private static string shownText;
        private static AudioClip clip;
        private static readonly Dictionary<Transform, bool> vehicles = new Dictionary<Transform, bool>();
        private static readonly Dictionary<Transform, List<PlayMakerFSM>> hinges = new Dictionary<Transform, List<PlayMakerFSM>>();
        private static float nextHingeRefresh;

        internal static bool IsHeadlight(GameObject go)
        {
            if (go == null) return false;
            foreach (var fsm in go.GetComponents<PlayMakerFSM>())
                if (fsm.FsmName == "ID") return fsm.FsmVariables.GetFsmString("ID")?.Value == "headlight";
            return false;
        }

        // GrabItem's Item is the picked collider's object; the item is the nearest object carrying an ID FSM.
        private static GameObject ItemRoot(GameObject go)
        {
            for (var t = go == null ? null : go.transform; t != null; t = t.parent)
                if (AdjustmentRunner.Find(t.gameObject, "ID") != null) return t.gameObject;
            return go;
        }

        internal static string Label(GameObject go) => TailLights.IsTail(go) ? "Tail Light" : "Headlight";

        // The vanilla tool owns the frame while it points at a hinge wrench or adjusts one.
        internal static bool ToolBusy(PlayMakerFSM adjustTool)
        {
            if (adjustTool == null) return true;
            var s = adjustTool.ActiveStateName;
            return s == "over" || s == "compare Tag" || s == "adjust" || s == "finish";
        }

        internal static void Update(PlayMakerFSM grab, PlayMakerFSM adjustTool, Camera camera)
        {
            touchedFrame = Time.frameCount;
            Ready = false;
            if (!Plugin.AttachAnywhere.Value || grab == null || camera == null || grab.ActiveStateName != "ItemInHand") { ReleaseText(); return; }
            var held = ItemRoot(grab.FsmVariables.GetFsmGameObject("Item")?.Value);
            if (held == null || !IsHeadlight(held) || held.CompareTag("vehPart")) { ReleaseText(); return; }
            bool press = Controls.Pressed(Controls.Use);
            if (ToolBusy(adjustTool)) { Refused(press, held, "the tool points at a part hinge (" + adjustTool.ActiveStateName + ")"); return; }
            if (!Find(camera, held, out var parent, out var point, out var normal, out var car, out var why)) { Refused(press, held, why); return; }
            // Aiming at a free headlight slot that already offers this item: the vanilla slot attach takes the press.
            if (SlotOffered(car, held, point)) { Refused(press, held, "a free headlight slot right there takes it"); return; }
            Ready = true;
            ShowText("Attach " + Label(held) + ": " + Controls.Keys(Controls.Use));
            if (!press) return;
            ConsumedUseFrame = Time.frameCount;
            Attach(grab, adjustTool, held, parent, point, normal, car);
            Ready = false;
            ReleaseText();
        }

        private static void Refused(bool press, GameObject held, string why)
        {
            ReleaseText();
            if (press) Plugin.Log.LogInfo("Headlight mount: " + held.name + " not attached - " + why);
        }

        // Called every LateUpdate: anything that did not run Update this frame is not offering an attach.
        internal static void LateCheck() { if (touchedFrame != Time.frameCount && (Ready || shownText != null)) Clear(); }
        internal static void Clear() { Ready = false; ReleaseText(); }
        internal static void SceneChanged() { Clear(); uiText = null; clip = null; vehicles.Clear(); hinges.Clear(); }

        private static bool Find(Camera camera, GameObject held, out Transform parent, out Vector3 point, out Vector3 normal, out Transform car, out string why)
        {
            parent = car = null; point = normal = Vector3.zero; why = "nothing within " + Reach + " m";
            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var hits = Physics.RaycastAll(ray, Reach, BodyMask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(held.transform)) continue;
                // Only the first thing behind the held item counts: a wall in front of the car blocks the attach.
                var body = hit.collider.attachedRigidbody;
                if (body == null || !IsVehicle(body.transform)) { why = "aimed at " + Path(hit.collider.transform) + ", not a car body"; return false; }
                car = body.transform;
                parent = MountFor(hit.collider.transform, car);
                if (parent == null) { why = "aimed at a wheel (" + Path(hit.collider.transform) + ")"; return false; }
                point = hit.point; normal = hit.normal;
                return true;
            }
            return false;
        }

        private static bool IsVehicle(Transform root)
        {
            if (vehicles.TryGetValue(root, out var known)) return known;
            bool vehicle = false;
            if (!root.CompareTag("Player"))
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    var n = fsm.FsmName;
                    if (n == "RpmGear" || n == "getFuel" || n == "CrashDamage" || n == "TrailerAttached") { vehicle = true; break; }
                }
            if (vehicles.Count > 256) vehicles.Clear();
            vehicles[root] = vehicle;
            return vehicle;
        }

        // The collider's own object unless it is a hinge (a hinge treats any child as its part), another headlight,
        // or non-uniformly scaled (a rotated child would shear); then its nearest suitable ancestor. Wheels never.
        private static Transform MountFor(Transform hit, Transform car)
        {
            for (var t = hit; t != null && t != car; t = t.parent)
                if (t.name.StartsWith("hinge_wheel", System.StringComparison.Ordinal) || t.GetComponent<WheelController>() != null) return null;
            var p = hit;
            while (p != null && p != car && (p.name.StartsWith("hinge", System.StringComparison.Ordinal)
                || AdjustmentRunner.Find(p.gameObject, "vehPart_Attach") != null || IsHeadlight(p.gameObject) || !Uniform(p.lossyScale)))
                p = p.parent;
            return p;
        }

        private static bool Uniform(Vector3 s)
        {
            float a = Mathf.Abs(s.x), b = Mathf.Abs(s.y), c = Mathf.Abs(s.z);
            float max = Mathf.Max(a, Mathf.Max(b, c)), min = Mathf.Min(a, Mathf.Min(b, c));
            return min > 0f && max / min < 1.02f;
        }

        private static List<PlayMakerFSM> Hinges(Transform car)
        {
            if (Time.unscaledTime >= nextHingeRefresh) { hinges.Clear(); nextHingeRefresh = Time.unscaledTime + 2f; }
            if (!hinges.TryGetValue(car, out var list))
            {
                list = new List<PlayMakerFSM>();
                foreach (var fsm in car.GetComponentsInChildren<PlayMakerFSM>(true)) if (fsm.FsmName == "vehPart_Attach") list.Add(fsm);
                hinges[car] = list;
            }
            return list;
        }

        private static bool Offers(PlayMakerFSM fsm, GameObject held)
        {
            if (fsm == null || !fsm.isActiveAndEnabled || fsm.ActiveStateName != "Over") return false;
            var item = fsm.FsmVariables.GetFsmGameObject("Item")?.Value;
            return item != null && (item == held || item.transform.IsChildOf(held.transform));
        }

        // Only when the player aims at the slot itself (within SlotRadius of its hinge). A hinge can stay in "Over" after the
        // light has left its trigger (held items are triggers too), so a slot elsewhere on the car must not block the mount.
        private const float SlotRadius = 0.35f;
        private static bool SlotOffered(Transform car, GameObject held, Vector3 point)
        {
            foreach (var fsm in Hinges(car))
                if (Offers(fsm, held) && (fsm.transform.position - point).sqrMagnitude < SlotRadius * SlotRadius) return true;
            return false;
        }

        // After our mount no hinge may keep offering the light (its next Use would pull it into the slot).
        private static void ResetOffers(Transform car, GameObject held)
        {
            foreach (var fsm in Hinges(car))
            {
                if (!Offers(fsm, held)) continue;
                var item = fsm.FsmVariables.GetFsmGameObject("Item");
                if (item != null) item.Value = null;
                fsm.Fsm.SetState("partMissing");
            }
        }

        // Lights on <=> the switch's LightOn FSM is enabled (its useDoor turns them off; Apocaplayer reads it the same way).
        internal static bool LightsOn(Transform car)
        {
            foreach (var fsm in car.GetComponentsInChildren<PlayMakerFSM>())
                if (fsm.FsmName == "LightOn" && fsm.gameObject.name == "switch_lights") return fsm.enabled;
            return false;
        }

        // The car's switch reaches the lights in the headlight slots only; mounted lights get the same event from us.
        internal static void SwitchLights(Transform car, string evt)
        {
            if (car == null) return;
            foreach (var fsm in car.GetComponentsInChildren<PlayMakerFSM>())
                if (fsm.FsmName == "Headlight" && IsHeadlight(fsm.gameObject)) fsm.SendEvent(evt);
        }

        private static void Attach(PlayMakerFSM grab, PlayMakerFSM adjustTool, GameObject held, Transform parent, Vector3 point, Vector3 normal, Transform car)
        {
            var t = held.transform;
            // Keep the hand pose when the light already touches the surface; otherwise put it flush on the aimed point.
            if (RenderBounds(held, out var bounds) && (bounds.ClosestPoint(point) - point).sqrMagnitude > 0.12f * 0.12f)
            {
                float extent = Mathf.Abs(normal.x) * bounds.extents.x + Mathf.Abs(normal.y) * bounds.extents.y + Mathf.Abs(normal.z) * bounds.extents.z;
                t.position += point + normal * extent - bounds.center;
            }
            var position = t.position;
            var rotation = t.rotation;
            grab.SendEvent("not_Hold");
            var body = held.GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = true; Object.Destroy(body); }
            held.tag = "vehPart";
            held.layer = 8;
            t.SetParent(parent, true);
            t.SetPositionAndRotation(position, rotation);
            ResetOffers(car, held);
            var light = AdjustmentRunner.Find(held, "Headlight");
            if (light != null) light.SendEvent(LightsOn(car) ? "HeadlightsON" : "HeadlightsOFF");
            PlayWrench(adjustTool, position);
            Plugin.Log.LogInfo("Attached " + held.name + " to " + Path(parent));
        }

        internal static void Detach(GameObject light, PlayMakerFSM adjustTool)
        {
            var fsm = AdjustmentRunner.Find(light, "de_Attach");
            if (fsm == null) return;
            ConsumedUseFrame = Time.frameCount;
            PlayWrench(adjustTool, light.transform.position);
            fsm.SendEvent("de_Attach");
        }

        private static bool RenderBounds(GameObject go, out Bounds bounds)
        {
            bounds = default(Bounds);
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        private static void PlayWrench(PlayMakerFSM adjustTool, Vector3 position)
        {
            if (clip == null && adjustTool != null)
            {
                var state = adjustTool.Fsm.GetState("adjust");
                if (state != null)
                    foreach (var action in state.Actions)
                        if (action is PlaySound sound && sound.clip != null) { clip = sound.clip.Value as AudioClip; if (clip != null) break; }
            }
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, 1f);
        }

        private static void ShowText(string text)
        {
            if (uiText == null)
            {
                var go = FsmVariables.GlobalVariables.GetFsmGameObject("UI_ItemUse")?.Value;
                uiText = go == null ? null : go.GetComponent<Text>();
            }
            if (uiText == null) return;
            if (uiText.text != text) uiText.text = text;
            shownText = text;
        }

        private static void ReleaseText()
        {
            if (shownText != null && uiText != null && uiText.text == shownText) uiText.text = "";
            shownText = null;
        }

        private static string Path(Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}

namespace PartAdjustment
{
    // The light switch (switch_lights FSMs LightOn / LightOff) sends HeadlightsON / OFF that only reaches the slot lights;
    // repeat it for every headlight / tail light anywhere on that car, so freely mounted ones follow the switch.
    [HarmonyLib.HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.SendEvent), nameof(HutongGames.PlayMaker.Actions.SendEvent.OnEnter))]
    internal static class LightSwitchPatch
    {
        private static void Postfix(HutongGames.PlayMaker.Actions.SendEvent __instance)
        {
            var name = __instance.Fsm?.Name;
            if (name != "LightOn" && name != "LightOff") return;
            var evt = __instance.sendEvent?.Name;
            // LightOff's useDoor switches the lights on, LightOn's switches them off.
            if (evt != "HeadlightsON" && evt != "HeadlightsOFF") evt = name == "LightOff" ? "HeadlightsON" : "HeadlightsOFF";
            try
            {
                var body = __instance.Owner == null ? null : __instance.Owner.GetComponentInParent<UnityEngine.Rigidbody>();
                if (body != null) HeadlightMount.SwitchLights(body.transform, evt);
            }
            catch (System.Exception e) { Plugin.Log.LogError("Headlight switch: " + e); }
        }
    }
}
