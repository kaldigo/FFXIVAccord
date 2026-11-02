using Accord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WrathCombo.Engine;
using WrathFork.Contract;

internal static class RotationBehaviourTests
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool condition, string reason) { count++; if (!condition) throw new Exception("Rotation behaviour: " + reason); }
        T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value))!;
        var settings = new AccordSettings { ActiveJobs = [24, 28, 40, 37, 35] };
        Check(settings.Layouts.Resolve(24).Rotation.Empty, "Existing configurations must preserve Wrath preferences");
        settings.Layouts.Global.Rotation = new() { Targeting = EnemySelection.Manual, AoeTargets = 3, Openers = true, OpenerPotions = false };
        settings.Layouts.Roles[JobRole.Healer] = new() { Rotation = new() { Targeting = EnemySelection.PartyCombat } };
        settings.Layouts.Classes[40] = new() { Rotation = new() { AoeTargets = 2, Openers = false } };
        var sage = settings.Layouts.Resolve(40).Rotation;
        Check(sage.Targeting == EnemySelection.PartyCombat && sage.AoeTargets == 2 && sage.Openers == false && sage.OpenerPotions == false, "Global/role/class precedence");
        settings.Layouts.Global.Rotation.AoeTargets = 5;
        settings.Layouts.Global.Rotation.OpenerPotions = true;
        Check(settings.Layouts.Resolve(24).Rotation.AoeTargets == 5 && settings.Layouts.Resolve(40).Rotation.AoeTargets == 2 && settings.Layouts.Resolve(40).Rotation.OpenerPotions == true, "Global changes must remain live through sparse overrides");
        settings.Layouts.Classes[40].Rotation.AoeTargets = null;
        Check(settings.Layouts.Resolve(40).Rotation.AoeTargets == 5, "Unchecking Apply restores inheritance");
        Check(Clone(settings).Layouts.Resolve(40).Rotation.Openers == false, "Explicit false lost on save/load");
        Check(JObject.FromObject(settings.Layouts.Classes[40].Rotation)["AoeTargets"] is null, "Unchecked setting must not be saved as applied");
        settings.Layouts.PruneUnusedOverrides();
        Check(settings.Layouts.Classes.ContainsKey(40), "Rotation-only overrides must not be pruned");
        settings.Layouts.PvP.Global.Automation[VariantId.Support] = false;
        for (int mask = 0; mask < 16; mask++)
        {
            foreach (var id in Enum.GetValues<VariantId>()) settings.Variants[id] = (mask & (1 << (int)id)) != 0;
            settings.Layouts.Roles[JobRole.Healer].Automation[VariantId.Support] = !settings.Enabled(VariantId.Support);
            settings.Layouts.Classes[40].Automation[VariantId.Support] = settings.Enabled(VariantId.Support);
            foreach (uint job in Enumerable.Range(19, 24).Select(j => (uint)j).Where(j => FeatureScope.Supports(j, Feature.RunnerAutomation)))
            {
                var features = SetupPack.EngineFeatures(job, settings);
                Check(features[Feature.RunnerAutomation], "Missing managed capability");
                Check(features[Feature.RunnerSupport] == settings.Enabled(VariantId.Support, job), "Runner Support ignored override");
                Check(features[Feature.RunnerProtection] == settings.Enabled(VariantId.Protection, job), "Runner Protection ignored override");
            }
            Check(settings.Enabled(VariantId.Support, 24) != settings.Enabled(VariantId.Support), "Role automation not inherited");
            Check(settings.Enabled(VariantId.Support, 40) == settings.Enabled(VariantId.Support), "Class must override role");
            Check(!settings.SharedPvP().Enabled(VariantId.Support, 24), "PvP overlay must remain highest");
        }
        bool globalBefore = settings.Enabled(VariantId.Support);
        settings.SetAutomation(VariantId.Support, true, 24);
        Check(settings.Layouts.Roles[JobRole.Healer].Automation[VariantId.Support] && settings.Enabled(VariantId.Support) == globalBefore, "Server status must update applied role, not global");
        settings.SetAutomation(VariantId.Support, false, 40);
        Check(!settings.Layouts.Classes[40].Automation[VariantId.Support] && settings.Layouts.Roles[JobRole.Healer].Automation[VariantId.Support], "Server status must update highest class override");
        var baseline = Clone(settings.Layouts);
        var draft = Clone(settings.Layouts);
        settings.Layouts.Roles[JobRole.Healer].Automation[VariantId.Support] = false;
        draft.RebaseAutomation(baseline, settings.Layouts);
        Check(!draft.Roles[JobRole.Healer].Automation[VariantId.Support], "Open editor must preserve live role changes");
        // Every eligibility gate is independent: in particular, an activity exit or PvP entry rejects immediately.
        for (int mask = 0; mask < 128; mask++)
        {
            bool Bit(int n) => (mask & (1 << n)) != 0;
            bool expected = Bit(0) && Bit(1) && !Bit(2) && Bit(3) && !Bit(4) && (!Bit(5) || Bit(6));
            Check(UntargetedAoePolicy.Eligible(Bit(0), Bit(1), Bit(2), Bit(3), Bit(4), Bit(5), Bit(6)) == expected, "Untargeted AoE eligibility");
        }
        Check(UntargetedAoePolicy.Suitable(true, 2, false, 1), "One enemy must permit suitable AoE");
        Check(!UntargetedAoePolicy.Suitable(true, 2, false, 0), "Empty area must not trigger AoE");
        Check(!UntargetedAoePolicy.Suitable(false, 2, false, 5), "Targeted circle cannot use self");
        Check(!UntargetedAoePolicy.Suitable(true, 3, false, 5) && !UntargetedAoePolicy.Suitable(true, 4, false, 5), "Directional attacks require a target");
        Check(!UntargetedAoePolicy.Suitable(true, 2, true, 5), "Ground placement is not untargeted self AoE");
        var current = new JObject { ["EnabledActionsV6"] = new JArray(), ["RotationPreferences"] = new JObject {
            ["24"] = JObject.FromObject(new RotationPreferences { Openers = true, AoeTargets = 5 }),
            ["37"] = JObject.FromObject(new RotationPreferences { TargetRange = 20 }) } };
        JObject Patch(JObject preferences) => new() { ["Settings"] = new JObject { ["RotationPreferences"] = preferences }, ["Enable"] = new JArray(), ["Disable"] = new JArray() };
        var cleared = SettingsPatch.Apply(current, Patch(new JObject { ["24"] = new JObject() }), new HashSet<int>());
        Check(((JObject)cleared["RotationPreferences"]!["24"]!).Count == 0 && cleared["RotationPreferences"]!["37"]!["TargetRange"]!.Value<int>() == 20, "Removed settings must not survive merging; other classes stay intact");
        foreach (var invalid in new[] { "{'AoeTargets':0}", "{'AoeTargets':11}", "{'TargetRange':31}", "{'TargetRange':-1}", "{'Targeting':3}", "{'Openers':1}", "{'Unknown':true}" })
        {
            bool rejected = false;
            try { SettingsPatch.Apply(current, Patch(new JObject { ["24"] = JObject.Parse(invalid) }), new HashSet<int>()); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid transport preference accepted: " + invalid);
        }
        return count;
    }
}
