using Accord;
using HotbarTools;
using Newtonsoft.Json;

internal static class LayoutPreferencesTests
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool value, string reason) { count++; if (!value) throw new Exception("Layout preferences: " + reason); }
        ActionChoice Action(uint id, VariantId? owner = null, bool selector = false) => new() { Id = id, Name = "Action " + id, Automation = owner, Selector = selector };
        var preferences = new LayoutPreferences();
        preferences.Roles[JobRole.PhysicalRanged] = new() { KeepManual = true };
        preferences.Classes[23] = new() { SplitMechanics = true };
        Check(preferences.Resolve(23).KeepManual && preferences.Resolve(23).SplitMechanics, "role/class inheritance");
        preferences.Global.Mapping.Areas.Add(new() { Name = "Damage", Bar = 1, Last = 4 });
        Check(ReferenceEquals(preferences.Resolve(23).Mapping, preferences.Global.Mapping), "global mapping changes do not flow through");
        preferences.Classes[23].Mapping = new();
        Check(preferences.Resolve(23).Mapping.Areas.Count == 0 && preferences.Resolve(31).Mapping.Areas.Count == 1, "whole mapping override leaks");
        Check(!JsonConvert.SerializeObject(new LayoutOverride()).Contains("Mapping"), "unchecked Apply persisted");
        preferences.Classes[23].Mapping = null;
        var potions = new PersonalGroup { Id = "potions", Contents = [Action(1)] };
        potions.Roles[JobRole.PhysicalRanged] = [Action(2)];
        potions.Classes[23] = [Action(3), Action(4)];
        Check(potions.Resolve(23).Select(a => a.Id).SequenceEqual([3u,4u]) && potions.Resolve(31)[0].Id == 2 && potions.Resolve(19)[0].Id == 1, "child contents do not replace parent");

        preferences = new();
        preferences.Groups.Add(potions);
        preferences.Global.Mapping.Areas =
        [
            new() { Name = "Left", Bar = 3, Last = 4, Entries = [new() { Action = Action(10), Priority = 20 }] },
            new() { Name = "Right", Bar = 3, First = 5, FromRight = true, Entries =
                [new() { Action = Action(10), Priority = 10 }, new() { Action = Action(11, VariantId.Protection) }, new() { GroupId = "potions" }] },
        ];
        LayoutResult Resolve(bool manual = false) => IntentLayoutResolver.Resolve(23, preferences, _ => [], _ => true, _ => true, _ => false, manual);
        var all = Resolve(true);
        Check(all.Valid && all.Controls.Count == 4 && all.Controls.Single(c => c.Action.Id == 10).Position == new Position(2,0), "priority does not pick first placement");
        Check(all.Controls.Single(c => c.Action.Id == 11).Position == new Position(2,11) && all.Controls.Single(c => c.Action.Id == 3).Position == new Position(2,10), "edge-inward ordering or duplicate gap");
        Check(all.Decisions.Count == 1, "duplicate explanation missing");
        Check(Resolve().Controls.All(c => c.Action.Id != 11), "automated control retained without group choice");
        preferences.Global.Mapping.Areas[1].Entries[1].KeepManual = true;
        Check(Resolve().Controls.Any(c => c.Action.Id == 11), "group retain ignored");
        preferences.Global.Mapping.Areas[1].Entries[1].KeepManual = false;
        preferences.Global.KeepManual = true;
        Check(Resolve().Controls.Count == 4, "global retain does not force group");
        preferences.Global.KeepManual = false;
        Check(Resolve().Controls.Count == 3, "global retain overwrote stored group choice");
        preferences.Global.Exclusions.Add(new() { Action = Action(11), ExcludedFrom = [ControlIntent.SingleTarget] });
        Check(Resolve().Controls.Count == 4, "excluded control lost manual access");
        // Automated selectors are intentionally independent even with the same native ID.
        preferences.Global.Mapping.Areas[0].Entries.Add(new() { Action = Action(10, selector: true) });
        Check(Resolve(true).Controls.Count == 5, "selector incorrectly deduplicated");
        preferences.Global.Mapping.Areas[1].First = 12;
        Check(!Resolve(true).Valid, "all-off overflow accepted");
        preferences.Global.Mapping.Areas[1].First = 5;
        preferences.Global.Mapping.Areas[1].Bar = 4;
        Check(!Resolve(true).Valid, "unmanaged mapping accepted");
        preferences.AdditionalBars.Add(4);
        Check(Resolve(true).Valid, "optional managed bar rejected");
        Check(!IntentLayoutResolver.Resolve(23, preferences, _ => [], _ => true, _ => false, b => b == 4, true).Valid, "newly shared managed bar accepted");
        preferences.Global.Mapping.Areas.Add(new() { Bar = 3, Last = 1, Entries = [new() { Action = Action(99) }] });
        Check(!Resolve(true).Valid, "exact slot collision accepted");
        // The native guard must permit live updates during combat but retain loading checks.
        ECommons.DalamudServices.Svc.Condition.InCombat = true;
        ECommons.DalamudServices.Svc.Condition.Blocked = false;
        HotbarLayoutStore.Ready(HotbarTools.NativeHotbars.Mode);
        Check(true, "combat refresh allowed");
        ECommons.DalamudServices.Svc.Condition.InCombat = false;
        ECommons.DalamudServices.Svc.Condition.Blocked = true;
        bool blocked = false;
        try { HotbarLayoutStore.Ready(HotbarTools.NativeHotbars.Mode); } catch (InvalidOperationException) { blocked = true; }
        Check(blocked, "loading guard lost");
        ECommons.DalamudServices.Svc.Condition.Blocked = false;
        var pack = new SetupPack();
        var catalog = new ActionCatalog(null!, null!);
        var configured = new ConfiguredLayout(pack, (job, name) => catalog.ResolveSlot(job, name));
        bool Available(JobPreset job, ActionChoice a) => a.Name switch
        {
            "Second Wind" => LayoutPreferences.Role(job.JobId) is JobRole.Melee or JobRole.PhysicalRanged,
            "Bloodbath" => LayoutPreferences.Role(job.JobId) == JobRole.Melee,
            "Head Graze" => LayoutPreferences.Role(job.JobId) == JobRole.PhysicalRanged,
            "Rampart" or "Reprisal" or "Interject" => LayoutPreferences.Role(job.JobId) == JobRole.Tank,
            "Arm's Length" => LayoutPreferences.Role(job.JobId) is JobRole.Melee or JobRole.PhysicalRanged or JobRole.Tank,
            "Swiftcast" or "Lucid Dreaming" or "Surecast" => LayoutPreferences.Role(job.JobId) is JobRole.Healer or JobRole.MagicalRanged,
            "Esuna" => LayoutPreferences.Role(job.JobId) == JobRole.Healer,
            _ => true,
        };
        var defaults = new AccordSettings();
        Check(configured.InitializeDefaults(defaults.Layouts), "default migration did not run");
        Check(!configured.InitializeDefaults(defaults.Layouts), "default migration repeated");
        Check(defaults.Layouts.Global.Mapping.JobDefaults.Count == 0 && defaults.Layouts.Roles.Count == 0 && defaults.Layouts.Classes.Count == 0,
            "global default manufactured class templates or overrides");
        Check(defaults.Layouts.Global.Mapping.Areas.SelectMany(a => a.Entries).Select(e => e.Intent).ToHashSet().SetEquals(Enum.GetValues<ControlIntent>().Select(i => (ControlIntent?)i)),
            "global default omits an intent");
        Check(!JsonConvert.SerializeObject(defaults.Layouts).Contains("JobDefaults"), "per-class templates leaked into saved global settings");
        var noSprint = configured.Plan(pack.Find(23)!, defaults, _ => false, _ => true, true, includeDefaultSprint: false);
        Check(noSprint.Controls.All(c => c.Action.Name != "Sprint"), "default duplicated shared Sprint");
        foreach (var job in pack.Data.Jobs.Where(j => j.JobId >= 19))
        {
            var known = configured.Contents(job, defaults);
            Check(known[ControlIntent.Other].Count == 0, job.Job + " audited controls still filed as Other: " + string.Join(",", known[ControlIntent.Other].Select(c => c.Action.Name)));
            foreach (bool split in new[] { false, true })
            {
                defaults.Layouts.Global.SplitMechanics = split;
                var envelope = configured.Plan(job, defaults, _ => false, a => Available(job, a), true);
                Check(envelope.Valid, job.Job + " global all-off layout: " + string.Join(";", envelope.Issues.Select(i => i.Message)));
                foreach (var slot in pack.Resolve(job, new()).Slots.Where(s => s.Action != "Empty"))
                    Check(envelope.Controls.Any(c => c.Action.Name == slot.Action), job.Job + " lost " + slot.Action + " from Global");
                var movement = configured.Contents(job, defaults)[ControlIntent.Movement].FirstOrDefault();
                if (movement is not null) Check(envelope.Controls.Any(c => c.Position == new Position(1, 0) && c.Action.Name == movement.Action.Name), job.Job + " movement exception survived");
                if (new uint[] { 24, 28, 33, 40, 27, 35 }.Contains(job.JobId))
                    Check(envelope.Controls.Any(c => c.Position == new Position(1, 11) && c.Action.Name == "Swiftcast"), job.Job + " resurrection absent from common end-slot rule");
                for (int mask = 0; mask < 16; mask++)
                {
                    foreach (var variant in Enum.GetValues<VariantId>()) defaults.Variants[variant] = (mask & (1 << (int)variant)) != 0;
                    var actual = configured.Plan(job, defaults, _ => false, a => Available(job, a), false);
                    Check(actual.Valid && actual.Controls.Count <= envelope.Controls.Count, job.Job + " global layout toggles " + mask + ": " + string.Join(";", actual.Issues.Select(i => i.Message)));
                }
            }
        }
        defaults.Variants.Clear(); defaults.Layouts.Global.SplitMechanics = false;
        var editable = defaults.Layouts.Global.Mapping.Areas;
        var movementEntry = editable.SelectMany(a => a.Entries).Single(e => e.Intent == ControlIntent.Movement);
        editable.Single(a => a.Entries.Contains(movementEntry)).Entries.Remove(movementEntry);
        editable.Single(a => a.Bar == 3).Last = 10;
        editable.Add(new() { Name = "Moved movement", Bar = 3, First = 11, Last = 12, Entries = [movementEntry] });
        foreach (var job in pack.Data.Jobs.Where(j => j.JobId >= 19))
        {
            var moved = configured.Plan(job, defaults, _ => false, a => Available(job, a), true);
            var movement = configured.Contents(job, defaults)[ControlIntent.Movement].FirstOrDefault();
            if (movement is not null) Check(moved.Controls.Any(c => c.Position == new Position(2, 10) && c.Action.Name == movement.Action.Name), "global edit failed to reach " + job.Job);
        }
        var roundTrip = JsonConvert.DeserializeObject<LayoutPreferences>(JsonConvert.SerializeObject(defaults.Layouts))!;
        Check(roundTrip.Global.Mapping.Areas.Any(a => a.Name == "Moved movement"), "global edit did not persist");
        roundTrip.Global.Mapping.Areas.Clear();
        Check(!configured.InitializeDefaults(roundTrip) && roundTrip.Global.Mapping.Areas.Count == 0, "cleared mapping silently returned");
        var legacy = new LayoutPreferences { DefaultsInitialized = true };
        foreach (var job in pack.Data.Jobs.Where(j => j.JobId >= 19)) legacy.Global.Mapping.JobDefaults[job.JobId] = [new()];
        legacy.Roles[JobRole.Healer] = new() { KeepManual = true };
        configured.InitializeDefaults(legacy);
        Check(legacy.Global.Mapping.JobDefaults.Count == 0 && legacy.Classes.Count == 0 && legacy.Roles.Count == 1 && legacy.Roles[JobRole.Healer].KeepManual == true,
            "template migration created overrides or removed an explicit setting");
        var customBefore = new LayoutPreferences(); customBefore.Global.Mapping.Areas.Add(new() { Name = "Personal layout" });
        configured.InitializeDefaults(customBefore);
        Check(customBefore.Global.Mapping.Areas.Single().Name == "Personal layout", "migration replaced a custom global mapping");
        var legacyClass = new LayoutPreferences { DefaultsInitialized = true };
        legacyClass.Classes[23] = new() { Mapping = new() { JobDefaults = new() { [23] = [new() { Entries = [new() { DefaultSlot = 1 }, new() { DefaultSlot = 99 }] }] } } };
        configured.InitializeDefaults(legacyClass);
        Check(legacyClass.Classes.Count == 1 && legacyClass.Classes[23].Mapping!.JobDefaults.Count == 0 &&
            legacyClass.Classes[23].Mapping!.Areas[0].Entries[0].Action!.Name == "Heavy Shot" &&
            legacyClass.Classes[23].Mapping!.Areas[0].Entries[1].Intent == ControlIntent.Sprint,
            "explicit legacy class mapping was not converted");
        var explicitEmpty = JsonConvert.DeserializeObject<LayoutPreferences>(JsonConvert.SerializeObject(new LayoutPreferences
            { MappingVersion = 2, Classes = new() { [23] = new() } }))!;
        Check(!configured.InitializeDefaults(explicitEmpty) && explicitEmpty.Classes.ContainsKey(23), "explicitly added empty override disappeared");
        var bard = pack.Find(23)!;
        var settings = new AccordSettings();
        var rain = new ActionChoice { Name = "Rain of Death", Id = catalog.ResolveSlot("BRD", "Rain of Death").Id };
        settings.Layouts.Global.Exclusions.Add(new() { Action = rain, ExcludedFrom = [ControlIntent.AreaDamage] });
        settings.Layouts.Classes[23] = new() { SplitMechanics = true };
        settings.Layouts.Global.Mapping.Areas =
        [
            new() { Bar = 1, Last = 4, Entries = [new() { Intent = ControlIntent.SingleTarget }, new() { Intent = ControlIntent.AreaDamage }, new() { Intent = ControlIntent.Exclusions }, new() { Intent = ControlIntent.Burst }] },
            new() { Bar = 2, Last = 4, Entries = [new() { Intent = ControlIntent.Mechanics }, new() { Intent = ControlIntent.Travel }] },
            new() { Bar = 3, Last = 4, Entries = [new() { Intent = ControlIntent.ImportantRole, Priority = 20 }, new() { Intent = ControlIntent.Cleanse }] },
            new() { Bar = 3, First = 8, FromRight = true, Entries = [new() { Intent = ControlIntent.Movement }, new() { Intent = ControlIntent.Protection, KeepManual = true }, new() { Intent = ControlIntent.Support, KeepManual = true }] },
        ];
        bool BardAvailable(ActionChoice a) => !new uint[] { 7542, 7561, 7562, 7559, 7531, 7535 }.Contains(a.Id);
        var bardPlan = configured.Plan(bard, settings, _ => false, BardAvailable, true);
        Check(bardPlan.Valid, "Bard acceptance layout does not fit");
        Check(bardPlan.Controls.Single(c => c.Action.Name == "Rain of Death").Position == new Position(0,2), "Bard exclusion placement");
        Check(bardPlan.Controls.Single(c => c.Action.Name == "Repelling Shot").Position == new Position(2,11), "Bard right edge");
        Check(bardPlan.Controls.Single(c => c.Action.Name == "Troubadour").Position == new Position(2,10), "Bard protection order");
        Check(bardPlan.Controls.Single(c => c.Action.Name == "Nature's Minne").Position == new Position(2,9), "Bard support order");
        settings.Variants[VariantId.Protection] = settings.Variants[VariantId.Support] = true;
        Check(configured.Plan(bard, settings, _ => false, BardAvailable, false).Controls.Count == bardPlan.Controls.Count, "Bard retained protection/support vanished");
        var routes = configured.Routes(bard, settings);
        var aoe = catalog.ResolveSlot("BRD", "Quick Nock").Id.ToString();
        Check(routes[aoe]!["Excluded"]!.Values<uint>().Contains(rain.Id), "Rain exclusion not sent to engine route");
        Check(routes[WrathFork.Contract.DirectAction.Id(rain.Id).ToString()]!.Value<bool>("Direct") &&
            routes[WrathFork.Contract.DirectAction.Id(rain.Id).ToString()]!.Value<uint>("Target") == rain.Id, "excluded direct button still uses selector");
        Check(bardPlan.Controls.Single(c => c.Action.Name == "Rain of Death").Action.Id == WrathFork.Contract.DirectAction.Id(rain.Id), "direct control shares its selector's input");
        foreach (var job in pack.Data.Jobs.Where(j => j.JobId >= 19))
        {
            var normal = configured.Contents(job, new());
            var flattened = normal.Values.SelectMany(c => c).ToArray();
            var manualJob = pack.Resolve(job, new());
            Check(manualJob.Slots.Where(s => s.Action != "Empty").All(s => flattened.Any(c => c.Action.Name == s.Action)), job.Job + " lost a manual default control");
        }
        var personal = new AccordSettings(); personal.Variants[VariantId.Support] = true;
        personal.Layouts.Groups.Add(new() { Id = "support", Contents = [new() { Name = "Nature's Minne", Id = catalog.ResolveSlot("BRD", "Nature's Minne").Id }] });
        personal.Layouts.Global.Mapping.Areas = [new() { Entries = [new() { GroupId = "support" }] }];
        Check(configured.Plan(bard, personal, _ => false, _ => true, false).Controls.Count == 0, "personal group lost action automation owner");
        personal.Layouts.Global.Mapping.Areas[0].Entries[0].KeepManual = true;
        Check(configured.Plan(bard, personal, _ => false, _ => true, false).Controls.Count == 1, "personal group manual retention ignored");
        using (var route = new WrathFork.Contract.ActionRouteContext(new() { Excluded = [117] }))
        {
            Check(!WrathFork.Contract.ActionRouteContext.Allowed(117) && WrathFork.Contract.ActionRouteContext.Allowed(110), "route scope did not isolate action");
            using (new WrathFork.Contract.ActionRouteContext(null)) Check(WrathFork.Contract.ActionRouteContext.Allowed(117), "nested direct scope inherited exclusion");
            Check(!WrathFork.Contract.ActionRouteContext.Allowed(117), "nested scope did not restore exclusion");
        }
        Check(WrathFork.Contract.ActionRouteContext.Allowed(117), "exclusion leaked out of route");
        var state = Newtonsoft.Json.Linq.JObject.Parse("{ 'EnabledActionsV6': [], 'ActionRoutes': { '23': { '97': { 'Direct': false, 'Target': 0, 'Excluded': [117] } }, '19': {} } }");
        var patch = Newtonsoft.Json.Linq.JObject.Parse("{ 'Settings': { 'ActionRoutes': { '23': {} } }, 'Enable': [], 'Disable': [] }");
        var cleared = WrathFork.Contract.SettingsPatch.Apply(state, patch, new HashSet<int>());
        Check(!cleared["ActionRoutes"]!["23"]!.HasValues && cleared["ActionRoutes"]!["19"] is not null, "deleted routes survived or other job was lost");
        patch["Settings"]!["ActionRoutes"]!["23"] = Newtonsoft.Json.Linq.JObject.Parse("{ '2000117': { 'Direct': true, 'Target': 117, 'Excluded': [] } }");
        var directState = WrathFork.Contract.SettingsPatch.Apply(state, patch, new HashSet<int>());
        Check(directState["ActionRoutes"]!["23"]!["2000117"]!.Value<uint>("Target") == 117, "direct alias target did not survive transaction");
        patch["Settings"]!["ActionRoutes"]!["23"]!["2000117"]!["Target"] = 118;
        bool badTarget = false;
        try { WrathFork.Contract.SettingsPatch.Apply(state, patch, new HashSet<int>()); } catch (ArgumentException) { badTarget = true; }
        Check(badTarget, "mismatched direct input/target accepted");
        var prior = Newtonsoft.Json.Linq.JObject.Parse("{ 'EnabledActionsV6': [], 'ActionRoutes': {}, 'ForkFeatures': {} }");
        directState["ForkFeatures"] = Newtonsoft.Json.Linq.JObject.Parse("{ '23': { 'BardSongInitiation': true } }");
        var recovered = WrathFork.Contract.SettingsPatch.Apply(directState, WrathFork.Contract.SettingsPatch.RecoveryPatch(prior, directState), new HashSet<int>());
        Check(Newtonsoft.Json.Linq.JToken.DeepEquals(prior, recovered), "recovery left newly introduced routes/features behind");
        return count;
    }
}
