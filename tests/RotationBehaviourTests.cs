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
        var pack = new SetupPack();
        var before = new AccordSettings { ActiveJobs = pack.Data.Jobs.Select(j => j.JobId).ToHashSet() };
        var after = Clone(before);
        after.SetAutomation(VariantId.Burst, true);
        Check(before.ActiveJobs.Count >= 32 && pack.CurrentVariantJobs(before, after, 38).Select(j => j.JobId).SequenceEqual(new uint[] { 38 }),
            "Global Burst click on Dancer must apply only Dancer even with all jobs selected");
        Check(pack.CurrentVariantJobs(before, after, 0).Length == 0, "Logged-out variant change must not apply inactive jobs");
        Check(Clone(after).Enabled(VariantId.Burst, 41), "Viper must inherit the saved burst choice on switch or reload");
        after.Layouts.Classes[38] = new() { Automation = new() { [VariantId.Burst] = false } };
        Check(pack.CurrentVariantJobs(before, after, 38).Length == 0, "Unaffected Dancer override must skip engine application");
        var settings = new AccordSettings { ActiveJobs = [24, 28, 40, 37, 35] };
        Check(!settings.Layouts.Resolve(24).Rotation.Empty && settings.Layouts.Resolve(24).Rotation.Targeting == EnemySelection.PartyCombat, "Accord must supply defaults for old sparse configurations");
        settings.Layouts.Roles[JobRole.Healer] = new() { Rotation = new() { IncludeHealing = true } };
        Check(settings.Layouts.Resolve(24).Rotation.IncludeHealing == true && settings.Layouts.Resolve(24).Rotation.HealingGcds == false, "Healing weaving must inherit independently of GCD healing");
        settings.Layouts.Classes[40] = new() { Rotation = new() { IncludeHealing = false } };
        Check(Clone(settings).Layouts.Resolve(40).Rotation.IncludeHealing == false, "Explicit healing opt-out must survive serialization");
        settings.Layouts.Global.Rotation = new() { Targeting = EnemySelection.Manual, AoeTargets = 3, Openers = true, OpenerPotions = false };
        settings.Layouts.Roles[JobRole.Healer] = new() { Rotation = new() { Targeting = EnemySelection.PartyCombat } };
        settings.Layouts.Classes[40] = new() { Rotation = new() { AoeTargets = 2, Openers = false } };
        settings.HoldWrathOpeners = true;
        Check(Clone(settings).EffectiveRotation(24).Openers == false && settings.Layouts.Resolve(24).Rotation.Openers == true, "Holding openers must survive reload without overwriting profiles");
        settings.HoldWrathOpeners = false;
        Check(settings.EffectiveRotation(24).Openers == true && settings.EffectiveRotation(40).Openers == false, "Releasing openers must restore each job's preference");
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
        var expectedSettings = JObject.Parse("{'Enabled':true,'Weaving':false}");
        var engine = JObject.Parse("{'Enabled':false,'Weaving':false,'Other':true}");
        int writes = 0;
        VerifiedSettings.Apply(expectedSettings, () => (JObject)engine.DeepClone(), (key, value) => { writes++; engine[key] = value.DeepClone(); });
        Check(writes == 1 && engine.Value<bool>("Other"), "Apply only changed owned settings and preserve unrelated engine settings");
        VerifiedSettings.Apply(expectedSettings, () => (JObject)engine.DeepClone(), (_, _) => throw new Exception("Unnecessary write"));
        engine["Enabled"] = false;
        bool mismatch = false;
        try { VerifiedSettings.Apply(expectedSettings, () => engine, (_, _) => { }); }
        catch (InvalidOperationException) { mismatch = true; }
        Check(mismatch, "A successful IPC response with no applied value must fail read-back");
        VerifiedSettings.Apply(expectedSettings, () => (JObject)engine.DeepClone(), (key, value) => { writes++; engine[key] = value.DeepClone(); });
        Check(writes == 2 && engine.Value<bool>("Enabled"), "Rechecking must repair engine settings drift without a manual apply");
        engine.Remove("Weaving");
        bool missing = false;
        try { VerifiedSettings.Apply(expectedSettings, () => engine, (_, _) => throw new Exception("Mutated before schema validation")); }
        catch (InvalidOperationException) { missing = true; }
        Check(missing, "Missing capability must fail before any settings mutation");
        return count;
    }
}
