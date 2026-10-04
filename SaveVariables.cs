using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;

namespace PartAdjustment
{
    internal static class SaveVariables
    {
        internal const string Width = "PartAdjustment_Width";
        internal const string Height = "PartAdjustment_Height";
        internal const string ModelPosition = "PartAdjustment_ModelPosition";
        internal const string ModelScale = "PartAdjustment_ModelScale";
        internal const string HingePosition = "PartAdjustment_HingePosition";

        internal static FsmFloat Float(FsmVariables variables, string name, float fallback)
        {
            var value = variables.FloatVariables.FirstOrDefault(v => v.Name == name);
            if (value != null) return value;
            value = new FsmFloat(name) { Value = fallback };
            variables.FloatVariables = variables.FloatVariables.Concat(new[] { value }).ToArray();
            variables.Reinitialize();
            return value;
        }

        internal static FsmVector3 Vector(FsmVariables variables, string name, Vector3 fallback)
        {
            var value = variables.Vector3Variables.FirstOrDefault(v => v.Name == name);
            if (value != null) return value;
            value = new FsmVector3(name) { Value = fallback };
            variables.Vector3Variables = variables.Vector3Variables.Concat(new[] { value }).ToArray();
            variables.Reinitialize();
            return value;
        }
    }
}
