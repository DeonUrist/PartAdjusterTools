using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PartAdjustment
{
    [BepInPlugin(Guid, "Part Adjustment", "1.3.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.denis.apocalypter.partadjustment";
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> WidthStep, HeightStep, HeightLimit;
        internal static ConfigEntry<bool> LiftCenterOfMass, StandstillDamping;
        internal static ConfigEntry<int> ReferenceFrame;
        internal static ConfigEntry<float> MovementStep, RotationAngle, FastMovementStep, FastRotationAngle;
        internal static ConfigEntry<KeyCode> ModifierKey, ModifierAlternative;
        private static GameObject runner;
        private void Awake()
        {
            Log = Logger;
            Config.Bind("General", "Apocasetter", true, "Show this mod in the Apocasetter Mods menu.");
            Enabled = Config.Bind("General", "Enabled", true, "Enable suspension and attached-item adjustment with the Part Adjustment tool. Existing saved poses remain on vehicles.");
            WidthStep = Config.Bind("Adjustment", "WidthStep", 0.025f, new ConfigDescription("Width multiplier changed per press of the game's Adjust left/right controls (range is always 1.0 to 1.5).", new AcceptableValueRange<float>(0.005f, 0.1f)));
            HeightStep = Config.Bind("Adjustment", "HeightStep", 0.025f, new ConfigDescription("Suspension and wheel mount movement in metres per press of the game's Adjust down/up controls.", new AcceptableValueRange<float>(0.005f, 0.1f)));
            HeightLimit = Config.Bind("Adjustment", "HeightLimit", 0.5f, new ConfigDescription("Maximum vertical offset in either direction, in metres, relative to the standard mounts.", new AcceptableValueRange<float>(0.05f, 1f)));
            LiftCenterOfMass = Config.Bind("Adjustment", "LiftCenterOfMass", true, "A lifted suspension (body raised) lowers the car's centre of mass by the lift, so a tall car on big wheels does not flip in turns.");
            StandstillDamping = Config.Bind("Adjustment", "StandstillDamping", true, "A parked car (under 0.3 m/s, no engine torque on the wheels) is held still - horizontal position and roll/pitch frozen until the engine drives it, something hits it or it moves - so a lifted chassis on heavy wheels does not shake in place (the game's tyre model reverses any sideways motion every physics step at those loads).");
            ReferenceFrame = Config.Bind("Items", "ReferenceFrame", 0, new ConfigDescription("0 = camera axes, including pitch and roll. 1 = world axes. Applies to item movement/rotation and held-item rotation while the adjustment tool is selected.", new AcceptableValueRange<int>(0, 1)));
            MovementStep = ItemStep("MovementStep", 0.01f, 0.001f, 1f, "Metres per item movement key press.");
            RotationAngle = ItemStep("RotationAngle", 1f, 0.1f, 90f, "Degrees per item rotation key press.");
            FastMovementStep = ItemStep("FastMovementStep", 0.1f, 0.001f, 2f, "Metres per item movement key press with the modifier held.");
            FastRotationAngle = ItemStep("FastRotationAngle", 10f, 0.1f, 180f, "Degrees per item rotation key press with the modifier held.");
            ModifierKey = Config.Bind("Items", "ModifierKey", KeyCode.LeftAlt, "Hold for faster item movement/rotation. None disables this binding.");
            ModifierAlternative = Config.Bind("Items", "ModifierAlternative", KeyCode.None, "Optional alternative fast-step modifier. Item toggle and Adjust controls use the game's primary/alternate bindings.");
            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            SceneManager.sceneLoaded += (scene, mode) => { EnsureRunner(); ToolRunner.SceneChanged(); AdjustmentRunner.SceneChanged(); };
            EnsureRunner();
            Logger.LogInfo("Part Adjustment loaded.");
        }
        private static void EnsureRunner()
        {
            if (runner != null && runner.activeInHierarchy) return;
            if (runner != null) Destroy(runner);
            runner = new GameObject("PartAdjustment.Runner") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(runner);
            runner.AddComponent<ToolRunner>();
            runner.AddComponent<AdjustmentRunner>();
            runner.AddComponent<AdjustmentLateRunner>();
            runner.AddComponent<DriverContacts>();
            runner.AddComponent<StandstillDamperRunner>();
        }

        private ConfigEntry<float> ItemStep(string name, float value, float min, float max, string description) =>
            Config.Bind("Items", name, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));
    }
}
