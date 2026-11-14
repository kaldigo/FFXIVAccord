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
    internal static AetherglassUI.Topic[] Rows(TaskPage task)
    {
        var options=WrathPreferences.Resolve(Plugin.Instance.Settings.WrathPreferences);
        AetherglassUI.Setting Flag(string key,string label,string detail="")=>
            AccordControls.Flag(key,label,()=>options.Value<bool>(key),v=>Save(key,v),detail);
        AetherglassUI.Setting Order(string key,string label)=>new(key,label,()=>{},Detail:"Earlier targets take priority."){
            ExpandedContent=()=>DrawOrder(options,key,label)
        };
        AetherglassUI.Topic[] topics=task switch {
            TaskPage.Enemies=>[
                new("Wrath · Visible target in Wrath auto · saved automatically",
                    Flag("AcquireVisibleEnemy","Select the enemy I attack","When I have no target or have myself selected."),
                    Flag("SwitchEnemyTarget","Replace my current target","Allow Wrath to visibly select a different enemy.")),
                new("Wrath · Damage targeting · saved automatically",
                    Flag("EnemyTargeting","Check action coverage","Validate targets across PvE damage buttons."),
                    Flag("AcquireEnemy","Acquire an engaged enemy","When I have no enemy selected."),
                    Flag("AutomaticEnemies","Switch for coverage","Allow automatic enemy switching for action coverage."),
                    Flag("Fallback","Use a fallback target","When my deliberate target is invalid.")),
                new("Wrath · Manual overrides · saved automatically",
                    Flag("EnemyMouseover","World mouseover","Use my hovered enemy for damage actions."),
                    Flag("EnemyFocus","Focus target","Allow my focus target as a damage override."),
                    Flag("OverridesInAutomation","Use overrides in auto rotation","Apply mouseover and focus to automatic actions too.")),
                new("Wrath · priority",Order("EnemyOverrideOrder","Enemy override order")),
            ],
            TaskPage.Healing=>[
                new("Wrath · Healing actions · saved automatically",
                    Flag("RaiseNearbyPlayers","Raise nearby players outside my party","PvE resurrection fallback when no eligible party target is available."),
                    Flag("IncludeRaise","Include resurrection in damage buttons"),
                    Flag("HardCastRaise","Allow resurrection without instant cast"),
                    Flag("IncludeCleanse","Include cleansing in damage buttons"),
                    Flag("PreventiveHealing","Prepare healing before incoming damage"),
                    Flag("GroundTargets","Use target overrides and coverage for ground heals")),
                new("Wrath · Healing targets · saved automatically",
                    Flag("PartyMouseover","Party-list mouseover"),
                    Flag("WorldMouseover","World mouseover"),
                    Flag("Focus","Focus target"),
                    Flag("AutomaticAllies","Choose an eligible ally when no override applies"),
                    Flag("SwitchAllyTarget","Let Wrath visibly select the healing target")),
                new("Wrath · priority",Order("AllyOverrideOrder","Ally override order")),
            ],
            TaskPage.JobTuning=>[
                new("Wrath · Occult Crescent", Flag("AllowPhantomKick","Include Phantom Kick in rotation","Off keeps the dash manual. Enabling it uses the normal distance check.")),
                new("Wrath · Encounter forecasts · saved automatically",
                    Flag("Forecasts","Read BossMod encounter forecasts"),
                    Flag("SpaceMitigation","Defer forecast party mitigation when an observed defense covers the hit")),
                new("Wrath · Tanks · saved automatically",
                    Flag("Paladin","PLD: coordinate burst with the planned GCD and active buffs"),
                    Flag("Warrior","WAR: check buff duration and downtime before burst"),
                    Flag("Gunbreaker","GNB: use observed No Mercy state and downtime checks"),
                    Flag("DarkKnight","DRK: reserve shield mana for observed incoming damage")),
                new("Wrath · Healers · saved automatically",
                    Flag("WhiteMage","WHM: shared healing and preparation"),
                    Flag("Scholar","SCH: shared healing and retained Deployment recipient"),
                    Flag("Astrologian","AST: shared healing and prepared spell"),
                    Flag("Sage","SGE: retain prepared spell and recipient")),
                new("Wrath · Melee · saved automatically",
                    Flag("Monk","MNK: recover Perfect Balance using accepted chakra rather than press order"),
                    Flag("Drg","DRG: prepare Life Surge for the planned hit"),
                    Flag("Ninja","NIN: commit mudra intent only after an accepted action"),
                    Flag("Samurai","SAM: retain Iaijutsu across ST/AoE changes and use equivalent available positionals"),
                    Flag("Reaper","RPR: protect combo and known uptime before Enshroud"),
                    Flag("Viper","VPR: protect buff/combo duration before either Reawaken path")),
                new("Wrath · Physical ranged · saved automatically",
                    Flag("Bard","BRD: refresh existing DoTs under an expiring buff"),
                    Flag("Machinist","MCH: match Reassemble to the selected weaponskill"),
                    Flag("Dancer","DNC: finish accepted dances, protect expiring procs and budget new dances")),
                new("Wrath · Casters and limited jobs · saved automatically",
                    Flag("BlackMage","BLM: use permitted instant options when a selected cast cannot fit the forecast"),
                    Flag("Summoner","SMN: bind Swiftcast to its spell and resurrection recipient"),
                    Flag("RedMage","RDM: reserve Swiftcast/Dualcast and mana for a requested resurrection"),
                    Flag("Pictomancer","PCT: fit motif preparation into known cast and downtime windows"),
                    Flag("BlueMage","BLU: bind Bristle to an equipped spell and validate Final Sting before preparation"),
                    Flag("Beastmaster","BST: bind finisher steps to the current pet and confirmed actions")),
                new("Wrath · Diagnostics · saved automatically",
                    Flag("CompareDrg","DRG: capture planned-hit comparisons with the original provider")),
            ],
            TaskPage.PvP=>[
                new("Wrath · PvP · saved automatically",
                    Flag("PvpMachinist","PvP MCH: prepare the selected tool and preserve Drill through Guard"),
                    Flag("PvpScheduling","PvP: coordinated RDM/SMN burst and accepted Wildfire/Warrant/Honing releases"),
                    Flag("PvpTargeting","PvP: action-specific damage targets and Guard eligibility")),
            ],
            _=>[]
        };
        return [..topics,new("Verification",new AetherglassUI.Setting("wrath-status","Wrath settings status",()=>ImGui.TextWrapped(status)))];
    }
    private static void Save(string key, JToken value)
    {
        Plugin.Instance.Settings.WrathPreferences[key] = value.DeepClone();
        Svc.PluginInterface.SavePluginConfig(Plugin.Instance.Settings);
        nextCheck = 0;
        status = "Saved; waiting for Wrath to apply and verify the change.";
    }
    private static void DrawOrder(JObject options, string key, string label)
    {
        if (options[key] is not JArray order) return;
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

    }
}
