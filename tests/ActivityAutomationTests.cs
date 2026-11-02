using Accord;
using Newtonsoft.Json.Linq;
using WrathCombo.Engine;
using WrathFork.Contract;

internal static class ActivityAutomationTests
{
    internal static int Run(JObject presets)
    {
        int count = 0;
        void Check(bool value, string reason) { count++; if (!value) throw new Exception("Activity automation: " + reason); }
        var names = presets.Properties().Select(p => p.Name).Where(n => n.StartsWith("Variant_") || n.StartsWith("Bozja_") || n.StartsWith("Phantom_") ||
            n is "PoTD_SustainingPotion" or "HoH_EmpyreanPotion" or "EO_OrthosPotion" or "PT_PilgrimsPotion").ToArray();
        foreach (string name in names)
            Check(ActivityPresetPolicy.Actions.ContainsKey(name) || ActivityPresetPolicy.DirectDamage.Contains(name) || ActivityPresetPolicy.Parents.Contains(name) || ActivityPresetPolicy.Preserved.Contains(name), "Unreviewed activity preset " + name);
        var owners = new Dictionary<Feature, VariantId>
        {
            [Feature.ActivityOffense] = VariantId.Burst, [Feature.ActivityDefense] = VariantId.Protection,
            [Feature.ActivityRecovery] = VariantId.Support, [Feature.ActivityUtility] = VariantId.ClassMechanics,
        };
        foreach (uint job in Enumerable.Range(19, 24).Select(i => (uint)i).Where(j => FeatureScope.Supports(j, Feature.ActivityAutomation)))
        for (int mask = 0; mask < 16; mask++)
        {
            var settings = new AccordSettings { ActiveJobs = [job] };
            foreach (var id in Enum.GetValues<VariantId>()) settings.Variants[id] = (mask & (1 << (int)id)) != 0;
            var features = SetupPack.EngineFeatures(job, settings);
            bool Enabled(Feature f) => features.GetValueOrDefault(f);
            Check(Enabled(Feature.ActivityAutomation), "Managed combat class missing capability");
            foreach (var (feature, toggle) in owners) Check(Enabled(feature) == settings.Enabled(toggle, job), "Wrong toggle owner");
            foreach (string name in names)
            {
                var resolved = ActivityPresetPolicy.Resolve(name, false, Enabled);
                if (ActivityPresetPolicy.Preserved.Contains(name)) Check(resolved is null, "Saved policy overridden: " + name);
                else if (ActivityPresetPolicy.DirectDamage.Contains(name)) Check(resolved == true, "Direct damage suppressed by automation toggle: " + name);
                else if (ActivityPresetPolicy.Actions.TryGetValue(name, out var owner)) Check(resolved == settings.Enabled(owners[owner], job), "Action follows wrong toggle: " + name);
                else Check(resolved == (mask != 0 || ActivityPresetPolicy.DirectDamage.Any(action => action.StartsWith(name + "_", StringComparison.Ordinal))), "Parent blocks independent category: " + name);
                Check(ActivityPresetPolicy.Resolve(name, true, Enabled) is null, "Leaked into PvP");
                Check(ActivityPresetPolicy.Resolve(name, false, _ => false) is null, "Changed unconfigured class");
            }
            Check(ActivityPresetPolicy.Resolve("Phantom_Necromancer_DrainTouch", false, Enabled, true) == settings.Enabled(VariantId.Support, job), "Healing Drain Touch follows damage toggle");
            Check(ActivityPresetPolicy.Resolve("GNB_AoE_Advanced", false, Enabled) is null, "Changed ordinary rotation selection");
        }
        var overridden = new AccordSettings();
        overridden.Variants[VariantId.Support] = true;
        overridden.Layouts.Classes[23] = new() { Automation = { [VariantId.Support] = false } };
        overridden.Layouts.PvP.Global.Automation[VariantId.Support] = false;
        Check(!SetupPack.EngineFeatures(23, overridden)[Feature.ActivityRecovery] && SetupPack.EngineFeatures(31, overridden)[Feature.ActivityRecovery], "Class override or PvP overlay escaped scope");
        Check(ActivityPresetPolicy.Actions["Variant_Tank_Cure"] == Feature.ActivityRecovery &&
            ActivityPresetPolicy.Actions["Variant_Tank_Ultimatum"] == Feature.ActivityDefense &&
            ActivityPresetPolicy.DirectDamage.Contains("Variant_Tank_SpiritDart") &&
            ActivityPresetPolicy.Actions["PT_PilgrimsPotion"] == Feature.ActivityRecovery, "Variant or potion ownership changed");
        return count;
    }
}
