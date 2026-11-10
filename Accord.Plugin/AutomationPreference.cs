namespace Accord;
internal static class AutomationPreference
{
    internal static bool WaitingForCombatJob(uint job) => job is 0 or (>= 8 and <= 18);

    internal static bool PvPPaused(bool previouslyPaused, bool pvp, bool loading) => pvp || previouslyPaused && loading;

    internal static AutomaticRunner Resolve(bool enabled, bool followAutoDuty, bool dutyAvailable, bool dutyRunning, AutomaticRunner selected, bool pvpPaused = false) =>
        enabled && !pvpPaused && (!followAutoDuty || dutyAvailable && dutyRunning) ? selected : AutomaticRunner.Off;
}
