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

        private static readonly string[] names = { Left, Right, Down, Up, Reset };
        private static readonly KeyAction[] actions = new KeyAction[5];
        private static readonly KeyCode[] keys = new KeyCode[10];
        private static string hint;

        private static string Keys(KeyAction action)
        {
            if (action.Key == KeyCode.None)
                return action.AlternativeKey == KeyCode.None ? "Unbound" : action.AltKeyName;
            if (action.AlternativeKey == KeyCode.None || action.AlternativeKey == action.Key) return action.KeyName;
            return action.KeyName + " or " + action.AltKeyName;
        }

        internal static string Hint
        {
            get
            {
                for (int i = 0; i < names.Length; i++) actions[i] = InputController.GetKeyAction(names[i]);
                return HintFor(actions);
            }
        }

        internal static string HintFor(KeyAction[] currentActions)
        {
            bool changed = hint == null;
            for (int i = 0; i < names.Length; i++)
            {
                var action = currentActions[i];
                if (keys[i * 2] != action.Key || keys[i * 2 + 1] != action.AlternativeKey) changed = true;
                keys[i * 2] = action.Key;
                keys[i * 2 + 1] = action.AlternativeKey;
            }
            if (changed)
                hint = Keys(currentActions[0]) + " / " + Keys(currentActions[1]) + " - narrow / widen wheel spacing\n"
                    + Keys(currentActions[2]) + " / " + Keys(currentActions[3]) + " - lower / raise suspension\n"
                    + Keys(currentActions[4]) + " - reset suspension";
            return hint;
        }
    }
}
