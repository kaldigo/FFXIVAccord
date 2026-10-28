using System;
using System.Collections.Generic;
using System.Linq;
using HotbarTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WrathFork.Contract;

namespace Accord;

// Builds logical content from reviewed job data. Placement is resolved separately;
// controller layouts continue to use the existing SetupPack/PveLayoutPlan path.
internal sealed class ConfiguredLayout
{
    private readonly SetupPack pack;
    private readonly Func<string, string, SlotValue> resolve;
    private readonly Dictionary<uint, Dictionary<ControlIntent, List<LayoutControl>>> baseContents = new();
    internal ConfiguredLayout(SetupPack pack, Func<string, string, SlotValue> resolve) { this.pack = pack; this.resolve = resolve; }

    internal static ControlIntent Intent(JobPreset job, PresetSlot slot) => slot.Action switch
    {
        "The Warden's Paean" or "Esuna" => ControlIntent.Cleanse,
        "Peloton" => ControlIntent.Travel,
        "Arm's Length" or "Surecast" => ControlIntent.Knockback,
        "Hallowed Ground" or "Holmgang" or "Living Dead" or "Superbolide" => ControlIntent.Invulnerability,
        "Provoke" => ControlIntent.Enmity,
        "Shirk" => ControlIntent.Shirk,
        "Iron Will" or "Defiance" or "Grit" or "Royal Guard" => ControlIntent.Stance,
        "Swiftcast" => ControlIntent.Raise,
        "Elusive Jump" => ControlIntent.Movement,
        "The Wanderer's Minuet" or "Standard Step" or "Technical Step" or "Astral Draw" => ControlIntent.Mechanics,
        "Perfect Balance" or "Geirskogul" or "Kassatsu" or "Ten Chi Jin" or "Meikyo Shisui" or "Enshroud" or "Reawaken" or "Flamethrower" or "Transpose" or "Hammer Stamp" => ControlIntent.Mechanics,
        "Nature's Minne" or "Mantra" or "Lux Solaris" or "Closed Position" => ControlIntent.Support,
        "Presence of Mind" or "Chain Stratagem" or "Divination" or "Devilment" => ControlIntent.Burst,
        _ when LayoutPreferences.Role(job.JobId) == JobRole.Healer => slot.LogicalSlot switch
        {
            1 => ControlIntent.SingleTarget, 2 => ControlIntent.AreaDamage,
            3 => ControlIntent.SingleHealing, 4 => ControlIntent.AreaHealing,
            5 => ControlIntent.Protection, 10 => ControlIntent.Movement, _ => ControlIntent.Other,
        },
        _ => slot.LogicalSlot switch
        {
            1 => ControlIntent.SingleTarget, 2 => ControlIntent.AreaDamage, 3 => ControlIntent.Burst,
            4 => ControlIntent.Movement, 5 => ControlIntent.Protection, _ => ControlIntent.Other,
        },
    };

    internal Dictionary<ControlIntent, List<LayoutControl>> Contents(JobPreset job, AccordSettings settings)
    {
        if (!baseContents.TryGetValue(job.JobId, out var baselineContents))
            baseContents[job.JobId] = baselineContents = BuildContents(job);
        var content = baselineContents.ToDictionary(p => p.Key, p => p.Value.ToList());
        var values = settings.Layouts.Resolve(job.JobId);
        if (values.SplitMechanics && SplitActions.TryGetValue(job.JobId, out var split))
        {
            content[ControlIntent.Mechanics].Clear();
            foreach (string name in split)
            {
                var value = resolve(job.Job, name);
                content[ControlIntent.Mechanics].Add(new(new ActionChoice { Name = name, Type = value.Type, Id = value.Id, Automation = VariantId.ClassMechanics }));
            }
        }
        return content;
    }

    private Dictionary<ControlIntent, List<LayoutControl>> BuildContents(JobPreset job)
    {
        var manual = new AccordSettings();
        var baseline = pack.Resolve(job, manual);
        var variants = Enum.GetValues<VariantId>().ToDictionary(v => v, v =>
        {
            var probe = Clone(manual); probe.Variants[v] = true;
            return pack.Resolve(job, probe);
        });
        var content = Enum.GetValues<ControlIntent>().ToDictionary(i => i, _ => new List<LayoutControl>());
        foreach (var slot in baseline.Slots.Where(s => s.Action != "Empty"))
        {
            var intent = Intent(job, slot);
            VariantId? owner = null;
            // Determine ownership from reviewed per-job variant effects, not a
            // generic guess based on the role or the action's name.
            foreach (var variant in Enum.GetValues<VariantId>())
            {
                if (variants[variant].Slots.SingleOrDefault(s => s.LogicalSlot == slot.LogicalSlot)?.Action != slot.Action)
                    owner = variant;
            }
            var value = resolve(job.Job, slot.Action);
            content[intent].Add(new(new ActionChoice
            {
                Name = slot.Action, Type = value.Type, Id = value.Id, Automation = owner,
                Selector = intent is ControlIntent.SingleTarget or ControlIntent.AreaDamage or ControlIntent.Burst or ControlIntent.Protection or ControlIntent.Mechanics or ControlIntent.SingleHealing or ControlIntent.AreaHealing or ControlIntent.Raise or ControlIntent.Movement,
            }));
        }
        // Some reviewed variants expose a different deliberate utility alongside
        // automation (e.g. Equilibrium). Include those controls in the all-off
        // capacity envelope as well; they must not appear as unbudgeted additions.
        foreach (var slot in variants.Values.SelectMany(p => p.Slots).Where(s => s.Action != "Empty")
                     .DistinctBy(s => s.Action).Where(s => baseline.Slots.All(b => b.Action != s.Action)))
        {
            var value = resolve(job.Job, slot.Action);
            content[Intent(job, slot)].Add(new(new ActionChoice { Name = slot.Action, Id = value.Id, Type = value.Type }));
        }
        return content;
    }

    internal static readonly Dictionary<uint, string[]> SplitActions = new()
    {
        [23] = ["The Wanderer's Minuet", "Mage's Ballad", "Army's Paeon"],
        [38] = ["Standard Step", "Technical Step"],
        // Umbral Draw is a native replacement, not an assignable action. Keep
        // the draw root so the game alternates it, alongside separate play slots.
        [33] = ["Astral Draw", "Play I", "Play II", "Play III", "Minor Arcana"],
    };

    internal LayoutResult Plan(JobPreset job, AccordSettings settings, Func<int,bool> shared, Func<ActionChoice,bool> available, bool allManual)
    {
        var content = Contents(job, settings);
        var owners = content.Values.SelectMany(c => c).Where(c => c.Action.Automation is not null)
            .GroupBy(c => c.Action.Value).ToDictionary(g => g.Key, g => g.First().Action.Automation);
        var result = IntentLayoutResolver.Resolve(job.JobId, settings.Layouts, i => content[i], available, settings.Enabled, shared, allManual,
            a => owners.GetValueOrDefault(a.Value));
        return result with { Controls = result.Controls.Select(c => c with { Action = DirectControl(c.Action) }).ToArray() };
    }

    internal static ActionChoice DirectControl(ActionChoice action) => action.Type != 1 || action.Selector ? action : new()
    {
        Id = DirectAction.Id(action.Id), Type = action.Type, Name = action.Name,
        Selector = false, Automation = action.Automation,
    };

    internal JObject Routes(JobPreset job, AccordSettings settings)
    {
        var values = settings.Layouts.Resolve(job.JobId);
        // Exclusion routes refer to original intent inputs, even when the layout
        // splits the visible mechanic controls into direct actions.
        var unsplit = Clone(settings);
        unsplit.Layouts.Classes.TryAdd(job.JobId, new());
        unsplit.Layouts.Classes[job.JobId].SplitMechanics = false;
        var contents = Contents(job, unsplit);
        var routes = new Dictionary<uint, ActionRoutePolicy>();
        foreach (var exclusion in values.Exclusions)
            foreach (var intent in exclusion.ExcludedFrom)
            {
                foreach (var source in contents[intent])
                {
                    if (!routes.TryGetValue(source.Action.Id, out var route)) routes[source.Action.Id] = route = new();
                    route.Excluded.Add(exclusion.Action.Id);
                }
                uint generic = intent switch
                {
                    ControlIntent.SingleTarget => 1000000, ControlIntent.AreaDamage => 1000001,
                    ControlIntent.SingleHealing => 1000002, ControlIntent.AreaHealing => 1000003,
                    _ => 0,
                };
                if (generic != 0)
                {
                    if (!routes.TryGetValue(generic, out var route)) routes[generic] = route = new();
                    route.Excluded.Add(exclusion.Action.Id);
                }
            }
        var direct = Contents(job, settings).Values.SelectMany(c => c).Select(c => c.Action)
            .Concat(values.RoleActions.Important).Concat(values.RoleActions.Other)
            .Concat(values.Exclusions.Where(e => e.ExcludedFrom.Count > 0).Select(e => e.Action))
            .Concat(settings.Layouts.Groups.SelectMany(g => g.Resolve(job.JobId)))
            .Concat(values.Mapping.Areas.SelectMany(a => a.Entries).Where(e => e.Action is not null).Select(e => e.Action!));
        foreach (var action in direct.Where(a => !a.Selector && a.Type == 1 && a.Id is > 0 and < 1000000))
            routes[DirectAction.Id(action.Id)] = new() { Direct = true, Target = action.Id };
        return JObject.FromObject(routes);
    }

    internal static AccordSettings Clone(AccordSettings source) => JsonConvert.DeserializeObject<AccordSettings>(JsonConvert.SerializeObject(source))!;
}
