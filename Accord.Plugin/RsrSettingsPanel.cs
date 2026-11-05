using System;
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
    private static readonly (string Key, string Label)[] options =
    [
        ("AutoBurst", "RSR: initiate burst automatically"),
        ("UseAbility", "RSR: use abilities between GCDs"),
        ("UseDefenseAbility", "RSR: permit defensive actions"),
        ("UseStDefense", "RSR: permit single-target defense"),
        ("UseAoeDefense", "RSR: permit party defense"),
        ("UseBmrTimeline", "RSR: use BossMod timelines for prevention"),
        ("UseDutyActions", "RSR: permit duty actions"),
        ("AutoTankStance", "RSR: manage tank stance"),
        ("UseGroundBeneficialAbility", "RSR: place beneficial ground actions automatically"),
        ("UseGroundBeneficialAbilityWhenMoving", "RSR: allow ground actions while moving"),
        ("UseGroundBeneficialAbilityOnlySelf", "RSR: center ground actions on self"),
        ("AutoHeal", "RSR: calculate healing thresholds automatically"),
    ];
    internal static void ApplyVerified()
    {
        var desired = Plugin.Instance.Settings.RsrPreferences;
        var expected = new JObject { ["TeachingMode"] = false, ["ShowInfoOnDtr"] = false };
        foreach (var (key, _) in options)
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
        if (!ImGui.CollapsingHeader("RSR automation preferences")) return;
        ImGui.TextWrapped("Accord automatically saves, applies and verifies these settings, including while auto rotation is off. Unavailable engines are retried automatically. These control RSR's automatic runner; Wrath's buttons use Accord's rotation and variant settings.");
        foreach (var (key, label) in options)
        {
            bool value = Plugin.Instance.Settings.RsrPreferences.TryGetValue(key, out var saved) ? saved : Default(key);
            if (ImGui.Checkbox(label, ref value))
            {
                Plugin.Instance.Settings.RsrPreferences[key] = value;
                Svc.PluginInterface.SavePluginConfig(Plugin.Instance.Settings);
                nextCheck = 0;
            }
        }
        ImGui.TextWrapped(status);
    }
}
