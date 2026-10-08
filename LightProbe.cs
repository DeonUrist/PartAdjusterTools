using System.Linq;
using System.Text;
using UnityEngine;

namespace PartAdjustment
{
    // Diagnostic (1.5.8): what lights reach the ground behind a car with tail lights, and how Unity renders them.
    // Written to LogOutput.log a moment after the car's lights are switched on and after a tail light setting changes
    // (at most every 10 s). Investigates "where two tail lights overlap the ground gets darker".
    internal static class LightProbe
    {
        private static Transform pendingCar;
        private static int dueFrame = -1;
        private static float nextAllowed;

        internal static void Request(Transform car)
        {
            if (car == null || Time.unscaledTime < nextAllowed) return;
            pendingCar = car; dueFrame = Time.frameCount + 5;   // the Headlight FSMs activate their "Light" children first
        }

        internal static void Tick()
        {
            if (dueFrame < 0 || Time.frameCount < dueFrame) return;
            dueFrame = -1;
            var car = pendingCar; pendingCar = null;
            if (car == null) return;
            nextAllowed = Time.unscaledTime + 10f;
            try { Report(car); } catch (System.Exception e) { Plugin.Log.LogWarning("Light probe failed: " + e); }
        }

        private static void Report(Transform car)
        {
            var sb = new StringBuilder("Light probe for " + car.name + ":");
            var cam = Camera.main;
            sb.Append("\n  camera ").Append(cam == null ? "none" : cam.name + " path=" + cam.renderingPath + " actual=" + cam.actualRenderingPath + " hdr=" + cam.allowHDR);
            sb.Append("\n  quality '").Append(QualitySettings.names[QualitySettings.GetQualityLevel()]).Append("' pixelLights=").Append(QualitySettings.pixelLightCount)
              .Append(" shadows=").Append(QualitySettings.shadows).Append(" colorSpace=").Append(QualitySettings.activeColorSpace);
            var center = car.position;
            foreach (var light in Object.FindObjectsOfType<Light>().OrderBy(l => (l.transform.position - center).sqrMagnitude))
            {
                if (!light.enabled) continue;
                float d = (light.transform.position - center).magnitude;
                if (light.type != LightType.Directional && d > 60f) continue;
                sb.Append("\n  ").Append(Path(light.transform)).Append(" | ").Append(light.type).Append(" mode=").Append(light.renderMode)
                  .Append(" i=").Append(light.intensity.ToString("0.###")).Append(" r=").Append(light.range.ToString("0.#"))
                  .Append(" angle=").Append(light.spotAngle.ToString("0")).Append(" col=").Append(ColorUtility.ToHtmlStringRGB(light.color))
                  .Append(" shadows=").Append(light.shadows).Append(" cookie=").Append(light.cookie == null ? "-" : light.cookie.name)
                  .Append(" mask=").Append(light.cullingMask).Append(" d=").Append(d.ToString("0.0"));
            }
            // What the tail lights shine on.
            foreach (var mb in car.GetComponentsInChildren<Light>().Where(l => l.type == LightType.Spot && TailLights.IsTail(Owner(l.transform))))
            {
                var t = mb.transform;
                if (Physics.Raycast(t.position + t.forward * 0.3f, (t.forward + Vector3.down).normalized, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var r = hit.collider.GetComponent<Renderer>();
                    string shader = r != null && r.sharedMaterial != null ? r.sharedMaterial.shader.name : "?";
                    var terrain = hit.collider.GetComponent("Terrain");
                    if (terrain != null)
                    {
                        var m = terrain.GetType().GetProperty("materialTemplate")?.GetValue(terrain, null) as Material;
                        shader = "terrain " + (m != null ? m.shader.name : "default");
                    }
                    sb.Append("\n  ").Append(Path(t)).Append(" lights ").Append(Path(hit.collider.transform)).Append(" (").Append(hit.collider.GetType().Name)
                      .Append(", shader ").Append(shader).Append(") at ").Append(hit.distance.ToString("0.0")).Append(" m");
                }
            }
            Plugin.Log.LogInfo(sb.ToString());
        }

        private static GameObject Owner(Transform t)
        {
            for (var p = t; p != null; p = p.parent) if (AdjustmentRunner.Find(p.gameObject, "ID") != null) return p.gameObject;
            return null;
        }

        private static string Path(Transform t)
        {
            var s = t.name;
            int n = 0;
            for (var p = t.parent; p != null && n < 6; p = p.parent, n++) s = p.name + "/" + s;
            return s;
        }
    }
}
