using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Glasswork;
using Glasswork.Backend;

namespace Accord;

internal static class AccordControls
{
    internal static bool Toggle(string label, ref bool value, string? detail = null)
    {
        ImGui.PushID(label);
        float room = ImGui.GetContentRegionAvail().X - 80 * Frame.Scale;
        if (ImGui.CalcTextSize(label).X > room || (detail != null && ImGui.CalcTextSize(detail).X > room))
        {
            bool changed = false;
            if (ImGui.BeginTable("wrapped", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Text", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 54 * Frame.Scale);
                ImGui.TableNextColumn();
                ImGui.TextWrapped(label);
                if (!string.IsNullOrEmpty(detail)) ImGui.TextWrapped(detail);
                ImGui.TableNextColumn();
                changed = Gw.Toggle(ref value, "##value");
                ImGui.EndTable();
            }
            ImGui.PopID();
            return changed;
        }
        Gw.BeginRow(label, detail ?? "");
        try { return Gw.Toggle(ref value, "##value"); }
        finally { Gw.EndRow(); ImGui.PopID(); }
    }

    internal static void Section(string label, Action draw)
    {
        bool visible = Gw.BeginSection(label);
        try { if (visible) draw(); }
        finally { Gw.EndSection(); }
    }

    internal static void EnemyRules(Func<string, bool> read, Action<string, bool> write)
    {
        void Rule(string key, string label, string detail)
        {
            bool value = read(key);
            if (Toggle(label, ref value, detail)) write(key, value);
        }
        Section("Visible target in Wrath auto", () =>
        {
            Rule("AcquireVisibleEnemy", "Select the enemy I attack", "When I have no target or have myself selected.");
            Rule("SwitchEnemyTarget", "Replace my current target", "Allow Wrath to visibly select a different enemy.");
        });
        Section("Damage targeting", () =>
        {
            Rule("EnemyTargeting", "Check action coverage", "Validate targets across PvE damage buttons.");
            Rule("AcquireEnemy", "Acquire an engaged enemy", "When I have no enemy selected.");
            Rule("AutomaticEnemies", "Switch for coverage", "Allow automatic enemy switching for action coverage.");
            Rule("Fallback", "Use a fallback target", "When my deliberate target is invalid.");
        });
        Section("Manual overrides", () =>
        {
            Rule("EnemyMouseover", "World mouseover", "Use my hovered enemy for damage actions.");
            Rule("EnemyFocus", "Focus target", "Allow my focus target as a damage override.");
            Rule("OverridesInAutomation", "Use overrides in auto rotation", "Apply mouseover and focus to automatic actions too.");
        });
    }

    internal static void HealingRules(Func<string, bool> read, Action<string, bool> write)
    {
        void Rule(string key, string label)
        {
            bool value = read(key);
            if (Toggle(label, ref value)) write(key, value);
        }
        Section("Healing actions", () =>
        {
            Rule("IncludeRaise", "Include resurrection in damage buttons");
            Rule("HardCastRaise", "Allow resurrection without instant cast");
            Rule("IncludeCleanse", "Include cleansing in damage buttons");
            Rule("PreventiveHealing", "Prepare healing before incoming damage");
            Rule("GroundTargets", "Use target overrides and coverage for ground heals");
        });
        Section("Healing targets", () =>
        {
            Rule("PartyMouseover", "Party-list mouseover");
            Rule("WorldMouseover", "World mouseover");
            Rule("Focus", "Focus target");
            Rule("AutomaticAllies", "Choose an eligible ally when no override applies");
            Rule("SwitchAllyTarget", "Let Wrath visibly select the healing target");
        });
    }

    internal static void MappingTable(Action<bool> drawRows)
    {
        bool stacked = ImGui.GetContentRegionAvail().X < 590 * Frame.Scale;
        if (!ImGui.BeginTable("Groups", stacked ? 1 : 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp)) return;
        try
        {
            ImGui.TableSetupColumn("Intent / group", ImGuiTableColumnFlags.WidthStretch);
            if (!stacked)
            {
                ImGui.TableSetupColumn("Keep when automated", ImGuiTableColumnFlags.WidthFixed, 145 * Frame.Scale);
                ImGui.TableSetupColumn("Order / destination", ImGuiTableColumnFlags.WidthFixed, 220 * Frame.Scale);
            }
            ImGui.TableHeadersRow();
            drawRows(stacked);
        }
        finally { ImGui.EndTable(); }
    }

    internal static bool MappingRow(bool stacked, string label, bool keepAll, ref bool keep)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextWrapped(label);
        if (!stacked) ImGui.TableNextColumn();
        ImGui.BeginDisabled(keepAll);
        bool changed = ImGui.Checkbox(stacked ? "Keep when automated" : "##keep", ref keep);
        ImGui.EndDisabled();
        if (keepAll && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Enabled by Keep manual controls when automated.");
        if (!stacked) ImGui.TableNextColumn();
        return changed;
    }

    internal static void Runner(Action engine, bool follow, string activity, Action<bool> setFollow)
    {
        Section("Automatic execution", () =>
        {
            engine();
            if (Toggle("Follow AutoDuty", ref follow, activity)) setFollow(follow);
        });
    }

    internal static void Dock(bool enabled, Action<bool> enable)
    {
        if (ImGui.Checkbox("Auto rotation", ref enabled)) enable(enabled);
    }

}

internal enum Desk { Play, Rules, Bars, Tools }
internal enum TaskPage { Home, Permissions, Enemies, Healing, Opener, Reserved, JobTuning, Rsr, Jobs, Managed, Mapping, RoleActions, Groups, Overrides, PvP, PvPOverrides, Preview, DutyLog, Trace, Recovery }
internal sealed record Destination(TaskPage Id, Desk Desk, string Group, string Name, string Detail);

internal sealed class AccordShell
{
    internal static Look Theme() => AccordChrome.Theme();

    internal static void DrawBackground()
    {
        var pos = ImGui.GetWindowPos();
        ImGui.GetWindowDrawList().AddRectFilled(pos, pos + ImGui.GetWindowSize(),
            ImGui.GetColorU32(Frame.Look.Background.ToVector4()), ImGui.GetStyle().WindowRounding);
    }
    private static readonly string[] Areas = ["Play", "Rotation rules", "Hotbar studio", "Testing & recovery"];
    private static readonly Destination[] Pages =
    [
        new(TaskPage.Home,Desk.Play,"","Play controls","Auto rotation, engine selection and AutoDuty"),
        new(TaskPage.Permissions,Desk.Rules,"Action selection","Automation permissions","Burst, support, protection and class mechanics"),
        new(TaskPage.Enemies,Desk.Rules,"Action selection","Enemy targeting","Single target, AoE threshold, mouseover and visible targets"),
        new(TaskPage.Healing,Desk.Rules,"Action selection","Healing and allies","Weave healing, raise, cleanse and choose recipients"),
        new(TaskPage.Reserved,Desk.Rules,"Action selection","Reserve actions for manual use","Wrath button exclusions and RSR action permissions"),
        new(TaskPage.Opener,Desk.Rules,"Job and engine behaviour","Pulls and openers","Combat-only actions, job openers and potions"),
        new(TaskPage.JobTuning,Desk.Rules,"Job and engine behaviour","Job improvements","Per-job rotation choices, forecasts and mitigation"),
        new(TaskPage.Rsr,Desk.Rules,"Job and engine behaviour","RSR automation rules","Defense, healing thresholds, ground actions and burst"),
        new(TaskPage.Jobs,Desk.Bars,"Setup","Choose managed jobs","Enable Accord for combat, crafting and gathering jobs"),
        new(TaskPage.Managed,Desk.Bars,"Setup","Choose managed bars","Live updates, shared bars and controller cleanup"),
        new(TaskPage.Mapping,Desk.Bars,"Layout","Place rotation buttons","Map actions and intents to hotbar slots"),
        new(TaskPage.RoleActions,Desk.Bars,"Layout","Place role actions","Choose role actions for managed slots"),
        new(TaskPage.Groups,Desk.Bars,"Layout","Personal action groups","Items, potions and extra actions"),
        new(TaskPage.Overrides,Desk.Bars,"Exceptions and review","Role and job overrides","Override inherited mappings, automation and rotation rules"),
        new(TaskPage.PvP,Desk.Bars,"Exceptions and review","PvP setup","Compact bars, Guard, rotation buttons and action selection"),
        new(TaskPage.PvPOverrides,Desk.Bars,"Exceptions and review","PvP overrides","Role and job exceptions for PvP"),
        new(TaskPage.Preview,Desk.Bars,"Exceptions and review","Preview resolved hotbars","Inspect the layout a job inherits before applying it"),
        new(TaskPage.DutyLog,Desk.Tools,"Record a problem","Latest duty log","Arm logging, mark a moment and export the duty ZIP"),
        new(TaskPage.Trace,Desk.Tools,"Record a problem","Inspect rotation decisions","Capture a short trace of actions and targets"),
        new(TaskPage.Recovery,Desk.Tools,"Restore a setup","Hotbar recovery","Manual fallback, backup restore and troubleshooting report")
    ];
    private Desk area;
    private TaskPage? selected = TaskPage.Home;
    private string search = "";
    private (Desk Area, TaskPage? Page, string Search)? previous;
    private string BackLabel => previous is { } p
        ? p.Search.Length > 0 ? "Search results" : Areas[(int)p.Area]
        : Areas[(int)area];
    private void Back()
    {
        if (previous is { } p) { (area, selected, search) = p; previous = null; }
        else OpenArea(area);
    }
    internal void OpenArea(Desk value) { previous = null; area = value; selected = value == Desk.Play ? TaskPage.Home : null; search = ""; }
    private void Open(Destination dest) { previous = (area, selected, search); area = dest.Desk; selected = dest.Id; search = ""; }

    internal void Draw(string job, string engine, Action<TaskPage> draw, Action pending, Action dock, Action close)
    {
        AccordChrome.Header(job, engine, close);
        float footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y + 1;
        if (ImGui.BeginChild("Accord body", new Vector2(0, -footerHeight), false))
        {
            if (ImGui.BeginChild("Workspace", new Vector2(48 * Frame.Scale, 0), false))
            {
                for (int i = 0; i < Areas.Length; i++)
                    if (AccordChrome.RailItem((Desk)i, Areas[i], i == (int)area)) OpenArea((Desk)i);
            }
            ImGui.EndChild();
            ImGui.SameLine();
            DrawWorkspace(draw, pending);
        }
        ImGui.EndChild();
        ImGui.Separator();
        dock();
    }

    private void DrawWorkspace(Action<TaskPage> draw, Action pending)
    {
        if (ImGui.BeginChild("Settings workspace", Vector2.Zero, false))
        {
            if (area != Desk.Play)
            {
                ImGui.SetNextItemWidth(-1);
                Gw.TextField(ref search, "Find a setting");
            }
            pending();
            bool visible = ImGui.BeginChild($"Content-{area}-{selected}-{!string.IsNullOrWhiteSpace(search)}", Vector2.Zero, false);
            try { if (visible) DrawContent(draw); } finally { ImGui.EndChild(); }
        }
        ImGui.EndChild();
    }

    private void DrawContent(Action<TaskPage> draw)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            Gw.Heading("Search results");
            var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var matches = Pages.Where(p =>
            {
                string terms = $"{Areas[(int)p.Desk]} {p.Group} {p.Name} {p.Detail} {Keywords(p.Id)}";
                return words.All(word => terms.Contains(word, StringComparison.OrdinalIgnoreCase));
            }).ToArray();
            if (matches.Length == 0) ImGui.TextDisabled("No matching settings.");
            foreach (var dest in matches) if (Gw.Row(dest.Name, dest.Detail)) Open(dest);
        }
        else if (selected is { } id)
        {
            if ((id != TaskPage.Home || previous.HasValue) && Gw.Button("<  " + BackLabel, ButtonStyle.Default)) { Back(); return; }
            var dest = Array.Find(Pages, p => p.Id == id)!;
            Gw.Heading(dest.Name);
            ImGui.PushID((int)id);
            try { draw(id); } finally { ImGui.PopID(); }
            if (id == TaskPage.Home)
                AccordControls.Section("Quick settings", () =>
                {
                    foreach (var link in new[] { TaskPage.Permissions, TaskPage.Enemies, TaskPage.Healing, TaskPage.Mapping })
                    {
                        var item = Array.Find(Pages, p => p.Id == link)!;
                        if (Gw.Row(item.Name, item.Detail)) Open(item);
                    }
                });
        }
        else
        {
            Gw.Heading(Areas[(int)area]);
            foreach (var section in Pages.Where(p => p.Desk == area).GroupBy(p => p.Group))
                AccordControls.Section(section.Key, () =>
                {
                    foreach (var item in section) if (Gw.Row(item.Name, item.Detail)) Open(item);
                });
        }
    }

    private static string Keywords(TaskPage id) => id switch
    {
        TaskPage.Mapping => "joined split single ST AoE damage mechanic placement",
        TaskPage.Enemies => "ST range retarget focus acquire switch aggro",
        TaskPage.Healing => "resurrection GCD oGCD mouseover party list focus preventive",
        TaskPage.Permissions => "auto burst mitigation auto support class actions",
        TaskPage.JobTuning => "BossMod forecast tank savage downtime",
        TaskPage.DutyLog => "logging record marker diagnostics export",
        TaskPage.Trace => "capture rejection recipient debugging",
        TaskPage.Recovery => "backup import migration logout reset restore",
        _ => ""
    };
}
