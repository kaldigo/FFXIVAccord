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

    internal bool InitializeDefaults(LayoutPreferences preferences)
    {
        if (preferences.MappingVersion >= 2) return false;
        if (preferences.Global.Mapping.JobDefaults.Count > 0 ||
            (!preferences.DefaultsInitialized && preferences.Global.Mapping.Areas.Count == 0))
            preferences.Global.Mapping = CreateDefaults();
        // Never manufacture overrides from the old templates. Explicitly added
        // overrides survive; their legacy layout data has a separate file backup.
        foreach (var (job, over) in preferences.Classes)
            if (over.Mapping is { } mapping && mapping.JobDefaults.Count > 0)
            {
                mapping.Areas = mapping.JobDefaults.GetValueOrDefault(job) ?? CreateDefaults().Areas;
                foreach (var entry in mapping.Areas.SelectMany(a => a.Entries).Where(e => e.DefaultSlot.HasValue).ToArray())
                {
                    int slot = entry.DefaultSlot!.Value;
                    var source = pack.Resolve(pack.Find(job)!, new()).Slots.FirstOrDefault(s => s.LogicalSlot == slot);
                    entry.DefaultSlot = null;
                    if (slot == 99) entry.Intent = ControlIntent.Sprint;
                    else if (source is not null && source.Action != "Empty")
                    {
                        var value = resolve(pack.Find(job)!.Job, source.Action);
                        var control = Contents(pack.Find(job)!, new()).Values.SelectMany(c => c).First(c => c.Action.Value == value);
                        entry.Action = control.Action;
                    }
                }
                foreach (var area in mapping.Areas) area.Entries.RemoveAll(e => e.Intent is null && e.Action is null && e.GroupId.Length == 0);
                mapping.JobDefaults.Clear();
            }
        foreach (var over in preferences.Roles.Values)
            if (over.Mapping is { } mapping && mapping.JobDefaults.Count > 0) over.Mapping = CreateDefaults();
        preferences.PruneUnusedOverrides();
        preferences.DefaultsInitialized = true;
        preferences.MappingVersion = 2;
        return true;
    }

    internal IntentMapping CreateDefaults() => new()
    {
        Areas = [
            new() { Name = "Main actions", Bar = 1, Entries = Entries(ControlIntent.SingleTarget, ControlIntent.AreaDamage,
                ControlIntent.SingleHealing, ControlIntent.AreaHealing, ControlIntent.Protection, ControlIntent.Burst) },
            new() { Name = "Movement and class utility", Bar = 2, Last = 10, Entries = Entries(ControlIntent.Movement,
                ControlIntent.Enmity, ControlIntent.Travel, ControlIntent.TargetedProtection, ControlIntent.PartyProtection,
                ControlIntent.PersonalProtection, ControlIntent.Support, ControlIntent.Mechanics, ControlIntent.Knockback,
                ControlIntent.Invulnerability, ControlIntent.Rescue, ControlIntent.Preparation, ControlIntent.Other) },
            new() { Name = "Tank swap", Bar = 2, First = 11, Last = 11, Entries = Entries(ControlIntent.Shirk) },
            new() { Name = "Resurrection or tank stance", Bar = 2, First = 12, Last = 12, Entries = Entries(ControlIntent.Raise, ControlIntent.Stance) },
            new() { Name = "Role actions and manual utilities", Bar = 3, Entries = Entries(ControlIntent.ImportantRole,
                ControlIntent.OtherRole, ControlIntent.Interrupt, ControlIntent.Exclusions, ControlIntent.Recovery, ControlIntent.Cleanse, ControlIntent.Sprint) },
        ],
    };
    private static List<MappingEntry> Entries(params ControlIntent[] intents) => intents
        .Select(i => new MappingEntry { Intent = i, Priority = i == ControlIntent.ImportantRole ? 20 : 10 }).ToList();

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
        "Rescue" => ControlIntent.Rescue,
        "Divine Veil" or "Passage of Arms" => ControlIntent.PartyProtection,
        "Bulwark" or "Thrill of Battle" or "Dark Mind" or "Camouflage" or "Holos" => ControlIntent.Protection,
        "Riddle of Earth" or "Third Eye" or "Arcane Crest" or "Manaward" or "Radiant Aegis" or "Tempera Coat" => ControlIntent.PersonalProtection,
        "Sheltron" or "Raw Intuition" or "The Blackest Night" or "Heart of Stone" or "Cover" or "Oblation" or "Deployment Tactics" => ControlIntent.TargetedProtection,
        "Second Wind" or "Equilibrium" => ControlIntent.Recovery,
        "Meditate" or "Soulsow" or "Meisui" or "Dissipation" => ControlIntent.Preparation,
        "Interject" or "Head Graze" => ControlIntent.Interrupt,
        "Vercure" or "Clemency" => ControlIntent.SingleHealing,
        "Benediction" or "Liturgy of the Bell" or "Synastry" or "Macrocosmos" or "Kardia" or "Pneuma" or "Curing Waltz" or "Improvisation" or "Aurora" => ControlIntent.Support,
        "Astral Flow" => ControlIntent.Mechanics,
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
        content[ControlIntent.Sprint].Add(new(new() { Name = "Sprint", Id = 3, Selector = true }));
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
        if (LayoutPreferences.Role(job.JobId) == JobRole.Healer) AddCapability(ControlIntent.Cleanse, "Esuna");
        if (LayoutPreferences.Role(job.JobId) == JobRole.Tank) AddCapability(ControlIntent.Interrupt, "Interject");
        if (LayoutPreferences.Role(job.JobId) == JobRole.PhysicalRanged) AddCapability(ControlIntent.Interrupt, "Head Graze");
        if (LayoutPreferences.Role(job.JobId) is JobRole.Melee or JobRole.PhysicalRanged) AddCapability(ControlIntent.Recovery, "Second Wind");
        if (LayoutPreferences.Role(job.JobId) == JobRole.Melee) AddCapability(ControlIntent.Recovery, "Bloodbath");
        AddCapability(ControlIntent.Knockback, LayoutPreferences.Role(job.JobId) is JobRole.Healer or JobRole.MagicalRanged ? "Surecast" : "Arm's Length");
        return content;

        void AddCapability(ControlIntent intent, string name)
        {
            var value = resolve(job.Job, name);
            if (!content[intent].Any(c => c.Action.Value == value))
                content[intent].Add(new(new() { Name = name, Type = value.Type, Id = value.Id }));
        }
    }

    internal static readonly Dictionary<uint, string[]> SplitActions = new()
    {
        [23] = ["The Wanderer's Minuet", "Mage's Ballad", "Army's Paeon"],
        [38] = ["Standard Step", "Technical Step"],
        // Umbral Draw is a native replacement, not an assignable action. Keep
        // the draw root so the game alternates it, alongside separate play slots.
        [33] = ["Astral Draw", "Play I", "Play II", "Play III", "Minor Arcana"],
    };

    internal LayoutResult Plan(JobPreset job, AccordSettings settings, Func<int,bool> shared, Func<ActionChoice,bool> available, bool allManual, bool includeDefaultSprint = true)
    {
        var content = Contents(job, settings);
        var owners = content.Values.SelectMany(c => c).Where(c => c.Action.Automation is not null)
            .GroupBy(c => c.Action.Value).ToDictionary(g => g.Key, g => g.First().Action.Automation);
        if (!includeDefaultSprint) content[ControlIntent.Sprint].Clear();
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
