using HotbarTools;
using System.Text.Json;
using Sheets= Lumina.Excel.Sheets;

if(args.Length!=1)throw new ArgumentException("Pass the installed game's sqpack directory.");
using var game=new Lumina.GameData(args[0]);
var rows=game.Excel.GetSheet<Sheets.Action>(Lumina.Data.Language.English);
var catalog=new ActionCatalog(rows,game.Excel.GetSheet<Sheets.CraftAction>(Lumina.Data.Language.English));
var pack=JsonSerializer.Deserialize<PresetPack>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"presets.json")))!;
int assignments=0;
foreach(var job in pack.Jobs)
foreach(var slot in job.Slots)
{
    if(slot.Action=="Empty")continue;
    catalog.ResolveSlot(job.Job,slot.Action);
    assignments++;
}
foreach(var job in pack.Jobs.Where(j=>j.JobId is 16 or 17 or 18))
{
    var assigned=job.Slots.Select(s=>s.Action).ToHashSet(StringComparer.OrdinalIgnoreCase);
    // Every unassigned current action needs a specific native upgrade or enabled engine replacement.
    var covered=job.JobId switch {
        16=>new Dictionary<string,string>{{"Bountiful Yield II","Bountiful Yield"},{"Wise to the World","Solid Reason"}},
        17=>new Dictionary<string,string>{{"Bountiful Harvest II","Bountiful Harvest"},{"Wise to the World","Ageless Words"}},
        _=>new Dictionary<string,string>{{"Hook","Cast"},{"Gig","Cast"},{"Shark Eye","Mooch"},{"Shark Eye II","Mooch II"},
            {"Veteran Trade","Surface Slap"},{"Nature's Bounty","Prize Catch"},{"Salvage","Snagging"},
            {"Vital Sight","Fish Eyes"},{"Baited Breath","Chum"},{"Electric Current","Cast Light"}}
    };
    var enabled=job.Settings["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
    var requiredPresets=job.JobId==18 ? new[]{51002,51008,51003,51004,51005,51006,51007,51009,51010,51011} : new[]{51001,51012};
    if(!requiredPresets.All(enabled.Contains))throw new Exception(job.Job+": required context replacement disabled");
    foreach(var (replacement,root) in covered)
        if(!assigned.Contains(root))throw new Exception(job.Job+": replacement root missing: "+replacement);
    var available=rows.Where(a=>a.IsPlayerAction && !a.IsPvP && a.ClassJob.RowId==job.JobId)
        .Select(a=>a.Name.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    if(!available.SetEquals(assigned.Concat(covered.Keys)))
        throw new Exception(job.Job+": action coverage changed: "+string.Join(", ",available.Except(assigned.Concat(covered.Keys),StringComparer.OrdinalIgnoreCase)));
    if(!covered.Keys.ToHashSet().SetEquals(job.OmittedActions))throw new Exception(job.Job+": omission report does not match replacements");
    if(assigned.Count!=job.Slots.Length)throw new Exception(job.Job+": duplicate action assignments");
    Console.WriteLine($"{job.Job}: {assigned.Count} direct controls + {covered.Count} verified replacement paths cover all {available.Count} current class actions.");
}
Console.WriteLine($"All {assignments} PvE preset assignments resolve against installed game data.");
