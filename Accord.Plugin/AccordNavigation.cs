using System;
using System.Collections.Generic;
using System.Linq;

namespace Accord;

internal enum Desk { Play, Rules, Bars, Tools, Setup }
internal enum TaskPage { Home, Permissions, Enemies, Healing, Opener, Reserved, JobTuning, Rsr, Jobs, Managed, Mapping, RoleActions, Groups, Overrides, PvP, PvPOverrides, Preview, DutyLog, Trace, Recovery, Integrations }
internal sealed record Destination(TaskPage Id, Desk Desk, string Group, string Name, string Detail);

// Every original destination retains a stable ID and its existing controller.
internal static class AccordNavigation
{
    internal static readonly Destination[] Pages =
    [
        new(TaskPage.Home,Desk.Play,"","Play","Auto rotation, engine selection, AutoDuty and effective permissions"),
        new(TaskPage.Permissions,Desk.Rules,"Combat","Permissions","Global burst, protection, support and class mechanics"),
        new(TaskPage.Enemies,Desk.Rules,"Combat","Targeting","Enemy selection, target range, AoE, mouseover and focus"),
        new(TaskPage.Healing,Desk.Rules,"Combat","Healing & allies","Healing, resurrection, cleansing and ally selection"),
        new(TaskPage.Opener,Desk.Rules,"Combat","Openers & potions","Combat-only actions, job openers and damage potions"),
        new(TaskPage.Reserved,Desk.Rules,"Combat","Manual actions","Wrath button exclusions and RSR action permissions"),
        new(TaskPage.JobTuning,Desk.Rules,"Combat","Job preferences","All existing job improvements, forecasts and comparisons"),
        new(TaskPage.Rsr,Desk.Rules,"Combat","RSR rules","Defense, healing thresholds, ground actions and burst"),
        new(TaskPage.Mapping,Desk.Bars,"Hotbars","Layout","Intent assignments, manual visibility, damage and mechanic buttons"),
        new(TaskPage.RoleActions,Desk.Bars,"Hotbars","Role actions","Important and other role-action groups"),
        new(TaskPage.Groups,Desk.Bars,"Hotbars","Personal groups","Actions, items, HQ choices, role/job and PvP contents"),
        new(TaskPage.Overrides,Desk.Bars,"Hotbars","Role & job overrides","Mapping, role actions, exclusions, automation and rotation overrides"),
        new(TaskPage.PvP,Desk.Bars,"Hotbars","PvP setup","Compact mapping, one/two rotation buttons, permissions and job improvements"),
        new(TaskPage.PvPOverrides,Desk.Bars,"Hotbars","PvP overrides","Explicit PvP role and job exceptions"),
        new(TaskPage.Preview,Desk.Bars,"Hotbars","Preview & apply","Resolved PvE/PvP layouts, sources, warnings and setup actions"),
        new(TaskPage.Jobs,Desk.Setup,"Setup","Managed jobs","Combat, crafting, gathering and existing selected-job setup actions"),
        new(TaskPage.Managed,Desk.Setup,"Setup","Managed bars","Live/static mode, cleanup, shared and additional bars"),
        new(TaskPage.Integrations,Desk.Setup,"Setup","Movement integration","BossMod distances, positionals and current movement status"),
        new(TaskPage.DutyLog,Desk.Tools,"Help & recovery","Duty recording","Arm logging, mark a moment and export the duty ZIP"),
        new(TaskPage.Trace,Desk.Tools,"Help & recovery","Diagnostics","Start, stop, inspect and export rotation decisions"),
        new(TaskPage.Recovery,Desk.Tools,"Help & recovery","Restore hotbars","Backups, manual fallback, operation recovery and troubleshooting")
    ];
    internal static string Area(Desk desk)=>desk switch {
        Desk.Play=>"Play",Desk.Rules=>"Combat",Desk.Bars=>"Hotbars",Desk.Setup=>"Setup",_=>"Help & recovery"
    };
    internal static IReadOnlyList<Desk> Areas => [Desk.Play,Desk.Rules,Desk.Bars,Desk.Setup,Desk.Tools];
    internal static string Keywords(TaskPage task)=>task switch {
        TaskPage.Mapping=>"joined join split single ST AoE damage mechanic placement manual defaults override",
        TaskPage.Enemies=>"ST range retarget focus acquire switch aggro",
        TaskPage.Healing=>"resurrection GCD oGCD mouseover party list focus preventive",
        TaskPage.Permissions=>"auto burst mitigation auto support class actions",
        TaskPage.JobTuning=>"BossMod forecast tank savage downtime",
        TaskPage.DutyLog=>"logging record marker diagnostics export",
        TaskPage.Trace=>"capture rejection recipient debugging",
        TaskPage.Recovery=>"backup import migration logout reset restore",
        _=>""
    };
}
