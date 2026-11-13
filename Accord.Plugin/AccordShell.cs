using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using AetherglassUI;
using System.Collections.Generic;

namespace Accord;

internal static class AccordControls
{
    internal static bool Toggle(string label, ref bool value, string? detail = null)
    {
        bool edited=value;
        Controls.Row(label,label,detail??"",()=>edited=Controls.Switch("value",edited));
        bool changed=edited!=value;
        value=edited;
        return changed;
    }
    internal static Setting Flag(string id,string label,Func<bool> read,Action<bool> write,string detail="") =>
        new(id,label,()=>{bool old=read(),next=Controls.Switch("value",old);if(next!=old)write(next);},Detail:detail){Layout=FieldLayout.Switch};

    internal static void Section(string label,Action draw)
    {
        ImGui.PushID(label);
        try { ImGui.TextDisabled(label); draw(); } finally { ImGui.PopID(); }
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
        bool stacked = ImGui.GetContentRegionAvail().X < 590 * (ImGui.GetTextLineHeight()/16f);
        if (!ImGui.BeginTable("Groups", stacked ? 1 : 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp)) return;
        try
        {
            ImGui.TableSetupColumn("Intent / group", ImGuiTableColumnFlags.WidthStretch);
            if (!stacked)
            {
                ImGui.TableSetupColumn("Keep when automated", ImGuiTableColumnFlags.WidthFixed, 145 * (ImGui.GetTextLineHeight()/16f));
                ImGui.TableSetupColumn("Order / destination", ImGuiTableColumnFlags.WidthFixed, 220 * (ImGui.GetTextLineHeight()/16f));
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
        var next=Controls.Switch("rotation",enabled);
        if(next!=enabled)enable(next);
        ImGui.SameLine();ImGui.TextUnformatted("Auto rotation");
    }

}

internal enum Desk { Play, Rules, Bars, Tools }
internal enum TaskPage { Home, Permissions, Enemies, Healing, Opener, Reserved, JobTuning, Rsr, Jobs, Managed, Mapping, RoleActions, Groups, Overrides, PvP, PvPOverrides, Preview, DutyLog, Trace, Recovery }
internal sealed record Destination(TaskPage Id, Desk Desk, string Group, string Name, string Detail);

internal sealed class AccordShell
{
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
    private readonly WindowShell window=new();
    private readonly SettingsPage[] states=[new(),new(),new(),new()];
    private bool initialized;
    private void Initialize()
    {
        if(!initialized){
            foreach(var page in Pages)states[(int)page.Desk].SetCollapsed(page.Id.ToString(),page.Id is not (TaskPage.Home or TaskPage.Permissions or TaskPage.Jobs or TaskPage.DutyLog));
            initialized=true;
        }
    }
    internal void OpenArea(Desk desk)=>window.Selected=desk.ToString();
    internal void OpenTask(TaskPage task)
    {
        Initialize();
        var dest=Pages.First(p=>p.Id==task);
        OpenArea(dest.Desk);
        var state=states[(int)dest.Desk];
        state.Query="";
        state.SetCollapsed(task.ToString(),false);
        state.ScrollToGroup(task.ToString());
    }
    internal void Draw(string job,string engine,Action<TaskPage> draw,Action pending,Action dock,Action close,
        Func<TaskPage,Topic[]?>? topics=null, Action<Vector2,float>? drawMark=null)
    {
        Initialize();
        window.Close=close; window.Dock=dock;
        var rail=Enum.GetValues<Desk>().Select(desk=>new AetherglassUI.Destination(desk.ToString(),Areas[(int)desk],"",()=>{
            pending();
            var groups=Pages.Where(p=>p.Desk==desk).Select(p=>{
                var rows=topics?.Invoke(p.Id);
                if(rows!=null)return new Group(p.Id.ToString(),p.Name,rows);
                // A complex editor keeps its selection, validation and draft together when found by search.
                if(string.IsNullOrWhiteSpace(states[(int)desk].Query))return new Group(p.Id.ToString(),p.Name){Body=()=>draw(p.Id),Summary=p.Detail};
                var editor=new Setting("editor",p.Name,()=>{},p.Detail+" "+p.Group+" "+Keywords(p.Id)){ExpandedContent=()=>draw(p.Id)};
                return new Group(p.Id.ToString(),p.Name,new Topic(p.Group,editor));
            }).ToArray();
            states[(int)desk].Draw("workspace-"+desk,groups,searchable:desk!=Desk.Play,inlineTopics:true);
        }){DrawIcon=(pos,size,ink)=>AccordChrome.Glyph(desk,pos,size,ink)}).ToArray();
        window.Draw("Accord",string.IsNullOrEmpty(engine)?job:job+" / "+engine,rail,drawMark ?? AccordChrome.Mark);
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
