using RotationInterop;

internal static class AutomationLeaseTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        var lease = new AutomationLease();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        Check(lease.Allowed(0), "Unmanaged automation must preserve native behavior.");
        Check(!lease.Block(Guid.Empty, 0) && !lease.Managed, "Invalid owner took control.");
        Check(lease.Block(owner, 10) && !lease.Allowed(10), "Acquisition did not stop automation.");
        Check(!lease.Start(stranger, 11, false), "A different owner started automation.");
        Check(!lease.Start(owner, 11, true), "Start overlapped an unsettled native action.");
        Check(lease.Start(owner, 11, false) && lease.Allowed(11), "Valid start failed.");
        Check(!lease.Renew(stranger, 20), "A different owner extended authority.");
        Check(lease.Renew(owner, 100) && lease.Allowed(3099), "Heartbeat did not extend authority.");
        Check(!lease.Allowed(3100) && !lease.Renew(owner, 3100), "Expired authority resurrected.");
        Check(!lease.Start(owner, 3100, false), "Expired authority started automation.");
        Check(lease.Block(owner, 3200) && !lease.Allowed(3200), "Reacquisition resumed automatically.");
        Check(lease.Start(owner, 3201, false), "Reacquired authority cannot start.");
        lease.Stop();
        Check(!lease.Allowed(3202) && lease.Renew(owner, 3202) && !lease.Allowed(3202), "Heartbeat resumed a stopped runner.");
        Check(lease.Start(owner, 3203, false), "Explicit restart failed.");
        Check(!lease.Allowed(3201), "Clock reversal retained authority.");
        Check(lease.Block(stranger, 3210) && !lease.Allowed(3210), "Takeover did not stop the previous runner.");
        Check(!lease.Start(owner, 3211, false), "Previous owner retained authority after takeover.");
        Check(!lease.Release(owner, 3211), "Another controller released ownership.");
        Check(lease.Start(stranger, 3211, false) && !lease.Release(stranger, 3211), "Running authority was released without stopping.");
        lease.Stop();
        Check(lease.Release(stranger, 3212) && lease.Allowed(3212) && !lease.Managed, "Stopped ownership could not be released.");
        var queue = new ExternalQueue();
        queue.Observe(false, true, 1, 10, 20, 10, 20, 100);
        Check(!queue.Matches(1, 10, 20, 101), "Failed external attempt claimed a manual queue.");
        queue.Observe(true, true, 1, 10, 20, 10, 20, 100);
        Check(queue.Matches(1, 10, 20, 101), "Accepted external queue lost native action meaning.");
        Check(!queue.Matches(1, 11, 20, 101) && !queue.Matches(1, 10, 21, 101) && !queue.Matches(2, 10, 20, 101), "Unrelated action bypassed Wrath routing.");
        Check(!queue.Matches(1, 10, 20, 3100) && !queue.Matches(1, 10, 20, 99), "Stale external queue retained ownership.");
        queue.Validate(true, 11, 20);
        Check(!queue.Matches(1, 10, 20, 102), "Overwritten queue retained ownership.");
        queue.Observe(true, true, 1, 10, 20, 10, 20, 200);
        queue.Observe(false, false, 1, 10, 20, 0, 0, 201);
        Check(!queue.Matches(1, 10, 20, 202), "Completed queue retained ownership.");
        return checks;
    }
}
