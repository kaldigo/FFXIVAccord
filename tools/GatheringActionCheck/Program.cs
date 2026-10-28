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
        16=>new Dictionary<string,string>{{"Scour","Bountiful Yield"},{"Meticulous Prospector","King's Yield II"},{"Brazen Prospector","Sharp Vision III"},{"Collector's Focus","Mountaineer's Gift II"},{"Priming Touch","Nald'thal's Tidings"},{"Scrutiny","The Giving Land"},{"Collect","the Twelve's Bounty"},{"Prospect","Clear Vision"},{"Truth of Mountains","Luck of the Mountaineer"},{"Lay of the Land","Sharp Vision"},{"Lay of the Land II","Sharp Vision II"},{"Sneak","King's Yield"},{"Bountiful Yield II","Bountiful Yield"},{"Wise to the World","Solid Reason"}},
        17=>new Dictionary<string,string>{{"Scour","Bountiful Harvest"},{"Meticulous Woodsman","Blessed Harvest II"},{"Brazen Woodsman","Field Mastery III"},{"Collector's Focus","Pioneer's Gift II"},{"Priming Touch","Nophica's Tidings"},{"Scrutiny","The Giving Land"},{"Collect","the Twelve's Bounty"},{"Triangulate","Flora Mastery"},{"Truth of Forests","Luck of the Pioneer"},{"Arbor Call","Field Mastery"},{"Arbor Call II","Field Mastery II"},{"Sneak","Blessed Harvest"},{"Bountiful Harvest II","Bountiful Harvest"},{"Wise to the World","Ageless Words"}},
        18=>new Dictionary<string,string>{{"Precision Hookset","Patience"},{"Powerful Hookset","Patience II"},{"Double Hook","Prize Catch"},{"Triple Hook","Big-game Fishing"},{"Modest Lure","Surface Slap"},{"Ambitious Lure","Identical Cast"},{"Mooch","Mooch II"},{"Fathom","Bait"},{"Truth of Oceans","Spareful Hand"},{"Hook","Cast"},{"Gig","Cast"},{"Shark Eye","Makeshift Bait"},{"Shark Eye II","Mooch II"},{"Veteran Trade","Surface Slap"},{"Nature's Bounty","Prize Catch"},{"Salvage","Snagging"},{"Vital Sight","Fish Eyes"},{"Baited Breath","Chum"},{"Electric Current","Cast Light"}},
        _=>throw new Exception("Unexpected job")
    };
    var enabled=job.Settings["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
    var requiredPresets=job.JobId==18 ? new[]{51014} : new[]{51001,51013};
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
foreach (var (job, names) in new (string, string[])[]
{
    ("BRD", ["The Wanderer's Minuet", "Mage's Ballad", "Army's Paeon", "Rain of Death", "Second Wind", "Head Graze", "Arm's Length"]),
    ("DNC", ["Standard Step", "Technical Step"]),
    ("AST", ["Astral Draw", "Play I", "Play II", "Play III", "Minor Arcana"]),
    ("WHM", ["Esuna"]), ("SCH", ["Esuna"]), ("SGE", ["Esuna"]), ("AST", ["Esuna"]),
    ("PLD", ["Interject"]), ("WAR", ["Interject"]), ("DRK", ["Interject"]), ("GNB", ["Interject"]),
    ("MCH", ["Head Graze", "Second Wind"]), ("DNC", ["Head Graze", "Second Wind"]),
    ("MNK", ["Second Wind", "Bloodbath", "Arm's Length"]), ("DRG", ["Second Wind", "Bloodbath", "Arm's Length"]),
    ("NIN", ["Second Wind", "Bloodbath", "Arm's Length"]), ("SAM", ["Second Wind", "Bloodbath", "Arm's Length"]),
    ("RPR", ["Second Wind", "Bloodbath", "Arm's Length"]), ("VPR", ["Second Wind", "Bloodbath", "Arm's Length"]),
    ("BLM", ["Surecast"]), ("SMN", ["Surecast"]), ("RDM", ["Surecast"]), ("PCT", ["Surecast"]),
})
    foreach (string name in names)
        Console.WriteLine($"Configurable control: {job} / {name} = {catalog.ResolveSlot(job, name)}");
