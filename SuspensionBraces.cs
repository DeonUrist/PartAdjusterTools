using System.Collections.Generic;
using UnityEngine;

namespace PartAdjustment
{
    // Recreated from the model after loading; never persisted as extra vehicle parts.
    [ES3NonSerializable]
    public sealed class SuspensionBrace : MonoBehaviour { }

    internal sealed class SuspensionBraces
    {
        private readonly List<GameObject> bars = new List<GameObject>();

        internal SuspensionBraces(Transform model)
        {
            foreach (var axle in model.GetComponentsInChildren<MeshFilter>(true))
            {
                string name = axle.name.ToLowerInvariant();
                if (!name.Contains("suspension") || (!name.Contains("front") && !name.Contains("rear"))) continue;
                var source = axle.GetComponent<MeshRenderer>();
                if (source == null || source.sharedMaterial == null || axle.sharedMesh == null) continue;
                Vector3 left, right;
                if (!RodAnchors.TryGet(axle.sharedMesh.name, out left, out right))
                {
                    // Bounds fallback for a future/custom axle mesh; stock meshes use inspected rod-cap geometry.
                    var bounds = axle.sharedMesh.bounds;
                    left = new Vector3(bounds.center.x - bounds.extents.x * 0.75f, bounds.max.y, bounds.center.z);
                    right = new Vector3(bounds.center.x + bounds.extents.x * 0.75f, bounds.max.y, bounds.center.z);
                }
                float length = right.x - left.x;
                if (length <= 0.01f) continue;
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bar.name = "PartAdjustment.SuspensionBrace";
                bar.SetActive(false);
                // Match the visible axle: driving cameras exclude Ignore Raycast (layer 2).
                // The disabled/removed collider already keeps this beam out of part-selection raycasts.
                bar.layer = axle.gameObject.layer;
                var collider = bar.GetComponent<Collider>();
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
                bar.AddComponent<SuspensionBrace>();
                bar.transform.SetParent(axle.transform, false);
                // Unity's cylinder is two units high along Y. Rotate its axis onto the vehicle's X axis.
                // Parenting to the axle makes the length stretch once with suspension_model, while its
                // diameter and vertical/longitudinal placement remain unchanged by the width adjustment.
                bar.transform.localPosition = (left + right) * 0.5f - Vector3.up * 0.01f;
                bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                bar.transform.localScale = new Vector3(0.04f, (length + 0.02f) * 0.5f, 0.04f);
                // The suspension's rusted_black_metal material includes its original rust albedo/normal maps.
                // Share the asset without modifying the material or requiring an external texture file.
                bar.GetComponent<MeshRenderer>().sharedMaterial = source.sharedMaterial;
                bars.Add(bar);
            }
        }

        internal void ShowForWidth(float width)
        {
            bool show = width > 1.0001f;
            foreach (var bar in bars) if (bar != null && bar.activeSelf != show) bar.SetActive(show);
        }
    }

    internal static class RodAnchors
    {
        // Mesh-local rod-cap centers measured from this installation's original assets. Some truck meshes
        // are not readable at runtime, so deriving their endpoints from mesh.vertices would fail in game.
        // Stock/lifted variants have separate meshes; Rustcargo and Rustchief share the four truck meshes.
        internal static bool TryGet(string mesh, out Vector3 left, out Vector3 right)
        {
            switch (mesh)
            {
                case "front_suspension":
                    left = new Vector3(-0.4055034f, 0.2927555f, 0.0002128f);
                    right = new Vector3(0.4082605f, 0.2927555f, 0.0002128f); return true;
                case "rear_suspension":
                    left = new Vector3(-0.3932932f, 0.1337374f, -0.0125802f);
                    right = new Vector3(0.3932925f, 0.1337374f, -0.0125802f); return true;
                case "front_suspension_offroad":
                    left = new Vector3(-0.496318f, 0.3507718f, -0.000639f);
                    right = new Vector3(0.496318f, 0.3507718f, -0.000639f); return true;
                case "rear_suspension_offroad":
                    left = new Vector3(-0.5018032f, 0.3607947f, 0.003607f);
                    right = new Vector3(0.5018032f, 0.3607947f, 0.003607f); return true;
                case "suspension front":
                    left = new Vector3(-0.5983636f, 0.158157f, -0.0447996f);
                    right = new Vector3(0.5979246f, 0.158157f, -0.0447997f); return true;
                case "suspension rear":
                    left = new Vector3(-0.2469477f, 0.2199986f, 0.1646071f);
                    right = new Vector3(0.2464914f, 0.2199986f, 0.164607f); return true;
                case "suspension rear lifted.001":
                    left = new Vector3(-0.3251258f, 0.5401379f, -0.0700647f);
                    right = new Vector3(0.3251258f, 0.5401379f, -0.0700647f); return true;
                case "suspension_front_lifted.001":
                    left = new Vector3(-0.4861487f, 0.3376175f, -0.002561f);
                    right = new Vector3(0.4861489f, 0.3376175f, -0.002561f); return true;
                default: left = right = Vector3.zero; return false;
            }
        }
    }
}
