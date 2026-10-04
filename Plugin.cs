using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PartAdjustment
{
    [BepInPlugin(Guid, "Part Adjustment", "1.0.4")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.denis.apocalypter.partadjustment";
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> WidthStep, HeightStep, HeightLimit;
        private static GameObject runner;
        private void Awake()
        {
            Log = Logger;
            Config.Bind("General", "Apocasetter", true, "Show this mod in the Apocasetter Mods menu.");
            Enabled = Config.Bind("General", "Enabled", true, "Enable suspension selection and adjustment. Existing saved adjustments remain on the vehicle.");
            WidthStep = Config.Bind("Adjustment", "WidthStep", 0.025f, new ConfigDescription("Width multiplier changed per numpad 4/6 press (range is always 1.0 to 1.5).", new AcceptableValueRange<float>(0.005f, 0.1f)));
            HeightStep = Config.Bind("Adjustment", "HeightStep", 0.025f, new ConfigDescription("Suspension and wheel mount movement in metres per numpad 2/8 press.", new AcceptableValueRange<float>(0.005f, 0.1f)));
            HeightLimit = Config.Bind("Adjustment", "HeightLimit", 0.5f, new ConfigDescription("Maximum vertical offset in either direction, in metres, relative to the standard mounts.", new AcceptableValueRange<float>(0.05f, 1f)));
            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            SceneManager.sceneLoaded += (scene, mode) => EnsureRunner();
            EnsureRunner();
            Logger.LogInfo("Part Adjustment loaded.");
        }
        private static void EnsureRunner()
        {
            if (runner != null) return;
            runner = new GameObject("PartAdjustment.Runner") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(runner);
            runner.AddComponent<ToolRunner>();
        }
    }
}
