namespace PartAdjustment
{
    internal struct ToolVisibilityState
    {
        private bool initialized, toolOn;
        private int revision;

        internal bool Changed(bool requested, int currentRevision)
        {
            if (initialized && toolOn == requested && revision == currentRevision) return false;
            initialized = true;
            toolOn = requested;
            revision = currentRevision;
            return true;
        }
    }
}
