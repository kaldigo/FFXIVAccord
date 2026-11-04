using WrathCombo.Engine;

internal static class DecisionTraceTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }
        var trace = new DecisionTrace();
        trace.Add(0, "SGE", "Selection", 1, 2);
        Check(trace.Snapshot().Length == 0, "Capture must be opt-in");
        trace.Start(100, "SGE");
        trace.Add(101, "SGE", "Selection", 1, 2);
        var before = trace.Snapshot();
        trace.Add(102, "SGE", "Selection", 1, 2);
        Check(trace.Snapshot().Length == 1 && trace.RepeatedSelections == 1, "Repeated UI previews must be collapsed");
        trace.Add(103, "SGE", "Selection", 1, 3);
        trace.Add(104, "SGE", "Send:Action", 0, 3, 99);
        trace.Add(105, "SGE", "Send:Action", 0, 3, 99);
        Check(trace.Snapshot().Length == 4, "Changed suggestions and repeated sends must survive");
        Check(before.Length == 1 && before[0].Output == 2, "Previously read snapshots must remain stable");
        Check(trace.Snapshot()[0].Recipient is null && trace.Snapshot()[2].Recipient == 99, "Suggestions must not invent recipients");
        Check(trace.Snapshot().Select(e => e.Sequence).SequenceEqual(new long[] {1,2,3,4}), "Ordering must survive coalescing");
        trace.Add(106, "DRG", "Selection", 1, 4);
        Check(!trace.Active && trace.Snapshot().Length == 4, "Context change must stop before mixing jobs");
        trace.Start(200, "DRG");
        Check(trace.Snapshot().Length == 0 && trace.RepeatedSelections == 0, "New capture must clear old data");
        trace.Add(60199, "DRG", "Selection", 1, 2);
        trace.Add(60200, "DRG", "Selection", 1, 3);
        Check(!trace.Active && trace.Snapshot().Length == 1, "Deadline is exclusive");
        trace.Start(0, "PvP");
        for (uint i=0; i<2050; i++) trace.Add(i, "PvP", "Send:Action", 0, i, i);
        Check(trace.Dropped == 2 && trace.Snapshot().Length == 2048 && trace.Snapshot()[0].Output == 2, "Overflow must be bounded and reported");
        trace.Stop("User");
        trace.Add(3000, "PvP", "Send:Action", 0, 4);
        Check(trace.Snapshot().Length == 2048 && trace.StopReason == "User", "Stopped capture must be read-only");
        return checks;
    }
}
