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
        Plugin.Instance.Automation.Draw();
        RsrPermissionsPanel.Draw();
        RsrSettingsPanel.Draw();
        ImGui.TextWrapped("Observe the existing Wrath rotation for up to 60 seconds. This does not run a second resolver or change your rotation settings. Start replaces the previous capture.");
        ImGui.BeginDisabled(pending);
        if (ImGui.Button("Start 60 second capture")) Request("start");
        ImGui.SameLine(); if (ImGui.Button("Stop")) Request("stop");
        ImGui.SameLine(); if (ImGui.Button("Refresh")) Request("read");
        ImGui.EndDisabled();
        ImGui.TextWrapped(status);
        if (report is null) return;
        if (report["Experimental"] is JObject options)
        {
            ImGui.Separator();
            ImGui.TextWrapped("Testing providers (session only; change outside combat). Reload restores the original providers. These options affect Wrath only.");
            ImGui.BeginDisabled(pending);
            Toggle(options, "Drg", "DRG: prepare Life Surge for the planned hit");
            Toggle(options, "Machinist", "MCH: match Reassemble to the selected weaponskill");
            Toggle(options, "Paladin", "PLD: coordinate burst with the planned GCD and active buffs");
            Toggle(options, "Warrior", "WAR: check buff duration and downtime before burst");
            Toggle(options, "Gunbreaker", "GNB: use observed No Mercy state and downtime checks");
            Toggle(options, "DarkKnight", "DRK: reserve shield mana for observed incoming damage");
            Toggle(options, "CompareDrg", "DRG: capture planned-hit comparisons with the original provider");
            Toggle(options, "Sage", "SGE: retain prepared spell and recipient");
            Toggle(options, "Scholar", "SCH: shared healing and retained Deployment recipient");
            Toggle(options, "Astrologian", "AST: shared healing and prepared spell");
            Toggle(options, "WhiteMage", "WHM: shared healing and preparation");
            Toggle(options, "Forecasts", "Read BossMod encounter forecasts");
            Toggle(options, "IncludeHealing", "Healers: include healing abilities in damage buttons");
            Toggle(options, "HealingGcds", "Healers: also allow healing spells to replace damage casts");
            Toggle(options, "PartyMouseover", "Healing override: party-list mouseover");
            Toggle(options, "WorldMouseover", "Healing override: world mouseover");
            Toggle(options, "Focus", "Healing override: focus target");
            Toggle(options, "Fallback", "Fall back when a deliberate target is invalid");
            Toggle(options, "AutomaticAllies", "Select an eligible ally automatically when no override exists");
            Toggle(options, "EnemyTargeting", "Use action coverage and target validation across PvE damage buttons");
            Toggle(options, "AcquireEnemy", "Acquire an engaged enemy when none is selected");
            Toggle(options, "EnemyMouseover", "Damage override: world mouseover");
            Toggle(options, "EnemyFocus", "Damage override: focus target");
            Toggle(options, "AutomaticEnemies", "Allow automatic enemy switching for action coverage");
            Toggle(options, "OverridesInAutomation", "Apply mouseover overrides to automatic actions too");
            ImGui.EndDisabled();
            if (report["Encounter"] is JObject encounter) ImGui.TextWrapped("Encounter guidance: " + encounter.Value<string>("Module"));
            else ImGui.TextDisabled("No current encounter forecast available.");
        }
        ImGui.TextWrapped($"Last refreshed status: {(report.Value<bool>("Active") ? "capturing" : report.Value<string>("StopReason"))}. Older observations discarded: {report["Dropped"]}.");
        if (ImGui.Button("Copy capture report")) ImGui.SetClipboardText(report.ToString(Formatting.Indented));
        ImGui.TextWrapped($"Unchanged selections omitted: {report["RepeatedSelections"]}. Selection means a normal uncached hotbar query, not a key press or successful execution. Send records the outgoing action and recipient; it does not prove server acceptance. Refresh before copying.");
        if (ImGui.BeginChild("TraceEntries", new System.Numerics.Vector2(0, 240), true))
            foreach (var entry in (JArray?)report["Entries"] ?? new JArray())
                ImGui.TextUnformatted($"{entry["ElapsedMs"]} ms | {entry["Kind"]} | {entry["Input"]} → {entry["Output"]} | recipient {entry["Recipient"]}");
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
    private static void Toggle(JObject options, string key, string label)
    {
        bool value = options.Value<bool>(key);
        if (ImGui.Checkbox(label, ref value)) Request("configure:" + new JObject { [key] = value }.ToString(Formatting.None));
    }
}
