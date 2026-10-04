using InsaneSystems.InputManager;
using System.Collections.Generic;
using UnityEngine;

namespace PartAdjustment
{
    internal static class Controls
    {
        private sealed class Binding { internal KeyCode Primary, Alternative; internal string Text; }
        private static readonly Dictionary<string, Binding> bindings = new Dictionary<string, Binding>();
        internal const string Secondary = "Use_2", Use = "Use";
        internal const string Left = "Adjust-move left/rotate left", Right = "Adjust-move right/rotate right";
        internal const string Up = "Adjust-move up/rotate forward", Down = "Adjust-move down/rotate backward";
        internal const string Forward = "Adjust-move forward/tilt left", Backward = "Adjust-move backward/tilt right";
        internal const string Mode = "Adjust-change move/rotate mode", Reset = "Adjust-reset";

        // Read the binding directly so our native input suppression does not suppress our own toggle.
        internal static bool Pressed(string name) => InputController.GetKeyAction(name).IsDown();
        internal static string Keys(string name)
        {
            var action = InputController.GetKeyAction(name);
            if (bindings.TryGetValue(name, out var cached) && cached.Primary == action.Key && cached.Alternative == action.AlternativeKey) return cached.Text;
            string text = action.Key == KeyCode.None ? (action.AlternativeKey == KeyCode.None ? "Unbound" : Label(action.AlternativeKey))
                : Label(action.Key) + (action.AlternativeKey == KeyCode.None || action.AlternativeKey == action.Key ? "" : " / " + Label(action.AlternativeKey));
            bindings[name] = new Binding { Primary = action.Key, Alternative = action.AlternativeKey, Text = text };
            return text;
        }
        internal static string Label(KeyCode key) => key.ToString().Replace("Keypad", "Num ");
        internal static bool Fast => Held(Plugin.ModifierKey.Value) || Held(Plugin.ModifierAlternative.Value);
        private static bool Held(KeyCode key) => key != KeyCode.None && Input.GetKey(key);

        internal static Vector3 Direction(bool rotating)
        {
            float horizontal = (Pressed(Right) ? 1 : 0) - (Pressed(Left) ? 1 : 0);
            float vertical = (Pressed(Up) ? 1 : 0) - (Pressed(Down) ? 1 : 0);
            float depth = (Pressed(Forward) ? 1 : 0) - (Pressed(Backward) ? 1 : 0);
            // Match the vanilla tool's pitch, yaw and tilt signs.
            return rotating ? new Vector3(vertical, -horizontal, depth) : new Vector3(horizontal, vertical, depth);
        }
        internal static float Step(bool rotating, bool fast)
        {
            float value = rotating ? (fast ? Plugin.FastRotationAngle.Value : Plugin.RotationAngle.Value)
                : (fast ? Plugin.FastMovementStep.Value : Plugin.MovementStep.Value);
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? (rotating ? 1f : 0.01f) : value;
        }
    }
}
