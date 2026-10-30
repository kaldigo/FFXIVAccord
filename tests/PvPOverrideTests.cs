using Accord;
using Newtonsoft.Json;

internal static class PvPOverrideTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
        const VariantId toggle = VariantId.Support;
        var legacy = new PvPOverrides { RotationButtons = PvPRotationButtons.Two };
        legacy.Global.Mapping = PvPConfiguredLayout.PreviousDefaultMapping();
        legacy.Global.Automation[toggle] = true;
        legacy.Classes[23] = new() { Mapping = PvPConfiguredLayout.PreviousDefaultMapping() };
        string classMapping = JsonConvert.SerializeObject(legacy.Classes[23]);
        Check(legacy.InitializeDefaults() && legacy.RotationButtons == PvPRotationButtons.One,
            "Shipped defaults did not migrate to one button.");
        Check(JsonConvert.SerializeObject(legacy.Global.Mapping) == JsonConvert.SerializeObject(PvPConfiguredLayout.DefaultMapping()),
            "Shipped mapping did not migrate.");
        Check(legacy.Global.Automation[toggle] && JsonConvert.SerializeObject(legacy.Classes[23]) == classMapping,
            "Migration changed automation or an explicit class mapping.");
        legacy.RotationButtons = PvPRotationButtons.Two;
        var restarted = JsonConvert.DeserializeObject<PvPOverrides>(JsonConvert.SerializeObject(legacy))!;
        Check(!restarted.InitializeDefaults() && restarted.RotationButtons == PvPRotationButtons.Two,
            "Restart reset the user's later two-button choice.");
        foreach (int edit in Enumerable.Range(0, 4))
        {
            var custom = new PvPOverrides { RotationButtons = PvPRotationButtons.Two };
            custom.Global.Mapping = PvPConfiguredLayout.PreviousDefaultMapping();
            if (edit == 0) custom.Global.Mapping.Areas[0].Entries[0].KeepManual = true;
            if (edit == 1) custom.Global.Mapping.Areas[0].Entries[0].Priority = 99;
            if (edit == 2) custom.Global.Mapping.Areas[0].Bar = 3;
            if (edit == 3) custom.Global.Mapping = null;
            string original = JsonConvert.SerializeObject(custom.Global);
            custom.InitializeDefaults();
            Check(JsonConvert.SerializeObject(custom.Global) == original && custom.RotationButtons == PvPRotationButtons.Two,
                "Migration overwrote custom or inherited mapping/count.");
        }
        var area = new WrathCombo.Engine.PvPAreaChoice();
        Check(!area.Select(true, false, 1), "Ninja chose area for one enemy.");
        Check(area.Select(true, false, 2), "Ninja did not choose area for multiple enemies.");
        Check(area.Select(true, true, 0), "Ninja changed mudras during an active area sequence.");
        Check(!area.Select(true, false, 1), "Ninja failed to return to single target after mudras.");
        Check(!area.Select(true, true, 5), "Ninja changed mudras during an active ST sequence.");
        Check(!area.Select(false, false, 5), "Two-button mode used automatic selection.");
        var settings = new AccordSettings { ActiveJobs = [23, 31] };
        settings.Layouts.Classes[23] = new() { Automation = { [toggle] = true }, KeepManual = true };
        var pvp = settings.Layouts.PvP;
        Check(settings.Enabled(toggle, 23, true), "Empty PvP overlay lost normal class value.");
        pvp.Global.Automation[toggle] = false;
        Check(!settings.Enabled(toggle, 23, true) && settings.Enabled(toggle, 23, false), "PvP Global did not override normal class independently.");
        pvp.Roles[JobRole.PhysicalRanged] = new() { Automation = { [toggle] = true } };
        pvp.Classes[23] = new() { Automation = { [toggle] = false } };
        Check(!settings.SharedPvP().Enabled(toggle, 23) && settings.SharedPvP().Enabled(toggle, 31), "PvP role/class precedence incorrect.");
        settings.SetAutomation(toggle, true, 23, true);
        Check(pvp.Classes[23].Automation[toggle] && !pvp.Global.Automation[toggle], "PvP class status write escaped its applied layer.");
        pvp.Classes[23].Automation.Remove(toggle);
        settings.SetAutomation(toggle, false, 23, true);
        Check(!pvp.Roles[JobRole.PhysicalRanged].Automation[toggle] && settings.Enabled(toggle, 23, false), "PvP role status write leaked into PvE.");
        pvp.Roles.Clear();
        settings.SetAutomation(toggle, true, 23, true);
        Check(pvp.Global.Automation[toggle], "PvP Global status write failed.");
        pvp.Global.Automation.Clear();
        settings.SetAutomation(toggle, false, 23, true);
        Check(!settings.Layouts.Classes[23].Automation[toggle] && !settings.Variants.ContainsKey(toggle), "Unchecked PvP layers did not fall back to normal class.");
        settings.SetAutomation(toggle, true, 31, true);
        Check(settings.Variants[toggle], "No applicable override did not fall back to Global.");
        pvp.Global.KeepManual = false;
        Check(!pvp.Resolve(23, settings.Layouts.Resolve(23)).KeepManual && settings.Layouts.Resolve(23).KeepManual, "PvP layout precedence/isolation failed.");
        var before = JsonConvert.DeserializeObject<LayoutPreferences>(JsonConvert.SerializeObject(settings.Layouts))!;
        var draft = JsonConvert.DeserializeObject<LayoutPreferences>(JsonConvert.SerializeObject(before))!;
        draft.PvP.Global.Mapping = new();
        settings.Layouts.PvP.Global.Automation[toggle] = true;
        draft.RebaseAutomation(before, settings.Layouts);
        Check(draft.PvP.Global.Mapping is not null && draft.PvP.Global.Automation[toggle], "Dirty PvP mapping lost a saved status toggle.");
        var roundtrip = JsonConvert.DeserializeObject<AccordSettings>(JsonConvert.SerializeObject(settings))!;
        Check(roundtrip.Layouts.PvP.Global.KeepManual == false && roundtrip.Enabled(toggle, 23, true), "PvP explicit false or automation lost on serialization.");
        Check(roundtrip.Layouts.PvP.RotationButtons == PvPRotationButtons.One, "New default is not one rotation button.");
        roundtrip.Layouts.PvP.Global.Mapping = null;
        roundtrip.Layouts.PvP.Global.RoleActions = null;
        roundtrip = JsonConvert.DeserializeObject<AccordSettings>(JsonConvert.SerializeObject(roundtrip))!;
        Check(roundtrip.Layouts.PvP.Global.Mapping is null && roundtrip.Layouts.PvP.Global.RoleActions is null,
            "Restart reapplied an unchecked PvP Global setting.");
        int count = roundtrip.Layouts.PvP.Classes.Count;
        roundtrip.Layouts.PvP.Resolve(40, roundtrip.Layouts.Resolve(40));
        Check(roundtrip.Layouts.PvP.Classes.Count == count, "Read-only resolution generated an override.");
        foreach (var id in Enum.GetValues<VariantId>())
        {
            var isolated = new AccordSettings { ActiveJobs = [23] };
            isolated.Layouts.PvP.Global.Automation[id] = false;
            isolated.SetAutomation(id, true, 23, true);
            Check(isolated.Enabled(id, 23, true) && !isolated.Enabled(id, 23, false), "Mode isolation failed for " + id);
        }
        return checks;
    }
}
