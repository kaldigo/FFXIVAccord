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
            return new(response.Value<bool>("Success") && response.Value<int>("Version") == 1,
                response.Value<bool>("Owned"), response.Value<bool>("Allowed"), response.Value<bool>("Busy"), response.Value<string>("Build") ?? "", Running: response.Value<bool>("Running"));
        }
    }

    private readonly Endpoint wrath = new("WrathCombo", "WrathCombo.AutomationOwnership.v1");
    private readonly Endpoint rsr = new("RotationSolver", "RotationSolverReborn.AccordAutomationOwnership");
    private readonly AutomationHandoff handoff;
    private readonly ICallGateProvider<string, string> api = Svc.PluginInterface.GetIpcProvider<string, string>("Accord.AutomationRunner.v1");
    private readonly IDtrBarEntry entry = Svc.DtrBar.Get("Accord Auto rotation");
    private bool wrathAvailable, rsrAvailable, initialized, used;
    private bool dutyAvailable, dutyRunning, pvpPaused, waitingForCombatJob;
    private const string CombatJobStatus = "Waiting for a combat job";
    private bool RunnerPaused => pvpPaused || waitingForCombatJob;
    private long nextDiscovery, nextAttempt, nextActivityPoll;
    private string settingsError = "", lastStatus = "", activityStatus = "";
    private AccordSettings Settings => Plugin.Instance.Settings;
    private AutomaticRunner Selected => EngineAvailability.Resolve(Settings.AutomaticEngine, wrathAvailable, rsrAvailable);
    private string EngineLabel => Selected == AutomaticRunner.Off ? "Waiting for a compatible engine" : Selected.ToString();
    private AutomaticRunner Desired => Settings.EmergencyRotation ? AutomaticRunner.Off : AutomationPreference.Resolve(Settings.AutoRotationEnabled, Settings.FollowAutoDuty, dutyAvailable, dutyRunning, Selected, RunnerPaused);
    private string Status => pvpPaused && Settings.AutoRotationEnabled && handoff.IsCoordinating
        ? "Auto rotation paused in PvP." : waitingForCombatJob && Settings.AutoRotationEnabled && !Settings.EmergencyRotation
        ? CombatJobStatus : Settings.AutoRotationEnabled && Selected == AutomaticRunner.Off ? EngineLabel : settingsError.Length > 0 ? settingsError : handoff.Status;
    private void Discover() { wrathAvailable = wrath.Compatible(); rsrAvailable = rsr.Compatible(); }
    private readonly IDtrBarEntry backup = Svc.DtrBar.Get("Accord Wrath backup");
    private readonly ICallGateProvider<bool> backupArmed = Svc.PluginInterface.GetIpcProvider<bool>("Accord.EmergencyHotbarsArmed.v1");
    internal AutomationControl()
    {
        handoff = new(wrath, rsr);
        api.RegisterFunc(Request);
        backupArmed.RegisterFunc(() => Svc.Framework.IsInFrameworkUpdateThread && Settings.EmergencyRotation &&
            handoff.IsCoordinating && handoff.Requested == AutomaticRunner.Off && handoff.Owner == AutomaticRunner.Off);
        backup.Tooltip = "Toggle Wrath backup on your PvE rotation hotbar.";
        backup.OnClick = args => _ = Svc.Framework.RunOnTick(() =>
        {
            if (!Settings.EmergencyRotation)
            {
                try
                {
                    SetupController.Instance!.CheckEmergencyPlacement();
                }
                catch (Exception ex) { Svc.Chat.PrintError("Wrath backup unavailable. Check the Wrath testing version and your PvE rotation slots. " + ex.Message); return; }
            }
            Settings.EmergencyRotation = !Settings.EmergencyRotation;
            Svc.PluginInterface.SavePluginConfig(Settings);
            if (Settings.EmergencyRotation) { used = true; handoff.Request(AutomaticRunner.Off, Environment.TickCount64); }
            SetupController.Instance!.Run(() => SetupController.Instance.SyncEmergencyPlacement());
            nextAttempt = 0;
            AccordDiagnostics.DiagnosticLog.Event("Emergency.Mode", () => new { Enabled = Settings.EmergencyRotation });
        });
        entry.Shown = true;
        entry.Text = new SeStringBuilder().AddIcon(BitmapFontIcon.ElementLightning).AddText(" M").Build();
        entry.OnClick = _ => Queue(Settings.AutoRotationEnabled ? "off" : "on");
    }
    private void Start(long now)
    {
        used = true;
        handoff.Request(AutomaticRunner.Off, now);
        if (!handoff.IsCoordinating) { settingsError = handoff.Status; return; }
        try
        {
            Discover();
            if (Selected == AutomaticRunner.Off) throw new InvalidOperationException("Waiting for the selected Accord-compatible engine to load.");
            if (wrathAvailable)
            {
                SetupController.Instance!.ApplyAutomationSettings();
                WrathSettingsPanel.ApplyVerified();
            }
            if (Selected == AutomaticRunner.Rsr) RsrSettingsPanel.ApplyVerified();
            settingsError = "";
            handoff.Request(Selected, Environment.TickCount64);
        }
        catch (Exception ex)
        {
            handoff.Stop();
            settingsError = "Auto rotation is enabled; execution is waiting: " + ex.Message;
            AccordDiagnostics.DiagnosticLog.Event("Error.AutomationSettings", () => new { Error = ex.ToString() });
        }
    }
    internal void Tick()
    {
        long now = Environment.TickCount64;
        bool loading = HotbarLayoutStore.Loading;
        pvpPaused = AutomationPreference.PvPPaused(pvpPaused, Svc.ClientState.IsPvP || SetupController.IsPvP, loading || !Svc.ClientState.IsLoggedIn);
        uint job = ECommons.GameHelpers.Player.Available ? (uint)ECommons.GameHelpers.Player.Job : 0;
        waitingForCombatJob = AutomationPreference.WaitingForCombatJob(job);
        // Cancel even a pending start before renewing the lease could grant it.
        if (RunnerPaused && handoff.Requested != AutomaticRunner.Off)
            handoff.Request(AutomaticRunner.Off, now);
        if (now >= nextDiscovery) { nextDiscovery = now + 1000; Discover(); }
        if (!initialized && (wrathAvailable || rsrAvailable))
        {
            used = true;
            handoff.Request(AutomaticRunner.Off, now);
            initialized = handoff.IsCoordinating;
        }
        if (now >= nextActivityPoll)
        {
            nextActivityPoll = now + 500;
            dutyAvailable = dutyRunning = false;
            if (Settings.FollowAutoDuty)
            {
                try
                {
                    bool stopped = Svc.PluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped").InvokeFunc();
                    bool navigating = Svc.PluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsNavigating").InvokeFunc();
                    dutyAvailable = true;
                    dutyRunning = !stopped && navigating;
                    activityStatus = dutyRunning ? "Following AutoDuty navigation." : "Waiting for AutoDuty navigation.";
                }
                catch { activityStatus = "Waiting for AutoDuty to become available."; }
            }
            else activityStatus = "AutoDuty does not control when Accord runs the selected engine.";
        }
        bool verified = true;
        if (wrathAvailable) verified &= WrathSettingsPanel.Tick();
        if (rsrAvailable)
        {
            bool rsrVerified = RsrSettingsPanel.Tick();
            if (Selected == AutomaticRunner.Rsr) verified &= rsrVerified;
        }
        if (!verified && handoff.IsCoordinating && handoff.Requested != AutomaticRunner.Off)
        {
            handoff.Stop();
            settingsError = "Auto rotation is enabled; waiting for saved engine settings to be applied and verified.";
        }
        backup.Shown = wrathAvailable || Settings.EmergencyRotation;
        backup.Text = Settings.EmergencyRotation ? "Exit backup" : "Wrath backup";

        handoff.Tick(now);
        RecordStatus();
        // Saved intent is independent of temporary runner/IPC availability and diagnostic capture.
        var desired = Desired;
        if (desired == AutomaticRunner.Off)
        {
            if (handoff.IsCoordinating && handoff.Requested != AutomaticRunner.Off)
                handoff.Request(AutomaticRunner.Off, now);
            else if ((Settings.EmergencyRotation && wrathAvailable || RunnerPaused && (wrathAvailable || rsrAvailable)) && !handoff.IsCoordinating && now >= nextAttempt)
            {
                nextAttempt = now + 2000;
                handoff.Request(AutomaticRunner.Off, now);
            }
        }
        else if ((!handoff.IsCoordinating || handoff.Requested != desired) && now >= nextAttempt && !loading && ECommons.GameHelpers.Player.Available)
        {
            nextAttempt = now + 2000;
            Start(now);
        }
        RecordStatus();
        string status = Status;
        entry.Text = new SeStringBuilder().AddIcon(BitmapFontIcon.ElementLightning)
            .AddText(handoff.Owner != AutomaticRunner.Off ? " A" : Settings.AutoRotationEnabled ? " \u2026" : " M").Build();
        entry.Tooltip = (Settings.EmergencyRotation ? "Wrath backup active. Automatic execution is paused." : status) + "\nAuto rotation setting: " + (Settings.AutoRotationEnabled ? "on" : "off") + ". Selected engine: " + EngineLabel + ". Click to change the saved choice.";
    }
    private void RecordStatus()
    {
        string status = Status;
        if (status != lastStatus)
        {
            // Retain transitions even when temporary detailed capture is off.
            Svc.Log.Information("Accord automation: {Status}; saved enabled={Enabled}; engine={Engine}", status, Settings.AutoRotationEnabled, Selected);
            AccordDiagnostics.DiagnosticLog.Event("Automation.StateChanged", () => new { Status = status, Enabled = Settings.AutoRotationEnabled, Engine = Selected.ToString() });
            lastStatus = status;
        }
    }
    internal string Request(string command)
    {
        if (!Svc.Framework.IsInFrameworkUpdateThread) return "Use the framework thread.";
        command = command.ToLowerInvariant();
        if (command == "status") return Status;
        if (command is not ("on" or "off")) return "Choose on, off or status. Select the automatic engine in Accord.";
        Settings.AutoRotationEnabled = command == "on";
        Svc.PluginInterface.SavePluginConfig(Settings);
        AccordDiagnostics.DiagnosticLog.Event("Automation.Command", () => new { Command = command });
        nextAttempt = 0;
        settingsError = "";
        if (command == "off") { used = true; handoff.Request(AutomaticRunner.Off, Environment.TickCount64); }
        return command == "on" && pvpPaused ? "Auto rotation enabled and saved; execution is paused in PvP." :
            command == "on" && waitingForCombatJob ? CombatJobStatus :
            command == "on" ? "Auto rotation enabled and saved; waiting for the selected engine." : "Auto rotation disabled and saved.";
    }
    internal void Draw()
    {
        AccordControls.Runner(Settings.AutoRotationEnabled, Status, value => Queue(value ? "on" : "off"),
            DrawEngine, Settings.FollowAutoDuty, activityStatus, SetFollow);
    }
    private void DrawEngine()
    {
        string[] choices = (wrathAvailable, rsrAvailable) switch
        {
            (true, true) => ["Wrath", "RSR"],
            (true, false) => ["Wrath"],
            (false, true) => ["RSR"],
            _ => []
        };
        if (choices.Length == 0)
        {
            ImGui.TextDisabled("No compatible automation engine is loaded.");
            return;
        }
        int index = Array.IndexOf(choices, Selected == AutomaticRunner.Rsr ? "RSR" : "Wrath");
        if (Glasswork.Backend.Gw.ComboBox(ref index, choices, "Automation engine"))
            Select(choices[index] == "Wrath" ? AutomationEngine.Wrath : AutomationEngine.Rsr);
        if (Settings.AutomaticEngine is { } preferred && Selected != AutomaticRunner.Off && preferred.ToString() != Selected.ToString())
        {
            ImGui.TextWrapped($"Using {Selected} until your preferred engine ({preferred}) is available.");
            if (Glasswork.Backend.Gw.Button($"Prefer {Selected}"))
                Select(Selected == AutomaticRunner.Rsr ? AutomationEngine.Rsr : AutomationEngine.Wrath);
        }
    }
    private void SetFollow(bool value) => _ = Svc.Framework.RunOnTick(() =>
    {
        Settings.FollowAutoDuty = value;
        Svc.PluginInterface.SavePluginConfig(Settings);
        nextActivityPoll = nextAttempt = 0;
    });
    private void Select(AutomationEngine engine) => _ = Svc.Framework.RunOnTick(() =>
    {
        Settings.AutomaticEngine = engine;
        Svc.PluginInterface.SavePluginConfig(Settings);
        nextAttempt = 0;
    });
    private void Queue(string command) => _ = Svc.Framework.RunOnTick(() => Request(command));
    public void Dispose()
    {
        if (used) handoff.Stop();
        Svc.DtrBar.Remove("Accord Wrath backup");
        backupArmed.UnregisterFunc();
        api.UnregisterFunc();
        Svc.DtrBar.Remove("Accord Auto rotation");
    }
}
