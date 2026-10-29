using System;
using System.Collections.Generic;
using System.Linq;
using HotbarTools;

namespace Accord;

// Intent/group definitions have no keyboard or controller coordinates. Only the
// mapping owns destinations, so a future controller editor can reuse the content.
public enum JobRole { Tank, Healer, Melee, PhysicalRanged, MagicalRanged, Noncombat }
public enum ControlIntent { SingleTarget, AreaDamage, Burst, Protection, Support, Mechanics, Movement, Cleanse, ImportantRole, OtherRole, Exclusions, Travel, SingleHealing, AreaHealing, Raise, Knockback, Invulnerability, Enmity, Stance, Shirk, Other, Sprint, PartyProtection, PersonalProtection, TargetedProtection, Recovery, Rescue, Preparation, Interrupt, HealingProtection, AdditionalBurst, PrimaryMechanics }

public sealed class ActionChoice
{
    public string Name { get; set; } = "";
    public byte Type { get; set; } = 1;
    public uint Id { get; set; }
    public bool Selector { get; set; }
    public VariantId? Automation { get; set; }
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public SlotValue Value => new(Type, Id);
}

public sealed class ActionExclusion
{
    public ActionChoice Action { get; set; } = new();
    public HashSet<ControlIntent> ExcludedFrom { get; set; } = new();
}

public sealed class RoleActionSelection
{
    // Eligibility filters this shared default order for each role. Explicit role
    // overrides may replace the complete selection, including with empty lists.
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public List<ActionChoice> Important { get; set; } =
    [
        new() { Id = 7541, Name = "Second Wind" },
        new() { Id = 7551, Name = "Head Graze" },
        new() { Id = 7542, Name = "Bloodbath" },
        new() { Id = 7531, Name = "Rampart" },
        new() { Id = 7535, Name = "Reprisal" },
        new() { Id = 7548, Name = "Arm's Length" },
        new() { Id = 7561, Name = "Swiftcast" },
        new() { Id = 7562, Name = "Lucid Dreaming" },
        new() { Id = 7559, Name = "Surecast" },
    ];
    public List<ActionChoice> Other { get; set; } = new();
}

public sealed class PersonalGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New group";
    public List<ActionChoice> Contents { get; set; } = new();
    public Dictionary<JobRole, List<ActionChoice>> Roles { get; set; } = new();
    public Dictionary<uint, List<ActionChoice>> Classes { get; set; } = new();
    public IReadOnlyList<ActionChoice> Resolve(uint job) => Classes.TryGetValue(job, out var cls) ? cls
        : Roles.TryGetValue(LayoutPreferences.Role(job), out var role) ? role : Contents;
}

public sealed class MappingEntry
{
    // Read only for migration from the rejected per-class templates.
    public int? DefaultSlot { get; set; }
    public bool ShouldSerializeDefaultSlot() => DefaultSlot.HasValue;
    public ControlIntent? Intent { get; set; }
    public string GroupId { get; set; } = "";
    public ActionChoice? Action { get; set; }
    public int Priority { get; set; } = 10;
    public bool KeepManual { get; set; }
}

public sealed class MappingArea
{
    public string Name { get; set; } = "Area";
    public int Bar { get; set; } = 1;
    public int First { get; set; } = 1;
    public int Last { get; set; } = 12;
    public bool FromRight { get; set; }
    public bool AvoidFixedSlots { get; set; }
    // A one-slot area expresses an exact assignment with the same validation.
    public List<MappingEntry> Entries { get; set; } = new();
}

public sealed class IntentMapping
{
    public int IntentVersion { get; set; }
    public bool ShouldSerializeIntentVersion() => IntentVersion != 0;
    // Legacy templates are read only for migration; live layouts use Areas exclusively.
    public Dictionary<uint, List<MappingArea>> JobDefaults { get; set; } = new();
    public List<MappingArea> Areas { get; set; } = new();
    public bool ShouldSerializeJobDefaults() => JobDefaults.Count > 0;
}

public sealed class LayoutValues
{
    public IntentMapping Mapping { get; set; } = new();
    public bool KeepManual { get; set; }
    public bool SplitMechanics { get; set; }
    public RoleActionSelection RoleActions { get; set; } = new();
    public List<ActionExclusion> Exclusions { get; set; } = new();
}

// Null means Apply is unchecked. Only applied values are serialized; the editor
// displays effective inherited values without freezing them into the override.
public sealed class LayoutOverride
{
    // Presence is Apply; false is an explicit manual choice, not inheritance.
    public Dictionary<VariantId, bool> Automation { get; set; } = new();
    public bool ShouldSerializeAutomation() => Automation.Count > 0;
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public IntentMapping? Mapping { get; set; }
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public bool? KeepManual { get; set; }
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public bool? SplitMechanics { get; set; }
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public RoleActionSelection? RoleActions { get; set; }
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public List<ActionExclusion>? Exclusions { get; set; }

    internal LayoutValues Over(LayoutValues parent) => new()
    {
        Mapping = Mapping ?? parent.Mapping, KeepManual = KeepManual ?? parent.KeepManual,
        SplitMechanics = SplitMechanics ?? parent.SplitMechanics,
        RoleActions = RoleActions ?? parent.RoleActions, Exclusions = Exclusions ?? parent.Exclusions,
    };
}

public sealed class LayoutPreferences
{
    public bool DefaultsInitialized { get; set; }
    public int MappingVersion { get; set; }
    public LayoutValues Global { get; set; } = new();
    public Dictionary<JobRole, LayoutOverride> Roles { get; set; } = new();
    public Dictionary<uint, LayoutOverride> Classes { get; set; } = new();
    public List<PersonalGroup> Groups { get; set; } = new();
    public HashSet<int> AdditionalBars { get; set; } = new();
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<int> ManagedBars => new[] { 1, 2, 3 }.Concat(AdditionalBars).Distinct().Order();
    public LayoutValues Resolve(uint job)
    {
        var result = Global;
        if (Roles.TryGetValue(Role(job), out var role)) result = role.Over(result);
        if (Classes.TryGetValue(job, out var cls)) result = cls.Over(result);
        return result;
    }
    public static JobRole Role(uint job) => job switch
    {
        1 or 3 or 19 or 21 or 32 or 37 => JobRole.Tank,
        6 or 24 or 28 or 33 or 40 => JobRole.Healer,
        2 or 4 or 20 or 22 or 29 or 30 or 34 or 39 or 41 => JobRole.Melee,
        5 or 23 or 31 or 38 => JobRole.PhysicalRanged,
        7 or 25 or 26 or 27 or 35 or 42 => JobRole.MagicalRanged,
        _ => JobRole.Noncombat,
    };
    internal void RebaseAutomation(LayoutPreferences baseline, LayoutPreferences latest)
    {
        foreach (var (job, draft) in Classes)
        {
            if (!baseline.Classes.TryGetValue(job, out var before) || !latest.Classes.TryGetValue(job, out var current)) continue;
            foreach (var id in Enum.GetValues<VariantId>())
            {
                bool? oldValue = before.Automation.TryGetValue(id, out var old) ? old : null;
                bool? draftValue = draft.Automation.TryGetValue(id, out var edited) ? edited : null;
                if (oldValue != draftValue) continue; // Explicit unsaved changes still belong to the editor.
                if (current.Automation.TryGetValue(id, out var value)) draft.Automation[id] = value;
                else draft.Automation.Remove(id);
            }
        }
    }

    internal void PruneUnusedOverrides()
    {
        static bool Empty(LayoutOverride o) => o.Automation.Count == 0 && o.Mapping is null && o.KeepManual is null && o.SplitMechanics is null && o.RoleActions is null && o.Exclusions is null;
        foreach (var role in Roles.Where(p => Empty(p.Value)).Select(p => p.Key).ToArray()) Roles.Remove(role);
        foreach (var job in Classes.Where(p => Empty(p.Value)).Select(p => p.Key).ToArray()) Classes.Remove(job);
    }
}

internal sealed record LayoutControl(ActionChoice Action, bool Available = true, bool AlwaysManual = false);
internal sealed record PlacedControl(Position Position, ActionChoice Action, string Source);
internal sealed record LayoutIssue(uint Job, int Bar, string Message);
internal sealed record LayoutResult(IReadOnlyList<PlacedControl> Controls, IReadOnlyList<LayoutIssue> Issues,
    IReadOnlyList<string> Decisions)
{
    internal bool Valid => Issues.Count == 0;
}

internal static class IntentLayoutResolver
{
    private sealed record Candidate(int Order, MappingArea Area, MappingEntry Entry, LayoutControl Control);

    // Call with all automation false for acceptance, then with live choices for
    // rendering. Inventory quantity is deliberately absent from availability.
    internal static LayoutResult Resolve(uint job, LayoutPreferences preferences,
        Func<ControlIntent, IEnumerable<LayoutControl>> intentControls,
        Func<ActionChoice, bool> available, Func<VariantId, bool> automated,
        Func<int, bool> shared, bool allManual = false, Func<ActionChoice, VariantId?>? automationOwner = null)
    {
        var values = preferences.Resolve(job);
        var issues = new List<LayoutIssue>();
        var decisions = new List<string>();
        var placed = new List<PlacedControl>();
        var candidates = new List<Candidate>();
        var bars = preferences.ManagedBars.ToHashSet();
        foreach (int bar in bars)
            if (bar is < 1 or > 10 || shared(bar))
                issues.Add(new(job, bar, bar is < 1 or > 10 ? "Invalid managed bar." : "Managed bar is shared."));
        if (preferences.Groups.Select(g => g.Id).Distinct().Count() != preferences.Groups.Count)
            issues.Add(new(job, 0, "Group identifiers must be unique."));
        int order = 0;
        var areas = values.Mapping.Areas;
        foreach (var area in areas)
        {
            if (!bars.Contains(area.Bar) || area.First < 1 || area.Last > 12 || area.First > area.Last)
            {
                issues.Add(new(job, area.Bar, $"{area.Name}: invalid area or unmanaged bar."));
                continue;
            }
            foreach (var entry in area.Entries)
            {
                if ((entry.Intent.HasValue ? 1 : 0) + (entry.GroupId.Length > 0 ? 1 : 0) + (entry.Action is not null ? 1 : 0) != 1)
                {
                    issues.Add(new(job, area.Bar, $"{area.Name}: choose exactly one intent, group or action."));
                    continue;
                }
                IEnumerable<LayoutControl> controls;
                if (entry.Action is { } action) controls = [new(action)];
                else if (entry.Intent is { } intent)
                    controls = intent switch
                    {
                        ControlIntent.ImportantRole => values.RoleActions.Important.Select(a => new LayoutControl(a)),
                        ControlIntent.OtherRole => values.RoleActions.Other.Select(a => new LayoutControl(a)),
                        ControlIntent.Exclusions => values.Exclusions.Where(e => e.ExcludedFrom.Count > 0).Select(e => new LayoutControl(e.Action, AlwaysManual: true)),
                        _ => intentControls(intent),
                    };
                else
                {
                    var group = preferences.Groups.FirstOrDefault(g => g.Id == entry.GroupId);
                    if (group is null) { issues.Add(new(job, area.Bar, $"{area.Name}: missing group {entry.GroupId}.")); continue; }
                    controls = group.Resolve(job).Select(a => new LayoutControl(a));
                }
                foreach (var control in controls)
                {
                    if (control.Action.Id == 0 || control.Action.Type == 0)
                    {
                        issues.Add(new(job, area.Bar, $"{area.Name}: invalid action or item."));
                        continue;
                    }
                    if (!control.Available || !available(control.Action)) continue;
                    var effective = control;
                    if (control.Action.Automation is null && automationOwner?.Invoke(control.Action) is { } owner)
                    {
                        var a = control.Action;
                        effective = control with { Action = new() { Name = a.Name, Type = a.Type, Id = a.Id,
                            Selector = a.Selector, Automation = owner } };
                    }
                    candidates.Add(new(order++, area, entry, effective));
                }
            }
        }
        // Select winners before visibility, so automation cannot promote a losing
        // duplicate into an area that was not sized for it during all-manual validation.
        var winners = candidates.Where(c => c.Control.Action.Selector).ToHashSet();
        foreach (var duplicates in candidates.Where(c => !c.Control.Action.Selector).GroupBy(c => c.Control.Action.Value))
        {
            var winner = duplicates.OrderByDescending(c => c.Entry.Priority).ThenBy(c => c.Order).First();
            winners.Add(winner);
            foreach (var loser in duplicates.Where(c => c != winner))
                decisions.Add($"{winner.Control.Action.Name}: {winner.Area.Name} wins over {loser.Area.Name} by priority/order.");
        }
        // Preserve manual access for excluded controls even if another entry won placement.
        var excluded = values.Exclusions.Where(e => e.ExcludedFrom.Count > 0).Select(e => e.Action.Value).ToHashSet();
        var occupied = new HashSet<Position>();
        foreach (var area in areas)
        {
            if (!bars.Contains(area.Bar) || area.First < 1 || area.Last > 12 || area.First > area.Last) continue;
            var controls = candidates.Where(c => ReferenceEquals(c.Area, area) && winners.Contains(c))
                .Where(c => allManual || values.KeepManual || c.Entry.KeepManual || c.Control.AlwaysManual ||
                    excluded.Contains(c.Control.Action.Value) || c.Control.Action.Automation is not { } owner || !automated(owner)).ToArray();
            var destinations = Enumerable.Range(area.First, area.Last - area.First + 1)
                .Where(slot => !area.AvoidFixedSlots || !areas.Any(other => other != area && other.Bar == area.Bar && other.First == other.Last && other.First == slot));
            var slots = (area.FromRight ? destinations.Reverse() : destinations).ToArray();
            int capacity = slots.Length;
            if (controls.Length > capacity)
                issues.Add(new(job, area.Bar, $"{area.Name}: {controls.Length} controls need {capacity} slots."));
            for (int i = 0; i < Math.Min(capacity, controls.Length); i++)
            {
                var position = new Position(area.Bar - 1, slots[i] - 1);
                if (!occupied.Add(position)) issues.Add(new(job, area.Bar, $"{area.Name}: collision at slot {position.Slot + 1}."));
                placed.Add(new(position, controls[i].Control.Action, area.Name));
            }
        }
        if (allManual)
            foreach (var exclusion in values.Exclusions.Where(e => e.ExcludedFrom.Count > 0 && available(e.Action)))
                if (!placed.Any(c => !c.Action.Selector && c.Action.Value == exclusion.Action.Value))
                    issues.Add(new(job, 0, $"{exclusion.Action.Name}: add the Exclusions group or a direct action entry to preserve manual access."));
        return new(placed, issues, decisions);
    }
}
