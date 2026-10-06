using System;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using NWH.WheelController3D;
using UnityEngine;

namespace PartAdjustment
{
    [ES3NonSerializable]
    public sealed class SuspensionPickTarget : MonoBehaviour
    {
        internal SuspensionAdjustment Adjustment;
        internal Collider PickCollider;
    }

    [ES3NonSerializable]
    public sealed class SuspensionAdjustment : MonoBehaviour
    {
        internal static readonly List<SuspensionAdjustment> All = new List<SuspensionAdjustment>();
        internal static int Revision;
        internal float Width = 1f, Height;
        internal Transform Hinge;
        private Transform model;
        private Vector3 modelPosition, modelScale, hingePosition;
        private readonly List<Mount> mounts = new List<Mount>();
        private readonly List<SuspensionPickTarget> picks = new List<SuspensionPickTarget>();
        private SuspensionBraces braces;
        private SuspensionIndicators indicators;
        private SuspensionWidthGeometry widthGeometry;
        private PlayMakerFSM attachment, suspensionCheck;
        private Rigidbody body;
        private bool toolRequested, pickable;
        private bool ready;

        private sealed class Mount
        {
            internal Transform Transform;
            internal Vector3 Baseline;
            internal float Center;
            internal PlayMakerFSM Suspension;
            internal bool Lifted;
        }

        internal static SuspensionAdjustment Ensure(Transform car)
        {
            if (car == null || !car.gameObject.scene.IsValid()) return null;
            var existing = car.GetComponent<SuspensionAdjustment>();
            if (existing != null) return existing.Supported() ? existing : null;
            Transform model, hinge;
            WheelController[] wheels;
            if (!SuspensionSupport.TryAssembly(car, out model, out hinge, out wheels)) return null;
            var adjustment = car.gameObject.AddComponent<SuspensionAdjustment>();
            adjustment.Initialize(model, hinge, wheels);
            return adjustment;
        }

        private void Initialize(Transform suspensionModel, Transform hinge, WheelController[] wheels)
        {
            model = suspensionModel;
            Hinge = hinge;
            modelPosition = model.localPosition;
            modelScale = model.localScale;
            hingePosition = hinge.localPosition;
            attachment = SuspensionSupport.EnabledFsm(hinge, "vehPart_Attach");
            suspensionCheck = SuspensionSupport.EnabledFsm(hinge, "checkSuspension");
            body = GetComponent<Rigidbody>();
            foreach (var wheel in wheels)
            {
                var suspension = SuspensionSupport.EnabledFsm(wheel.transform, "Suspension");
                mounts.Add(new Mount { Transform = wheel.transform, Baseline = VanillaPosition(wheel.transform),
                    Suspension = suspension, Lifted = suspension?.ActiveStateName == "lifted" });
            }
            RefreshCenters();
            widthGeometry = new SuspensionWidthGeometry(model);
            // Separate trigger targets preserve the layer/tag and attachment child counts of all vanilla hinges.
            // Never parent a selection target under hinge_suspension: GameObjectHasChildren detects the lift kit there.
            foreach (var mesh in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.sharedMesh == null || mesh.GetComponent<MeshRenderer>() == null) continue;
                var go = new GameObject("PartAdjustment.SuspensionTarget") { layer = 13, tag = "Hinge" };
                go.transform.SetParent(mesh.transform, false);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = mesh.sharedMesh.bounds.center;
                var size = mesh.sharedMesh.bounds.size;
                box.size = new Vector3(Mathf.Max(size.x, 0.06f), Mathf.Max(size.y, 0.06f), Mathf.Max(size.z, 0.06f));
                box.enabled = false;
                var target = go.AddComponent<SuspensionPickTarget>();
                target.Adjustment = this;
                target.PickCollider = box;
                picks.Add(target);
            }
            braces = new SuspensionBraces(model);
            indicators = new SuspensionIndicators(this, model);
            ready = true;
            All.Add(this);
            Revision++;
            Plugin.Log.LogDebug("Suspension adjustment ready: " + name + ", " + mounts.Count + " physical mounts.");
        }

        private static Vector3 VanillaPosition(Transform wheel)
        {
            var fsm = SuspensionSupport.EnabledFsm(wheel, "Suspension");
            // These constants are authoritative even if ES3 has already restored an adjusted transform.
            var state = fsm?.FsmStates.FirstOrDefault(s => s.Name == fsm.ActiveStateName)
                ?? fsm?.FsmStates.FirstOrDefault(s => s.Name == "stock");
            var action = SuspensionSupport.MountAction(fsm == null ? null : SuspensionSupport.Actions(fsm.Fsm, state), fsm != null,
                a => fsm.Fsm.GetOwnerDefaultTarget(a.gameObject) == wheel.gameObject);
            return SuspensionSupport.Position(action, wheel.localPosition);
        }

        internal void SetPickable(bool toolOn)
        {
            toolRequested = toolOn;
            SetColliderVisibility(toolOn && Supported());
            if (!toolOn) indicators?.Hide();
        }

        private void SetColliderVisibility(bool value)
        {
            if (pickable == value) return;
            pickable = value;
            foreach (var target in picks)
                if (target != null && target.PickCollider != null) target.PickCollider.enabled = pickable;
        }

        internal void UpdateIndicators(Camera camera)
        {
            SetColliderVisibility(toolRequested && Supported());
            if (pickable) indicators?.Update(camera);
            else indicators?.Hide();
        }

        // 1.2.0: the suspension can only be adjusted while a lift kit sits on hinge_suspension (the game's own checkSuspension looks at
        // the same thing: GameObjectHasChildren). Without one the width and height are standard, and stay standard.
        internal bool KitFitted
        {
            get
            {
                if (Hinge == null) return false;
                for (int i = 0; i < Hinge.childCount; i++) if (Hinge.GetChild(i).CompareTag("vehPart")) return true;
                return false;
            }
        }

        // 1.2.0: another mod (Apocapatrol's car templates) sets the values; false when the car has no kit or is not supported
        internal bool Set(float width, float height)
        {
            if (!Supported() || !KitFitted) return false;
            Width = AdjustmentMath.Width(width);
            Height = AdjustmentMath.Height(height, 1f);
            Apply();
            return true;
        }

        internal void Change(float widthDelta, float heightDelta, bool reset)
        {
            if (!Supported() || !KitFitted) return;
            float width = reset ? 1f : AdjustmentMath.Width(Width + widthDelta);
            float height = reset ? 0f : heightDelta != 0f ? AdjustmentMath.Height(Height + heightDelta, Plugin.HeightLimit.Value) : Height;
            if (!reset && width == Width && height == Height) return;
            Width = width;
            Height = height;
            Apply();
        }

        internal void Apply()
        {
            if (!Supported()) return;
            if (!KitFitted && (Width != 1f || Height != 0f))
            {
                Width = 1f; Height = 0f;
                Plugin.Log.LogDebug("Suspension of " + name + " back to standard: no lift kit fitted.");
            }
            model.localScale = new Vector3(modelScale.x * Width, modelScale.y, modelScale.z);
            model.localPosition = modelPosition + Vector3.up * Height;
            braces.ShowForWidth(Width);
            // The lift kit/attachment trigger moves vertically with the assembly, but is never stretched.
            if (Hinge != null) Hinge.localPosition = hingePosition + Hinge.parent.InverseTransformVector(transform.TransformVector(Vector3.up * Height));
            foreach (var mount in mounts) ApplyMount(mount);
            if (body != null) body.WakeUp();
        }

        internal void WriteSaveVariables(FsmVariables variables)
        {
            SaveVariables.Float(variables, SaveVariables.Width, 1f).Value = Width;
            SaveVariables.Float(variables, SaveVariables.Height, 0f).Value = Height;
            SaveVariables.Vector(variables, SaveVariables.ModelPosition, modelPosition).Value = modelPosition;
            SaveVariables.Vector(variables, SaveVariables.ModelScale, modelScale).Value = modelScale;
            SaveVariables.Vector(variables, SaveVariables.HingePosition, hingePosition).Value = hingePosition;
        }

        internal void PrepareLoad(FsmVariables variables)
        {
            // Reset defaults before every load: old saves omit these fields and must not inherit another slot's values.
            SaveVariables.Float(variables, SaveVariables.Width, 1f).Value = 1f;
            SaveVariables.Float(variables, SaveVariables.Height, 0f).Value = 0f;
            SaveVariables.Vector(variables, SaveVariables.ModelPosition, modelPosition).Value = modelPosition;
            SaveVariables.Vector(variables, SaveVariables.ModelScale, modelScale).Value = modelScale;
            SaveVariables.Vector(variables, SaveVariables.HingePosition, hingePosition).Value = hingePosition;
        }

        internal void ReadSaveVariables(FsmVariables variables)
        {
            Width = AdjustmentMath.Width(SaveVariables.Float(variables, SaveVariables.Width, 1f).Value);
            // Preserve existing valid offsets when a user later lowers the input limit in Apocasetter.
            Height = AdjustmentMath.Height(SaveVariables.Float(variables, SaveVariables.Height, 0f).Value, 1f);
            modelPosition = SaveVariables.Vector(variables, SaveVariables.ModelPosition, modelPosition).Value;
            modelScale = SaveVariables.Vector(variables, SaveVariables.ModelScale, modelScale).Value;
            hingePosition = SaveVariables.Vector(variables, SaveVariables.HingePosition, hingePosition).Value;
            foreach (var mount in mounts) if (mount.Transform != null)
            {
                mount.Baseline = VanillaPosition(mount.Transform);
                mount.Lifted = mount.Suspension?.ActiveStateName == "lifted";
            }
            RefreshCenters();
            Apply();
        }

        private void ApplyMount(Mount mount)
        {
            if (mount.Transform == null) return;
            var position = mount.Baseline;
            if (widthGeometry.TryOffset(mount.Transform, mount.Baseline, mount.Lifted, modelScale, Width, out var offset)) position += offset;
            else position.x = AdjustmentMath.SpacedX(position.x, mount.Center, Width); // Preserve custom meshes without recognized axle variants.
            position.y += Height;
            if (!mount.Transform.localPosition.Equals(position)) mount.Transform.localPosition = position;
            // NWH WheelController.Step reads this mount to cast the suspension ray, apply forces, and place
            // wheel.visualTransform and wheel.colliderTransform. The wheel's own scale/radius stays unchanged.
        }

        internal void VanillaMountChanged(Transform wheel, string state)
        {
            if (!Supported()) return;
            Mount mount = null;
            foreach (var candidate in mounts) if (candidate.Transform == wheel) { mount = candidate; break; }
            if (mount == null) return;
            mount.Baseline = wheel.localPosition;
            mount.Lifted = state == "lifted";
            RefreshCenters();
            // the kit went on or came off (that is what moves the vanilla mounts): without a kit everything returns to standard
            if (!KitFitted && (Width != 1f || Height != 0f)) { Apply(); return; }
            foreach (var m in mounts) ApplyMount(m);
        }

        private bool Supported()
        {
            if (!ready || !SuspensionSupport.BranchActive(model, transform) || !SuspensionSupport.BranchActive(Hinge, transform)
                || attachment == null || !attachment.enabled || suspensionCheck == null || !suspensionCheck.enabled) return false;
            foreach (var mount in mounts) if (mount.Transform == null || mount.Suspension == null || !mount.Suspension.enabled) return false;
            return true;
        }

        private void RefreshCenters()
        {
            // Match opposite mounts at the same longitudinal position, keeping each axle centered.
            foreach (var mount in mounts)
            {
                Mount opposite = null;
                float nearest = float.PositiveInfinity;
                foreach (var candidate in mounts)
                {
                    if (candidate == mount || Mathf.Sign(candidate.Baseline.x) == Mathf.Sign(mount.Baseline.x)) continue;
                    float distance = Mathf.Abs(candidate.Baseline.z - mount.Baseline.z);
                    if (distance < nearest) { opposite = candidate; nearest = distance; }
                }
                mount.Center = opposite == null ? 0f : (mount.Baseline.x + opposite.Baseline.x) * 0.5f;
            }
        }

        private void OnDestroy()
        {
            indicators?.Destroy();
            All.Remove(this);
            Revision++;
            if (ToolRunner.Active == this) ToolRunner.Active = null;
        }
    }
}
