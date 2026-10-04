using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PartAdjustment
{
    [HarmonyPatch(typeof(SendEvent), nameof(SendEvent.OnEnter))]
    internal static class ToolEventPatch
    {
        private static bool Prefix(SendEvent __instance)
        {
            if (__instance.Fsm.Name != "UseAdjustTool" || __instance.eventTarget?.fsmName?.Value != "AdjustHinge") return true;
            if (__instance.sendEvent?.Name == "Activate")
            {
                var picked = __instance.Fsm.Variables.GetFsmGameObject("Hinge")?.Value;
                if (!ToolRunner.Begin(__instance.Fsm, picked)) return true;
                __instance.Finish();
                return false;
            }
            if (__instance.sendEvent?.Name == "Deactivate" && ToolRunner.Active != null)
            {
                ToolRunner.Active = null;
                __instance.Finish();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SetPosition), "DoSetPosition")]
    internal static class WheelMountPatch
    {
        private static void Postfix(SetPosition __instance)
        {
            if (__instance.Fsm.Name != "Suspension") return;
            var owner = __instance.Fsm.Owner;
            if (owner == null || !owner.name.StartsWith("hinge_wheel_")) return;
            var adjustment = owner.transform.parent?.GetComponent<SuspensionAdjustment>();
            if (adjustment != null) adjustment.VanillaMountChanged(owner.transform);
        }
    }

    // The game's per-car saveItemVar SaveAll/LoadAll already follows the active slot, cache and vehicle ID.
    // Extend its variable set rather than writing to a separate file or guessing which save slot is active.
    [HarmonyPatch(typeof(ES3PlayMaker.SaveAll), nameof(ES3PlayMaker.SaveAll.Enter))]
    internal static class SavePatch
    {
        private static void Prefix(ES3PlayMaker.SaveAll __instance)
        {
            if (__instance.Fsm.Name != "saveItemVar") return;
            try
            {
                var adjustment = SuspensionAdjustment.Ensure(__instance.Fsm.Owner?.transform);
                if (adjustment != null) adjustment.WriteSaveVariables(__instance.Fsm.Variables);
            }
            catch (Exception e) { Plugin.Log.LogError("Could not save suspension adjustment: " + e); }
        }
    }

    [HarmonyPatch(typeof(ES3PlayMaker.LoadAll), nameof(ES3PlayMaker.LoadAll.Enter))]
    internal static class LoadPatch
    {
        private static void Prefix(ES3PlayMaker.LoadAll __instance, out SuspensionAdjustment __state)
        {
            __state = null;
            if (__instance.Fsm.Name != "saveItemVar") return;
            try
            {
                __state = SuspensionAdjustment.Ensure(__instance.Fsm.Owner?.transform);
                if (__state != null) __state.PrepareLoad(__instance.Fsm.Variables);
            }
            catch (Exception e) { Plugin.Log.LogError("Could not prepare suspension load: " + e); }
        }
        private static void Postfix(ES3PlayMaker.LoadAll __instance, SuspensionAdjustment __state)
        {
            if (__state == null) return;
            try { __state.ReadSaveVariables(__instance.Fsm.Variables); }
            catch (Exception e) { Plugin.Log.LogError("Could not restore suspension adjustment: " + e); }
        }
    }

    [HarmonyPatch(typeof(ES3Types.ES3Type_GameObject), nameof(ES3Types.ES3Type_GameObject.GetChildren))]
    internal static class TransientPickTargetsPatch
    {
        private static void Postfix(List<GameObject> __result)
        {
            // A setup that enables ES3 child saving must never persist generated selection proxies or braces.
            __result.RemoveAll(go => go != null && (go.GetComponent<SuspensionPickTarget>() != null || go.GetComponent<SuspensionBrace>() != null));
        }
    }
}
