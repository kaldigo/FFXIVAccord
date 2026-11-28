using Accord;

internal static class ActivityRunnerTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool valid, string message) { checks++; if (!valid) throw new Exception(message); }
        var policy = new ActivityRunnerPolicy();
        Check(policy.Observe(true, false, true, AutomaticRunner.Rsr) is null, "Do not take over a run already in progress on enable/reload");
        Check(policy.Observe(true, true, false, AutomaticRunner.Rsr) is null, "Stopped activity arms a later start without starting combat");
        Check(policy.Observe(true, false, true, AutomaticRunner.Rsr) == AutomaticRunner.Rsr, "Start requests the selected runner");
        Check(policy.Observe(true, false, true, AutomaticRunner.Rsr) is null, "Repeated active observations do not restart the engine");
        Check(policy.Observe(true, false, false, AutomaticRunner.Rsr) == AutomaticRunner.Off, "Leaving navigation stops automation");
        Check(policy.Observe(true, false, true, AutomaticRunner.Rsr) == AutomaticRunner.Rsr, "Next navigation segment passes through a fresh ownership handoff");
        Check(policy.Observe(false, false, true, AutomaticRunner.Rsr) == AutomaticRunner.Off, "Activity disconnect stops automation");
        Check(policy.Observe(true, false, true, AutomaticRunner.Rsr) is null, "Reconnect during a run cannot silently restart");
        policy.Observe(true, true, false, AutomaticRunner.Wrath);
        Check(policy.Observe(true, false, true, AutomaticRunner.Wrath) == AutomaticRunner.Wrath, "Wrath uses the same activity ownership path");
        policy.Reset();
        Check(policy.Observe(true, false, true, AutomaticRunner.Wrath) is null, "Manual override clears activity restart authority");
        return checks;
    }
}
