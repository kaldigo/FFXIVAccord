using System;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Accord;

internal static class WrathSettingsPanel
{
    private static long nextCheck;
    private static JObject? lastVerified;
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
        var desired = WrathPreferences.Resolve(Plugin.Instance.Settings.WrathPreferences);
        JObject? actual;
        try { actual = WrathPreferences.Apply(desired, () => Call("read"), changes => Call(changes.ToString(Formatting.None)), lastVerified); }
        catch { lastVerified = null; throw; }
        if (actual == null)
        {
            const string pending = "Wrath is using your previous settings. Saved changes will apply automatically after combat and the current action finish.";
            if (status != pending) AccordDiagnostics.DiagnosticLog.Event("Settings.WrathSelectionDeferred", () => new { Settings = desired });
            status = pending;
            return;
        }
        lastVerified = actual;
        status = "Saved rotation improvements and targeting settings applied and verified in Wrath.";
        if (!actual.ContainsKey("AcquireVisibleEnemy")) status += " Update Wrath testing to enable visible enemy acquisition.";
        AccordDiagnostics.DiagnosticLog.Event("Settings.WrathSelectionVerified", () => new { Settings = actual });
    }
    internal static bool Tick()
    {
        if (!Player.Available || Environment.TickCount64 < nextCheck) return lastVerified != null;
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
    internal static void DrawTargeting()
    {
        var options = WrathPreferences.Resolve(Plugin.Instance.Settings.WrathPreferences);
        Toggle(options, "Fallback", "Fall back when a deliberate target is invalid");
        Toggle(options, "EnemyTargeting", "Use action coverage and target validation across PvE damage buttons");
        Toggle(options, "AcquireEnemy", "Acquire an engaged enemy when none is selected");
        Toggle(options, "EnemyMouseover", "Damage override: world mouseover");
        Toggle(options, "EnemyFocus", "Damage override: focus target");
        Toggle(options, "AutomaticEnemies", "Allow automatic enemy switching for action coverage");
        Toggle(options, "OverridesInAutomation", "Apply mouseover and focus overrides to automatic actions too");
        Toggle(options, "AcquireVisibleEnemy", "Wrath auto: target the enemy when my target is empty or myself");
        Toggle(options, "SwitchEnemyTarget", "Wrath auto: replace an existing target with the enemy being attacked");
        DrawOrder(options, "EnemyOverrideOrder", "Enemy override order");
        ImGui.TextWrapped(status);
    }
    internal static void DrawHealing()
    {
        var options = WrathPreferences.Resolve(Plugin.Instance.Settings.WrathPreferences);
        Toggle(options, "GroundTargets", "Healing ground actions: target overrides, coverage and retained placement");
        Toggle(options, "IncludeRaise", "Healers: include resurrection in damage buttons");
        Toggle(options, "IncludeCleanse", "Healers: include cleansing in damage buttons");
        Toggle(options, "PreventiveHealing", "Healers: allow preparation and protection before incoming damage");
        Toggle(options, "HardCastRaise", "Resurrection: allow casting without an instant-cast effect");
        Toggle(options, "PartyMouseover", "Healing override: party-list mouseover");
        Toggle(options, "WorldMouseover", "Healing override: world mouseover");
        Toggle(options, "Focus", "Healing override: focus target");
        Toggle(options, "AutomaticAllies", "Select an eligible ally automatically when no override exists");
        Toggle(options, "SwitchAllyTarget", "Allow the Wrath runner to visibly select its ally recipient");
        DrawOrder(options, "AllyOverrideOrder", "Ally override order");
        ImGui.TextWrapped(status);
    }
    internal static void DrawJobs()
    {
        var options = WrathPreferences.Resolve(Plugin.Instance.Settings.WrathPreferences);
        RebornMaterial.M3Widgets.SectionLabel("Encounter forecasts");
        Toggle(options, "Forecasts", "Read BossMod encounter forecasts");
        Toggle(options, "SpaceMitigation", "Defer forecast party mitigation when an observed defense covers the hit");
        RebornMaterial.M3Widgets.SectionLabel("Tanks");
        Toggle(options, "Paladin", "PLD: coordinate burst with the planned GCD and active buffs");
        Toggle(options, "Warrior", "WAR: check buff duration and downtime before burst");
        Toggle(options, "Gunbreaker", "GNB: use observed No Mercy state and downtime checks");
        Toggle(options, "DarkKnight", "DRK: reserve shield mana for observed incoming damage");
        RebornMaterial.M3Widgets.SectionLabel("Healers");
        Toggle(options, "WhiteMage", "WHM: shared healing and preparation");
        Toggle(options, "Scholar", "SCH: shared healing and retained Deployment recipient");
        Toggle(options, "Astrologian", "AST: shared healing and prepared spell");
        Toggle(options, "Sage", "SGE: retain prepared spell and recipient");
        RebornMaterial.M3Widgets.SectionLabel("Melee");
        Toggle(options, "Monk", "MNK: recover Perfect Balance using accepted chakra rather than press order");
        Toggle(options, "Drg", "DRG: prepare Life Surge for the planned hit");
        Toggle(options, "Ninja", "NIN: commit mudra intent only after an accepted action");
        Toggle(options, "Samurai", "SAM: retain Iaijutsu across ST/AoE changes and use equivalent available positionals");
        Toggle(options, "Reaper", "RPR: protect combo and known uptime before Enshroud");
        Toggle(options, "Viper", "VPR: protect buff/combo duration before either Reawaken path");
        RebornMaterial.M3Widgets.SectionLabel("Physical ranged");
        Toggle(options, "Bard", "BRD: refresh existing DoTs under an expiring buff");
        Toggle(options, "Machinist", "MCH: match Reassemble to the selected weaponskill");
        Toggle(options, "Dancer", "DNC: finish accepted dances, protect expiring procs and budget new dances");
        RebornMaterial.M3Widgets.SectionLabel("Casters and limited jobs");
        Toggle(options, "BlackMage", "BLM: use permitted instant options when a selected cast cannot fit the forecast");
        Toggle(options, "Summoner", "SMN: bind Swiftcast to its spell and resurrection recipient");
        Toggle(options, "RedMage", "RDM: reserve Swiftcast/Dualcast and mana for a requested resurrection");
        Toggle(options, "Pictomancer", "PCT: fit motif preparation into known cast and downtime windows");
        Toggle(options, "BlueMage", "BLU: bind Bristle to an equipped spell and validate Final Sting before preparation");
        Toggle(options, "Beastmaster", "BST: bind finisher steps to the current pet and confirmed actions");
        RebornMaterial.M3Widgets.SectionLabel("Diagnostics");
        Toggle(options, "CompareDrg", "DRG: capture planned-hit comparisons with the original provider");
        ImGui.TextWrapped(status);
    }
    internal static void DrawPvP()
    {
        var options = WrathPreferences.Resolve(Plugin.Instance.Settings.WrathPreferences);
        Toggle(options, "PvpMachinist", "PvP MCH: prepare the selected tool and preserve Drill through Guard");
        Toggle(options, "PvpScheduling", "PvP: coordinated RDM/SMN burst and accepted Wildfire/Warrant/Honing releases");
        Toggle(options, "PvpTargeting", "PvP: action-specific damage targets and Guard eligibility");
        ImGui.TextWrapped(status);
    }
    private static void Save(string key, JToken value)
    {
        Plugin.Instance.Settings.WrathPreferences[key] = value.DeepClone();
        Svc.PluginInterface.SavePluginConfig(Plugin.Instance.Settings);
        nextCheck = 0;
        status = "Saved; waiting for Wrath to apply and verify the change.";
    }
    private static void Toggle(JObject options, string key, string label)
    {
        bool value = options.Value<bool>(key);
        if (RebornMaterial.M3Widgets.RowSwitch(label, ref value)) Save(key, new JValue(value));
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
