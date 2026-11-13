using System;
using System.Linq;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json.Linq;

namespace Accord;

// Explicit mappings only. RSR's AutoHeal is automatic threshold calculation,
// not an equivalent of Accord's support permission.
internal static class RsrSettingsPanel
{
    private static JObject? settings;
    private static long nextCheck;
    private static uint lastJob;
    private static string status = "";
    private static readonly (string Group, string Key, string Label)[] options =
    [
        ("Combat actions", "AutoBurst", "initiate burst automatically"),
        ("Combat actions", "UseAbility", "use abilities between GCDs"),
        ("Defense and duty actions", "UseDefenseAbility", "permit defensive actions"),
        ("Defense and duty actions", "UseStDefense", "permit single-target defense"),
        ("Defense and duty actions", "UseAoeDefense", "permit party defense"),
        ("Defense and duty actions", "UseBmrTimeline", "use BossMod timelines for prevention"),
        ("Defense and duty actions", "UseDutyActions", "permit duty actions"),
        ("Defense and duty actions", "AutoTankStance", "manage tank stance"),
        ("Ground actions and healing", "UseGroundBeneficialAbility", "place beneficial ground actions automatically"),
        ("Ground actions and healing", "UseGroundBeneficialAbilityWhenMoving", "allow ground actions while moving"),
        ("Ground actions and healing", "UseGroundBeneficialAbilityOnlySelf", "center ground actions on self"),
        ("Ground actions and healing", "AutoHeal", "calculate healing thresholds automatically"),
    ];
    private static readonly IGrouping<string, (string Group, string Key, string Label)>[] sections = options.GroupBy(o => o.Group).ToArray();
    internal static void ApplyVerified()
    {
        var desired = Plugin.Instance.Settings.RsrPreferences;
        var expected = new JObject { ["TeachingMode"] = false, ["ShowInfoOnDtr"] = false };
        foreach (var (_, key, _) in options)
            expected[key] = desired.TryGetValue(key, out var saved) ? saved : Default(key);
        settings = VerifiedSettings.Apply(expected, Read, (key, value) =>
        {
            var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string, string, string>("RotationSolverReborn.AccordSetSetting")
                .InvokeFunc(key, value.ToString(Newtonsoft.Json.Formatting.None)));
            if (!response.Value<bool>("Success")) throw new InvalidOperationException(response.Value<string>("Error") ?? "RSR rejected " + key);
        });
        status = "Accord settings applied and verified in RSR.";
        AccordDiagnostics.DiagnosticLog.Event("Settings.RsrVerified", () => new { Settings = settings });
    }
    private static bool Default(string key) => key is not ("AutoBurst" or "UseGroundBeneficialAbilityWhenMoving" or "UseGroundBeneficialAbilityOnlySelf");
    private static JObject Read()
    {
        var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string>("RotationSolverReborn.AccordReadSettings").InvokeFunc());
        if (!response.Value<bool>("Success") || response.Value<int>("ApiVersion") != 1)
            throw new InvalidOperationException(response.Value<string>("Error") ?? "Unsupported RSR settings API.");
        return response["Settings"] as JObject ?? throw new InvalidOperationException("Missing RSR settings.");
    }
    // Runs independently of auto rotation: Accord owns engine configuration even
    // when the user is playing manually or RSR has just been reloaded.
    internal static bool Tick()
    {
        if (!Player.Available) return true;
        long now = Environment.TickCount64;
        uint job = (uint)Player.Job;
        if (now < nextCheck && job == lastJob) return true;
        nextCheck = now + 2000;
        lastJob = job;
        try { ApplyVerified(); return true; }
        catch (Exception ex)
        {
            string failure = "Waiting to apply Accord settings to RSR: " + ex.Message;
            if (status != failure)
                AccordDiagnostics.DiagnosticLog.Event("Error.RsrSettings", () => new { Error = ex.ToString(), Job = job });
            status = failure;
            return false;
        }
    }
    internal static void Draw()
    {
        ImGui.TextWrapped("Accord saves these settings automatically and applies them when RSR is available.");
        foreach (var section in sections)
            AccordControls.Section(section.Key, () =>
            {
                foreach (var option in section) DrawOption(option.Key, option.Label);
            });
        ImGui.TextWrapped(status);
    }
    private static void DrawOption(string key, string label)
    {
        bool value = Plugin.Instance.Settings.RsrPreferences.TryGetValue(key, out var saved) ? saved : Default(key);
        if (AccordControls.Toggle(label, ref value))
        {
            Plugin.Instance.Settings.RsrPreferences[key] = value;
            Svc.PluginInterface.SavePluginConfig(Plugin.Instance.Settings);
            nextCheck = 0;
        }
    }
}
