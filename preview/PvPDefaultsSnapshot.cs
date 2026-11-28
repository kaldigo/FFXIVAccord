// Generated from Accord.Plugin/PvPConfiguredLayout.cs: shipped mapping methods only.
using System;
using System.Collections.Generic;
using System.Linq;
using HotbarTools;
using Newtonsoft.Json.Linq;
using WrathFork.Contract;

namespace Accord;

internal static class PvPConfiguredLayout
{
    internal static IntentMapping CompactMapping()
    {
        MappingArea Fixed(int slot, uint id, string name) => new()
        { Name = name, Bar = 1, First = slot, Last = slot, Entries = [new() { KeepManual = true, Action = new() { Id = id, Name = name } }] };
        return new()
        {
            IntentVersion = 3,
            Areas = [
                new() { Name = "Attack", Bar = 1, First = 1, Last = 1, Entries = Entries(ControlIntent.SingleTarget) },
                new() { Name = "Limit breaks", Bar = 1, First = 2, Last = 3, Entries = [new() { Intent = ControlIntent.LimitBreak, KeepManual = true }] },
                Fixed(4, 29711, "Recuperate"), Fixed(5, 29054, "Guard"), Fixed(6, 29056, "Purify"), Fixed(7, 29057, "Sprint"),
                new() { Name = "Role action", Bar = 1, First = 8, Last = 8, Entries = [new() { Intent = ControlIntent.ImportantRole, KeepManual = true }] },
                new() { Name = "Healing and burst", Bar = 1, First = 9, Last = 12,
                    Entries = Entries(ControlIntent.SingleHealing, ControlIntent.Burst, ControlIntent.AdditionalBurst) },
                new() { Name = "Tactical and utility controls", Bar = 2, Entries = Entries(ControlIntent.Recovery,
                    ControlIntent.Movement, ControlIntent.Other, ControlIntent.OtherRole, ControlIntent.Cleanse,
                    ControlIntent.PersonalProtection, ControlIntent.Protection, ControlIntent.Mechanics, ControlIntent.Support,
                    ControlIntent.Preparation, ControlIntent.Exclusions) },
            ],
        };
    }

    internal static IntentMapping DefaultMapping() => new()
    {
        IntentVersion = 3,
        Areas = [
            new() { Name = "Rotation, limit break and healing", Bar = 1, Entries = [
                new() { Intent = ControlIntent.SingleTarget }, new() { Intent = ControlIntent.AreaDamage },
                new() { Intent = ControlIntent.LimitBreak, KeepManual = true },
                new() { Intent = ControlIntent.Recovery, KeepManual = true },
                .. Entries(ControlIntent.SingleHealing, ControlIntent.Other, ControlIntent.Burst, ControlIntent.AdditionalBurst)] },
            new() { Name = "Role action and utility", Bar = 2, Entries = Entries(ControlIntent.ImportantRole,
                ControlIntent.OtherRole, ControlIntent.Movement, ControlIntent.Cleanse, ControlIntent.PersonalProtection,
                ControlIntent.Sprint, ControlIntent.Protection, ControlIntent.Mechanics, ControlIntent.Support,
                ControlIntent.Preparation, ControlIntent.Exclusions) },
        ],
    };

    // Exact historical fingerprint: even a retention or priority edit is a custom mapping.
    internal static IntentMapping PreviousDefaultMapping() => new()
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

}
