using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using AccordDiagnostics;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace Accord;
internal static class DiagnosticCapturePanel
{
    private static DiagnosticEndpoint? local;
    private static readonly string[] endpoints = ["Accord", "WrathCombo.Diagnostics.v1", "RotationSolverReborn.Diagnostics.v1"];
    private static readonly Dictionary<string, JObject> health = new();
    private static string? session, duty;
    private static uint territory;
    private static ulong character;
    private static bool openDuty;
    private static string Root => Path.Combine(DiagnosticLog.Root(Svc.PluginInterface.GetPluginConfigDirectory()), "LastDuty");
    private static string Index => Path.Combine(Root, "current.json");
    private static string DutyDirectory => Path.Combine(Root, duty!);
    private static void SaveIndex()
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(Index + ".tmp", new JObject { ["Duty"] = duty, ["Territory"] = territory, ["Character"] = character, ["Open"] = openDuty }.ToString());
        File.Move(Index + ".tmp", Index, true);
    }
    private static void LoadIndex()
    {
        if (!File.Exists(Index)) return;
        var index = JObject.Parse(File.ReadAllText(Index));
        if (!Guid.TryParseExact(index.Value<string>("Duty"), "N", out _)) return;
        duty = index.Value<string>("Duty");
        territory = index.Value<uint>("Territory");
        character = index.Value<ulong>("Character");
        openDuty = index.Value<bool>("Open");
    }
    private static bool enabled, busy, disposed;
    private static long nextPoll, nextTrim;
    private static bool trimPending;
    private static Task? cleanup;
    private static string message = "Off. Detailed logs stay on this computer; nothing is uploaded.";
    private static string marker = "";
    internal static void Init()
    {
        try { LoadIndex(); } catch (Exception ex) { message = ex.Message; }
        local = new("Accord", null,
        () => new { Automation = Plugin.Instance.Automation.Request("status"), SetupController.Instance!.Status, Hotbars = Hotbars() },
        () => new { Settings = Plugin.Instance.Settings, PvE = SetupController.Instance!.Pack, PvP = SetupController.Instance!.PvP.Pack });
    }
    private static unsafe object? Hotbars()
    {
        var module = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.Instance();
        if (module == null || !module->ModuleReady) return new { Ready = false };
        var bars = new JArray();
        for (int bar = 0; bar < 18; bar++)
        {
            var slots = new JArray();
            for (int slot = 0; slot < (bar < 10 ? 12 : 16); slot++)
            {
                ref var value = ref module->Hotbars[bar].Slots[slot];
                slots.Add(new JObject { ["Slot"] = slot, ["Type"] = (byte)value.CommandType, ["Action"] = value.CommandId });
            }
            bars.Add(new JObject { ["Bar"] = bar, ["Shared"] = module->IsHotbarShared((uint)bar), ["Slots"] = slots });
        }
        return new { Ready = true, Job = module->ActiveHotbarClassJobId, PvP = module->PvPHotbarsActive, Bars = bars };
    }
    private static JObject Send(string endpoint, string command)
    {
        try
        {
            if (command == "start" && Send(endpoint, "status").Value<int>("DutyCaptureVersion") != 1)
                return new JObject { ["Success"] = false, ["Error"] = "Duty capture unavailable. Check the installed testing version." };
            string json = new JObject { ["Command"] = command, ["Session"] = session, ["Duty"] = duty }.ToString(Formatting.None);
            return JObject.Parse(endpoint == "Accord" ? local!.Handle(json) : Svc.PluginInterface.GetIpcSubscriber<string, string>(endpoint).InvokeFunc(json));
        }
        catch (Exception ex) { return new JObject { ["Success"] = false, ["Error"] = ex.Message }; }
    }
    private static void SetEnabled(bool value)
    {
        if (value)
        {
            session = Guid.CreateVersion7().ToString("N"); health.Clear(); enabled = true;
            foreach (string endpoint in endpoints) health[endpoint] = Send(endpoint, "start");
            DiagnosticLog.Event("Controller.Start", () => new { Endpoints = health });
            message = "Recording this duty. The next duty replaces this capture.";
        }
        else
        {
            DiagnosticLog.Event("Controller.Stop", () => new { Endpoints = health });
            foreach (string endpoint in endpoints) health[endpoint] = Send(endpoint, "stop");
            enabled = false; message = "Stopped. Wait for files to finish writing, then export the session.";
        }
    }
    internal static void Tick()
    {
        if (Environment.TickCount64 < nextPoll || busy) return;
        nextPoll = Environment.TickCount64 + 500;
        if (cleanup?.IsFaulted == true)
        {
            message = "Previous capture cleanup will retry: " + cleanup.Exception!.GetBaseException().Message;
            cleanup = null;
            trimPending = true;
        }
        bool bound = Svc.ClientState.IsLoggedIn && (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty]
            || Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty56]);
        bool loading = Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] || Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51];
        try
        {
            if (Plugin.Instance.Settings.ContinuousDiagnostics && bound && !loading && Svc.PlayerState.ContentId != 0)
            {
                if (!openDuty || territory != Svc.ClientState.TerritoryType || character != Svc.PlayerState.ContentId)
                {
                    if (enabled) SetEnabled(false);
                    duty = Guid.CreateVersion7().ToString("N"); territory = Svc.ClientState.TerritoryType; character = Svc.PlayerState.ContentId;
                    Directory.CreateDirectory(DutyDirectory);
                    File.WriteAllText(Path.Combine(DutyDirectory, "managed-duty"), duty);
                    openDuty = true;
                    SaveIndex();
                    trimPending = true;
                }
                if (!enabled) SetEnabled(true);
                if (trimPending && health.TryGetValue("Accord", out var localHealth) && localHealth.Value<bool>("Success") && Environment.TickCount64 >= nextTrim)
                {
                    nextTrim = Environment.TickCount64 + 5000;
                    if (cleanup == null || cleanup.IsCompleted)
                    {
                        var root = Root;
                        var keep = duty!;
                        var candidates = new DirectoryInfo(root).GetDirectories();
                        cleanup = Task.Run(() =>
                        {
                            DutyLogRetention.Trim(root, keep, candidates);
                            DutyLogRetention.TrimLegacy(Directory.GetParent(root)!.FullName);
                        });
                        trimPending = false;
                    }
                }
            }
            else if (!loading && (!bound || !Plugin.Instance.Settings.ContinuousDiagnostics))
            {
                if (enabled) SetEnabled(false);
                if (!bound && openDuty) { openDuty = false; SaveIndex(); }
            }
        }
        catch (Exception ex) { message = "Duty capture: " + ex.Message; }
        if (session == null) return;
        foreach (string endpoint in endpoints)
        {
            var response = Send(endpoint, enabled ? "heartbeat" : "status");
            if (enabled && response.Value<bool>("Success") != true && response["Status"]?["Session"]?.Type == JTokenType.Null)
            {
                string component = endpoint == "WrathCombo.Diagnostics.v1" ? "WrathCombo" : "RotationSolver";
                if (Directory.Exists(Path.Combine(DutyDirectory, session, component)))
                {
                    // An engine reloaded during recording. Preserve its closed files and continue in a new directory.
                    SetEnabled(false);
                    SetEnabled(true);
                    break;
                }
                response = Send(endpoint, "start");
            }
            health[endpoint] = response;
        }
        DiagnosticLog.Event("Controller.Health", () => new { Endpoints = health });
    }
    private static bool BoundForDisplay() => Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty] || Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty56];
    internal static AetherglassUI.Topic[] Rows()=>[new("Latest duty",
        AccordControls.Flag("record","Keep advanced log of latest duty",()=>Plugin.Instance.Settings.ContinuousDiagnostics,
            requested=>{_ = Svc.Framework.RunOnTick(()=>{
                Plugin.Instance.Settings.ContinuousDiagnostics=requested;
                Svc.PluginInterface.SavePluginConfig(Plugin.Instance.Settings);
                nextPoll=0;
            });}) with {DisabledReason=()=>busy?"Export in progress":null},
        new AetherglassUI.Setting("state","Capture status",()=>ImGui.TextWrapped(
            Plugin.Instance.Settings.ContinuousDiagnostics&&!enabled&&!BoundForDisplay()?"Armed for duty entry.":message)){
            ExpandedContent=()=>{foreach(var item in health){
                var state=item.Value["Status"] as JObject;
                var detail=item.Value.Value<bool>("Success")!=true?"Unavailable: "+item.Value.Value<string>("Error"):
                    $"{(state?.Value<bool>("Active")==true?"recording":state?.Value<bool>("Flushing")==true?"finishing files":"stopped")} · events {state?["Written"]} · lost {state?["Dropped"]} {state?["Error"]}";
                ImGui.TextWrapped(item.Key+" · "+detail);
            }}},
        new("marker","Mark this moment",()=>{AetherglassUI.Controls.Text(ref marker,500);
            if(ImGui.SmallButton("Record marker")){string note=marker;marker="";_=Svc.Framework.RunOnTick(()=>DiagnosticLog.Event("User.Marker",()=>new {Note=note}));}},Visible:()=>enabled),
        new("folder","Session folder",()=>{if(ImGui.Button("Copy path"))ImGui.SetClipboardText(DutyDirectory);},Detail:duty==null?"":DutyDirectory,Visible:()=>duty!=null),
        new AetherglassUI.Setting("export","Export duty ZIP",()=>{if(ImGui.Button("Export"))Export(DutyDirectory);},Visible:()=>duty!=null){
            DisabledReason=()=>enabled||busy||health.Values.Any(x=>x["Status"]?.Value<bool>("Flushing")==true)?"Wait for recording and file writes to finish.":null},
        new("contents","What gets recorded",()=>{if(ImGui.TreeNode("Details")){ImGui.TextWrapped("Records while inside a duty. The next duty replaces this one. Each plugin uses up to about 32 MiB of compressed storage. Starting a new capture also retires completed older diagnostic recordings. Exported ZIPs are kept. Background checks are sampled and counted; individual presses, sends and confirmations are recorded. Capture includes selections and rejection reasons, button presses, targets, queues, action effects, settings, forecasts and automation ownership. Logs include local settings and nearby actor IDs/statuses; no chat or character names are collected by the world sampler."); ImGui.TreePop();}}))];
    private static void Export(string directory)
    {
        busy = true;
        string healthJson = JsonConvert.SerializeObject(health, Formatting.Indented);
        _ = Task.Run(() =>
        {
            string result;
            try
            {
                File.WriteAllText(Path.Combine(directory, "components.json"), healthJson);
                string archive = directory + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".zip";
                ZipFile.CreateFromDirectory(directory, archive, CompressionLevel.Fastest, false);
                result = "Exported: " + archive;
            }
            catch (Exception ex) { result = "Export failed: " + ex.Message; }
            if (!disposed) _ = Svc.Framework.RunOnTick(() => { message = result; busy = false; });
        });
    }
    internal static void Dispose()
    {
        if (enabled) SetEnabled(false);
        disposed = true;
        local?.Dispose(); local = null;
    }
}
