namespace Accord;
internal static class AutomationPreference
{
    internal static AutomaticRunner Resolve(bool enabled, bool followAutoDuty, bool dutyAvailable, bool dutyRunning, AutomaticRunner selected) =>
        enabled && (!followAutoDuty || dutyAvailable && dutyRunning) ? selected : AutomaticRunner.Off;
}
