using HotbarTools;
using Accord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class PersistentRotationTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string reason) { checks++; if (!ok) throw new Exception(reason); }
        Check(new AccordSettings().AutomaticEngine == null && JsonConvert.DeserializeObject<AccordSettings>("{}")!.AutomaticEngine == null, "Missing engine preference became explicit");
        var old = JsonConvert.DeserializeObject<AccordSettings>("{\"AutomaticEngine\":0}")!;
        Check(old.AutomaticEngine == AutomationEngine.Wrath, "Existing saved engine preference lost on upgrade");
        Check(old.WrathPreferences.Value<bool>("BlackMage") && old.WrathPreferences.Value<bool>("Forecasts") && old.WrathPreferences.Value<bool>("EnemyTargeting"), "Existing installs did not receive enabled rotation improvements");
        Check(old.WrathPreferences.Value<bool>("AcquireVisibleEnemy") && !old.WrathPreferences.Value<bool>("SwitchEnemyTarget"), "Existing installs should acquire empty/self and preserve selected enemies");
        old.WrathPreferences["AcquireVisibleEnemy"] = false;
        old.WrathPreferences["SwitchEnemyTarget"] = true;
        old.WrathPreferences["BlackMage"] = false;
        old.WrathPreferences["EnemyFocus"] = true;
        old.WrathPreferences["EnemyOverrideOrder"] = new JArray("Focus", "Selected", "WorldMouseover");
        old.FollowAutoDuty = true;
        old.AutoRotationEnabled = true;
        var reload = JsonConvert.DeserializeObject<AccordSettings>(JsonConvert.SerializeObject(old))!;
        Check(JToken.DeepEquals(old.WrathPreferences, reload.WrathPreferences) && reload.FollowAutoDuty && reload.AutoRotationEnabled, "Saved preferences did not survive reload");
        var desired = WrathPreferences.Resolve(reload.WrathPreferences);
        Check(!desired.Value<bool>("BlackMage") && desired.Value<bool>("EnemyFocus"), "Explicit choices overwritten by defaults");
        var engine = WrathPreferences.Resolve(new());
        int writes = 0;
        void Write(JObject changes) { writes++; foreach (var item in changes.Properties()) engine[item.Name] = item.Value.DeepClone(); }
        WrathPreferences.Apply(desired, () => (JObject)engine.DeepClone(), Write);
        Check(writes == 1 && JToken.DeepEquals(engine, desired), "Desired settings not applied as one batch");
        WrathPreferences.Apply(desired, () => (JObject)engine.DeepClone(), Write);
        Check(writes == 1, "Verification rewrites unchanged settings during combat");
        var active = (JObject)engine.DeepClone();
        var edit = (JObject)desired.DeepClone();
        edit["BlackMage"] = true;
        void Defer(JObject _) => throw new InvalidOperationException(WrathPreferences.DeferredWrite);
        Check(WrathPreferences.Apply(edit, () => engine, Defer, active) == null && JToken.DeepEquals(engine, active), "Deferred combat edit interrupted a verified profile");
        edit["EnemyFocus"] = false;
        Check(WrathPreferences.Apply(edit, () => engine, Defer, active) == null, "Second combat edit lost the running profile");
        bool failed = false;
        try { WrathPreferences.Apply(edit, () => engine, Defer); } catch (InvalidOperationException) { failed = true; }
        Check(failed, "An unverified profile was treated as ready");
        engine["Forecasts"] = false;
        failed = false;
        try { WrathPreferences.Apply(edit, () => engine, Defer, active); } catch (InvalidOperationException) { failed = true; }
        Check(failed, "Deferred response masked engine drift");
        engine = (JObject)active.DeepClone();
        failed = false;
        try { WrathPreferences.Apply(edit, () => engine, _ => throw new InvalidOperationException("disk failure"), active); }
        catch (InvalidOperationException) { failed = true; }
        Check(failed, "A genuine write failure was ignored");
        Check(JToken.DeepEquals(WrathPreferences.Apply(edit, () => engine, Write, active), edit), "Latest combat edits were not applied when the engine became ready");
        engine = (JObject)active.DeepClone(); writes = 1;
        engine.Remove("AcquireVisibleEnemy");
        WrathPreferences.Apply(desired, () => (JObject)engine.DeepClone(), Write);
        Check(writes == 1 && desired.Value<bool?>("AcquireVisibleEnemy") == false, "Older engine changed saved acquisition preference");
        engine["AcquireVisibleEnemy"] = false;
        engine["BlackMage"] = true;
        bool rejected = false;
        try { WrathPreferences.Apply(desired, () => (JObject)engine.DeepClone(), _ => {}); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Ignored engine mutation was accepted as verified");
        engine.Remove("Forecasts"); rejected = false;
        try { WrathPreferences.Apply(desired, () => (JObject)engine.DeepClone(), Write); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && writes == 1, "Missing API field allowed partial writes");
        engine = WrathPreferences.Resolve(new());
        WrathPreferences.Apply(desired, () => (JObject)engine.DeepClone(), Write);
        Check(JToken.DeepEquals(engine, desired), "Engine reload did not restore saved choices");
        Check(AutomationPreference.Resolve(true, false, false, false, AutomaticRunner.Wrath) == AutomaticRunner.Wrath, "Saved on depended on AutoDuty or transient availability");
        Check(AutomationPreference.Resolve(false, true, true, true, AutomaticRunner.Wrath) == AutomaticRunner.Off, "AutoDuty overrode explicit off");
        Check(AutomationPreference.Resolve(true, true, true, true, AutomaticRunner.Rsr) == AutomaticRunner.Rsr, "Saved following did not resume an active duty");
        Check(AutomationPreference.Resolve(true, true, true, false, AutomaticRunner.Wrath) == AutomaticRunner.Off && reload.AutoRotationEnabled, "Paused activity lost the saved choice");
        var rotation = new SlotValue(1,1000016);
        var spare = new SlotValue(1,99);
        var bars = new Dictionary<Position,SlotValue> { [new(0,3)] = rotation, [new(0,4)] = spare,
            [new(10,14)] = rotation, [new(10,15)] = spare };
        var backup = EmergencyBarPlacement.Plan(24, [(new(0,3),rotation,false),(new(10,14),rotation,false)],
            p => bars.GetValueOrDefault(p), _ => false);
        Check(backup.Count == 4 && backup.Any(e => e.Position == new Position(0,2) && e.After == EmergencyBarPlacement.Area)
            && backup.Any(e => e.Position == new Position(10,13) && e.After == EmergencyBarPlacement.Area), "Joined backup displaced another bound action");
        bars[new(0,7)] = new(1,139);
        backup = EmergencyBarPlacement.Plan(24, [(new(0,3),rotation,false),(new(0,7),new(1,139),true),(new(10,14),rotation,false)],
            p => bars.GetValueOrDefault(p), pos => pos.Bar==10);
        Check(backup.Count==2 && backup.Single(e=>e.After==EmergencyBarPlacement.Area).Position==new Position(0,7), "Split backup changed mapped positions or shared bar");
        return checks;
    }
}
