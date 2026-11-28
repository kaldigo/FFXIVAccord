// Generated from kaldigo/FFXIVAccord 72b5a1debbf903a108c026b698fa8b23ec3f5127, Accord.Plugin/ConfiguredLayout.cs.
// Run Preview/tools/ExportAccordDefaults.ps1 to refresh. No personal configuration.
using HotbarTools;
using System.Collections.Generic;
using System.Linq;
namespace Accord;
internal static class DefaultAccordData
{
    internal const string SourceCommit = "72b5a1debbf903a108c026b698fa8b23ec3f5127";
    internal static IntentMapping CreateDefaults()
    {
        var mapping = CreateVersionTwoDefaults();
        mapping.IntentVersion = 3;
        mapping.Areas.RemoveAll(a => a.Bar == 3);
        mapping.Areas.Single(a => a.Bar == 1).Entries.Insert(5, new() { Intent = ControlIntent.PrimaryMechanics });
        var utility = mapping.Areas.Single(a => a.Bar == 2 && a.First == 1);
        utility.Last = 12; utility.AvoidFixedSlots = true;
        mapping.Areas.RemoveAll(a => a.First == a.Last);
        mapping.Areas.Add(new() { Name = "Right edge", Bar = 2, FromRight = true,
            Entries = Entries(ControlIntent.Raise, ControlIntent.Stance, ControlIntent.Shirk) });
        utility.Entries.AddRange(
            Entries(ControlIntent.HealingProtection, ControlIntent.AdditionalBurst, ControlIntent.Recovery, ControlIntent.Cleanse, ControlIntent.Interrupt, ControlIntent.Sprint));
        AddPull(mapping);
        return mapping;
    }

    private static void AddPull(IntentMapping mapping)
    {
        if (mapping.Areas.SelectMany(a => a.Entries).Any(e => e.Intent == ControlIntent.Pull)) return;
        foreach (var area in mapping.Areas)
        {
            int index = area.Entries.FindLastIndex(e => e.Intent is ControlIntent.SingleTarget or ControlIntent.AreaDamage);
            if (index < 0) continue;
            area.Entries.Insert(index + 1, new() { Intent = ControlIntent.Pull });
            return;
        }
    }

    // Exact released shape, retained to recognize an untouched 0.2.15.6 default.
    private static IntentMapping CreateVersionTwoDefaults() => new()
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
        "Arm's Length" when slot.LogicalSlot == 5 => ControlIntent.Protection,
        "Arm's Length" or "Surecast" => ControlIntent.Knockback,
        "Hallowed Ground" or "Holmgang" or "Living Dead" or "Superbolide" => ControlIntent.Invulnerability,
        "Provoke" => ControlIntent.Enmity,
        "Shirk" => ControlIntent.Shirk,
        "Iron Will" or "Defiance" or "Grit" or "Royal Guard" => ControlIntent.Stance,
        "Swiftcast" => ControlIntent.Raise,
        "Rescue" => ControlIntent.Rescue,
        "Divine Veil" or "Passage of Arms" => ControlIntent.PartyProtection,
        "Bulwark" or "Thrill of Battle" or "Dark Mind" or "Camouflage" => ControlIntent.PersonalProtection,
        "Holos" => ControlIntent.HealingProtection,
        "Riddle of Earth" or "Third Eye" or "Arcane Crest" or "Manaward" or "Radiant Aegis" or "Tempera Coat" => ControlIntent.PersonalProtection,
        "Sheltron" or "Raw Intuition" or "The Blackest Night" or "Heart of Stone" or "Cover" or "Oblation" or "Deployment Tactics" => ControlIntent.TargetedProtection,
        "Second Wind" or "Equilibrium" => ControlIntent.Recovery,
        "Meditate" or "Soulsow" or "Meisui" or "Dissipation" => ControlIntent.Preparation,
        "Interject" or "Head Graze" => ControlIntent.Interrupt,
        "Clemency" => ControlIntent.SingleHealing,
        "Vercure" => ControlIntent.Support,
        "Benediction" or "Liturgy of the Bell" or "Synastry" or "Macrocosmos" or "Kardia" or "Pneuma" or "Curing Waltz" or "Improvisation" or "Aurora" => ControlIntent.Support,
        "Astral Flow" => ControlIntent.Mechanics,
        "Elusive Jump" => ControlIntent.Movement,
        "Technical Step" => ControlIntent.Burst,
        "The Wanderer's Minuet" or "Standard Step" or "Astral Draw" => ControlIntent.Mechanics,
        "Perfect Balance" or "Geirskogul" or "Kassatsu" or "Ten Chi Jin" or "Meikyo Shisui" or "Enshroud" or "Reawaken" or "Flamethrower" or "Transpose" or "Hammer Stamp" => ControlIntent.Mechanics,
        "Nature's Minne" or "Mantra" or "Lux Solaris" or "Closed Position" => ControlIntent.Support,
        "Presence of Mind" or "Chain Stratagem" or "Divination" or "Devilment" => ControlIntent.AdditionalBurst,
        _ when LayoutPreferences.Role(job.JobId) == JobRole.Healer => slot.LogicalSlot switch
        {
            1 => ControlIntent.SingleTarget, 2 => ControlIntent.AreaDamage,
            3 => ControlIntent.SingleHealing, 4 => ControlIntent.AreaHealing,
            5 => ControlIntent.HealingProtection, 10 => ControlIntent.Movement, _ => ControlIntent.Other,
        },
        _ => slot.LogicalSlot switch
        {
            1 => ControlIntent.SingleTarget, 2 => ControlIntent.AreaDamage, 3 => ControlIntent.Burst,
            4 => ControlIntent.Movement, 5 => ControlIntent.Protection, _ => ControlIntent.Other,
        },
    };

}
