using System;
using System.Globalization;
using System.IO;
using System.Linq;
using PartAdjustment;
using UnityEngine;

internal static class SuspensionWidthChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var fixtures = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "SuspensionWidthFixtures.csv"))
            .Skip(1).Select(line => line.Split(',')).ToArray();
        check(fixtures.Length == 72 && fixtures.Select(f => f[0]).Distinct().Count() == 9,
            "Mesh alignment covers all nine supported vehicles, four mounts and both variants");
        int reproduced = 0;
        foreach (bool transformedParent in new[] { false, true })
        foreach (var vehicle in fixtures.GroupBy(f => f[0]))
        {
            var car = new GameObject(vehicle.Key).transform;
            if (transformedParent)
            {
                car.localScale = new Vector3(2f, 3f, 4f);
                car.localRotation = Quaternion.AngleAxis(37f, Vector3.up) * Quaternion.AngleAxis(24f, Vector3.forward);
            }
            var model = new GameObject("suspension_model").transform; model.parent = car;
            var first = vehicle.First().Skip(3).Select(Number).ToArray();
            model.localPosition = new Vector3(first[3], first[4], first[5]);
            var factoryScale = new Vector3(first[6], 1f, 1f);
            // Construction after ES3 has restored an expanded model must still cache factory-local ends.
            model.localScale = new Vector3(factoryScale.x * 1.35f, 1f, 1f);
            foreach (string state in new[] { "stock", "lifted" })
            {
                var branch = new GameObject(state == "lifted" && vehicle.Key is "Poloska" or "PipeRat" or "TinyTyrant" ? "buggy" : state).transform;
                branch.parent = model;
                foreach (var row in vehicle.Where(f => f[2] == state).GroupBy(f => f[12]))
                {
                    var values = row.First().Skip(3).Select(Number).ToArray();
                    var axle = new GameObject(values[2] > 0 ? "front_suspension" : "rear_suspension"); axle.transform.parent = branch;
                    axle.AddComponent<MeshFilter>().sharedMesh = new Mesh { bounds = new Bounds
                    {
                        center = new Vector3((values[7] + values[8]) * .5f, 0f, values[9]),
                        extents = new Vector3((values[8] - values[7]) * .5f, .1f, .1f)
                    } };
                }
            }
            var geometry = new SuspensionWidthGeometry(model);
            foreach (var row in vehicle)
            {
                var values = row.Skip(3).Select(Number).ToArray();
                var baseline = new Vector3(values[0], values[1], values[2]);
                var wheel = new GameObject(row[1]).transform; wheel.parent = car;
                float endpoint = values[baseline.x < model.localPosition.x ? 7 : 8];
                var anchor = new Vector3(endpoint, 0f, values[9]);
                model.localScale = factoryScale;
                var factoryEnd = car.InverseTransformPoint(model.TransformPoint(anchor));
                var factoryGap = baseline - factoryEnd;
                foreach (float width in new[] { 1f, 1.025f, 1.25f, 1.5f, 1f })
                {
                    model.localScale = new Vector3(factoryScale.x * width, 1f, 1f);
                    check(geometry.TryOffset(wheel, baseline, row[2] == "lifted", factoryScale, width, out var delta),
                        vehicle.Key + " " + row[1] + " " + row[2] + " resolves matching axle geometry");
                    var changedEnd = car.InverseTransformPoint(model.TransformPoint(anchor));
                    check(Near(baseline + delta - changedEnd, factoryGap),
                        vehicle.Key + " preserves its factory wheel-to-end gap at width " + width);
                    check(Near(delta, (changedEnd - factoryEnd)), "Wheel displacement equals mesh-end displacement");
                    if (width == 1f) check(Near(delta, Vector3.zero), "Reset/load at standard width leaves factory mount unchanged");
                    if (!transformedParent && width == 1.5f)
                    {
                        float old = AdjustmentMath.SpacedX(baseline.x, 0f, width);
                        if (Math.Abs(old - baseline.x - delta.x) > .001f) reproduced++;
                    }
                }
            }
        }
        check(reproduced >= 48, "Old wheel-center formula measurably loses alignment across stock/lifted fixtures");
        var root = new GameObject("custom vehicle").transform;
        var unknown = new GameObject("unknown suspension").transform; unknown.parent = root;
        var mount = new GameObject("mount").transform; mount.parent = root;
        check(!new SuspensionWidthGeometry(unknown).TryOffset(mount, Vector3.right, false, new Vector3(1, 1, 1), 1.5f, out _),
            "Unrecognized custom meshes retain the caller's fallback");
    }
    private static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .000001f;
}
