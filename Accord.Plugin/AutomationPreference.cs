namespace Accord;
internal static class AutomationPreference
{
    internal static bool WaitingForCombatJob(uint job) => job is 0 or (>= 8 and <= 18);

    internal static bool PvPPaused(bool previouslyPaused, bool pvp, bool loading) => pvp || previouslyPaused && loading;

    internal static AutomaticRunner Resolve(bool enabled, AutomaticRunner selected, bool paused = false) =>
        enabled && !paused ? selected : AutomaticRunner.Off;
}

// AutoDuty supplies a start request, not permission for ordinary manual automation.
// Observe an idle run first so reloading during a run cannot undo saved manual off.
internal sealed class AutoDutyStart
{
    private bool armed, running;
    internal bool Observe(bool follow, bool? active)
    {
        if (!follow) { armed = running = false; return false; }
        if (active is null) return false; // An IPC interruption is not a new run.
        if (!active.Value) { armed = true; running = false; return false; }
        bool start = armed && !running;
        running = true;
        return start;
    }
}
