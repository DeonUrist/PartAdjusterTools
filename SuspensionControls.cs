using InsaneSystems.InputManager;
using UnityEngine;

namespace PartAdjustment
{
    internal static class SuspensionControls
    {
        // Exact action names used by the vanilla AdjustHinge movement state.
        internal const string Left = "Adjust-move left/rotate left";
        internal const string Right = "Adjust-move right/rotate right";
        internal const string Down = "Adjust-move down/rotate backward";
        internal const string Up = "Adjust-move up/rotate forward";
        internal const string Reset = "Adjust-reset";

        internal static bool Pressed(string action) => InputController.GetKeyActionIsDown(action);

        private static string Keys(string actionName)
        {
            var action = InputController.GetKeyAction(actionName);
            if (action.Key == KeyCode.None)
                return action.AlternativeKey == KeyCode.None ? "Unbound" : action.AltKeyName;
            if (action.AlternativeKey == KeyCode.None || action.AlternativeKey == action.Key) return action.KeyName;
            return action.KeyName + " or " + action.AltKeyName;
        }

        internal static string Hint =>
            Keys(Left) + " / " + Keys(Right) + " - narrow / widen wheel spacing\n"
            + Keys(Down) + " / " + Keys(Up) + " - lower / raise suspension\n"
            + Keys(Reset) + " - reset suspension";
    }
}
