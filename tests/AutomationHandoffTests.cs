using Accord;
using Newtonsoft.Json.Linq;
using RotationInterop;

internal static class AutomationHandoffTests
{
    private sealed class Endpoint : IRunnerEndpoint
    {
        internal AutomationLease Lease = new();
        internal long Now;
        internal bool Busy, Unavailable, RejectStart, Absent;
        internal string Build = "initial";
        internal int Starts, Blocks;
        public RunnerStatus Send(string command, Guid token)
        {
            if (Absent) return new(false, false, false, false, "", Absent: true);
            if (command == "block") Blocks++;
            if (Unavailable) throw new InvalidOperationException("Disconnected");
            bool success = command switch
            {
                "block" => Lease.Block(token, Now),
                "renew" => Lease.Renew(token, Now),
                "start" => Start(token),
                "release" => Lease.Release(token, Now),
                "status" => true,
                _ => false,
            };
            return new(success, Lease.OwnedBy(token, Now), Lease.Allowed(Now), Busy, Build);
        }
        private bool Start(Guid token) { Starts++; return !RejectStart && Lease.Start(token, Now, Busy); }
    }

    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        var capability = JObject.Parse("{'Success':true,'Version':1,'Build':'build-id','Running':false,'Managed':false}");
        Check(EngineAvailability.Compatible(true, capability), "Compatible idle fork should be selectable");
        Check(!EngineAvailability.Compatible(false, capability), "Installed but disabled plugin should not be offered as usable");
        Check(!EngineAvailability.Compatible(true, null), "Missing fork API should not be offered");
        Check(!EngineAvailability.Compatible(true, JObject.Parse("{'InternalName':'WrathCombo','Name':'Wrath Combo'}")), "Upstream identity alone is not a compatible engine");
        capability["Version"] = 2;
        Check(!EngineAvailability.Compatible(true, capability), "Unknown ownership protocol must not be offered");
        capability["Version"] = 1; capability.Remove("Running");
        Check(!EngineAvailability.Compatible(true, capability), "Older API without actual runner-state reporting must not be offered");
        var w = new Endpoint(); var r = new Endpoint();
        var coordinator = new AutomationHandoff(w, r);
        void Tick(long now) { w.Now = r.Now = now; coordinator.Tick(now); }
        coordinator.Request(AutomaticRunner.Rsr, 0);
        Check(!w.Lease.Allowed(0) && !r.Lease.Allowed(0), "Request started a runner before settling.");
        w.Busy = true; Tick(500);
        Check(r.Starts == 0 && coordinator.Owner == AutomaticRunner.Off, "Queued action overlapped the new runner.");
        w.Busy = false; Tick(1000);
        Check(coordinator.Owner == AutomaticRunner.Rsr && !w.Lease.Allowed(1000) && r.Lease.Allowed(1000), "RSR handoff failed.");
        Tick(1500);
        Check(r.Starts == 1, "Heartbeat repeatedly restarted the native runner.");
        coordinator.Request(AutomaticRunner.Wrath, 1500); Tick(2000);
        Check(coordinator.Owner == AutomaticRunner.Wrath && w.Lease.Allowed(2000) && !r.Lease.Allowed(2000), "Reverse handoff failed.");
        r.Unavailable = true; Tick(2500);
        Check(coordinator.Owner == AutomaticRunner.Off && !w.Lease.Allowed(2500), "Disconnect left the remaining engine running.");
        r.Unavailable = false;
        coordinator.Request(AutomaticRunner.Rsr, 2500); r.RejectStart = true; Tick(3000);
        Check(!w.Lease.Allowed(3000) && !r.Lease.Allowed(3000), "Rejected start did not stop both engines.");
        r.RejectStart = false;
        coordinator.Request(AutomaticRunner.Rsr, 3000); Tick(3500); Tick(6500);
        Check(coordinator.Owner == AutomaticRunner.Off && !r.Lease.Allowed(6500), "Expired heartbeat restarted automatically.");
        coordinator.Request(AutomaticRunner.Wrath, 6500); r.Build = "reloaded"; Tick(7000);
        Check(coordinator.Owner == AutomaticRunner.Off && !w.Lease.Allowed(7000), "Reload was accepted during transfer.");
        coordinator.Request(AutomaticRunner.Wrath, 7000); w.Busy = true;
        for (long now = 7500; now <= 22000; now += 500) Tick(now);
        Check(coordinator.Owner == AutomaticRunner.Off && !w.Lease.Allowed(22000), "Unsettled queue did not time out.");
        w.Busy = false;
        coordinator.Request(AutomaticRunner.Off, 22000); Tick(22500);
        Check(coordinator.Owner == AutomaticRunner.Off && !r.Lease.Allowed(22500), "Off mode enabled automation.");
        int blocks = r.Blocks;
        w.Unavailable = true;
        coordinator.Request(AutomaticRunner.Rsr, 22500);
        Check(r.Blocks > blocks && !r.Lease.Allowed(22500), "Failed first stop skipped the second engine.");
        w.Unavailable = false;
        coordinator.Release(22500);
        Check(!w.Lease.Managed && !r.Lease.Managed, "Explicit release did not restore native control.");
        r.Absent = true;
        coordinator.Request(AutomaticRunner.Wrath, 23000); Tick(23500);
        Check(coordinator.Owner == AutomaticRunner.Wrath, "Wrath-only installation must run without RSR");
        r.Absent = false; Tick(24000);
        Check(coordinator.Owner == AutomaticRunner.Off, "A newly loaded peer must stop existing automation until coordinated");
        w.Absent = true;
        coordinator.Request(AutomaticRunner.Rsr, 24000); Tick(24500);
        Check(coordinator.Owner == AutomaticRunner.Rsr, "RSR-only installation must run without Wrath");
        r.Absent = true; Tick(25000);
        Check(coordinator.Owner == AutomaticRunner.Off, "Unloading the active runner must stop coordination");
        coordinator.Request(AutomaticRunner.Rsr, 25000); Tick(25500);
        Check(!coordinator.IsCoordinating && coordinator.Owner == AutomaticRunner.Off, "Absent selected runner must never be considered started");
        w.Absent = false; r.Absent = false; r.Unavailable = true;
        coordinator.Request(AutomaticRunner.Wrath, 25500); Tick(26000);
        Check(!coordinator.IsCoordinating && !w.Lease.Allowed(26000), "Loaded incompatible peer must not be treated as absent");
        r.Unavailable = false; r.Absent = true;
        coordinator.Request(AutomaticRunner.Wrath, 26000); Tick(26500);
        coordinator.Release(26500);
        Check(!w.Lease.Managed, "Single-engine release must release the present engine");
        return checks;
    }
}
