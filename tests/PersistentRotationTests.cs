using Accord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class PersistentRotationTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string reason) { checks++; if (!ok) throw new Exception(reason); }
        var old = JsonConvert.DeserializeObject<AccordSettings>("{\"AutomaticEngine\":0}")!;
        Check(old.WrathPreferences.Value<bool>("BlackMage") && old.WrathPreferences.Value<bool>("Forecasts") && old.WrathPreferences.Value<bool>("EnemyTargeting"), "Existing installs did not receive enabled rotation improvements");
        old.WrathPreferences["BlackMage"] = false;
        old.WrathPreferences["EnemyFocus"] = true;
        old.WrathPreferences["EnemyOverrideOrder"] = new JArray("Focus", "Selected", "WorldMouseover");
        old.FollowAutoDuty = true;
        var reload = JsonConvert.DeserializeObject<AccordSettings>(JsonConvert.SerializeObject(old))!;
        Check(JToken.DeepEquals(old.WrathPreferences, reload.WrathPreferences) && reload.FollowAutoDuty, "Saved preferences did not survive reload");
        var desired = WrathPreferences.Resolve(reload.WrathPreferences);
        Check(!desired.Value<bool>("BlackMage") && desired.Value<bool>("EnemyFocus"), "Explicit choices overwritten by defaults");
        var engine = WrathPreferences.Resolve(new());
        int writes = 0;
        void Write(JObject changes) { writes++; foreach (var item in changes.Properties()) engine[item.Name] = item.Value.DeepClone(); }
        WrathPreferences.Apply(desired, () => (JObject)engine.DeepClone(), Write);
        Check(writes == 1 && JToken.DeepEquals(engine, desired), "Desired settings not applied as one batch");
        WrathPreferences.Apply(desired, () => (JObject)engine.DeepClone(), Write);
        Check(writes == 1, "Verification rewrites unchanged settings during combat");
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
        return checks;
    }
}
