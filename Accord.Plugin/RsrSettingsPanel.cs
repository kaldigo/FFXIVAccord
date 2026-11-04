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
        ("UseSTDefense", "RSR: permit single-target defense"),
        ("UseAOEDefense", "RSR: permit party defense"),
        ("UseBmrTimeline", "RSR: use BossMod timelines for prevention"),
        ("UseDutyActions", "RSR: permit duty actions"),
        ("AutoTankStance", "RSR: manage tank stance"),
        ("UseGroundBeneficialAbility", "RSR: place beneficial ground actions automatically"),
        ("UseGroundBeneficialAbilityWhenMoving", "RSR: allow ground actions while moving"),
        ("UseGroundBeneficialAbilityOnlySelf", "RSR: center ground actions on self"),
        ("AutoHeal", "RSR: calculate healing thresholds automatically"),
    ];
    internal static void Draw()
    {
        if (!ImGui.CollapsingHeader("RSR automation preferences")) return;
        ImGui.TextWrapped("These edit RSR's own saved settings. They do not change Wrath's buttons or toggles. RSR conditions and job settings can further restrict or override these configured values.");
        ImGui.BeginDisabled(pending);
        if (ImGui.Button("Refresh RSR settings")) Request();
        if (settings is not null)
            foreach (var (key, label) in options)
            {
                if (settings[key]?.Type != JTokenType.Boolean) continue;
                bool value = settings.Value<bool>(key);
                if (ImGui.Checkbox(label, ref value)) Request(key, value);
            }
        ImGui.EndDisabled();
        ImGui.TextWrapped(status);
    }
    private static void Request(string? key = null, bool value = false)
    {
        pending = true;
        _ = Svc.Framework.RunOnTick(() =>
        {
            try
            {
                if (key is not null)
                {
                    var saved = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string, string, string>("RotationSolverReborn.AccordSetSetting")
                        .InvokeFunc(key, value ? "true" : "false"));
                    if (!saved.Value<bool>("Success")) throw new InvalidOperationException(saved.Value<string>("Error"));
                }
                var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string>("RotationSolverReborn.AccordReadSettings").InvokeFunc());
                if (!response.Value<bool>("Success") || response.Value<int>("ApiVersion") != 1)
                    throw new InvalidOperationException(response.Value<string>("Error") ?? "Unsupported settings API.");
                settings = response["Settings"] as JObject ?? throw new InvalidOperationException("Missing settings.");
                status = key is null ? "RSR settings loaded." : "Saved in RSR.";
            }
            catch (Exception ex) { status = "RSR settings unavailable: " + ex.Message; }
            finally { pending = false; }
        });
    }
}
