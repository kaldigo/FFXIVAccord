namespace Accord;

// An activity observation may request ownership, but cannot grant it. The handoff
// still performs both stops, queue settlement and verification.
internal sealed class ActivityRunnerPolicy
{
    private bool armed;
    private bool requested;
    private bool navigating;
    internal AutomaticRunner? Observe(bool available, bool stopped, bool moving, AutomaticRunner runner)
    {
        if (!available)
        {
            bool stop = requested;
            Reset();
            return stop ? AutomaticRunner.Off : null;
        }
        if (stopped)
        {
            bool stop = requested;
            armed = true;
            requested = navigating = false;
            return stop ? AutomaticRunner.Off : null;
        }
        if (!armed) return null; // Enabling/reloading during an existing run is not a start request.
        if (moving && !navigating)
        {
            navigating = requested = true;
            return runner;
        }
        if (!moving && navigating)
        {
            navigating = false;
            requested = false;
            return AutomaticRunner.Off;
        }
        return null;
    }
    internal void Reset() => armed = requested = navigating = false;
}
