using System.Collections.Generic;
using UnityEngine;

namespace PartAdjustment
{
    // Bounds remain available on non-readable meshes. Cache their ends in the model's own
    // coordinates, before any width multiplier, so saved poses cannot become new baselines.
    internal sealed class SuspensionWidthGeometry
    {
        private sealed class Axle
        {
            internal Transform Branch;
            internal bool Lifted;
            internal float Left, Right;
            internal Vector3 Center;
        }
        private readonly Transform model;
        private readonly List<Axle> axles = new List<Axle>();

        internal SuspensionWidthGeometry(Transform model)
        {
            this.model = model;
            foreach (var mesh in model.GetComponentsInChildren<MeshFilter>(true))
            {
                string name = mesh.name.ToLowerInvariant();
                if (mesh.sharedMesh == null || !name.Contains("suspension") || (!name.Contains("front") && !name.Contains("rear"))) continue;
                var branch = mesh.transform;
                while (branch.parent != null && branch.parent != model) branch = branch.parent;
                bool lifted = branch.name == "lifted" || branch.name == "buggy";
                if (branch.parent != model || (!lifted && branch.name != "stock")) continue;
                var bounds = mesh.sharedMesh.bounds;
                float left = float.PositiveInfinity, right = float.NegativeInfinity;
                // Include the child's rotation/scale and its variant offsets. Reading mesh vertices
                // would fail on several shipped vehicles; transformed bounds need no native readback.
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    float value = model.InverseTransformPoint(mesh.transform.TransformPoint(corner)).x;
                    left = Mathf.Min(left, value); right = Mathf.Max(right, value);
                }
                axles.Add(new Axle { Branch = branch, Lifted = lifted, Left = left, Right = right,
                    Center = model.InverseTransformPoint(mesh.transform.TransformPoint(bounds.center)) });
            }
        }

        internal bool TryOffset(Transform wheel, Vector3 baseline, bool lifted, Vector3 factoryScale, float width, out Vector3 offset)
        {
            offset = Vector3.zero;
            if (model == null || model.parent == null || wheel == null || wheel.parent == null) return false;
            Axle nearest = null;
            float distance = float.PositiveInfinity;
            foreach (var axle in axles)
            {
                if (axle.Branch == null || axle.Lifted != lifted) continue;
                float candidate = Mathf.Abs(wheel.parent.InverseTransformPoint(model.TransformPoint(axle.Center)).z - baseline.z);
                if (candidate < distance) { distance = candidate; nearest = axle; }
            }
            if (nearest == null) return false;
            bool left = baseline.x < wheel.parent.InverseTransformPoint(model.position).x;
            float endpoint = left ? nearest.Left : nearest.Right;
            var localDelta = new Vector3(AdjustmentMath.EndpointOffset(endpoint * factoryScale.x, width), 0f, 0f);
            offset = wheel.parent.InverseTransformVector(model.parent.TransformVector(model.localRotation * localDelta));
            return true;
        }
    }
}
