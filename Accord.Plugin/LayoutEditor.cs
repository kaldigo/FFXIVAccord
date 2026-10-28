using System;
using System.Collections.Generic;
using System.Linq;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using HotbarTools;
using Native = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;
using Sheets = Lumina.Excel.Sheets;

namespace Accord;

internal static class LayoutEditor
{
    private static LayoutPreferences? draft;
    private static string scope = "Global";
    private static uint previewJob = 23;
    private static string search = "";
    private static int kind;
    private static List<ActionChoice>? choices;
    private static int loadedKind = -1;
    private static bool highQuality;

    internal static void Draw(SetupController setup)
    {
        if (!ImGui.CollapsingHeader("Layout settings")) return;
        draft ??= Copy(setup.Settings.Layouts);
        ImGui.TextWrapped("Edit global defaults, then use Apply beside a role/class setting to override it. Mapping is overridden as a whole. Changes are staged until saved.");
        if (ImGui.BeginCombo("Settings for", scope))
        {
            if (ImGui.Selectable("Global", scope == "Global")) scope = "Global";
            foreach (var role in Enum.GetValues<JobRole>().Where(r => r != JobRole.Noncombat))
                if (ImGui.Selectable("Role: " + role, scope == "Role: " + role)) scope = "Role: " + role;
            foreach (var job in setup.Pack.Data.Jobs.Where(j => j.JobId >= 19))
                if (ImGui.Selectable(job.Job, scope == job.Job)) { scope = job.Job; previewJob = job.JobId; }
            ImGui.EndCombo();
        }
        LayoutOverride? over = null;
        LayoutValues parent = draft.Global;
        if (scope.StartsWith("Role: "))
        {
            var role = Enum.Parse<JobRole>(scope[6..]);
            if (!draft.Roles.TryGetValue(role, out over)) draft.Roles[role] = over = new();
            previewJob = setup.Pack.Data.Jobs.First(j => j.JobId >= 19 && LayoutPreferences.Role(j.JobId) == role).JobId;
        }
        else if (scope != "Global")
        {
            var job = setup.Pack.Data.Jobs.First(j => j.Job == scope);
            if (draft.Roles.TryGetValue(LayoutPreferences.Role(job.JobId), out var role)) parent = role.Over(parent);
            if (!draft.Classes.TryGetValue(job.JobId, out over)) draft.Classes[job.JobId] = over = new();
        }
        var effective = over?.Over(parent) ?? parent;
        Bool("Keep manual controls when automated", effective.KeepManual, over?.KeepManual, over is not null,
            v => { if (over is null) draft.Global.KeepManual = v!.Value; else over.KeepManual = v; });
        Bool("Split class mechanic controls", effective.SplitMechanics, over?.SplitMechanics, over is not null,
            v => { if (over is null) draft.Global.SplitMechanics = v!.Value; else over.SplitMechanics = v; });
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Separate Bard songs, Dancer dances and Astrologian draw/play controls. Other existing mechanic controls already remain separate.");

        if (ImGui.CollapsingHeader("Mapping"))
        {
            bool applied = over is null || over.Mapping is not null;
            if (over is not null && ImGui.Checkbox("Apply##mapping", ref applied)) over.Mapping = applied ? Copy(parent.Mapping) : null;
            ImGui.BeginDisabled(!applied);
            Mapping(over?.Mapping ?? effective.Mapping, effective.KeepManual, draft);
            ImGui.EndDisabled();
        }
        if (ImGui.CollapsingHeader("Role actions"))
        {
            bool applied = over is null || over.RoleActions is not null;
            if (over is not null && ImGui.Checkbox("Apply##roles", ref applied)) over.RoleActions = applied ? Copy(parent.RoleActions) : null;
            ImGui.BeginDisabled(!applied);
            var actions = over?.RoleActions ?? effective.RoleActions;
            ImGui.TextWrapped("Choose actions for each group. Actions in neither list are omitted from role groups. Mapping priorities resolve duplicates with other groups.");
            Actions("Important role actions", actions.Important, true);
            Actions("Other role actions", actions.Other, true);
            ImGui.EndDisabled();
        }
        if (ImGui.CollapsingHeader("Action exclusions"))
        {
            bool applied = over is null || over.Exclusions is not null;
            if (over is not null && ImGui.Checkbox("Apply##exclusions", ref applied)) over.Exclusions = applied ? Copy(parent.Exclusions) : null;
            ImGui.BeginDisabled(!applied);
            var exclusions = over?.Exclusions ?? effective.Exclusions;
            var routes = setup.LayoutIntents(previewJob, draft);
            ImGui.TextWrapped("Checked routes may use the action. Untick a route to exclude it. The Exclusions mapping group provides deliberate access.");
            if (ImGui.BeginTable("Exclusion routes", routes.Length + 2,
                ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg,
                new System.Numerics.Vector2(0, Math.Clamp(exclusions.Count * 30 + 64, 100, 300))))
            {
                ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 160);
                foreach (var route in routes) ImGui.TableSetupColumn(route.ToString(), ImGuiTableColumnFlags.WidthFixed, 115);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 65);
                ImGui.TableSetupScrollFreeze(1, 1);
                ImGui.TableHeadersRow();
                for (int i = 0; i < exclusions.Count; i++)
                {
                    var exclusion = exclusions[i];
                    ImGui.PushID(i);
                    ImGui.TableNextRow(); ImGui.TableNextColumn();
                    ImGui.TextUnformatted(exclusion.Action.Name);
                    foreach (var route in routes)
                    {
                        ImGui.TableNextColumn();
                        bool allowed = !exclusion.ExcludedFrom.Contains(route);
                        if (ImGui.Checkbox("##" + route, ref allowed))
                        { if (allowed) exclusion.ExcludedFrom.Remove(route); else exclusion.ExcludedFrom.Add(route); }
                    }
                    ImGui.TableNextColumn();
                    if (ImGui.SmallButton("Remove")) exclusions.RemoveAt(i--);
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
            Picker("Add excluded action", a => exclusions.Add(new() { Action = a }), actionsOnly: true);
            ImGui.EndDisabled();
        }
        if (ImGui.CollapsingHeader("Reusable groups")) Groups(setup, draft);
        if (ImGui.CollapsingHeader("Bars controlled by Accord"))
        {
            for (int bar = 1; bar <= 10; bar++)
            {
                bool included = bar <= 3 || draft.AdditionalBars.Contains(bar);
                bool shared = setup.SharedRegularBar(bar);
                ImGui.BeginDisabled(bar <= 3 || (shared && !included));
                if (ImGui.Checkbox($"Bar {bar}" + (shared ? " (shared)" : ""), ref included))
                { if (included) draft.AdditionalBars.Add(bar); else draft.AdditionalBars.Remove(bar); }
                ImGui.EndDisabled();
                if (shared && included) ImGui.TextWrapped($"Bar {bar} is shared. Unshare it" + (bar > 3 ? " or deselect it" : "") + " before applying layouts.");
            }
        }
        if (ImGui.BeginCombo("Preview class", setup.Pack.Find(previewJob)!.Job))
        {
            foreach (var job in setup.Pack.Data.Jobs.Where(j => j.JobId >= 19))
                if (ImGui.Selectable(job.Job, job.JobId == previewJob)) previewJob = job.JobId;
            ImGui.EndCombo();
        }
        if (ImGui.Button("Preview all-off layout")) setup.Run(() => setup.PreviewLayout(previewJob, draft));
        ImGui.SameLine();
        if (ImGui.Button("Save layout settings")) setup.Run(() => setup.SaveLayouts(Copy(draft)));
        ImGui.SameLine();
        if (ImGui.Button("Discard staged edits")) draft = Copy(setup.Settings.Layouts);
        ImGui.TextWrapped(setup.LayoutStatus);
    }

    private static void Bool(string label, bool effective, bool? local, bool overridden, Action<bool?> set)
    {
        ImGui.PushID(label);
        bool apply = local.HasValue;
        if (overridden)
        {
            if (ImGui.Checkbox("Apply", ref apply)) set(apply ? effective : null);
            ImGui.SameLine();
        }
        ImGui.BeginDisabled(overridden && !apply);
        if (ImGui.Checkbox(label, ref effective)) set(effective);
        ImGui.EndDisabled();
        ImGui.PopID();
    }

    private static void Mapping(IntentMapping mapping, bool keepAll, LayoutPreferences preferences)
    {
        if (mapping.Areas.Count == 0) ImGui.TextWrapped("Using existing Accord placement. Add an area to define a custom mapping.");
        for (int i = 0; i < mapping.Areas.Count; i++)
        {
            var area = mapping.Areas[i];
            ImGui.PushID(i);
            if (ImGui.TreeNode(area.Name + "##area"))
            {
                string name = area.Name;
                if (ImGui.InputText("Name", ref name, 100)) area.Name = name;
                int bar = area.Bar, first = area.First, last = area.Last;
                if (ImGui.InputInt("Bar", ref bar)) area.Bar = bar;
                if (ImGui.InputInt("First slot", ref first)) area.First = first;
                if (ImGui.InputInt("Last slot", ref last)) area.Last = last;
                bool right = area.FromRight;
                if (ImGui.Checkbox("Fill from right edge", ref right)) area.FromRight = right;
                for (int e = 0; e < area.Entries.Count; e++)
                {
                    var entry = area.Entries[e];
                    ImGui.PushID(e);
                    string label = entry.Intent?.ToString() ?? entry.Action?.Name ?? preferences.Groups.FirstOrDefault(g => g.Id == entry.GroupId)?.Name ?? "Missing group";
                    ImGui.TextUnformatted(label);
                    bool keep = keepAll || entry.KeepManual;
                    ImGui.BeginDisabled(keepAll);
                    if (ImGui.Checkbox("Keep when automated", ref keep)) entry.KeepManual = keep;
                    ImGui.EndDisabled();
                    int priority = entry.Priority;
                    if (ImGui.InputInt("Priority", ref priority)) entry.Priority = priority;
                    if (e > 0 && ImGui.SmallButton("Move earlier")) (area.Entries[e - 1], area.Entries[e]) = (entry, area.Entries[e - 1]);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Remove entry")) area.Entries.RemoveAt(e--);
                    ImGui.PopID();
                }
                if (ImGui.BeginCombo("Add intent/group", "Choose"))
                {
                    foreach (var intent in Enum.GetValues<ControlIntent>())
                        if (ImGui.Selectable(intent.ToString())) area.Entries.Add(new() { Intent = intent, Priority = intent == ControlIntent.ImportantRole ? 20 : 10 });
                    foreach (var group in preferences.Groups)
                        if (ImGui.Selectable(group.Name + "##" + group.Id)) area.Entries.Add(new() { GroupId = group.Id });
                    ImGui.EndCombo();
                }
                Picker("Add individual action/item", a => area.Entries.Add(new() { Action = a, Priority = 30 }));
                if (ImGui.Button("Remove area")) mapping.Areas.RemoveAt(i--);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        if (ImGui.Button("Add area")) mapping.Areas.Add(new() { Name = "Area " + (mapping.Areas.Count + 1) });
    }

    private static void Groups(SetupController setup, LayoutPreferences preferences)
    {
        for (int i = 0; i < preferences.Groups.Count; i++)
        {
            var group = preferences.Groups[i];
            ImGui.PushID(group.Id);
            if (ImGui.TreeNode(group.Name))
            {
                string name = group.Name;
                if (ImGui.InputText("Name", ref name, 100)) group.Name = name;
                Actions("Global contents", group.Contents);
                foreach (var role in Enum.GetValues<JobRole>().Where(r => r != JobRole.Noncombat))
                {
                    bool child = group.Roles.ContainsKey(role);
                    if (ImGui.Checkbox("Use contents for " + role, ref child))
                    { if (child) group.Roles[role] = Copy(group.Contents); else group.Roles.Remove(role); }
                    if (child) Actions(role.ToString(), group.Roles[role]);
                }
                foreach (var job in setup.Pack.Data.Jobs.Where(j => j.JobId >= 19))
                {
                    bool child = group.Classes.ContainsKey(job.JobId);
                    if (ImGui.Checkbox("Use contents for " + job.Job, ref child))
                    { if (child) group.Classes[job.JobId] = Copy(group.Resolve(job.JobId).ToList()); else group.Classes.Remove(job.JobId); }
                    if (child) Actions(job.Job, group.Classes[job.JobId]);
                }
                if (ImGui.Button("Delete group")) preferences.Groups.RemoveAt(i--);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        if (ImGui.Button("Create reusable group")) preferences.Groups.Add(new());
    }

    private static void Actions(string title, List<ActionChoice> actions, bool roleOnly = false)
    {
        ImGui.PushID(title);
        ImGui.TextUnformatted(title);
        for (int i = 0; i < actions.Count; i++)
        {
            if (roleOnly && !FitsScope(actions[i])) continue;
            ImGui.PushID(i);
            ImGui.TextUnformatted(actions[i].Name);
            ImGui.SameLine();
            if (i > 0 && ImGui.SmallButton("Up")) (actions[i-1], actions[i]) = (actions[i], actions[i-1]);
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove")) actions.RemoveAt(i--);
            ImGui.PopID();
        }
        Picker("Add", actions.Add, actionsOnly: roleOnly, roleOnly: roleOnly);
        ImGui.PopID();
    }

    private static void Picker(string label, Action<ActionChoice> selected, bool actionsOnly = false, bool roleOnly = false)
    {
        if (!ImGui.BeginCombo(label, "Choose action or item")) return;
        if (!actionsOnly) ImGui.Combo("Type", ref kind, new[] { "Action", "Item", "General action" }, 3);
        int activeKind = actionsOnly ? 0 : kind;
        if (choices is null || loadedKind != activeKind)
        {
            loadedKind = activeKind;
            choices = activeKind switch
            {
                1 => Svc.Data.GetExcelSheet<Sheets.Item>().Where(r => !r.Name.IsEmpty && r.ItemAction.RowId != 0).Select(r => new ActionChoice { Name = r.Name.ToString(), Type = (byte)Native.HotbarSlotType.Item, Id = r.RowId }).ToList(),
                2 => Svc.Data.GetExcelSheet<Sheets.GeneralAction>().Where(r => !r.Name.IsEmpty).Select(r => new ActionChoice { Name = r.Name.ToString(), Type = (byte)Native.HotbarSlotType.GeneralAction, Id = r.RowId }).ToList(),
                _ => Svc.Data.GetExcelSheet<Sheets.Action>().Where(r => r.IsPlayerAction && !r.IsPvP && !r.Name.IsEmpty).Select(r => new ActionChoice { Name = r.Name.ToString(), Id = r.RowId }).ToList(),
            };
        }
        ImGui.InputText("Search", ref search, 100);
        if (activeKind == 1) ImGui.Checkbox("High-quality item", ref highQuality);
        foreach (var action in choices.Where(a => a.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                     (!roleOnly || (Svc.Data.GetExcelSheet<Sheets.Action>().GetRow(a.Id).IsRoleAction && FitsScope(a)))).Take(100))
            if (ImGui.Selectable(action.Name + "##" + action.Id))
            {
                var choice = Copy(action);
                if (activeKind == 1 && highQuality && Svc.Data.GetExcelSheet<Sheets.Item>().GetRow(action.Id).CanBeHq)
                { choice.Id += 1000000; choice.Name += " (HQ)"; }
                selected(choice); ImGui.CloseCurrentPopup();
            }
        ImGui.EndCombo();
    }
    private static bool FitsScope(ActionChoice action)
    {
        if (scope == "Global") return true;
        var row = Svc.Data.GetExcelSheet<Sheets.Action>().GetRowOrDefault(action.Id);
        if (row is null) return false;
        string job = Svc.Data.GetExcelSheet<Sheets.ClassJob>().GetRow(previewJob).Abbreviation.ToString();
        return typeof(Sheets.ClassJobCategory).GetProperty(job)?.GetValue(row.Value.ClassJobCategory.Value) is true;
    }
    private static T Copy<T>(T source) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source))!;
}
