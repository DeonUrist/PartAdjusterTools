using System;

namespace PartAdjustment
{
    internal static class AdjustmentMath
    {
        internal static float Width(float value) => ClampFinite(value, 1f, 1.5f, 1f);
        internal static float Height(float value, float limit) => ClampFinite(value, -limit, limit, 0f);
        internal static float SpacedX(float original, float axleCenter, float width) => axleCenter + (original - axleCenter) * Width(width);
        internal static float EndpointOffset(float endpointFromModelPivot, float width) => endpointFromModelPivot * (Width(width) - 1f);
        private static float ClampFinite(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));
    }
}
