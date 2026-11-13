using System;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Accord;

internal static class RotationTestingPanel
{
    private static JObject? report;
    private static string status = "Capture is off by default. Requires the matching Wrath testing branch.";
    private static bool pending;
    private static JObject? clientBaseline;
    private static JToken? defaultProfile;
    internal static void Draw()
    {
        ImGui.TextWrapped("Observe the existing Wrath rotation for up to 60 seconds. This does not run a second resolver or change your rotation settings. Start replaces the previous capture.");
        ImGui.BeginDisabled(pending);
        if (ImGui.Button("Start 60 second capture")) Request("start");
        ImGui.SameLine(); if (ImGui.Button("Stop")) Request("stop");
        ImGui.SameLine(); if (ImGui.Button("Refresh")) Request("read");
        ImGui.EndDisabled();
        ImGui.TextWrapped(status);
        if (report is null) return;
        ImGui.TextWrapped($"Last refreshed status: {(report.Value<bool>("Active") ? "capturing" : report.Value<string>("StopReason"))}. Older observations discarded: {report["Dropped"]}.");
        if (ImGui.Button("Copy capture report")) ImGui.SetClipboardText(report.ToString(Formatting.Indented));
        ImGui.TextWrapped($"Unchanged selections omitted: {report["RepeatedSelections"]}. Selection means a normal uncached hotbar query, not a key press or successful execution. Send records the outgoing action and recipient; it does not prove server acceptance. Refresh before copying.");
        if (ImGui.BeginChild("TraceEntries", new System.Numerics.Vector2(0, 240), true))
            foreach (var entry in (JArray?)report["Entries"] ?? new JArray())
                ImGui.TextWrapped($"{entry["ElapsedMs"]} ms | {entry["Kind"]} | {entry["Input"]} → {entry["Output"]} | recipient {entry["Recipient"]}");
        ImGui.EndChild();
    }
    private static void Request(string command)
    {
        pending = true;
        _ = Svc.Framework.RunOnTick(() =>
        {
            try
            {
                var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string, string>("WrathCombo.Testing.Trace.v1").InvokeFunc(command));
                if (response.Value<bool>("Success") != true) throw new InvalidOperationException(response.Value<string>("Error"));
                response["AccordBuild"] = typeof(RotationTestingPanel).Assembly.ManifestModule.ModuleVersionId.ToString();
                if (command == "start")
                {
                    var setup = SetupController.Instance!;
                    clientBaseline = JObject.FromObject(setup.Settings);
                    defaultProfile = SetupController.IsPvP
                        ? JToken.Parse(JsonConvert.SerializeObject(setup.PvP.Pack.Find((uint)Player.Job)))
                        : JToken.Parse(System.Text.Json.JsonSerializer.Serialize(setup.Pack.Find((uint)Player.Job)));
                }
                response["AccordSettingsAtStart"] = clientBaseline?.DeepClone();
                response["DefaultProfileAtStart"] = defaultProfile?.DeepClone();
                response["AccordSettingsAtRead"] = JObject.FromObject(SetupController.Instance!.Settings);
                report = response;
                status = "Capture refreshed at " + DateTime.Now.ToLongTimeString();
            }
            catch (Exception ex) { status = "Capture unavailable: " + ex.Message; }
            finally { pending = false; }
        });
    }
}
