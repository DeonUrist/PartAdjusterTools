using BepInEx.Logging;

namespace PartAdjustment
{
    // Only the logger is needed when linking the recovery source into the offline verifier.
    internal static class Plugin
    {
        internal static readonly ManualLogSource Log = new ManualLogSource("Verifier");
    }
}
