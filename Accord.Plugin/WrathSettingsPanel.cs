using System;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Accord;

internal static class WrathSettingsPanel
{
    private static long nextCheck;
    private static bool verified;
    private static string status = "Waiting to verify Wrath configuration.";
    private static JObject Call(string request)
    {
        var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string, string>("WrathCombo.RotationSettings.v1").InvokeFunc(request));
        if (response.Value<bool>("Success") != true || response.Value<int>("Version") != 1)
            throw new InvalidOperationException(response.Value<string>("Error") ?? "Update the Wrath testing plugin: persistent rotation settings API unavailable.");
        return response["Settings"] as JObject ?? throw new InvalidOperationException("Missing Wrath settings.");
    }
    internal static void ApplyVerified()
    {
        verified = false;
        var desired = WrathPreferences.Resolve(Plugin.Instance.Settings.WrathPreferences);
        var actual = WrathPreferences.Apply(desired, () => Call("read"), changes => Call(changes.ToString(Formatting.None)));
        verified = true;
        status = "Saved rotation improvements and targeting settings applied and verified in Wrath.";
        if (!actual.ContainsKey("AcquireVisibleEnemy")) status += " Update Wrath testing to enable visible enemy acquisition.";
        AccordDiagnostics.DiagnosticLog.Event("Settings.WrathSelectionVerified", () => new { Settings = actual });
    }
    internal static bool Tick()
    {
        if (!Player.Available) return verified;
        if (Environment.TickCount64 < nextCheck) return verified;
        nextCheck = Environment.TickCount64 + 2000;
        try { ApplyVerified(); return true; }
        catch (Exception ex)
        {
            string failure = "Wrath configuration pending: " + ex.Message;
            if (status != failure) AccordDiagnostics.DiagnosticLog.Event("Error.WrathSelectionSettings", () => new { Error = ex.ToString() });
            status = failure;
            return false;
        }
    }
    internal static void Draw()
    {
        ImGui.TextWrapped(status);
        if (!ImGui.CollapsingHeader("Wrath rotation improvements and targeting")) return;
        ImGui.TextWrapped("These are saved Accord settings, applied automatically for manual buttons and auto rotation. Job improvements and encounter forecasts are enabled by default. Changes during combat wait until the current action and combat finish. Healing inclusion uses your Rotation behaviour settings; support and protection still follow their automation toggles.");
        var options = WrathPreferences.Resolve(Plugin.Instance.Settings.WrathPreferences);
            Toggle(options, "Drg", "DRG: prepare Life Surge for the planned hit");
            Toggle(options, "Machinist", "MCH: match Reassemble to the selected weaponskill");
            Toggle(options, "Bard", "BRD: refresh existing DoTs under an expiring buff");
            Toggle(options, "Viper", "VPR: protect buff/combo duration before either Reawaken path");
            Toggle(options, "Reaper", "RPR: protect combo and known uptime before Enshroud");
            Toggle(options, "Summoner", "SMN: bind Swiftcast to its spell and resurrection recipient");
            Toggle(options, "RedMage", "RDM: reserve Swiftcast/Dualcast and mana for a requested resurrection");
            Toggle(options, "Pictomancer", "PCT: fit motif preparation into known cast and downtime windows");
            Toggle(options, "BlackMage", "BLM: use permitted instant options when a selected cast cannot fit the forecast");
            Toggle(options, "Samurai", "SAM: retain Iaijutsu across ST/AoE changes and use equivalent available positionals");
            Toggle(options, "Monk", "MNK: recover Perfect Balance using accepted chakra rather than press order");
            Toggle(options, "Ninja", "NIN: commit mudra intent only after an accepted action");
            Toggle(options, "Dancer", "DNC: finish accepted dances, protect expiring procs and budget new dances");
            Toggle(options, "BlueMage", "BLU: bind Bristle to an equipped spell; validate Final Sting before preparation");
            Toggle(options, "Beastmaster", "BST: bind finisher steps to the current pet and confirmed actions");
            Toggle(options, "GroundTargets", "Healing ground actions: target overrides, coverage and retained placement");
            Toggle(options, "SpaceMitigation", "Defer forecast party mitigation when an observed defense covers the hit");
            Toggle(options, "PvpMachinist", "PvP MCH: prepare the selected tool and preserve Drill through Guard");
            Toggle(options, "PvpScheduling", "PvP: coordinated RDM/SMN burst and accepted Wildfire/Warrant/Honing releases");
            Toggle(options, "PvpTargeting", "PvP: action-specific damage targets and Guard eligibility");
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
            Toggle(options, "IncludeRaise", "Healers: include resurrection in damage buttons");
            Toggle(options, "IncludeCleanse", "Healers: include cleansing in damage buttons");
            Toggle(options, "PreventiveHealing", "Healers: allow preparation and protection before incoming damage");
            Toggle(options, "HardCastRaise", "Resurrection: allow casting without an instant-cast effect");
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
            Toggle(options, "OverridesInAutomation", "Apply mouseover and focus overrides to automatic actions too");
            Toggle(options, "AcquireVisibleEnemy", "Wrath auto: target the enemy when my target is empty or myself");
            Toggle(options, "SwitchEnemyTarget", "Wrath auto: replace an existing target with the enemy being attacked");
            Toggle(options, "SwitchAllyTarget", "Allow the Wrath runner to visibly select its ally recipient");
            DrawOrder(options, "AllyOverrideOrder", "Ally override order");
            DrawOrder(options, "EnemyOverrideOrder", "Enemy override order");
    }
    private static void Save(string key, JToken value)
    {
        Plugin.Instance.Settings.WrathPreferences[key] = value.DeepClone();
        Svc.PluginInterface.SavePluginConfig(Plugin.Instance.Settings);
        nextCheck = 0;
        verified = false;
        status = "Saved; waiting for Wrath to apply and verify the change.";
    }
    private static void Toggle(JObject options, string key, string label)
    {
        bool value = options.Value<bool>(key);
        if (ImGui.Checkbox(label, ref value)) Save(key, new JValue(value));
    }
    private static void DrawOrder(JObject options, string key, string label)
    {
        if (options[key] is not JArray order || !ImGui.TreeNode(label)) return;
        for (int i = 0; i < order.Count; i++)
        {
            ImGui.PushID(key + i);
            ImGui.TextUnformatted(order[i].Value<string>() switch
            { "PartyMouseover" => "Party-list mouseover", "WorldMouseover" => "World mouseover", "Selected" => "Selected target", "Focus" => "Focus target", _ => "Unknown" });
            if (i > 0)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Move up"))
                {
                    var changed = (JArray)order.DeepClone();
                    var before = changed[i - 1]!.DeepClone(); changed[i - 1] = changed[i]!.DeepClone(); changed[i] = before;
                    Save(key, changed);
                }
            }
            ImGui.PopID();
        }
        ImGui.TreePop();
    }
}
