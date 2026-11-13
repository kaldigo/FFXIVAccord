using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using HotbarTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WrathFork.Contract;



namespace Accord;

internal sealed class SetupController : IDisposable
{
    internal static SetupController? Instance { get; private set; }
    internal SetupPack Pack { get; } = new();
    private readonly HotbarSetup hotbars = new();
    private readonly List<IDtrBarEntry> entries = new();
    private readonly IDtrBarEntry openers = Svc.DtrBar.Get("Accord Wrath openers");
    private readonly LiveUpdateState updates = new();
    private string status = "Waiting for a character.";
    internal string Status { get => status; private set => status=value; }
    internal PvPSetup PvP { get; }
    internal static unsafe bool IsPvP => FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.Instance()!=null && FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.Instance()->PvPHotbarsActive;
    private bool previousPvP;
    internal bool Live => Settings.LiveMode;
    internal bool VariantEnabled(VariantId id) => Player.Available && Pack.IsActive((uint)Player.Job, Settings)
        ? (pendingChoices ?? Settings).Enabled(id, Pack.Find((uint)Player.Job)!.JobId, IsPvP) : Settings.Enabled(id);
    internal AccordSettings Settings => Plugin.Instance.Settings;
    internal string LayoutStatus { get; private set; } = "";
    private ActionCatalog? layoutCatalog;
    private ConfiguredLayout? configuredLayout;
    private ConfiguredLayout Layouts => configuredLayout ??= new(Pack, (job, action) =>
    {
        layoutCatalog ??= new ActionCatalog(Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>(Dalamud.Game.ClientLanguage.English),
            Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.CraftAction>(Dalamud.Game.ClientLanguage.English));
        return layoutCatalog.ResolveSlot(job, action);
    });

    internal unsafe bool SharedRegularBar(int bar)
    {
        var module = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.Instance();
        return module != null && module->ModuleReady && bar is >= 1 and <= 10 && module->IsHotbarShared((uint)(bar - 1));
    }

    private bool ActionAvailable(JobPreset job, ActionChoice action)
    {
        if (action.Type == 1 && action.Selector && action.Id == 1000016) return ConfiguredLayout.SupportsJoinedDamage(job.JobId);
        if (action.Type != 1) return true; // Inventory count never affects placement.
        var row = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(action.Id);
        if (row is null || !row.Value.IsPlayerAction || row.Value.IsPvP) return false;
        var property = typeof(Lumina.Excel.Sheets.ClassJobCategory).GetProperty(job.Job);
        if (property is null || !(bool)property.GetValue(row.Value.ClassJobCategory.Value)!) return false;
        // Layouts represent the class's complete max-level toolkit. Level sync
        // affects whether the game can execute an action, never its placement.
        // Use catalog eligibility rather than a numeric cap so future caps work too.
        return true;
    }

    private unsafe LayoutResult LayoutPlan(JobPreset job, AccordSettings settings, bool manual)
    {
        using var timing = AccordDiagnostics.PerformanceTimings.Measure(AccordDiagnostics.TimedPath.Plan);
        var module = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.Instance();
        bool sharedSprint = module != null && module->ModuleReady && ModeActions.HasSharedSprint(
            p => module->IsHotbarShared((uint)p.Bar), p => NativeHotbars.Read(job.JobId, p));
        return Layouts.Plan(job, settings, SharedRegularBar, a => ActionAvailable(job, a), manual, !sharedSprint);
    }

    internal LayoutResult InspectLayout(uint job, LayoutPreferences preferences)
    {
        var desired = CloneSettings(); desired.Layouts = preferences;
        return LayoutPlan(Pack.Find(job)!, desired, true);
    }
    internal LayoutResult InspectPvPLayout(uint job, LayoutPreferences preferences)
    {
        var desired = CloneSettings(); desired.Layouts = preferences;
        var profile = PvP.Pack.Find(job)!;
        return PvPConfiguredLayout.Plan(profile, desired.SharedPvP(), engine.Read(), SharedRegularBar,
            a => PvPHotbars.Available(profile, a), allManual: true);
    }
    internal IntentMapping DefaultMapping() => Layouts.CreateDefaults();

    internal void PreviewLayout(uint jobId, LayoutPreferences preferences)
    {
        var desired = CloneSettings(); desired.Layouts = preferences;
        var job = Pack.Find(jobId)!;
        var plan = LayoutPlan(job, desired, true);
        LayoutStatus = job.Job + " — all automation off\n" + string.Join("\n", plan.Issues.Select(i => $"Bar {i.Bar}: {i.Message}")) + "\n" +
            string.Join("\n", plan.Controls.Select(c => $"{c.Position}: {c.Action.Name}")) + "\n" + string.Join("\n", plan.Decisions);
    }

    internal void SaveLayouts(LayoutPreferences preferences)
    {
        var desired = CloneSettings(); desired.Layouts = preferences;
        ApplyChoices(desired);
        var snapshot = engine.Read();
        var issues = PvP.Pack.Jobs.Where(j => desired.ActiveJobs?.Contains(j.JobId) == true)
            .SelectMany(j => PvPConfiguredLayout.Plan(j, desired.SharedPvP(), snapshot, SharedRegularBar,
                a => PvPHotbars.Available(j, a), allManual: true).Issues).ToArray();
        var warnings = issues.GroupBy(i => (Role: LayoutPreferences.Role(i.Job), i.Bar, i.Message)).Select(group =>
        {
            var affected = group.Select(i => i.Job).ToHashSet();
            var roleJobs = PvP.Pack.Jobs.Where(j => LayoutPreferences.Role(j.JobId) == group.Key.Role).Select(j => j.JobId).ToHashSet();
            string who = roleJobs.SetEquals(affected) ? group.Key.Role + " (all classes)" : string.Join(", ", affected.Select(id => Pack.Find(id)!.Job));
            return $"PvP {who}, bar {group.Key.Bar}: {group.Key.Message}";
        });
        LayoutStatus = string.Join("\n", new[] { LayoutStatus }.Concat(warnings).Where(s => s.Length > 0));
    }

    private AccordSettings? pendingChoices;
    private long pendingRetry;
    private bool pendingVariantsOnly;
    private void ApplyChoices(AccordSettings desired, bool variantsOnly = false)
    {
        RequireNormalLayouts();
        var jobs = variantsOnly ? Pack.CurrentVariantJobs(Settings, desired, Player.Available ? (uint)Player.Job : 0) : Pack.SelectedJobs(desired);
        if (!desired.LiveMode || !Player.Available)
        {
            ApplySettings(jobs, desired);
            pendingChoices = null;
            PvP.Invalidate();
            if (!variantsOnly) updates.Request(); else updates.Wake();
            return;
        }
        if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas] ||
            Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51])
        {
            pendingVariantsOnly = variantsOnly;
            pendingChoices = desired;
            Status = "Settings and layout change pending until loading finishes.";
            updates.Request();
            return;
        }
        var previous = CloneSettings();
        if (IsPvP)
        {
            if (variantsOnly && jobs.Length == 0)
                ApplySettings(jobs, desired);
            else PvP.ApplyChoices(desired, before => ApplySettings(jobs, desired, snapshot: before));
            pendingChoices = null;
            return;
        }
        var snapshot = engine.Read();
        JobPreset[] valid = [];
        CombinedUpdate.Apply(() => valid = ApplySettings(jobs, desired, snapshot: snapshot), () =>
        {
            if (valid.Length > 0) ApplyHotbars(valid, null, prepareSettings: false, validated: true);
            pendingChoices = null;
            if (!variantsOnly) updates.Request(); else updates.Wake();
            PvP.Invalidate();
        }, () => engine.RestoreSettings(snapshot, previous));
    }
    private readonly EngineClient engine;
    private readonly ManualFallback fallback = new();
    private readonly ManualFallbackLifecycle logout;
    internal string ManualStatus => string.IsNullOrEmpty(logout.Error) ? fallback.Status : logout.Error;
    private bool disposed;
    internal static bool Enabled(VariantId id) => Instance?.VariantEnabled(id) ?? false;

    internal SetupController()
    {
        Instance = this;
        var previousLayouts = Settings.Layouts;
        var migrated = ConfiguredLayout.Clone(Settings);
        if (Layouts.InitializeDefaults(migrated.Layouts))
        {
            if (previousLayouts.DefaultsInitialized || previousLayouts.Global.Mapping.Areas.Count > 0)
                File.WriteAllText(Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(),
                    $"layout-before-global-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.json"), JsonConvert.SerializeObject(Settings, Formatting.Indented));
            try { Settings.Layouts = migrated.Layouts; AccordDiagnostics.PerformanceTimings.SaveConfig(Settings); }
            catch { Settings.Layouts = previousLayouts; throw; }
        }
        engine = new EngineClient(() => {updates.Wake(); PvP?.Invalidate();});
        logout = new ManualFallbackLifecycle(() =>
        {
            if (Settings.ManualLogoutFallback)
                PrepareManualHotbars(exiting: true);
        });
        Svc.ClientState.Login += FallbackLogin;
        PvP = new PvPSetup(engine);
        foreach (var definition in VariantDefinitions.All)
        {
            var entry = Svc.DtrBar.Get("Accord " + definition.Label);
            entry.Shown = false;
            entry.OnClick = _ => Run(() => SetVariant(definition.Id, !VariantEnabled(definition.Id)));
            entry.Tooltip = definition.Label + ": click to switch A/M.\n" + definition.Description;
            entries.Add(entry);
        }
        openers.Shown = false;
        openers.OnClick = _ => Run(ToggleWrathOpeners);
        Settings.ActiveJobs ??= Pack.SelectedJobs(Settings).Select(j => j.JobId).ToHashSet();
        // Plugin construction may run off the framework thread. Tick updates
        // visibility and text once player state can safely be read.
    }

    internal void Run(Action operation)
    {
        if (disposed) return;
        if (!Svc.Framework.IsInFrameworkUpdateThread)
        {
            _ = Svc.Framework.RunOnFrameworkThread(() => Run(operation));
            return;
        }
        try { operation(); }
        catch (Exception ex)
        {
            AccordDiagnostics.DiagnosticLog.Event("Error.Setup", () => new { Error = ex.ToString() });
            if (Status != ex.Message) Svc.Log.Error(ex, "Accord setup operation failed");
            Status = ex.Message;
        }
    }

    internal void Tick()
    {
        if (HotbarLayoutStore.Loading)
        {
            Status = "Live layout waiting for loading to finish.";
            return;
        }
        if (pendingChoices is not null && Player.Available)
        {
            if (Environment.TickCount64 < pendingRetry) return;
            pendingRetry = Environment.TickCount64 + 2000;
            try { ApplyChoices(pendingChoices, pendingVariantsOnly); }
            catch (AggregateException) { pendingChoices = null; throw; }
            catch (Exception ex) { Status = "Settings and layout update pending: " + ex.Message; return; }
        }
        if (fallback.Pending)
        {
            // Manual layouts are a separate mode, including while the compatible engine is absent.
            // Never let regular live repair or a PvP transition consume the original snapshot.
            if (fallback.Holding) return;
            if (Environment.TickCount64 < fallbackRetry) return;
            fallbackRetry = Environment.TickCount64 + 2000;
            try { RestoreAccordHotbars(); }
            catch (Exception ex) { Status = "Manual hotbars retained. " + ex.Message; return; }
        }
        if (IsPvP != previousPvP) { previousPvP=IsPvP; updates.Request(); PvP.Invalidate(); }
        uint observed = Player.Available ? (uint)Player.Job : 0;
        ulong observedId = Player.Available && Svc.ClientState.IsLoggedIn ? Svc.PlayerState.ContentId : 0;
        if (!updates.Poll(Environment.TickCount64, observed, observedId)) return;
        try { TickReady(); }
        finally { updates.DeferRetry(Environment.TickCount64); }
    }

    private long fallbackRetry;
    private void FallbackLogin() { fallback.Login(); fallbackRetry = 0; updates.Invalidate(); }
    internal void PrepareManualHotbars() => PrepareManualHotbars(false);
    private void PrepareManualHotbars(bool exiting)
    {
        if (Plugin.Instance.MigrationPending || engine.HasPending)
            throw new InvalidOperationException("Complete the pending settings operation before preparing manual layouts.");
        SyncEmergencyPlacement(restoreOnly: true);
        fallback.Prepare(Pack.SelectedJobs(Settings).Select(j => j.JobId), exiting);
        Status = fallback.Status;
        updates.Invalidate();
    }
    internal void RestoreAccordHotbars()
    {
        if (!fallback.Pending) { Status = "No manual fallback is pending for this character."; return; }
        if (Plugin.Instance.MigrationPending || engine.HasPending)
            throw new InvalidOperationException("Complete the pending settings operation before restoring Accord layouts.");
        engine.Read(); // Require the compatible, responding engine, not just an installed plugin name.
        fallback.Restore(Pack.SelectedJobs(Settings).Select(j => j.JobId));
        Status = fallback.Status;
        updates.Invalidate();
        PvP.Invalidate();
    }
    internal void SetManualFallback(bool enabled) => Mutate(() => Settings.ManualLogoutFallback = enabled);
    private void RequireNormalLayouts()
    {
        if (fallback.Pending) throw new InvalidOperationException("Manual fallback is active. Use Restore Accord hotbars before changing Accord layouts or settings.");
    }

    private string EmergencyJournal => Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(),
        "emergency-hotbars", NativeHotbars.CharacterKey(Svc.PlayerState.ContentId) + ".json");
    private unsafe List<SlotEdit> EmergencyPlan()
    {
        HotbarLayoutStore.Ready(false);
        if (!Svc.PluginInterface.GetIpcSubscriber<bool>("WrathCombo.EmergencyHotbars.v1").InvokeFunc())
            throw new InvalidOperationException("Update the Wrath testing plugin before enabling hotbar backup.");
        uint job = (uint)Player.Job;
        if (fallback.Pending || !Pack.IsActive(job, Settings) || !ConfiguredLayout.SupportsJoinedDamage(job))
            throw new InvalidOperationException("Choose an active PvE combat job with its Accord layout.");
        var preset = Pack.Find(job)!;
        var contents = Layouts.Contents(preset, Settings);
        var st = contents[ControlIntent.SingleTarget].Select(c => c.Action.Value).ToHashSet();
        var aoe = contents[ControlIntent.AreaDamage].Select(c => c.Action.Value).ToHashSet();
        var anchors = new List<(Position Position, SlotValue Expected, bool Area)>();
        foreach (var c in LayoutPlan(preset, Settings, false).Controls)
            if (st.Contains(c.Action.Value) || aoe.Contains(c.Action.Value))
                anchors.Add((c.Position, c.Action.Value, aoe.Contains(c.Action.Value)));
        foreach (var slot in preset.Slots.Where(s => s.LogicalSlot is 1 or 2))
        {
            var source = new Position(slot.RegularBar-1, slot.RegularSlot-1);
            var map = Defaults.Maps().FirstOrDefault(m => m.Applies(job) && m.Regular == source && !m.SharedAcrossJobs);
            if (map == null) continue;
            foreach (var value in slot.LogicalSlot == 1 ? st : aoe)
                anchors.Add((map.Cross, value, slot.LogicalSlot == 2));
        }
        return EmergencyBarPlacement.Plan(job, anchors, p => NativeHotbars.Read(job,p),
            p => NativeHotbars.Ready(job)->IsHotbarShared((uint)p.Bar));
    }
    internal void CheckEmergencyPlacement() => EmergencyPlan();
    internal unsafe bool SyncEmergencyPlacement(bool restoreOnly = false)
    {
        if (!Player.Available || IsPvP || Svc.PlayerState.ContentId == 0) return false;
        var path = EmergencyJournal;
        bool active = Settings.EmergencyRotation && !restoreOnly;
        if (!active && !File.Exists(path)) return false;
        HotbarLayoutStore.Ready(false);
        uint current = (uint)Player.Job;
        List<SlotEdit>? edits = null;
        if (File.Exists(path))
        {
            edits = System.Text.Json.JsonSerializer.Deserialize<List<SlotEdit>>(File.ReadAllText(path), JsonStore.Options);
            if (edits is not {Count: > 0} || edits.Any(e => !e.Position.Valid || e.Job != edits[0].Job ||
                    e.After != EmergencyBarPlacement.Single && e.After != EmergencyBarPlacement.Area))
                throw new InvalidDataException("Invalid emergency hotbar restore record.");
            if (!active || edits[0].Job != current)
            {
                foreach (var e in edits)
                {
                    if (NativeHotbars.Ready(current)->IsHotbarShared((uint)e.Position.Bar))
                        throw new InvalidOperationException("Unshare the backup bar before restoring it.");
                    if (NativeHotbars.Read(e.Job,e.Position) == e.After) NativeHotbars.Write(current,e.Job,e.Position,e.Before);
                }
                NativeHotbars.Flush(current);
                File.Delete(path);
                edits = null;
            }
        }
        if (!active || !Pack.IsActive(current, Settings) || !ConfiguredLayout.SupportsJoinedDamage(current)) return false;
        if (edits == null)
        {
            edits = EmergencyPlan();
            JsonStore.Write(path, edits);
            if (edits.GroupBy(e => e.Position.Bar >= 10).Any(group => group.Select(e => e.After).Distinct().Count() < 2))
                Svc.Chat.Print("Wrath backup replaced the existing rotation button. That bar has no empty slot for the second backup button.");
        }
        bool changed = false;
        foreach (var e in edits)
            if (!NativeHotbars.Ready(current)->IsHotbarShared((uint)e.Position.Bar) && NativeHotbars.Read(e.Job,e.Position) == e.Before)
            {
                NativeHotbars.Write(current,e.Job,e.Position,e.After);
                changed = true;
            }
        if (changed) NativeHotbars.Flush(current);
        Status = "Wrath backup is active on the rotation hotbar.";
        return true;
    }

    private string? lastEngineInstance;
    private (AccordSettings Settings, uint Job, ulong Character, int Shared, bool Sprint)? layoutKey;
    private Dictionary<Position, SlotValue>? cachedControls;
    private JobPreset? cachedPreset;
    private HashSet<int>? cachedBars;
    private readonly SlotValue[] observedSlots = new SlotValue[10 * 12 + 8 * 16];
    private bool slotsVerified;

    private bool ManagedSlotsChanged(uint job, int sharedMask)
    {
        bool changed = !slotsVerified;
        int index = 0;
        for (int bar = 0; bar < 18; bar++)
        {
            if ((sharedMask & (1 << bar)) != 0) continue;
            for (int slot = 0; slot < (bar < 10 ? 12 : 16); slot++)
            {
                var actual = NativeHotbars.Read(job, new Position(bar, slot));
                changed |= observedSlots[index] != actual;
                observedSlots[index++] = actual;
            }
        }
        return changed;
    }
    private unsafe (int Shared, bool Sprint) LayoutEnvironment(uint job)
    {
        var module = NativeHotbars.Ready(job);
        int mask = 0;
        for (int bar = 0; bar < 18; bar++)
            if (module->IsHotbarShared((uint)bar)) mask |= 1 << bar;
        return (mask, ModeActions.HasSharedSprint(p => (mask & (1 << p.Bar)) != 0, p => NativeHotbars.Read(job, p)));
    }
    private void TickReady()
    {
        using var timing = AccordDiagnostics.PerformanceTimings.Measure(AccordDiagnostics.TimedPath.LiveLayout);
        if (SyncEmergencyPlacement()) { UpdateEntries(); return; }
        if (engine.HasPending)
        {
            if (engine.Recover()) { updates.Invalidate(); UpdateEntries(); Status = "Settings operation recovered. Layout updates will resume after state verification."; }
        }
        if (Plugin.Instance.MigrationPending) return;
        if (Player.Available && Svc.ClientState.IsLoggedIn) SprintHotbars.Refresh(IsPvP);
        UpdateEntries();
        if(IsPvP){PvP.Tick();Status=PvP.Status;return;}
        if (!Player.Available || !Svc.ClientState.IsLoggedIn)
        {
            updates.Invalidate();

            return;
        }


        if (!Pack.IsActive((uint)Player.Job, Settings))
        {
            updates.Invalidate();
            Status = "Accord is inactive for this class. Choose active classes in /accord.";
            return;
        }

        JObject snapshot;
        try { snapshot = engine.Stamp(); }
        catch { updates.Invalidate(); throw; }
        // Re-read current settings on each transition. ApplySettings changes only Accord-owned
        // switches for prepared jobs; unrelated personal edits must not freeze live layouts.
        if (Settings.EngineConfigurationId != snapshot.Value<string>("ConfigurationId") ||
            lastEngineInstance != snapshot.Value<string>("Instance")) updates.Invalidate();
        lastEngineInstance = snapshot.Value<string>("Instance");
        if (snapshot["Hold"] != null)
        {
            if (!Settings.OpenerHoldMigrated && Settings.HoldWrathOpeners)
            {
                var migrated = CloneSettings();
                migrated.OpenerHoldMigrated = true;
                engine.SetOpenerHold(true);
                ApplySettings(Pack.SelectedJobs(migrated), migrated);
            }
            if (snapshot.Value<bool>("Hold") != Settings.HoldWrathOpeners)
                engine.SetOpenerHold(Settings.HoldWrathOpeners);
        }
        uint job = (uint)Player.Job;
        ulong contentId = Svc.PlayerState.ContentId;
        if (updates.Applied)
        {
            if (Settings.LiveMode)
            {
                var preset = Pack.Find(job)!;
                if (preset.JobId < 19)
                {
                    if (hotbars.NeedsRefresh(job, Pack.Resolve(preset, Settings), Settings.ClearOtherActions)) ApplyCurrentHotbars(prepareSettings: false);
                    return;
                }
                var environment = LayoutEnvironment(job);
                var key = (Settings, job, contentId, environment.Shared, environment.Sprint);
                if (layoutKey != key)
                {
                    var validation = LayoutPlan(preset, Settings, true);
                    if (!validation.Valid) { LayoutStatus = string.Join("\n", validation.Issues.Select(i => $"{preset.Job}, bar {i.Bar}: {i.Message}")); Status = LayoutStatus; return; }
                    cachedControls = LayoutPlan(preset, Settings, false).Controls.ToDictionary(c => c.Position, c => c.Action.Value);
                    cachedPreset = Pack.Resolve(preset, Settings);
                    cachedBars = Settings.Layouts.ManagedBars.ToHashSet();
                    layoutKey = key;
                    slotsVerified = false;
                }
                if (ManagedSlotsChanged(job, environment.Shared))
                {
                    slotsVerified = false;
                    if (hotbars.NeedsConfiguredRefresh(job, cachedPreset!, Settings.ClearOtherActions, cachedControls!, cachedBars!))
                        ApplyCurrentHotbars(prepareSettings: false);
                    else slotsVerified = true;
                }
                else Status = "Live layout up to date; watching for hotbar changes." + hotbars.Notice;
            }
            return;
        }
        if (contentId == 0 || Pack.Find(job) is null) return;
        // Wait for the game to finish activating this job before consuming the transition.
        unsafe { NativeHotbars.Ready(job); }
        updates.Apply(job, contentId, () =>
        {
            if (Settings.LiveMode) ApplyCurrentHotbars();
            else
            {
                Prepare([Pack.Find(job)!]);
                Status = $"{Pack.Find(job)!.Job}: behaviour ready. Static hotbars unchanged.";
            }
        });
    }

    private void Mutate(Action mutation)
    {
        displayedEntries = null; layoutKey = null; verifiedJob = null;
        if (engine.HasPending) throw new InvalidOperationException("Resolve the pending settings operation before changing Accord choices.");
        var before = JsonConvert.SerializeObject(Settings);
        try { mutation(); AccordDiagnostics.PerformanceTimings.SaveConfig(Settings); }
        catch { Plugin.Instance.Settings = JsonConvert.DeserializeObject<AccordSettings>(before)!; throw; }
        UpdateEntries();
    }

    private (uint Job, ulong Character, string? Instance, string? Revision, AccordSettings Settings)? verifiedJob;
    internal void ApplyAutomationSettings()
    {
        if (engine.HasPending) engine.Recover();
        if (!Player.Available || !Pack.IsActive((uint)Player.Job, Settings))
            throw new InvalidOperationException("Select the current class in Accord before enabling automation.");
        var job = Pack.Find((uint)Player.Job)!;
        var stamp = engine.Stamp();
        var character = Svc.PlayerState.ContentId;
        var identity = stamp.Value<string>("Instance");
        var revision = stamp.Value<string>("Revision");
        if (!IsPvP && revision == Settings.LastEngineRevision &&
            verifiedJob == (job.JobId, character, identity, revision, Settings)) return;
        if (!IsPvP && !LayoutPlan(job, Settings, true).Valid)
            throw new InvalidOperationException("Resolve the current class's layout issues before enabling automation.");
        ApplySettings([job], CloneSettings());
        if (!IsPvP)
        {
            var expected = JObject.FromObject(stamp["Hold"] != null ? Settings.Layouts.Resolve(job.JobId).Rotation : Settings.EffectiveRotation(job.JobId));
            var actual = engine.Read()["Settings"]?["RotationPreferences"]?[job.JobId.ToString()];
            if (!JToken.DeepEquals(expected, actual))
                throw new InvalidOperationException("Wrath's rotation preferences did not match Accord after applying settings.");
            verifiedJob = (job.JobId, character, identity, Settings.LastEngineRevision, Settings);
            AccordDiagnostics.DiagnosticLog.Event("Settings.WrathVerified", () => new { Job = job.JobId, Preferences = actual });
        }
    }

    private void Prepare(IEnumerable<JobPreset> jobs, bool reset = false) => ApplySettings(jobs, CloneSettings(), reset);
    private AccordSettings CloneSettings() => JsonConvert.DeserializeObject<AccordSettings>(JsonConvert.SerializeObject(Settings))!;

    private JobPreset[] ApplySettings(IEnumerable<JobPreset> jobs, AccordSettings desired, bool reset = false, JObject? snapshot = null)
    {
        RequireNormalLayouts();
        if (Plugin.Instance.MigrationPending) throw new InvalidOperationException("Open /accord to complete separate setup first.");
        var selectedJobs = jobs.Where(j => Pack.IsActive(j.JobId, desired)).ToArray();
        var invalid = new List<LayoutIssue>();
        var rejected = new HashSet<uint>();
        foreach (var j in selectedJobs)
        {
            if (IsPvP || j.JobId < 19) continue;
            var validation = LayoutPlan(j, desired, true);
            invalid.AddRange(validation.Issues);
            if (!validation.Valid) rejected.Add(j.JobId);
        }
        var managedJobs = selectedJobs.Where(j => !rejected.Contains(j.JobId)).ToArray();
        LayoutStatus = string.Join("\n", invalid.GroupBy(i => (Role: LayoutPreferences.Role(i.Job), i.Bar, i.Message)).Select(group =>
        {
            var affected = group.Select(i => i.Job).ToHashSet();
            var roleJobs = Pack.Data.Jobs.Where(j => j.JobId >= 19 && LayoutPreferences.Role(j.JobId) == group.Key.Role).Select(j => j.JobId).ToHashSet();
            string who = roleJobs.SetEquals(affected) ? group.Key.Role + " (all classes)" : string.Join(", ", affected.Select(id => Pack.Find(id)!.Job));
            return $"{who}, bar {group.Key.Bar}: {group.Key.Message}";
        }));
        desired.ActiveJobs = Pack.SelectedJobs(desired).Select(j => j.JobId).ToHashSet();
        if (managedJobs.Length == 0)
        {
            Mutate(() => Plugin.Instance.Settings = desired);
            return managedJobs;
        }
        snapshot ??= engine.Read();
        if (desired.EngineConfigurationId != snapshot.Value<string>("ConfigurationId"))
        {
            desired.PreparedJobs.Clear();
            desired.SharedSettingsPrepared = false;
        }
        bool nativeHold = engine.Stamp()["Hold"] != null;
        var changes = new JObject();
        var enable = new HashSet<int>();
        var disable = new HashSet<int>();
        foreach (var job in managedJobs)
        {
            var resolved = Pack.Resolve(job, desired);
            bool baseline = reset || desired.PreparedJobs.GetValueOrDefault(job.JobId) != job.SetupRevision;
            var selected = resolved.Settings["EnabledActionsV6"]!.AsArray().Select(n => n!.GetValue<int>()).ToHashSet();
            if (baseline)
            {
                var fragment = JObject.Parse(resolved.Settings.ToJsonString());
                fragment.Remove("EnabledActionsV6");
                changes.Merge(fragment, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
                disable.UnionWith(job.OwnedPresetIds);
                enable.UnionWith(selected);
                desired.PreparedJobs[job.JobId] = job.SetupRevision;
            }
            else
            {
                var controlled = Pack.ControlledPresets(job);
                disable.UnionWith(controlled);
                enable.UnionWith(selected.Where(controlled.Contains));
            }
            WrathFork.Contract.SettingsPatch.NormalizePresetMaps(changes, (JObject)snapshot["PresetIds"]!);
            if (changes["AutoActions"] is not JObject) changes["AutoActions"] = new JObject();
            ((JObject)changes["AutoActions"]!).Merge(Pack.AutoModes(job, desired, (JObject)snapshot["PresetIds"]!));
            if (changes["ForkFeatures"] is not JObject) changes["ForkFeatures"] = new JObject();
            changes["ForkFeatures"]![job.JobId.ToString()] = JObject.FromObject(SetupPack.EngineFeatures(job.JobId, desired));
            if (FeatureScope.Supports(job.JobId, Feature.RunnerAutomation))
            {
                if (changes["RotationPreferences"] is not JObject) changes["RotationPreferences"] = new JObject();
                changes["RotationPreferences"]![job.JobId.ToString()] = JObject.FromObject(nativeHold ? desired.Layouts.Resolve(job.JobId).Rotation : desired.EffectiveRotation(job.JobId));
            }
            if (job.JobId >= 19)
            {
                if (changes["ActionRoutes"] is not JObject) changes["ActionRoutes"] = new JObject();
                changes["ActionRoutes"]![job.JobId.ToString()] = Layouts.Routes(job, desired);
            }
        }
        if (!desired.SharedSettingsPrepared)
        {
            var shared = JObject.Parse(Pack.Data.SharedSettings.ToJsonString());
            if (shared["EnabledActionsV6"] is JArray sharedIds) enable.UnionWith(sharedIds.Values<int>());
            shared.Remove("EnabledActionsV6");
            changes.Merge(shared, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
            desired.SharedSettingsPrepared = true;
        }
        WrathFork.Contract.SettingsPatch.NormalizePresetMaps(changes, (JObject)snapshot["PresetIds"]!);
        var affectedIds = selectedJobs.Select(j => j.JobId).ToHashSet();
        var pvpJobs = PvP.Pack.Jobs.Where(j => affectedIds.Contains(j.JobId)).ToArray();
        var pvpPatch = PvP.Pack.Patch(pvpJobs, desired.SharedPvP(), snapshot);
        changes.Merge(pvpPatch["Settings"], new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        enable.UnionWith(pvpPatch["Enable"]!.Values<int>());
        disable.UnionWith(pvpPatch["Disable"]!.Values<int>());
        var patch = new JObject { ["Settings"] = changes, ["Enable"] = new JArray(enable.Order()), ["Disable"] = new JArray(disable.Order()) };
        engine.Apply(snapshot, patch, desired);
        UpdateEntries();
        return managedJobs;
    }

    private JObject? migrationSnapshot;
    private JObject? migrationPatch;
    private AccordSettings? migrationChoices;
    internal string MigrationSummary { get; private set; } = "Preview the import to review its contents.";
    internal bool MigrationReady => migrationPatch is not null;

    internal void PreviewMigration()
    {
        var sourcePath = Plugin.Instance.MigrationSource;
        if (string.IsNullOrWhiteSpace(sourcePath)) throw new InvalidOperationException("No combined prototype backup is selected.");
        migrationPatch = null;
        var source = JObject.Parse(File.ReadAllText(sourcePath));
        var snapshot = engine.Read();
        var (plan, desired) = MigrationPreparation.Build(Pack, source, snapshot, CloneSettings());
        var patch = (JObject)plan["Patch"]!;
        var validation = engine.Validate(patch);
        plan["Validation"] = validation;
        plan["SourceFile"] = sourcePath;
        var report = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "settings-migration-preview.json");
        File.WriteAllText(report, plan.ToString());
        migrationSnapshot = snapshot;
        migrationPatch = patch;
        migrationChoices = desired;
        MigrationSummary = $"Import from {Path.GetFileName(sourcePath)}. Personal job values and targeting settings are retained; intent-controlled switches follow your saved variants. Live mode stays off and hotbars are untouched. {plan["Omitted"]!.Count()} unsupported, retired or non-engine fields remain in the backup. Review report: {report}";
        Status = "Migration preview ready; no settings changed.";
    }

    internal void ImportMigration()
    {
        if (migrationPatch is null || migrationSnapshot is null || migrationChoices is null) throw new InvalidOperationException("Preview the migration first.");
        engine.Apply(migrationSnapshot, migrationPatch, migrationChoices);
        Plugin.Instance.MigrationPending = false;
        migrationPatch = null;
        UpdateEntries();
        Status = "Personal settings imported and backed up. Live mode remains off; hotbars unchanged.";
    }

    internal bool HasPendingOperation => engine.HasPending;
    internal void AbandonPendingOperation() { engine.ReviewRejectedOperation(); migrationPatch = null; Status = "Previous request retained for diagnosis. Review settings before reapplying."; }

    internal void SetVariant(VariantId id, bool enabled, bool global = false)
    {
        using var timing = AccordDiagnostics.PerformanceTimings.Measure(AccordDiagnostics.TimedPath.VariantChange);
        var desired = pendingChoices is null ? CloneSettings() : ConfiguredLayout.Clone(pendingChoices);
        desired.SetAutomation(id, enabled, !global && Player.Available ? Pack.Find((uint)Player.Job)?.JobId ?? 0 : 0, !global && IsPvP);
        ApplyChoices(desired, variantsOnly: pendingChoices == null || pendingVariantsOnly);
        PvP.Invalidate();
        if (!Settings.LiveMode) Status = "Variant saved. Static hotbars unchanged; use setup to restore any needed manual buttons.";
    }

    internal void SetLive(bool live)
    {
        RequireNormalLayouts();
        if (live && Pack.SelectedJobs(Settings).Length == 0)
            throw new InvalidOperationException("Select at least one active class first.");
        Mutate(() => Settings.LiveMode = live);
        PvP.Invalidate();
        if (live) RequestCurrentHotbars();
        else Status = "Static mode enabled. Current hotbars retained.";
    }

    internal void SetCleanup(bool value)
    {
        RequireNormalLayouts();
        Mutate(() => Settings.ClearOtherActions = value);
        PvP.Invalidate();
        if (Settings.LiveMode) RequestCurrentHotbars();
    }

    private void RequestCurrentHotbars()
    {
        updates.Request();
        if (Settings.LiveMode) Status = "Live layout pending for the current active class; it will retry when hotbars are ready.";
        // A UI/DTR change already runs on the framework thread. Try now, retaining
        // the pending transition if loading or native readiness prevents writing.
        if (Settings.LiveMode) Run(Tick);
    }

    internal void SetActiveJobs(IEnumerable<uint> jobs)
    {
        var desired = CloneSettings();
        desired.ActiveJobs = jobs.Select(id => Pack.Find(id)?.JobId).Where(id => id.HasValue)
            .Select(id => id!.Value).ToHashSet();
        ApplyChoices(desired);
        PvP.Invalidate();
        updates.Invalidate();
        if (Settings.LiveMode) RequestCurrentHotbars();
        else Status = "Active classes saved. Existing hotbars retained; deselected classes will receive no further Accord updates.";
    }

    internal void ApplyHotbars() { RequireNormalLayouts(); if(IsPvP){PvP.ApplySelected();Status=PvP.Status;return;} ApplyHotbars(Pack.SelectedJobs(Settings), null);}
    private void ApplyCurrentHotbars(bool prepareSettings = true)
    {
        uint job = (uint)Player.Job;
        if (Pack.IsActive(job, Settings)) ApplyHotbars([Pack.Find(job)!], job, prepareSettings);
    }

    private unsafe void ApplyHotbars(JobPreset[] selected, uint? currentClass, bool prepareSettings = true, bool validated = false)
    {
        using var timing = AccordDiagnostics.PerformanceTimings.Measure(AccordDiagnostics.TimedPath.HotbarApply);
        if (selected.Length == 0) throw new InvalidOperationException("Select at least one active class first.");
        if (!Player.Available || Svc.PlayerState.ContentId == 0) throw new InvalidOperationException("Log in before applying hotbars.");
        uint job = (uint)Player.Job;
        NativeHotbars.Ready(job);
        if (!validated) selected = selected.Where(p => p.JobId < 19 || LayoutPlan(p, Settings, true).Valid).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("Layout update blocked. Resolve the layout warnings first.");
        var previousChoices = prepareSettings ? CloneSettings() : null;
        var previousEngine = prepareSettings ? engine.Read() : null;
        try
        {
        if (prepareSettings) Prepare(selected);
        var layouts = selected.SelectMany(p => (currentClass is null ? new[] { p.JobId }.Concat(p.BaseClasses) : [currentClass.Value])
            .Select(id => (id, Pack.Resolve(p, Settings)))).ToArray();
        var regular = new Dictionary<uint, Dictionary<Position, SlotValue>>();
        foreach (var preset in selected.Where(p => p.JobId >= 19))
        {
            var plan = LayoutPlan(preset, Settings, false);
            if (!plan.Valid) throw new InvalidOperationException("Resolved live layout is invalid.");
            foreach (uint id in currentClass is null ? new[] { preset.JobId }.Concat(preset.BaseClasses) : [currentClass.Value])
                regular[id] = plan.Controls.ToDictionary(c => c.Position, c => c.Action.Value);
        }
        var backup = hotbars.ApplyConfigured(layouts, Settings.ClearOtherActions, regular, Settings.Layouts.ManagedBars.ToHashSet());
        if (backup.Length > 0)
        {
            try { Mutate(() => Settings.LastHotbarBackup = backup); }
            catch { hotbars.Restore(backup); throw; }
        }
        Status = $"Hotbars for {selected.Length} selected class(es) " + (backup.Length > 0 ? "applied and backed up." : "already match.") + hotbars.Notice + "\n" + LayoutStatus;
        }
        catch (Exception failure)
        {
            if (previousEngine is not null)
            {
                try { engine.RestoreSettings(previousEngine, previousChoices!); }
                catch (Exception rollback) { throw new AggregateException("Hotbar update failed and settings recovery requires attention.", failure, rollback); }
            }
            throw;
        }
    }

    internal void Restore()
    {
        RequireNormalLayouts();
        if(IsPvP){PvP.Restore();Status=PvP.Status;UpdateEntries();return;}
        if (string.IsNullOrWhiteSpace(Settings.LastHotbarBackup)) throw new InvalidOperationException("No hotbar backup has been recorded.");
        var undo = hotbars.Restore(Settings.LastHotbarBackup);
        try { Mutate(() => { Settings.LastHotbarBackup = undo; Settings.LiveMode = false; }); }
        catch { hotbars.Restore(undo); throw; }
        Status = "Hotbars restored. Live mode disabled so the restored layout is retained.";
    }

    internal void ResetSelectedSettings()
    {
        if(IsPvP){PvP.Reset();Status=PvP.Status;UpdateEntries();return;}
        var selected = Pack.SelectedJobs(Settings);
        if (selected.Length == 0) throw new InvalidOperationException("Select at least one active class first.");
        Prepare(selected, reset: true);
        Status = "Selected classes: reviewed settings restored. Previous settings backed up; variants and hotbars retained.";
    }

    internal void ExportDiagnostics()
    {
        var path = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "accord-diagnostics.json");
        File.WriteAllText(path, JsonConvert.SerializeObject(new
        {
            EngineVersion = Pack.Data.WrathVersion,
            Status,
            CurrentJob = (uint)Player.Job,
            Settings,
            // This is a private local export, never a file in the source repository.
            Engine = engine.Read(),
            Schema = engine.Discover(),
        }, Formatting.Indented));
        Status = "Diagnostics saved to " + path;
    }

    private void ToggleWrathOpeners()
    {
        if (!Player.Available || IsPvP || !Pack.IsActive((uint)Player.Job, Settings) ||
            !FeatureScope.Supports((uint)Player.Job, Feature.RunnerAutomation)) return;

        if (engine.HasPending) engine.Recover();
        var desired = CloneSettings();
        desired.HoldWrathOpeners = !desired.HoldWrathOpeners;
        var stamp = engine.Stamp();
        if (stamp["Hold"] != null && !Settings.OpenerHoldMigrated && Settings.HoldWrathOpeners)
        {
            var migrated = CloneSettings();
            migrated.OpenerHoldMigrated = true;
            engine.SetOpenerHold(true);
            ApplySettings(Pack.SelectedJobs(migrated), migrated);
            desired = CloneSettings();
            desired.HoldWrathOpeners = false;
        }
        if (stamp["Hold"] != null)
        {
            desired.OpenerHoldMigrated = true;
            engine.SetOpenerHold(desired.HoldWrathOpeners);
            try { AccordDiagnostics.PerformanceTimings.SaveConfig(desired); Plugin.Instance.Settings = desired; }
            catch { engine.SetOpenerHold(!desired.HoldWrathOpeners); throw; }
            if (pendingChoices != null) { pendingChoices.OpenerHoldMigrated = true; pendingChoices.HoldWrathOpeners = desired.HoldWrathOpeners; }
            UpdateEntries();
            return;
        }
        var rotations = new JObject();
        foreach (var job in Pack.SelectedJobs(desired).Where(j => FeatureScope.Supports(j.JobId, Feature.RunnerAutomation)))
            rotations[job.JobId.ToString()] = JObject.FromObject(desired.EffectiveRotation(job.JobId));

        var patch = new JObject { ["Settings"] = new JObject { ["RotationPreferences"] = rotations },
            ["Enable"] = new JArray(), ["Disable"] = new JArray() };
        engine.Apply(engine.Read(), patch, desired);
        if (pendingChoices != null) pendingChoices.HoldWrathOpeners = desired.HoldWrathOpeners;
        UpdateEntries();
    }

    private (AccordSettings Choices, uint Job, bool PvP, bool Active, bool Hold)? displayedEntries;
    private void UpdateEntries()
    {
        uint job = Player.Available ? (uint)Player.Job : 0;
        bool pvp = Player.Available && IsPvP;
        var display = (pendingChoices ?? Settings, job, pvp, Player.Available &&
            (pvp ? PvP.Active(job) : Pack.IsActive(job, Settings)), Settings.HoldWrathOpeners);
        if (displayedEntries == display) return;
        displayedEntries = display;
        openers.Shown = Player.Available && !IsPvP && Pack.IsActive((uint)Player.Job, Settings) &&
            FeatureScope.Supports((uint)Player.Job, Feature.RunnerAutomation);
        if (openers.Shown)
        {
            openers.Text = new SeStringBuilder().AddIcon(BitmapFontIcon.Alarm)
                .AddText(Settings.HoldWrathOpeners ? " M" : " A").Build();
            openers.Tooltip = Settings.HoldWrathOpeners
                ? "Wrath job openers held. Click to restore each job's opener settings."
                : "Wrath job openers follow each job's settings. Click to hold openers for all managed PvE jobs.";
        }
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Shown = Player.Available && (IsPvP ? PvP.Active((uint)Player.Job) : Pack.IsActive((uint)Player.Job, Settings));
            var definition = VariantDefinitions.All[i];
            var current = Player.Available ? Pack.Find((uint)Player.Job) : null;
            var choices = pendingChoices ?? Settings;
            entries[i].Tooltip = definition.Label + ": click to switch A/M.\nChanges " +
                choices.AutomationTarget(definition.Id, current?.JobId ?? 0, IsPvP) + ".\n" + definition.Description;
            entries[i].Text = new SeStringBuilder().AddIcon(Enum.Parse<BitmapFontIcon>(definition.Icon))
                .AddText(" " + (VariantEnabled(definition.Id) ? "A" : "M")).Build();
        }
    }

    public void Dispose()
    {
        disposed = true;
        Svc.ClientState.Login -= FallbackLogin;
        logout.Dispose();
        engine.Dispose();
        foreach (var definition in VariantDefinitions.All) Svc.DtrBar.Remove("Accord " + definition.Label);
        openers.Remove();
        Instance = null;
    }
}
