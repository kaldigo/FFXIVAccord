using System;
using System.Collections.Generic;
using System.Linq;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json.Linq;

namespace Accord;

internal static class RsrPermissionsPanel
{
    private static JObject? snapshot;
    private static readonly HashSet<uint> manual = new();
    private static bool pending;
    private static string status = "Refresh to load permissions for your current job and combat mode.";
    private static string filter = "";
    internal static void Draw()
    {
        if (!Plugin.Instance.Automation.RsrAvailable || !Player.Available)
        {
            ImGui.TextWrapped("Load the Accord-compatible RSR plugin and log in to edit its per-job permissions.");
            return;
        }
        ImGui.TextWrapped("Checked actions are reserved for manual use while Accord controls RSR automation. These settings are separate for each job and PvE/PvP. Your Wrath buttons and their exclusions remain independent.");
        bool dirty = snapshot is not null && !manual.SetEquals(((JArray?)snapshot["ManualActions"] ?? new JArray()).Values<uint>());
        ImGui.BeginDisabled(pending);
        if (ImGui.Button(dirty ? "Discard edits and reload RSR actions" : "Refresh RSR actions")) Request(new JObject { ["Command"] = "read" });
        ImGui.TextWrapped(status);
        bool current = snapshot is not null && snapshot.Value<uint>("Job") == (uint)Player.Job && snapshot.Value<bool>("PvP") == Svc.ClientState.IsPvP;
        if (snapshot is not null && !current) ImGui.TextWrapped("Your job or combat mode changed. Refresh before editing.");
        ImGui.BeginDisabled(!current);
        if (snapshot is not null)
        {
            ImGui.InputText("Find action", ref filter, 100);
            if (ImGui.BeginChild("RsrManualActions", new System.Numerics.Vector2(0, 220), true))
                foreach (var action in (JArray?)snapshot["Actions"] ?? new JArray())
                {
                    uint id = action.Value<uint>("Id");
                    string name = action.Value<string>("Name") ?? id.ToString();
                    if (!name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                    bool reserved = manual.Contains(id);
                    if (ImGui.Checkbox(name + "##rsr" + id, ref reserved))
                    { if (reserved) manual.Add(id); else manual.Remove(id); }
                }
            ImGui.EndChild();
            if (dirty) ImGui.TextWrapped("Unsaved RSR permissions for this job.");
            ImGui.BeginDisabled(!dirty);
            if (ImGui.Button("Save manual action permissions")) Request(new JObject
            {
                ["Command"] = "replace", ["Job"] = snapshot["Job"]!.DeepClone(),
                ["PvP"] = snapshot["PvP"]!.DeepClone(), ["ManualActions"] = JArray.FromObject(manual.OrderBy(id => id)),
            });
            ImGui.EndDisabled();
        }
        ImGui.EndDisabled();
        ImGui.EndDisabled();
    }
    private static void Request(JObject request)
    {
        pending = true;
        _ = Svc.Framework.RunOnTick(() =>
        {
            try
            {
                var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string, string>("RotationSolverReborn.AccordActionPermissions").InvokeFunc(request.ToString()));
                if (!response.Value<bool>("Success") || response.Value<int>("Version") != 1)
                    throw new InvalidOperationException(response.Value<string>("Error") ?? "Unsupported RSR permissions API.");
                snapshot = response;
                manual.Clear();
                foreach (var id in (JArray?)response["ManualActions"] ?? new JArray()) manual.Add(id.Value<uint>());
                status = request.Value<string>("Command") == "replace" ? "Permissions saved in RSR." : "Permissions loaded from RSR.";
            }
            catch (Exception ex) { status = "RSR permissions unavailable: " + ex.Message; }
            finally { pending = false; }
        });
    }
}
