using Accord;
using RotationInterop;

internal static class AutomationHandoffTests
{
    private sealed class Endpoint : IRunnerEndpoint
    {
        internal AutomationLease Lease = new();
        internal long Now;
        internal bool Busy, Unavailable, RejectStart;
        internal string Build = "initial";
        internal int Starts, Blocks;
        public RunnerStatus Send(string command, Guid token)
        {
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
        return checks;
    }
}
