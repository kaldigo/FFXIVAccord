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
    private static string? session;
    private static bool enabled, busy, disposed;
    private static long nextPoll;
    private static string message = "Off. Detailed logs stay on this computer; nothing is uploaded.";
    private static string marker = "";
    internal static void Init() => local = new("Accord", null,
        () => new { Automation = Plugin.Instance.Automation.Request("status"), SetupController.Instance!.Status, Hotbars = Hotbars() },
        () => new { Settings = Plugin.Instance.Settings, PvE = SetupController.Instance!.Pack, PvP = SetupController.Instance!.PvP.Pack });
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
            string json = new JObject { ["Command"] = command, ["Session"] = session }.ToString(Formatting.None);
            return JObject.Parse(endpoint == "Accord" ? local!.Handle(json) : Svc.PluginInterface.GetIpcSubscriber<string, string>(endpoint).InvokeFunc(json));
        }
        catch (Exception ex) { return new JObject { ["Success"] = false, ["Error"] = ex.Message }; }
    }
    private static void SetEnabled(bool value)
    {
        if (value)
        {
            session = Guid.NewGuid().ToString("N"); health.Clear(); enabled = true;
            foreach (string endpoint in endpoints) health[endpoint] = Send(endpoint, "start");
            DiagnosticLog.Event("Controller.Start", () => new { Endpoints = health });
            message = "Logging until switched off. Job and territory changes remain in this session. Check all three component statuses below.";
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
        if (session == null || Environment.TickCount64 < nextPoll) return;
        nextPoll = Environment.TickCount64 + 1000;
        foreach (string endpoint in endpoints) health[endpoint] = Send(endpoint, enabled ? "heartbeat" : "status");
        DiagnosticLog.Event("Controller.Health", () => new { Endpoints = health });
    }
    internal static void Draw()
    {
        ImGui.TextWrapped("Full session diagnostics: selections and rejection reasons, button presses, targets, queues, action effects, settings, forecasts and automation ownership. Logs include local settings and nearby actor IDs/statuses; no chat or character names are collected by the world sampler.");
        bool value = enabled;
        ImGui.BeginDisabled(busy);
        if (ImGui.Checkbox("Temporary detailed logging", ref value))
        {
            bool requested = value;
            _ = Svc.Framework.RunOnTick(() => SetEnabled(requested));
        }
        ImGui.EndDisabled();
        ImGui.TextWrapped(message);
        foreach (var item in health)
        {
            var state = item.Value["Status"] as JObject;
            string detail = item.Value.Value<bool>("Success") != true ? "UNAVAILABLE: " + item.Value.Value<string>("Error") :
                $"{(state?.Value<bool>("Active") == true ? "recording" : state?.Value<bool>("Flushing") == true ? "finishing files" : "stopped")} | events {state?["Written"]} | lost {state?["Dropped"]} | {state?["Error"]}";
            ImGui.TextWrapped(item.Key + ": " + detail);
        }
        if (enabled)
        {
            ImGui.InputText("What went wrong / expected action", ref marker, 500);
            if (ImGui.Button("Mark this moment"))
            {
                string note = marker; marker = "";
                _ = Svc.Framework.RunOnTick(() => DiagnosticLog.Event("User.Marker", () => new { Note = note }));
            }
        }
        if (session != null)
        {
            string directory = Path.Combine(DiagnosticLog.Root(Svc.PluginInterface.GetPluginConfigDirectory()), session);
            ImGui.TextWrapped("Session folder: " + directory);
            if (ImGui.Button("Copy session folder")) ImGui.SetClipboardText(directory);
            bool flushing = health.Values.Any(x => x["Status"]?.Value<bool>("Flushing") == true);
            ImGui.BeginDisabled(enabled || flushing || busy);
            if (ImGui.Button("Export diagnostic ZIP")) Export(directory);
            ImGui.EndDisabled();
        }
        ImGui.Separator();
    }
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
