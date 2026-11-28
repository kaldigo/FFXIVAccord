using System;
using System.Collections.Generic;
using System.Linq;
using HotbarTools;

namespace Accord;

internal static class EmergencyBarPlacement
{
    internal static readonly SlotValue Single = new(1, 1000017), Area = new(1, 1000018);
    internal static List<SlotEdit> Plan(uint job, IEnumerable<(Position Position, SlotValue Expected, bool Area)> anchors,
        Func<Position, SlotValue> read, Func<Position,bool> shared)
    {
        var edits = new List<SlotEdit>();
        foreach (var layout in anchors.Where(a => !shared(a.Position) && read(a.Position) == a.Expected)
                     .DistinctBy(a => a.Position)
                     .GroupBy(a => a.Position.Bar >= 10))
        {
            foreach (var anchor in layout)
                edits.Add(new(job, anchor.Position, anchor.Expected, anchor.Area ? Area : Single));
            bool hasArea = layout.Any(a => a.Area);
            if (hasArea && layout.Any(a => !a.Area)) continue;
            var source = layout.First().Position;
            var adjacent = Enumerable.Range(0, source.Bar >= 10 ? 16 : 12).Select(i => new Position(source.Bar,i))
                .Where(p => read(p) == default)
                .OrderBy(p => Math.Abs(p.Slot - source.Slot)).ThenByDescending(p => p.Slot)
                .Select(p => (Position?)p).FirstOrDefault();
            if (adjacent is {} empty) edits.Add(new(job, empty, default, hasArea ? Single : Area));
        }
        if (edits.Count == 0) throw new InvalidOperationException("No matching Accord rotation slot was found on an unshared bar.");
        return edits;
    }
}
