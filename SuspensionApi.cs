using UnityEngine;

namespace PartAdjustment
{
    // 1.2.0: for other mods (Apocapatrol applies a car template's suspension width / height through this, so the values live in this
    // mod's per-car save data and the two never disagree). Public on purpose; reached by reflection, so keep the signatures.
    public static class SuspensionApi
    {
        // Sets the suspension width (1..1.5) and height (metres, +/-) of a vehicle. False: not a supported vehicle, or no lift kit on it.
        public static bool Set(GameObject car, float width, float height)
        {
            var adjustment = car != null ? SuspensionAdjustment.Ensure(car.transform) : null;
            return adjustment != null && adjustment.Set(width, height);
        }

        // The current values (1 / 0 when standard, unsupported or without a kit)
        public static bool Get(GameObject car, out float width, out float height)
        {
            width = 1f; height = 0f;
            var adjustment = car != null ? SuspensionAdjustment.Ensure(car.transform) : null;
            if (adjustment == null) return false;
            width = adjustment.Width; height = adjustment.Height;
            return true;
        }

        public static bool KitFitted(GameObject car)
        {
            var adjustment = car != null ? SuspensionAdjustment.Ensure(car.transform) : null;
            return adjustment != null && adjustment.KitFitted;
        }
    }
}
