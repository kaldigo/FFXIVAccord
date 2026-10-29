using System;
using System.Collections.Generic;
using System.Linq;
using HotbarTools;
using Newtonsoft.Json.Linq;
using WrathFork.Contract;

namespace Accord;

internal static class PvPConfiguredLayout
{
    internal static IntentMapping DefaultMapping() => new()
    {
        IntentVersion = 3,
        Areas = [
            new() { Name = "Main actions", Bar = 1, Entries = Entries(ControlIntent.SingleTarget, ControlIntent.AreaDamage,
                ControlIntent.SingleHealing, ControlIntent.Other, ControlIntent.LimitBreak, ControlIntent.Burst, ControlIntent.AdditionalBurst) },
            new() { Name = "Movement and utility", Bar = 2, Entries = Entries(ControlIntent.Movement, ControlIntent.Recovery,
                ControlIntent.Cleanse, ControlIntent.PersonalProtection, ControlIntent.ImportantRole, ControlIntent.OtherRole,
                ControlIntent.Sprint, ControlIntent.Protection, ControlIntent.Mechanics, ControlIntent.Support, ControlIntent.Preparation,
                ControlIntent.Exclusions) },
        ],
    };
    private static List<MappingEntry> Entries(params ControlIntent[] intents) => intents.Select(i => new MappingEntry { Intent = i }).ToList();

    internal static LayoutPreferences Effective(uint job, PvPSettings settings)
    {
        var normal = settings.Layouts.Resolve(job);
        var values = settings.Overrides.Resolve(job, normal);
        return new() { Global = values, Groups = settings.Layouts.Groups.Select(g => new PersonalGroup
            { Id = g.Id, Name = g.Name, Contents = g.Resolve(job, true).ToList() }).ToList(), AdditionalBars = settings.Layouts.AdditionalBars };
    }

    internal static IEnumerable<LayoutControl> Contents(PvPProfile job, PvPSettings settings, JObject snapshot, ControlIntent intent)
    {
        foreach (var control in job.Controls.Where(c => c.Binding != default && c.Group == intent).OrderBy(c => c.Order))
        {
            if (control.Binding.Id == 1000001 && settings.Overrides.RotationButtons == PvPRotationButtons.One) continue;
            bool delegated = PvPPack.Resolve(control, settings, snapshot, job.JobId) == default;
            var action = new ActionChoice { Id = control.Binding.Id, Type = control.Binding.Type,
                Name = control.Binding.Id == 1000000 && settings.Overrides.RotationButtons == PvPRotationButtons.One ? "Full rotation" : control.Name,
                Selector = control.Custom == "PvPIntent",
                Automation = control.ManualOwner ?? (delegated ? control.Variant : null) };
            if(control.Binding.Id==29711 && snapshot["Settings"]?["CustomIntValuesV6"]?.Value<int?>("EmergencyHealThreshold")>0)
                action.Automation=VariantId.Support;
            if(control.Binding.Id==29054 && snapshot["Settings"]?["CustomIntValuesV6"]?.Value<int?>("EmergencyGuardThreshold")>0)
                action.Automation=VariantId.Protection;
            if(control.Binding.Id==29056 && snapshot["Settings"]?["CustomBoolArrayValuesV6"]?["QuickPurifyStatuses"]?.Values<bool>().Any(v=>v)==true)
                action.Automation=VariantId.Protection;
            if (control.Binding.Id == 1000000 && settings.Overrides.RotationButtons == PvPRotationButtons.One) action.Id = 1000015;
            yield return new(action);
        }
    }

    internal static LayoutResult Plan(PvPProfile job, PvPSettings settings, JObject snapshot, Func<int, bool> shared,
        Func<ActionChoice, bool> available, bool allManual = false)
    {
        var effective = Effective(job.JobId, settings);
        var result = IntentLayoutResolver.Resolve(job.JobId, effective, i => Contents(job, settings, snapshot, i),
            available, v => settings.Enabled(v, job.JobId), shared, allManual);
        var issues = result.Issues.ToList();
        foreach (var area in effective.Global.Mapping.Areas)
            foreach (var entry in area.Entries)
            {
                var extras = entry.Action is {} action ? new[] { action }
                    : effective.Groups.FirstOrDefault(g => g.Id == entry.GroupId)?.Contents.ToArray() ?? [];
                foreach (var extra in extras.Where(a => !available(a)))
                    issues.Add(new(job.JobId, area.Bar, $"{extra.Name} cannot be used by {job.Job} in PvP. Change this group’s PvP contents or mapping."));
            }
        return result with { Issues = issues };
    }

    internal static JObject Routes(PvPProfile job, PvPSettings settings)
    {
        var values = Effective(job.JobId, settings).Global;
        var routes = new Dictionary<uint, ActionRoutePolicy>();
        foreach (var exclusion in values.Exclusions)
            foreach (var intent in exclusion.ExcludedFrom)
                foreach (var input in job.Controls.Where(c => c.Group == intent && c.Binding.Type == 1).Select(c => c.Binding.Id).Where(i => i != 0))
                {
                    if (!routes.TryGetValue(input, out var route)) routes[input] = route = new();
                    route.Excluded.Add(exclusion.Action.Id);
                }
        return JObject.FromObject(routes);
    }
}
