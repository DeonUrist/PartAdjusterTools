using System.Collections.Generic;
using UnityEngine;

namespace PartAdjustment
{
    internal sealed class SuspensionIndicators
    {
        private readonly SuspensionAdjustment adjustment;
        private readonly Transform model;
        private readonly List<Indicator> indicators = new List<Indicator>();
        private bool initialized;
        private bool showing;
        private float nextInitialize;

        private sealed class Indicator
        {
            internal MeshFilter Axle;
            internal Transform AxleTransform;
            internal Vector3 Center;
            internal GameObject Target;
            internal Transform Visual;
            internal Collider Collider;
        }

        internal SuspensionIndicators(SuspensionAdjustment adjustment, Transform model)
        {
            this.adjustment = adjustment;
            this.model = model;
        }

        private void Initialize()
        {
            var group = adjustment.transform.Find("parts/hingeAdjustVisibility/HingesVisible");
            var source = group == null ? null : group.Find("engineHingeVisible");
            var mesh = source == null ? null : source.GetComponent<MeshFilter>();
            var renderer = source == null ? null : source.GetComponent<MeshRenderer>();
            if (mesh == null || mesh.sharedMesh == null || renderer == null) return;

            foreach (var axle in model.GetComponentsInChildren<MeshFilter>(true))
            {
                string name = axle.name.ToLowerInvariant();
                if (!name.Contains("suspension") || (!name.Contains("front") && !name.Contains("rear"))
                    || axle.sharedMesh == null) continue;

                // The native group's HingeAdjustVisible FSM shows/hides all floating wrenches.
                // Copy just its visual assets, never its HingeFollow FSM or engine references.
                var target = new GameObject("PartAdjustment.SuspensionIndicator") { layer = 13, tag = "Hinge" };
                target.SetActive(false);
                target.transform.SetParent(group, false);
                var box = target.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = Vector3.one * 0.2f; // Same selection reach/target size as vanilla hinges.
                box.enabled = false;
                var pick = target.AddComponent<SuspensionPickTarget>();
                pick.Adjustment = adjustment;
                pick.PickCollider = box;

                var visual = new GameObject("Wrench") { layer = source.gameObject.layer };
                visual.transform.SetParent(target.transform, false);
                visual.transform.localScale = source.localScale; // Native 0.3m billboard; never stretched.
                visual.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var copy = visual.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = renderer.shadowCastingMode;
                copy.receiveShadows = renderer.receiveShadows;
                copy.lightProbeUsage = renderer.lightProbeUsage;
                copy.reflectionProbeUsage = renderer.reflectionProbeUsage;
                copy.sortingLayerID = renderer.sortingLayerID;
                copy.sortingOrder = renderer.sortingOrder;
                indicators.Add(new Indicator { Axle = axle, AxleTransform = axle.transform, Center = axle.sharedMesh.bounds.center,
                    Target = target, Visual = visual.transform, Collider = box });
            }
            initialized = true;
        }

        internal void Update(Camera camera)
        {
            if (!initialized && Time.unscaledTime >= nextInitialize)
            {
                nextInitialize = Time.unscaledTime + 1f;
                Initialize();
            }
            showing = true;
            foreach (var indicator in indicators)
            {
                if (indicator.Target == null) continue;
                bool visible = indicator.Axle != null && indicator.Axle.gameObject.activeInHierarchy;
                if (indicator.Target.activeSelf != visible) indicator.Target.SetActive(visible);
                if (indicator.Collider.enabled != visible) indicator.Collider.enabled = visible;
                if (!visible) continue;
                indicator.Target.transform.position = indicator.AxleTransform.TransformPoint(indicator.Center);
                if (camera != null) indicator.Visual.LookAt(camera.transform, Vector3.up);
            }
        }

        internal void Hide()
        {
            if (!showing) return;
            showing = false;
            foreach (var indicator in indicators)
            {
                if (indicator.Target == null) continue;
                if (indicator.Target.activeSelf) indicator.Target.SetActive(false);
                if (indicator.Collider.enabled) indicator.Collider.enabled = false;
            }
        }

        internal void Destroy()
        {
            foreach (var indicator in indicators)
                if (indicator.Target != null) Object.Destroy(indicator.Target);
            indicators.Clear();
        }
    }
}
