using Accord;
using HotbarTools;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json;

internal static class ConfiguredHotbarTests
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool value, string reason) { count++; if (!value) throw new Exception("Configured hotbars: " + reason); }
        bool Reject(Action operation) { try { operation(); return false; } catch { return true; } }
        var root = Path.Combine(Path.GetTempPath(), "accord-configured-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Svc.PluginInterface.Root = root;
        Svc.PluginInterface.InstalledPlugins.Clear();
        Svc.Condition.Blocked = false; Svc.Condition.InCombat = true;
        Svc.PlayerState.ContentId = 123; Svc.ClientState.IsLoggedIn = true;
        Player.Available = true; Player.Job = 23;
        NativeHotbars.Mode = false; NativeHotbars.Shared.Clear(); NativeHotbars.Slots.Clear(); NativeHotbars.FailAt = 0;
        try
        {
            var settings = new AccordSettings();
            var json = JsonConvert.SerializeObject(settings);
            var restored = JsonConvert.DeserializeObject<AccordSettings>(json)!;
            Check(restored.Layouts.Global.RoleActions.Important.Count == settings.Layouts.Global.RoleActions.Important.Count, "serialization duplicates defaults");
            Check(!json.Contains("ManagedBars") && !json.Contains("Normalized"), "derived properties were persisted");
            var pack = new SetupPack(); var preset = pack.Resolve(pack.Find(23)!, settings);
            var writer = new HotbarSetup();
            NativeHotbars.Put(23, new(3,0), new(2,12345));
            NativeHotbars.Put(23, new(2,0), new(1,999));
            var regular = new Dictionary<Position,SlotValue> { [new(0,0)] = new(1,97), [new(2,11)] = new(2,1000455) };
            var plans = new Dictionary<uint,Dictionary<Position,SlotValue>> { [23] = regular };
            writer.ApplyConfigured([(23,preset)], false, plans, [1,2,3]);
            Check(NativeHotbars.Read(23,new(2,0)) == default, "managed bar did not clear old controls");
            Check(NativeHotbars.Read(23,new(3,0)) == new SlotValue(2,12345), "unmanaged bar changed");
            Check(NativeHotbars.Read(23,new(2,11)) == new SlotValue(2,1000455), "item button lost");
            Check(!writer.NeedsConfiguredRefresh(23,preset,false,regular,[1,2,3]), "configured layout never settles");
            Check(writer.ApplyConfigured([(23,preset)],false,plans,[1,2,3]) == "", "unchanged layout replaced recovery backup");
            var controller = NativeHotbars.Slots.Where(p => p.Key.Position.Bar >= 10).ToDictionary();
            regular[new(0,0)] = new(1,98);
            writer.ApplyConfigured([(23,preset)],false,plans,[1,2,3]);
            Check(controller.All(p => NativeHotbars.Slots[p.Key] == p.Value), "regular remapping changed controller placement");
            NativeHotbars.Shared.Add(2);
            int writes = NativeHotbars.Writes;
            Check(Reject(() => writer.ApplyConfigured([(23,preset)],false,plans,[1,2,3])) && writes == NativeHotbars.Writes, "newly shared required bar written");
            NativeHotbars.Shared.Clear();
            var before = NativeHotbars.Slots.ToDictionary();
            regular[new(0,0)] = new(1,99);
            NativeHotbars.FailAt = NativeHotbars.Writes + 1;
            bool engineChanged = false;
            Check(Reject(() => CombinedUpdate.Apply(() => engineChanged = true,
                () => writer.ApplyConfigured([(23,preset)],false,plans,[1,2,3]), () => engineChanged = false)), "combined write failure accepted");
            Check(!engineChanged && before.All(p => NativeHotbars.Slots[p.Key] == p.Value), "combined failure did not restore both stages");
            bool aggregate = false;
            try { CombinedUpdate.Apply(() => {}, () => throw new IOException("write"), () => throw new IOException("recovery")); }
            catch (AggregateException ex) { aggregate = ex.InnerExceptions.Count == 2; }
            Check(aggregate, "recovery failure hidden");
        }
        finally
        {
            NativeHotbars.FailAt = 0; Svc.Condition.InCombat = false;
            Directory.Delete(root, true);
        }
        return count;
    }
}
