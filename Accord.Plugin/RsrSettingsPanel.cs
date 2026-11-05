using System;
using ECommons.DalamudServices;
using Newtonsoft.Json.Linq;

namespace Accord;

// Explicit mappings only. RSR's AutoHeal is automatic threshold calculation,
// not an equivalent of Accord's support permission.
internal static class RsrSettingsPanel
{
    private static JObject? settings;
    private static bool pending;
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
    internal static void Draw()
    {
        if (!ImGui.CollapsingHeader("RSR automation preferences")) return;
        ImGui.TextWrapped("These preferences are saved by Accord, applied to RSR and read back before RSR starts. They control RSR's automatic runner; Wrath's manual buttons use Accord's rotation and variant settings.");
        foreach (var (key, label) in options)
        {
            bool value = Plugin.Instance.Settings.RsrPreferences.TryGetValue(key, out var saved) ? saved : Default(key);
            if (ImGui.Checkbox(label, ref value))
            {
                Plugin.Instance.Settings.RsrPreferences[key] = value;
                Svc.PluginInterface.SavePluginConfig(Plugin.Instance.Settings);
            }
        }
        ImGui.BeginDisabled(pending);
        if (ImGui.Button("Apply and verify RSR settings"))
        {
            pending = true;
            _ = Svc.Framework.RunOnTick(() =>
            {
                try { ApplyVerified(); }
                catch (Exception ex) { status = "RSR settings failed: " + ex.Message; }
                finally { pending = false; }
            });
        }
        ImGui.EndDisabled();
        ImGui.TextWrapped(status);
    }
}
