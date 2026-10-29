using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility;
using System.Text.RegularExpressions;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using HotbarTools;
using Native = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;
using Sheets = Lumina.Excel.Sheets;

namespace Accord;

internal static class LayoutEditor
{
    private static LayoutPreferences draft = null!;
    private static string scope = "Global";
    private static string? selectedOverride;
    private static bool includeOverrides = true;
    private static uint previewJob = 23;
    private static string search = "";
    private static int kind;
    private static List<ActionChoice>? choices;
    private static int loadedKind = -1;
    private static bool highQuality;

    private static string saved = "";
    private static int selectedGroup;
    private static string groupScope = "Global";
    private static LayoutResult? preview;
    private static string previewKey = "";
    internal static void BeginSession(SetupController setup)
    {
        string current = JsonConvert.SerializeObject(setup.Settings.Layouts);
        if (draft is null || (JsonConvert.SerializeObject(draft) == saved && current != saved))
        { draft = Copy(setup.Settings.Layouts); saved = current; }
        else if (current != saved)
        {
            draft.RebaseAutomation(JsonConvert.DeserializeObject<LayoutPreferences>(saved)!, setup.Settings.Layouts);
            saved = current;
        }
    }
    internal static void Footer(SetupController setup)
    {
        bool dirty = JsonConvert.SerializeObject(draft) != JsonConvert.SerializeObject(setup.Settings.Layouts);
        if (!dirty) saved = JsonConvert.SerializeObject(draft);
        ImGui.BeginDisabled(!dirty);
        if (ImGui.Button("Save layout changes")) setup.Run(() => setup.SaveLayouts(Copy(draft!)));
        ImGui.SameLine();
        if (ImGui.Button("Discard changes")) { draft = Copy(setup.Settings.Layouts); saved = JsonConvert.SerializeObject(draft); }
        ImGui.EndDisabled(); ImGui.SameLine();
        ImGui.TextDisabled(dirty ? "Unsaved layout changes" : string.IsNullOrWhiteSpace(setup.LayoutStatus) ? "Layout settings saved" : "Saved - review layout warnings");
    }
    private static string Label(object value) => value is ControlIntent intent ? intent switch
    {
        ControlIntent.SingleTarget => "Single target (ST)", ControlIntent.AreaDamage => "Area damage (AoE)",
        ControlIntent.Mechanics => "Class mechanics", ControlIntent.Protection => "Protection",
        ControlIntent.Exclusions => "Excluded actions", ControlIntent.Cleanse => "Cleanse / prevention",
        ControlIntent.Raise => "Resurrection", ControlIntent.Recovery => "Personal recovery",
        ControlIntent.PrimaryMechanics => "Primary class mechanics",
        ControlIntent.HealingProtection => "Healing protection",
        ControlIntent.AdditionalBurst => "Additional burst controls",
        _ => Regex.Replace(intent.ToString(), "([a-z])([A-Z])", "$1 $2"),
    } : Regex.Replace(value.ToString()!, "([a-z])([A-Z])", "$1 $2");
    internal static void Draw(SetupController setup, AccordUi.Page page)
    {
        if (page == AccordUi.Page.General) { Bars(setup); return; }
        if (page == AccordUi.Page.Groups) { Groups(setup, draft!); return; }
        if (page == AccordUi.Page.Preview) { DrawPreview(setup); return; }
        if (page == AccordUi.Page.Overrides) { Overrides(setup); return; }
        scope = "Global";
        DrawSettings(setup, page, null, draft.Global);
    }

    private static void DrawSettings(SetupController setup, AccordUi.Page page, LayoutOverride? over, LayoutValues parent)
    {
        var effective = over?.Over(parent) ?? parent;
        if (page == AccordUi.Page.Hotbars)
        {
        Bool("Keep manual controls when automated", effective.KeepManual, over?.KeepManual, over is not null,
            v => { if (over is null) draft.Global.KeepManual = v!.Value; else over.KeepManual = v; });
        Bool("Split class mechanic controls", effective.SplitMechanics, over?.SplitMechanics, over is not null,
            v => { if (over is null) draft.Global.SplitMechanics = v!.Value; else over.SplitMechanics = v; });
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Separate Bard songs, Dancer dances and Astrologian draw/play controls. Other existing mechanic controls already remain separate.");

        }
        if (page == AccordUi.Page.Hotbars)
        {
            bool applied = over is null || over.Mapping is not null;
            if (over is not null && ImGui.Checkbox("Apply this mapping##mapping", ref applied)) over.Mapping = applied ? Copy(parent.Mapping) : null;
            ImGui.BeginDisabled(!applied);
            Mapping(setup, over?.Mapping ?? effective.Mapping, effective.KeepManual, draft!);
            ImGui.EndDisabled();
        }
        if (page == AccordUi.Page.RoleActions)
        {
            bool applied = over is null || over.RoleActions is not null;
            if (over is not null && ImGui.Checkbox("Apply##roles", ref applied)) over.RoleActions = applied ? Copy(parent.RoleActions) : null;
            ImGui.BeginDisabled(!applied);
            var actions = over?.RoleActions ?? effective.RoleActions;
            ImGui.TextWrapped("Choose actions for these groups, then add Important role actions or Other role actions to the mapping. Higher-priority groups take precedence over duplicate individual controls.");
            Actions("Important role actions", actions.Important, true);
            Actions("Other role actions", actions.Other, true);
            ImGui.EndDisabled();
        }
        if (page == AccordUi.Page.Exclusions)
        {
            bool applied = over is null || over.Exclusions is not null;
            if (over is not null && ImGui.Checkbox("Apply##exclusions", ref applied)) over.Exclusions = applied ? Copy(parent.Exclusions) : null;
            ImGui.BeginDisabled(!applied);
            var exclusions = over?.Exclusions ?? effective.Exclusions;
            var routes = Enum.GetValues<ControlIntent>().Where(i => i is not (ControlIntent.ImportantRole or ControlIntent.OtherRole or ControlIntent.Exclusions)).ToArray();
            ImGui.TextWrapped("Checked routes may use the action. Untick a route to exclude it. The Exclusions mapping group provides deliberate access.");
            if (ImGui.BeginTable("Exclusion routes", routes.Length + 2,
                ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg,
                new System.Numerics.Vector2(0, Math.Clamp(exclusions.Count * 30 + 64, 100, 300))))
            {
                ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 160);
                foreach (var route in routes) ImGui.TableSetupColumn(Label(route), ImGuiTableColumnFlags.WidthFixed, 115);
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
    }

    private static void Bars(SetupController setup)
    {
        ImGui.TextWrapped("Bars 1–3 are required. Add other unshared bars here to use them in your mappings.");
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

    private static void Overrides(SetupController setup)
    {
        if (selectedOverride is null)
        {
            ImGui.TextWrapped("Global applies to every class. Add an override only where you want a role or class to behave differently.");
            if (ImGui.Button("Add override")) ImGui.OpenPopup("Add override");
            if (ImGui.BeginPopup("Add override"))
            {
                foreach (var role in Enum.GetValues<JobRole>().Where(r => r != JobRole.Noncombat && !draft.Roles.ContainsKey(r)))
                    if (ImGui.Selectable("Role: " + Label(role))) { draft.Roles[role] = new(); selectedOverride = "Role: " + role; }
                foreach (var job in setup.Pack.Data.Jobs.Where(j => j.JobId >= 19 && !draft.Classes.ContainsKey(j.JobId)))
                    if (ImGui.Selectable(job.Job)) { draft.Classes[job.JobId] = new(); selectedOverride = job.Job; }
                ImGui.EndPopup();
            }
            if (draft.Roles.Count + draft.Classes.Count == 0) ImGui.TextDisabled("No overrides. All classes use Global.");
            foreach (var role in draft.Roles.Keys.ToArray()) Row("Role: " + role, "Role: " + Label(role), () => draft.Roles.Remove(role));
            foreach (var job in draft.Classes.Keys.ToArray()) Row(setup.Pack.Find(job)!.Job, setup.Pack.Find(job)!.Job, () => draft.Classes.Remove(job));
            return;
        }
        if (ImGui.Button("Back to overrides")) { selectedOverride = null; return; }
        scope = selectedOverride;
        ImGui.TextUnformatted("Editing " + Label(scope));
        LayoutValues parent = draft.Global;
        LayoutOverride? over;
        if (scope.StartsWith("Role: ")) draft.Roles.TryGetValue(Enum.Parse<JobRole>(scope[6..]), out over);
        else
        {
            var job = setup.Pack.Data.Jobs.First(j => j.Job == scope);
            if (draft.Roles.TryGetValue(LayoutPreferences.Role(job.JobId), out var role)) parent = role.Over(parent);
            draft.Classes.TryGetValue(job.JobId, out over);
        }
        if (over is null) { selectedOverride = null; return; }
        ImGui.TextWrapped("Only checked Apply settings replace inherited values. Mapping is one complete setting.");
        if (ImGui.BeginTabBar("Override sections"))
        {
            string[] tabs = ["Mapping", "Role actions", "Exclusions"];
            for (int i = 0; i < tabs.Length; i++) if (ImGui.BeginTabItem(tabs[i]))
            {
                DrawSettings(setup, i == 0 ? AccordUi.Page.Hotbars : i == 1 ? AccordUi.Page.RoleActions : AccordUi.Page.Exclusions, over, parent);
                ImGui.EndTabItem();
            }
            if (!scope.StartsWith("Role: ") && ImGui.BeginTabItem("Automation"))
            {
                ImGui.TextWrapped("Apply overrides this option for this class. Server-status buttons then change that override instead of Global. Uncheck Apply to inherit Global again.");
                foreach (var definition in VariantDefinitions.All)
                {
                    bool? local = over.Automation.TryGetValue(definition.Id, out var value) ? value : null;
                    Bool(definition.Label, local ?? setup.Settings.Enabled(definition.Id), local, true,
                        v => { if (v.HasValue) over.Automation[definition.Id] = v.Value; else over.Automation.Remove(definition.Id); });
                }
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        void Row(string key, string label, Action remove)
        {
            ImGui.PushID(key); ImGui.TextUnformatted(label); ImGui.SameLine();
            if (ImGui.SmallButton("Edit")) { selectedOverride = key; }
            ImGui.SameLine(); if (ImGui.SmallButton("Remove")) remove(); ImGui.PopID();
        }
    }

    private static void DrawPreview(SetupController setup)
    {
        ImGui.TextWrapped("Preview only. Choosing a class here does not change Global or create an override.");
        if (ImGui.BeginCombo("Class", setup.Pack.Find(previewJob)!.Job))
        {
            foreach (var job in setup.Pack.Data.Jobs.Where(j => j.JobId >= 19))
                if (ImGui.Selectable(job.Job, previewJob == job.JobId)) previewJob = job.JobId;
            ImGui.EndCombo();
        }
        ImGui.Checkbox("Include explicitly added overrides", ref includeOverrides);
        var preferences = Copy(draft);
        if (!includeOverrides) { preferences.Roles.Clear(); preferences.Classes.Clear(); }
        string source = "Global";
        if (preferences.Roles.TryGetValue(LayoutPreferences.Role(previewJob), out var role) && role.Mapping is not null) source = "Role: " + Label(LayoutPreferences.Role(previewJob));
        if (preferences.Classes.TryGetValue(previewJob, out var cls) && cls.Mapping is not null) source = setup.Pack.Find(previewJob)!.Job;
        ImGui.TextUnformatted("Mapping source: " + source);
        string key = previewJob + JsonConvert.SerializeObject(preferences) + string.Join(",", preferences.ManagedBars.Where(setup.SharedRegularBar));
        if (key != previewKey) { preview = setup.InspectLayout(previewJob, preferences); previewKey = key; }
        ImGui.TextDisabled("All four automation options off: the largest required layout.");
        if (preview is null) return;
        foreach (int bar in preferences.ManagedBars)
        {
            ImGui.PushID(bar); ImGui.TextUnformatted($"Bar {bar}");
            if (ImGui.BeginTable("Slots", ImGui.GetContentRegionAvail().X < 600 * ImGuiHelpers.GlobalScale ? 6 : 12, ImGuiTableFlags.SizingStretchSame))
            {
                for (int slot = 1; slot <= 12; slot++)
                {
                    ImGui.TableNextColumn();
                    var control = preview.Controls.FirstOrDefault(c => c.Position == new Position(bar - 1, slot - 1));
                    string name = control?.Action.Name ?? "Empty";
                    string shortened = name.Length > 7 ? name[..6] + "..." : name;
                    ImGui.Button($"{slot}\n{shortened}##slot{slot}", new Vector2(-1, 42 * ImGuiHelpers.GlobalScale));
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Bar {bar}, slot {slot}: {name}");
                }
                ImGui.EndTable();
            }
            ImGui.PopID();
        }
        foreach (var issue in preview.Issues) ImGui.TextWrapped(issue.Message);
        if (ImGui.CollapsingHeader("Resolved controls"))
            foreach (var control in preview.Controls) ImGui.TextUnformatted($"{control.Position}: {control.Action.Name}");
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

    private static string EntryName(SetupController setup, MappingEntry entry, LayoutPreferences preferences) =>
        entry.Intent is { } intent ? Label(intent) :
        entry.Action?.Name ?? preferences.Groups.FirstOrDefault(g => g.Id == entry.GroupId)?.Name ?? "Missing group";

    private static string Destination(MappingArea area) => area.First == area.Last
        ? $"Exact position {area.First}" : area.First == 1 && area.Last == 12
        ? area.FromRight ? "Right edge" : "Left edge" : $"Custom area {area.First}–{area.Last}";

    private static MappingArea Edge(IntentMapping mapping, int bar, bool right)
    {
        var area = mapping.Areas.FirstOrDefault(a => a.Bar == bar && a.First == 1 && a.Last == 12 && a.FromRight == right);
        if (area is not null) return area;
        area = new() { Bar = bar, Name = right ? "Right edge" : "Left edge", FromRight = right, AvoidFixedSlots = true };
        mapping.Areas.Add(area);
        return area;
    }

    private static void Mapping(SetupController setup, IntentMapping mapping, bool keepAll, LayoutPreferences preferences)
    {
        ImGui.TextWrapped("Choose groups for each edge. The first group sits nearest that edge; the rest fill inward. Classes skip groups they cannot use.");
        ImGui.TextWrapped("Keep when automated leaves the group’s manual buttons available. Create extras on Personal groups; check the result on Preview.");
        if (ImGui.CollapsingHeader("Default layout"))
            if (ImGui.Button("Restore default intent mapping")) { var defaults = setup.DefaultMapping(); mapping.Areas = defaults.Areas; mapping.IntentVersion = defaults.IntentVersion; }
        if (!string.IsNullOrWhiteSpace(setup.LayoutStatus) && ImGui.CollapsingHeader("Layout warnings", ImGuiTreeNodeFlags.DefaultOpen)) ImGui.TextWrapped(setup.LayoutStatus);
        if (!ImGui.BeginTabBar("Mapping bars")) return;
        foreach (int bar in preferences.ManagedBars.Concat(mapping.Areas.Select(a => a.Bar)).Distinct().Order())
        {
            if (!ImGui.BeginTabItem($"Bar {bar}")) continue;
            foreach (bool right in new[] { false, true })
            {
                ImGui.PushID(right ? "right" : "left");
                ImGui.Spacing(); ImGui.TextUnformatted(right ? "Right edge  ←" : "Left edge  →");
                var edgeAreas = mapping.Areas.Where(a => a.Bar == bar && a.First == 1 && a.Last == 12 && a.FromRight == right).ToArray();
                if (edgeAreas.All(a => a.Entries.Count == 0)) ImGui.TextDisabled("No groups assigned.");
                foreach (var area in edgeAreas) { ImGui.PushID(mapping.Areas.IndexOf(area)); DrawEntries(area); ImGui.PopID(); }
                if (ImGui.BeginCombo("Add group", "Choose an intent or personal group..."))
                { ChooseSource(preferences, entry => Edge(mapping, bar, right).Entries.Add(entry)); ImGui.EndCombo(); }
                ImGui.PopID();
            }
            ImGui.Spacing();
            var custom = mapping.Areas.Where(a => a.Bar == bar && (a.First != 1 || a.Last != 12)).ToArray();
            if (custom.Length > 0 && ImGui.CollapsingHeader("Older custom assignments"))
            {
                ImGui.TextWrapped("These saved assignments are preserved. Move their groups to a bar edge to use the simpler layout.");
                foreach (var area in custom)
                {
                    ImGui.PushID(mapping.Areas.IndexOf(area));
                    ImGui.TextUnformatted(Destination(area));
                    DrawEntries(area);
                    if (ImGui.BeginCombo("Add group", "Choose...")) { ChooseSource(preferences, area.Entries.Add); ImGui.EndCombo(); }
                    if (ImGui.SmallButton("Remove assignment")) mapping.Areas.Remove(area);
                    ImGui.PopID();
                }
            }
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();

        void DrawEntries(MappingArea area)
        {
            if (!ImGui.BeginTable("Groups", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp)) return;
            ImGui.TableSetupColumn("Intent / group", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Keep when automated", ImGuiTableColumnFlags.WidthFixed, 145 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Order / destination", ImGuiTableColumnFlags.WidthFixed, 220 * ImGuiHelpers.GlobalScale);
            ImGui.TableHeadersRow();
            for (int i = 0; i < area.Entries.Count; i++)
            {
                var entry = area.Entries[i]; ImGui.PushID(i);
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                ImGui.TextWrapped($"{i + 1}. {EntryName(setup, entry, preferences)}");
                ImGui.TableNextColumn();
                bool keep = keepAll || entry.KeepManual;
                ImGui.BeginDisabled(keepAll);
                if (ImGui.Checkbox("##Keep manual buttons when automated", ref keep)) entry.KeepManual = keep;
                ImGui.EndDisabled();
                if (keepAll && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Enabled by Keep manual controls when automated.");
                ImGui.TableNextColumn();
                ImGui.BeginDisabled(i == 0);
                if (ImGui.SmallButton("Up")) (area.Entries[i - 1], area.Entries[i]) = (entry, area.Entries[i - 1]);
                ImGui.EndDisabled(); ImGui.SameLine(); ImGui.BeginDisabled(i == area.Entries.Count - 1);
                if (ImGui.SmallButton("Down")) (area.Entries[i + 1], area.Entries[i]) = (entry, area.Entries[i + 1]);
                ImGui.EndDisabled(); ImGui.SameLine();
                bool remove = false;
                if (ImGui.SmallButton("Move")) ImGui.OpenPopup("Move group");
                if (ImGui.BeginPopup("Move group"))
                {
                    foreach (int targetBar in preferences.ManagedBars)
                        foreach (bool targetRight in new[] { false, true })
                            if (ImGui.Selectable($"Bar {targetBar} · {(targetRight ? "Right edge" : "Left edge")}"))
                            {
                                var target = Edge(mapping, targetBar, targetRight);
                                if (target != area) { target.Entries.Add(entry); remove = true; }
                            }
                    ImGui.EndPopup();
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Options")) ImGui.OpenPopup("Group options");
                if (ImGui.BeginPopup("Group options"))
                {
                    if (ImGui.Selectable("Remove group")) remove = true;
                    int priority = entry.Priority;
                    if (ImGui.InputInt("Duplicate priority", ref priority)) entry.Priority = priority;
                    ImGui.TextWrapped("Higher priority keeps this group's copy of a repeated action. Automation selectors remain separate.");
                    if (entry.Action is not null && ImGui.Button("Save as personal group"))
                    {
                        var group = new PersonalGroup { Name = entry.Action.Name, Contents = [entry.Action] };
                        preferences.Groups.Add(group); entry.Action = null; entry.GroupId = group.Id;
                    }
                    ImGui.EndPopup();
                }
                if (remove) area.Entries.RemoveAt(i--);
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
    }
    private static void ChooseSource(LayoutPreferences preferences, Action<MappingEntry> selected)
    {
        ImGui.TextDisabled("INTENTS");
        foreach (var intent in Enum.GetValues<ControlIntent>())
            if (ImGui.Selectable(Label(intent))) selected(new() { Intent = intent, Priority = intent == ControlIntent.ImportantRole ? 20 : 10 });
        if (preferences.Groups.Count == 0) ImGui.TextDisabled("Create personal groups on the Personal groups page.");
        else ImGui.TextDisabled("PERSONAL GROUPS");
        foreach (var group in preferences.Groups)
            if (ImGui.Selectable(group.Name + "##" + group.Id)) selected(new() { GroupId = group.Id });
    }

    private static void Groups(SetupController setup, LayoutPreferences preferences)
    {
        ImGui.TextWrapped("Create named groups for potions, roulettes and other extras, then add them to your hotbar mapping.");
        if (ImGui.Button("New group")) { preferences.Groups.Add(new()); selectedGroup = preferences.Groups.Count - 1; }
        if (preferences.Groups.Count == 0) return;
        selectedGroup = Math.Clamp(selectedGroup, 0, preferences.Groups.Count - 1);
        if (ImGui.BeginCombo("Group", preferences.Groups[selectedGroup].Name))
        {
            for (int i = 0; i < preferences.Groups.Count; i++)
                if (ImGui.Selectable(preferences.Groups[i].Name + "##" + i, selectedGroup == i)) selectedGroup = i;
            ImGui.EndCombo();
        }
        var group = preferences.Groups[selectedGroup];
        string name = group.Name;
        if (ImGui.InputText("Name", ref name, 100)) group.Name = name;
        if (ImGui.BeginCombo("Contents for", Label(groupScope)))
        {
            if (ImGui.Selectable("Global")) groupScope = "Global";
            foreach (var role in Enum.GetValues<JobRole>().Where(r => r != JobRole.Noncombat))
                if (ImGui.Selectable("Role: " + Label(role))) groupScope = "Role: " + role;
            foreach (var job in setup.Pack.Data.Jobs.Where(j => j.JobId >= 19))
                if (ImGui.Selectable(job.Job)) groupScope = job.Job;
            ImGui.EndCombo();
        }
        var contents = group.Contents;
        bool editing = true;
        if (groupScope.StartsWith("Role: "))
        {
            var role = Enum.Parse<JobRole>(groupScope[6..]); editing = group.Roles.ContainsKey(role);
            if (ImGui.Checkbox("Use different contents for this role", ref editing))
            { if (editing) group.Roles[role] = Copy(group.Contents); else group.Roles.Remove(role); }
            contents = group.Roles.GetValueOrDefault(role) ?? group.Contents;
        }
        else if (groupScope != "Global")
        {
            var job = setup.Pack.Data.Jobs.First(j => j.Job == groupScope); editing = group.Classes.ContainsKey(job.JobId);
            if (ImGui.Checkbox("Use different contents for this class", ref editing))
            { if (editing) group.Classes[job.JobId] = Copy(group.Resolve(job.JobId).ToList()); else group.Classes.Remove(job.JobId); }
            contents = group.Resolve(job.JobId).ToList();
            if (editing) contents = group.Classes[job.JobId];
        }
        ImGui.BeginDisabled(!editing); Actions("Contents", contents); ImGui.EndDisabled();
        if (!editing) ImGui.TextDisabled("Inherited contents. Enable the checkbox above to customize them.");
        ImGui.Spacing();
        if (ImGui.Button("Delete group")) ImGui.OpenPopup("Delete group?");
        if (ImGui.BeginPopup("Delete group?"))
        {
            ImGui.TextWrapped("Mappings using this group will need a replacement.");
            if (ImGui.Button("Delete")) { preferences.Groups.RemoveAt(selectedGroup); selectedGroup = 0; ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }
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
        uint editingJob = scope.StartsWith("Role: ")
            ? SetupController.Instance!.Pack.Data.Jobs.First(j => j.JobId >= 19 && LayoutPreferences.Role(j.JobId) == Enum.Parse<JobRole>(scope[6..])).JobId
            : SetupController.Instance!.Pack.Data.Jobs.First(j => j.Job == scope).JobId;
        string job = Svc.Data.GetExcelSheet<Sheets.ClassJob>().GetRow(editingJob).Abbreviation.ToString();
        return typeof(Sheets.ClassJobCategory).GetProperty(job)?.GetValue(row.Value.ClassJobCategory.Value) is true;
    }
    private static T Copy<T>(T source) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source))!;
}
