using System;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using Newtonsoft.Json.Linq;

namespace Accord;

internal sealed class AutomationControl : IDisposable
{
    private sealed class Endpoint(string name) : IRunnerEndpoint
    {
        public RunnerStatus Send(string command, Guid token)
        {
            var request = new JObject { ["Command"] = command, ["Token"] = token.ToString() };
            var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string, string>(name).InvokeFunc(request.ToString()));
            return new(response.Value<bool>("Success") && response.Value<int>("Version") == 1,
                response.Value<bool>("Owned"), response.Value<bool>("Allowed"), response.Value<bool>("Busy"), response.Value<string>("Build") ?? "");
        }
    }

    private readonly AutomationHandoff handoff = new(new Endpoint("WrathCombo.AutomationOwnership.v1"), new Endpoint("RotationSolverReborn.AccordAutomationOwnership"));
    private readonly ICallGateProvider<string, string> api = Svc.PluginInterface.GetIpcProvider<string, string>("Accord.AutomationRunner.v1");
    private bool used;
    private readonly ActivityRunnerPolicy activity = new();
    private bool followAutoDuty;
    private bool activityRsr = true;
    private bool activityRequested;
    private long nextActivityPoll;
    private string activityStatus = "AutoDuty following is off.";
    internal AutomationControl() => api.RegisterFunc(Request);
    internal void Tick()
    {
        long now = Environment.TickCount64;
        handoff.Tick(now);
        if (followAutoDuty && activityRequested && !handoff.IsCoordinating)
        {
            followAutoDuty = false;
            activity.Reset();
            activityStatus = "AutoDuty following stopped after a handoff failure. Enable it again explicitly.";
        }
        if (!followAutoDuty || now < nextActivityPoll) return;
        nextActivityPoll = now + 500;
        bool available = false, stopped = true, navigating = false;
        try
        {
            stopped = Svc.PluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped").InvokeFunc();
            navigating = Svc.PluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsNavigating").InvokeFunc();
            available = true;
            activityStatus = stopped ? "Waiting for AutoDuty to start." : navigating ? "Following the AutoDuty run." : "AutoDuty navigation is inactive.";
        }
        catch { activityStatus = "AutoDuty unavailable; no automatic restart until a stopped state is observed."; }
        if (activity.Observe(available, stopped, navigating, activityRsr ? AutomaticRunner.Rsr : AutomaticRunner.Wrath) is { } request)
        {
            used = true;
            activityRequested = true;
            handoff.Request(request, now);
        }
    }
    internal string Request(string command)
    {
        if (!Svc.Framework.IsInFrameworkUpdateThread) return "Use the framework thread.";
        if (command.ToLowerInvariant() is "wrath" or "rsr" or "off" or "release")
        {
            followAutoDuty = false;
            activity.Reset();
            activityStatus = "AutoDuty following disabled by a direct automation command.";
        }
        switch (command.ToLowerInvariant())
        {
            case "wrath": used = true; handoff.Request(AutomaticRunner.Wrath, Environment.TickCount64); break;
            case "rsr": used = true; handoff.Request(AutomaticRunner.Rsr, Environment.TickCount64); break;
            case "off": used = true; handoff.Request(AutomaticRunner.Off, Environment.TickCount64); break;
            case "release": handoff.Release(Environment.TickCount64); used = false; break;
            case "status": break;
            default: return "Choose wrath, rsr, off, release or status.";
        }
        return handoff.Status;
    }
    internal void Draw()
    {
        ImGui.TextWrapped("Testing automation handoff. Requires the matching Wrath and RSR testing builds. Manual Wrath buttons remain available. An engine reload or lost connection stops automation; select a runner again to restart.");
        if (ImGui.Button("Wrath automation")) Queue("wrath");
        ImGui.SameLine(); if (ImGui.Button("RSR automation")) Queue("rsr");
        ImGui.SameLine(); if (ImGui.Button("Stop automation")) Queue("off");
        if (ImGui.Button("Return control to each engine")) Queue("release");
        ImGui.TextWrapped(handoff.Status);
        bool follow = followAutoDuty;
        if (ImGui.Checkbox("Follow AutoDuty navigation (this session)", ref follow))
        {
            bool enable = follow;
            _ = Svc.Framework.RunOnTick(() =>
            {
                followAutoDuty = enable;
                activityRequested = false;
                activity.Reset();
                if (!enable && used) handoff.Request(AutomaticRunner.Off, Environment.TickCount64);
                activityStatus = enable ? "Start AutoDuty after enabling this option." : "AutoDuty following is off.";
            });
        }
        ImGui.BeginDisabled(followAutoDuty);
        ImGui.Checkbox("Use RSR for AutoDuty (otherwise Wrath)", ref activityRsr);
        ImGui.EndDisabled();
        ImGui.TextWrapped(activityStatus);
        ImGui.TextWrapped("Direct runner/stop commands disable following. AutoDuty's route pause does not clear its navigation status: use Stop automation to stop combat as well. AutoDuty and BossMod retain movement control.");
        ImGui.Separator();
    }
    private void Queue(string command) => _ = Svc.Framework.RunOnTick(() => Request(command));
    public void Dispose()
    {
        if (used) handoff.Stop();
        api.UnregisterFunc();
    }
}
