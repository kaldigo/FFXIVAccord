using System;
using System.Linq;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using Newtonsoft.Json.Linq;

namespace Accord;

internal sealed class AutomationControl : IDisposable
{
    private sealed class Endpoint(string plugin, string name) : IRunnerEndpoint
    {
        internal bool Loaded => Svc.PluginInterface.InstalledPlugins.Any(p => p.InternalName == plugin && p.IsLoaded);
        internal bool Compatible()
        {
            if (!Loaded) return false;
            try
            {
                var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string, string>(name)
                    .InvokeFunc(new JObject { ["Command"] = "status" }.ToString()));
                return EngineAvailability.Compatible(Loaded, response);
            }
            catch { return false; }
        }
        public RunnerStatus Send(string command, Guid token)
        {
            if (!Loaded) return new(false, false, false, false, "", Absent: true);
            var request = new JObject { ["Command"] = command, ["Token"] = token.ToString() };
            AccordDiagnostics.DiagnosticLog.Event("Handoff.Request", () => new { Endpoint = name, Command = command, Token = token });
            var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string, string>(name).InvokeFunc(request.ToString()));
            AccordDiagnostics.DiagnosticLog.Event("Handoff.Response", () => new { Endpoint = name, Response = response });
            return new(response.Value<bool>("Success") && response.Value<int>("Version") == 1 &&
                (!response.Value<bool>("Allowed") || response.Value<bool>("Running")),
                response.Value<bool>("Owned"), response.Value<bool>("Allowed"), response.Value<bool>("Busy"), response.Value<string>("Build") ?? "");
        }
    }

    private readonly Endpoint wrath = new("WrathCombo", "WrathCombo.AutomationOwnership.v1");
    private readonly Endpoint rsr = new("RotationSolver", "RotationSolverReborn.AccordAutomationOwnership");
    private readonly AutomationHandoff handoff;
    private bool wrathAvailable, rsrAvailable;
    private long nextDiscovery;
    private AutomaticRunner Selected => Plugin.Instance.Settings.AutomaticEngine == AutomationEngine.Wrath ? AutomaticRunner.Wrath : AutomaticRunner.Rsr;
    private bool SelectedAvailable => Selected == AutomaticRunner.Wrath ? wrathAvailable : rsrAvailable;
    private void Discover()
    {
        wrathAvailable = wrath.Compatible();
        rsrAvailable = rsr.Compatible();
    }
    private readonly ICallGateProvider<string, string> api = Svc.PluginInterface.GetIpcProvider<string, string>("Accord.AutomationRunner.v1");
    private readonly IDtrBarEntry entry = Svc.DtrBar.Get("Accord Auto rotation");
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
        handoff = new(wrath, rsr);
        api.RegisterFunc(Request);
        entry.Shown = true;
        entry.Text = new SeStringBuilder().AddIcon(BitmapFontIcon.ElementLightning).AddText(" M").Build();
        entry.OnClick = _ => Queue(handoff.IsCoordinating && handoff.Requested != AutomaticRunner.Off
            ? "off" : "on");
    }
    private void Start(long now)
    {
        used = initialized = true;
        handoff.Request(AutomaticRunner.Off, now);
        if (!handoff.IsCoordinating) { settingsError = handoff.Status; return; }
        try
        {
            Discover();
            if (!SelectedAvailable) throw new InvalidOperationException("The selected Accord-compatible engine is not loaded or its API is unavailable.");
            if (wrathAvailable) SetupController.Instance!.ApplyAutomationSettings();
            if (Selected == AutomaticRunner.Rsr) RsrSettingsPanel.ApplyVerified();
            settingsError = "";
            handoff.Request(Selected, Environment.TickCount64);
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
        if (now >= nextDiscovery) { nextDiscovery = now + 1000; Discover(); }
        if (!initialized && now >= nextInitialization)
        {
            nextInitialization = now + 1000;
            if (wrathAvailable || rsrAvailable)
            {
                used = true;
                handoff.Request(AutomaticRunner.Off, now);
                initialized = handoff.IsCoordinating;
            }
        }
        if (rsrAvailable && !RsrSettingsPanel.Tick() && handoff.Requested == AutomaticRunner.Rsr && handoff.IsCoordinating)
        {
            handoff.Stop();
            settingsError = "Automation stopped because Accord could not verify RSR settings. Settings will retry automatically; enable auto rotation again after recovery.";
        }
        handoff.Tick(now);
        entry.Text = new SeStringBuilder().AddIcon(BitmapFontIcon.ElementLightning)
            .AddText(handoff.Owner != AutomaticRunner.Off ? " A"
                : handoff.IsCoordinating && handoff.Requested != AutomaticRunner.Off ? " \u2026" : " M").Build();
        entry.Tooltip = (settingsError.Length > 0 ? settingsError : handoff.Status) + "\nA: automatic rotation. M: manual buttons. Click to toggle. Selected automatic engine: " + Selected + ". Wrath handles rotation buttons.";
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
        if (activity.Observe(available, stopped, navigating, Selected) is { } request)
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
        if (command.ToLowerInvariant() is "on" or "off")
        {
            followAutoDuty = false;
            activity.Reset();
            activityStatus = "AutoDuty following disabled by a direct automation command.";
        }
        switch (command.ToLowerInvariant())
        {
            case "on":
                Start(Environment.TickCount64); break;
            case "off": used = true; handoff.Request(AutomaticRunner.Off, Environment.TickCount64); break;
            case "status": break;
            default: return "Choose on, off or status. Select the automatic engine in Accord.";
        }
        return handoff.Status;
    }
    internal void Draw()
    {
        ImGui.TextWrapped("Wrath handles Accord's rotation buttons. Choose the automatic engine for testing; only loaded Accord-compatible forks are offered. Switching engines stops the old runner before starting the new one. A missing or reloaded engine stops automation without silently selecting another.");
        if (ImGui.BeginCombo("Automation engine", SelectedAvailable ? Selected.ToString() : "Select an available engine"))
        {
            if (wrathAvailable && ImGui.Selectable("Wrath", Selected == AutomaticRunner.Wrath)) Select(AutomationEngine.Wrath);
            if (rsrAvailable && ImGui.Selectable("RSR", Selected == AutomaticRunner.Rsr)) Select(AutomationEngine.Rsr);
            if (!wrathAvailable && !rsrAvailable) ImGui.TextDisabled("No compatible engine is loaded.");
            ImGui.EndCombo();
        }
        ImGui.TextDisabled(rsrAvailable ? "RSR available for future learning mode (sprout)." : "Future learning mode requires the Accord-compatible RSR fork.");
        bool enabled = handoff.IsCoordinating && handoff.Requested != AutomaticRunner.Off;
        ImGui.BeginDisabled(!enabled && !SelectedAvailable);
        if (ImGui.Checkbox("Auto rotation", ref enabled)) Queue(enabled ? "on" : "off");
        ImGui.EndDisabled();
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
        ImGui.TextWrapped("Direct runner/stop commands disable following. AutoDuty's route pause does not clear its navigation status: uncheck Auto rotation to stop combat as well. AutoDuty and BossMod retain movement control.");
        ImGui.Separator();
    }
    private void Select(AutomationEngine engine) => _ = Svc.Framework.RunOnTick(() =>
    {
        bool restart = handoff.IsCoordinating && handoff.Requested != AutomaticRunner.Off;
        Plugin.Instance.Settings.AutomaticEngine = engine;
        Svc.PluginInterface.SavePluginConfig(Plugin.Instance.Settings);
        if (restart) Start(Environment.TickCount64);
    });
    private void Queue(string command) => _ = Svc.Framework.RunOnTick(() => Request(command));
    public void Dispose()
    {
        if (used) handoff.Stop();
        api.UnregisterFunc();
        Svc.DtrBar.Remove("Accord Auto rotation");
    }
}
