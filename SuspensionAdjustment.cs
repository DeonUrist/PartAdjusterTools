using System;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
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
        internal float Width = 1f, Height;
        internal Transform Hinge;
        private Transform model;
        private Vector3 modelPosition, modelScale, hingePosition;
        private readonly List<Mount> mounts = new List<Mount>();
        private readonly List<SuspensionPickTarget> picks = new List<SuspensionPickTarget>();
        private SuspensionBraces braces;
        private SuspensionIndicators indicators;
        private bool ready;

        private sealed class Mount
        {
            internal Transform Transform;
            internal Vector3 Baseline;
            internal float Center;
        }

        internal static SuspensionAdjustment Ensure(Transform car)
        {
            if (car == null || !car.gameObject.scene.IsValid()) return null;
            var existing = car.GetComponent<SuspensionAdjustment>();
            if (existing != null) return existing.ready ? existing : null;
            var model = car.Find("suspension_model");
            var hinge = car.Find("hinge_suspension_parent/hinge_suspension");
            if (model == null || hinge == null) return null;
            var wheels = car.GetComponentsInChildren<WheelController>(true)
                .Where(w => w.transform.parent == car && w.name.StartsWith("hinge_wheel_", StringComparison.Ordinal)).ToArray();
            if (wheels.Length < 4) return null;
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
            foreach (var wheel in wheels)
                mounts.Add(new Mount { Transform = wheel.transform, Baseline = VanillaPosition(wheel.transform) });
            RefreshCenters();
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
            Plugin.Log.LogDebug("Suspension adjustment ready: " + name + ", " + mounts.Count + " physical mounts.");
        }

        private static Vector3 VanillaPosition(Transform wheel)
        {
            var fsm = wheel.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Suspension");
            // These constants are authoritative even if ES3 has already restored an adjusted transform.
            var state = fsm?.FsmStates.FirstOrDefault(s => s.Name == fsm.ActiveStateName)
                ?? fsm?.FsmStates.FirstOrDefault(s => s.Name == "stock");
            var action = state?.Actions.OfType<SetPosition>().FirstOrDefault(a => a.space == Space.Self);
            if (action == null) return wheel.localPosition;
            var position = action.vector != null && !action.vector.IsNone ? action.vector.Value : wheel.localPosition;
            if (action.x != null && !action.x.IsNone) position.x = action.x.Value;
            if (action.y != null && !action.y.IsNone) position.y = action.y.Value;
            if (action.z != null && !action.z.IsNone) position.z = action.z.Value;
            return position;
        }

        internal void SetPickable(bool pickable)
        {
            foreach (var target in picks)
                if (target != null && target.PickCollider != null) target.PickCollider.enabled = pickable;
            indicators?.Update(pickable);
        }

        internal void Change(float widthDelta, float heightDelta, bool reset)
        {
            Width = reset ? 1f : AdjustmentMath.Width(Width + widthDelta);
            Height = reset ? 0f : heightDelta != 0f ? AdjustmentMath.Height(Height + heightDelta, Plugin.HeightLimit.Value) : Height;
            Apply();
        }

        internal void Apply()
        {
            if (!ready) return;
            model.localScale = new Vector3(modelScale.x * Width, modelScale.y, modelScale.z);
            model.localPosition = modelPosition + Vector3.up * Height;
            braces.ShowForWidth(Width);
            // The lift kit/attachment trigger moves vertically with the assembly, but is never stretched.
            if (Hinge != null) Hinge.localPosition = hingePosition + Hinge.parent.InverseTransformVector(transform.TransformVector(Vector3.up * Height));
            foreach (var mount in mounts) ApplyMount(mount);
            var body = GetComponent<Rigidbody>();
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
            foreach (var mount in mounts) if (mount.Transform != null) mount.Baseline = VanillaPosition(mount.Transform);
            RefreshCenters();
            Apply();
        }

        private void ApplyMount(Mount mount)
        {
            if (mount.Transform == null) return;
            var position = mount.Baseline;
            position.x = AdjustmentMath.SpacedX(position.x, mount.Center, Width);
            position.y += Height;
            mount.Transform.localPosition = position;
            // NWH WheelController.Step reads this mount to cast the suspension ray, apply forces, and place
            // wheel.visualTransform and wheel.colliderTransform. The wheel's own scale/radius stays unchanged.
        }

        internal void VanillaMountChanged(Transform wheel)
        {
            var mount = mounts.FirstOrDefault(m => m.Transform == wheel);
            if (mount == null) return;
            mount.Baseline = wheel.localPosition;
            RefreshCenters();
            foreach (var m in mounts) ApplyMount(m);
        }

        private void RefreshCenters()
        {
            // Match opposite mounts at the same longitudinal position, keeping each axle centered.
            foreach (var mount in mounts)
            {
                var opposite = mounts.Where(m => m != mount && Mathf.Sign(m.Baseline.x) != Mathf.Sign(mount.Baseline.x))
                    .OrderBy(m => Mathf.Abs(m.Baseline.z - mount.Baseline.z)).FirstOrDefault();
                mount.Center = opposite == null ? 0f : (mount.Baseline.x + opposite.Baseline.x) * 0.5f;
            }
        }

        private void OnDestroy()
        {
            indicators?.Destroy();
            All.Remove(this);
            if (ToolRunner.Active == this) ToolRunner.Active = null;
        }
    }
}
