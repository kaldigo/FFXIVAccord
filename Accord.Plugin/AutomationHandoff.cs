using System;

namespace Accord;

internal enum AutomaticRunner { Off, Wrath, Rsr }
internal readonly record struct RunnerStatus(bool Success, bool Owned, bool Allowed, bool Busy, string Build, bool Absent = false);
internal interface IRunnerEndpoint
{
    RunnerStatus Send(string command, Guid token);
}

/// <summary>Framework-thread coordinator. Endpoints must gate every unattended executor;
/// granting authority alone does not imply that the native runner is active.</summary>
internal sealed class AutomationHandoff(IRunnerEndpoint wrath, IRunnerEndpoint rsr)
{
    private Guid token;
    private long started;
    private long lastTick;
    private string wrathBuild = "", rsrBuild = "";
    private bool wrathAbsent, rsrAbsent;
    private bool waiting;
    private bool active;
    internal bool IsCoordinating => active;
    internal AutomaticRunner Requested { get; private set; }
    internal AutomaticRunner Owner { get; private set; }
    internal string Status { get; private set; } = "Not controlling automation.";

    internal void Request(AutomaticRunner runner, long now)
    {
        if (!Enum.IsDefined(runner)) throw new ArgumentOutOfRangeException(nameof(runner));
        token = Guid.NewGuid();
        Requested = runner;
        Owner = AutomaticRunner.Off;
        active = waiting = false;
        started = lastTick = now;
        // Attempt both stops even if one peer is absent or throws.
        var first = TrySend(wrath, "block");
        var second = TrySend(rsr, "block");
        if (!Stopped(first) || !Stopped(second) ||
            runner == AutomaticRunner.Wrath && first.Absent || runner == AutomaticRunner.Rsr && second.Absent)
        {
            Fail("Unable to confirm the selected engine is available and all loaded runners are stopped.");
            return;
        }
        wrathAbsent = first.Absent; rsrAbsent = second.Absent;
        wrathBuild = first.Build;
        rsrBuild = second.Build;
        active = true;
        waiting = runner != AutomaticRunner.Off;
        Status = waiting ? "Waiting for the current cast or queued action to finish." : "Automation stopped.";
    }

    internal void Tick(long now)
    {
        if (!active) return;
        if (now < lastTick || now - lastTick >= 3000)
        {
            Fail("Automation stopped after the controller heartbeat was interrupted.");
            return;
        }
        if (now - lastTick < 500) return;
        lastTick = now;
        var first = TrySend(wrath, "renew");
        var second = TrySend(rsr, "renew");
        if (!Valid(first) || !Valid(second) || first.Build != wrathBuild || second.Build != rsrBuild || first.Absent != wrathAbsent || second.Absent != rsrAbsent)
        {
            Fail("Automation stopped because an engine disconnected or changed.");
            return;
        }
        if (!waiting)
        {
            if (first.Allowed != (Owner == AutomaticRunner.Wrath) || second.Allowed != (Owner == AutomaticRunner.Rsr))
                Fail("Automation stopped because runner ownership changed unexpectedly.");
            return;
        }
        if (first.Allowed || second.Allowed)
        {
            Fail("A runner became enabled before the handoff completed.");
            return;
        }
        if (now - started >= 15000)
        {
            Fail("Automation remains stopped: the current action did not settle within 15 seconds.");
            return;
        }
        if (first.Busy || second.Busy) return;
        var result = TrySend(Requested == AutomaticRunner.Wrath ? wrath : rsr, "start");
        if (!Valid(result) || result.Absent || !result.Allowed)
        {
            Fail("The requested runner could not start; automation remains stopped.");
            return;
        }
        // Verify both sides after the mutation, including a peer which may have reloaded.
        first = TrySend(wrath, "status");
        second = TrySend(rsr, "status");
        if (!Valid(first) || !Valid(second) || first.Build != wrathBuild || second.Build != rsrBuild || first.Absent != wrathAbsent || second.Absent != rsrAbsent ||
            first.Allowed != (Requested == AutomaticRunner.Wrath) || second.Allowed != (Requested == AutomaticRunner.Rsr))
        {
            Fail("The handoff could not be verified; automation remains stopped.");
            return;
        }
        Owner = Requested;
        waiting = false;
        Status = Owner + " owns automatic execution.";
    }

    internal void Stop() => Fail("Automation stopped.");
    internal void Release(long now)
    {
        Request(AutomaticRunner.Off, now);
        if (!active) return;
        var first = TrySend(wrath, "release");
        var second = TrySend(rsr, "release");
        if ((!first.Absent && !first.Success) || (!second.Absent && !second.Success))
        {
            Fail("Unable to release both engines; coordination remains stopped.");
            return;
        }
        active = false;
        Status = "Coordination disabled. Each engine now uses its own controls.";
    }
    private static bool Valid(RunnerStatus value) => value.Absent || value.Success && value.Owned && !string.IsNullOrEmpty(value.Build);
    private static bool Stopped(RunnerStatus value) => Valid(value) && !value.Allowed;
    private RunnerStatus TrySend(IRunnerEndpoint endpoint, string command)
    {
        try { return endpoint.Send(command, token); }
        catch { return default; }
    }
    private void Fail(string reason)
    {
        active = waiting = false;
        Owner = AutomaticRunner.Off;
        // Both gates expire closed if a disconnected endpoint cannot acknowledge this stop.
        TrySend(wrath, "block");
        TrySend(rsr, "block");
        Status = reason;
    }
}
