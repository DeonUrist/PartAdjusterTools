using System;
using System.Collections.Generic;
using System.Linq;
using ES3Internal;
using HutongGames.PlayMaker;
using NWH.WheelController3D;
using UnityEngine;

namespace PartAdjustment
{
    internal static class LegacySuspensionRecovery
    {
        internal static bool TryOffsets(Dictionary<string, object> saved, out float width, out float height)
        {
            width = 1f;
            height = 0f;
            object savedWidth, savedHeight;
            if (saved == null || !saved.TryGetValue(SaveVariables.Width, out savedWidth)
                || !saved.TryGetValue(SaveVariables.Height, out savedHeight)
                || !(savedWidth is float) || !(savedHeight is float)) return false;
            width = AdjustmentMath.Width((float)savedWidth);
            height = AdjustmentMath.Height((float)savedHeight, 1f);
            return true;
        }

        internal static bool MatchesLegacy(Vector3 current, Vector3 factory, Vector3 legacyBaseline,
            float axleCenter, float width, float height)
        {
            var displaced = new Vector3(AdjustmentMath.SpacedX(legacyBaseline.x, axleCenter, width),
                legacyBaseline.y + height, legacyBaseline.z);
            return !Near(current, factory) && Near(current, displaced);
        }

        private static bool Near(Vector3 a, Vector3 b) => Math.Abs(a.x - b.x) <= 0.0001f
            && Math.Abs(a.y - b.y) <= 0.0001f && Math.Abs(a.z - b.z) <= 0.0001f;

        internal static void Restore(Transform car, FsmVariables variables, Dictionary<string, object> saved)
        {
            float width, height;
            if (car == null || !TryOffsets(saved, out width, out height)) return;
            Transform model, hinge;
            WheelController[] wheels;
            if (SuspensionSupport.TryAssembly(car, out model, out hinge, out wheels)) return;
            // Unsupported vehicles must not retain mod fields on their next save.
            SaveVariables.Remove(variables);
            var instance = car.GetComponent<ES3Prefab>();
            var manager = ES3ReferenceMgrBase.Current;
            var prefab = instance == null || manager == null ? null : manager.GetPrefab(instance.prefabId, true);
            if (prefab == null || prefab.transform == car) return;

            Transform factoryModel, factoryHinge;
            WheelController[] factoryWheels;
            SuspensionSupport.TryAssembly(prefab.transform, out factoryModel, out factoryHinge, out factoryWheels);
            // Recovery is limited to the dormant assemblies that old versions misidentified.
            // Never infer factory coordinates from a disabled Suspension FSM.
            if (factoryModel == null || factoryHinge == null || factoryWheels.Length < 4
                || SuspensionSupport.BranchActive(factoryModel, prefab.transform)
                || SuspensionSupport.BranchActive(factoryHinge, prefab.transform)
                || factoryWheels.Any(w => SuspensionSupport.EnabledFsm(w.transform, "Suspension") != null)
                || wheels.Length != factoryWheels.Length
                || wheels.Any(w => SuspensionSupport.EnabledFsm(w.transform, "Suspension") != null)) return;

            int repaired = 0;
            foreach (string stateName in new[] { "stock", "lifted" })
            {
                var baselines = new Dictionary<WheelController, Vector3>();
                foreach (var source in factoryWheels)
                {
                    var fsm = source.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Suspension");
                    var state = fsm?.FsmStates.FirstOrDefault(s => s.Name == stateName);
                    // Read dormant actions only to recognize the previous bug, never as factory geometry.
                    var action = SuspensionSupport.MountAction(fsm == null ? null : SuspensionSupport.Actions(fsm.Fsm, state), true,
                        a => fsm.Fsm.GetOwnerDefaultTarget(a.gameObject) == source.gameObject);
                    if (action != null) baselines.Add(source, SuspensionSupport.Position(action, source.transform.localPosition));
                }
                if (baselines.Count != factoryWheels.Length) continue;
                foreach (var source in factoryWheels)
                {
                    var wheel = wheels.FirstOrDefault(w => w.name == source.name);
                    if (wheel == null) continue;
                    var baseline = baselines[source];
                    var opposite = baselines.Where(p => p.Key != source && Math.Sign(p.Value.x) != Math.Sign(baseline.x))
                        .OrderBy(p => Math.Abs(p.Value.z - baseline.z)).FirstOrDefault();
                    float center = opposite.Key == null ? 0f : (baseline.x + opposite.Value.x) * 0.5f;
                    if (!MatchesLegacy(wheel.transform.localPosition, source.transform.localPosition, baseline, center, width, height)) continue;
                    wheel.transform.localPosition = source.transform.localPosition;
                    repaired++;
                }
            }
            if (repaired > 0)
            {
                car.GetComponent<Rigidbody>()?.WakeUp();
                Plugin.Log.LogInfo("Recovered " + repaired + " legacy suspension mounts on " + car.name + ".");
            }
        }
    }
}
