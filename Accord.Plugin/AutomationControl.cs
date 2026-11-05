using System;
using System.Linq;
using Dalamud.Game.Gui.Dtr;
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
            AccordDiagnostics.DiagnosticLog.Event("Handoff.Request", () => new { Endpoint = name, Command = command, Token = token });
            var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string, string>(name).InvokeFunc(request.ToString()));
            AccordDiagnostics.DiagnosticLog.Event("Handoff.Response", () => new { Endpoint = name, Response = response });
            return new(response.Value<bool>("Success") && response.Value<int>("Version") == 1 &&
                (!response.Value<bool>("Allowed") || response.Value<bool>("Running")),
                response.Value<bool>("Owned"), response.Value<bool>("Allowed"), response.Value<bool>("Busy"), response.Value<string>("Build") ?? "");
        }
    }

    private readonly AutomationHandoff handoff = new(new Endpoint("WrathCombo.AutomationOwnership.v1"), new Endpoint("RotationSolverReborn.AccordAutomationOwnership"));
    private readonly ICallGateProvider<string, string> api = Svc.PluginInterface.GetIpcProvider<string, string>("Accord.AutomationRunner.v1");
    private readonly IDtrBarEntry entry = Svc.DtrBar.Get("Accord Auto rotation");
    private long nextSettingsCheck;
    private string settingsError = "";
    private bool used;
    private bool initialized;
    private long nextInitialization;
    private readonly ActivityRunnerPolicy activity = new();
    private bool followAutoDuty;
    private bool activityRequested;
    private long nextActivityPoll;
    private string activityStatus = "AutoDuty following is off.";
    internal AutomationControl()
    {
        api.RegisterFunc(Request);
        entry.Shown = true;
        entry.Text = "Accord auto: Off";
        entry.OnClick = _ => Queue(handoff.IsCoordinating && handoff.Requested != AutomaticRunner.Off
            ? "off" : "on");
    }
    private void Start(long now)
    {
        used = initialized = true;
        handoff.Request(AutomaticRunner.Off, now);
        try
        {
            SetupController.Instance!.ApplyAutomationSettings();
            RsrSettingsPanel.ApplyVerified();
            settingsError = "";
            handoff.Request(AutomaticRunner.Rsr, Environment.TickCount64);
        }
        catch (Exception ex)
        {
            handoff.Stop();
            settingsError = "Automation stopped: " + ex.Message;
            AccordDiagnostics.DiagnosticLog.Event("Error.AutomationSettings", () => new { Error = ex.ToString() });
        }
    }
    internal void Tick()
    {
        long now = Environment.TickCount64;
        if (!initialized && now >= nextInitialization)
        {
            nextInitialization = now + 1000;
            var installed = Svc.PluginInterface.InstalledPlugins;
            if (installed.Any(p => p.InternalName == "WrathCombo" && p.IsLoaded) &&
                installed.Any(p => p.InternalName == "RotationSolver" && p.IsLoaded))
            {
                used = true;
                handoff.Request(AutomaticRunner.Off, now);
                initialized = handoff.IsCoordinating;
            }
        }
        if (handoff.Owner == AutomaticRunner.Rsr && now >= nextSettingsCheck)
        {
            nextSettingsCheck = now + 2000;
            try { RsrSettingsPanel.ApplyVerified(); }
            catch (Exception ex) { handoff.Stop(); settingsError = "Automation stopped: " + ex.Message; }
        }
        handoff.Tick(now);
        entry.Text = "Accord auto: " + (handoff.Owner != AutomaticRunner.Off ? handoff.Owner.ToString()
            : handoff.IsCoordinating && handoff.Requested != AutomaticRunner.Off ? "Starting" : "Off");
        entry.Tooltip = (settingsError.Length > 0 ? settingsError : handoff.Status) + "\nClick to toggle auto rotation. Wrath handles buttons; RSR handles automatic execution.";
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
        if (activity.Observe(available, stopped, navigating, AutomaticRunner.Rsr) is { } request)
        {
            used = true;
            activityRequested = true;
            if (request == AutomaticRunner.Off) handoff.Request(request, now); else Start(now);
        }
    }
    internal string Request(string command)
    {
        AccordDiagnostics.DiagnosticLog.Event("Automation.Command", () => new { Command = command });
        if (!Svc.Framework.IsInFrameworkUpdateThread) return "Use the framework thread.";
        if (command != "status") initialized = true;
        if (command.ToLowerInvariant() is "on" or "rsr" or "off")
        {
            followAutoDuty = false;
            activity.Reset();
            activityStatus = "AutoDuty following disabled by a direct automation command.";
        }
        switch (command.ToLowerInvariant())
        {
            case "on":
            case "rsr": Start(Environment.TickCount64); break;
            case "off": used = true; handoff.Request(AutomaticRunner.Off, Environment.TickCount64); break;
            case "status": break;
            default: return "Choose on, off or status. Accord uses Wrath for buttons and RSR for automation.";
        }
        return handoff.Status;
    }
    internal void Draw()
    {
        ImGui.TextWrapped("Wrath handles Accord's rotation buttons. Auto rotation hands automatic execution to RSR and stops Wrath's automatic runner. Turning it off returns to button-driven play. An engine reload or lost connection stops automation; enable it again explicitly to restart.");
        if (ImGui.Button("Enable auto rotation")) Queue("on");
        ImGui.SameLine(); if (ImGui.Button("Stop automation")) Queue("off");
        if (settingsError.Length > 0) ImGui.TextWrapped(settingsError);
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
        ImGui.TextWrapped(activityStatus);
        ImGui.TextWrapped("Direct runner/stop commands disable following. AutoDuty's route pause does not clear its navigation status: use Stop automation to stop combat as well. AutoDuty and BossMod retain movement control.");
        ImGui.Separator();
    }
    private void Queue(string command) => _ = Svc.Framework.RunOnTick(() => Request(command));
    public void Dispose()
    {
        if (used) handoff.Stop();
        api.UnregisterFunc();
        Svc.DtrBar.Remove("Accord Auto rotation");
    }
}
