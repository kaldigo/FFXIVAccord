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

internal static partial class LayoutEditor
{
    internal static AetherglassUI.Topic RotationRows(RotationSection section)
    {
        var v=draft.Global.Rotation;
        var def=WrathFork.Contract.RotationPreferences.Defaults();
        AetherglassUI.Setting Flag(string key,string label,Func<bool> get,Action<bool> set)=>AccordControls.Flag(key,label,get,set);
        AetherglassUI.Setting Number(string key,string label,Func<int> get,int max,Action<int> set)=>new(key,label,()=>{
            int old=get(),value=old;AetherglassUI.Controls.Integer(ref value,1,max);if(value!=old)set(value);
        }){Layout=AetherglassUI.FieldLayout.Number};
        AetherglassUI.Setting[] rows=section switch {
            RotationSection.Healing=>[
                Flag("heal","Weave healing into healer damage buttons",()=>v.IncludeHealing??def.IncludeHealing??false,x=>v.IncludeHealing=x),
                Flag("gcd","Allow healing GCDs in healer damage buttons",()=>v.HealingGcds??def.HealingGcds??false,x=>v.HealingGcds=x)],
            RotationSection.Targeting=>[
                new("mode","Enemy targeting",()=>{
                    string[] labels=["Manual","Enemies fighting your party","Any eligible enemy"];
                    string current=labels[Math.Clamp((int)(v.Targeting??def.Targeting??0),0,2)];
                    string next=AetherglassUI.Controls.Choice("##mode",current,labels);
                    if(next!=current)v.Targeting=(WrathFork.Contract.EnemySelection)Array.IndexOf(labels,next);
                }),
                Number("range","Target selection range",()=>v.TargetRange??def.TargetRange??30,30,x=>v.TargetRange=x),
                Number("aoe","Switch to AoE at (enemies)",()=>v.AoeTargets??def.AoeTargets??3,10,x=>v.AoeTargets=x),
                Flag("untargeted","Untargeted AoE in activities",()=>v.ActivityUntargetedAoe??def.ActivityUntargetedAoe??false,x=>v.ActivityUntargetedAoe=x)],
            _=>[Flag("combat","Act only during combat",()=>v.CombatOnly??def.CombatOnly??false,x=>v.CombatOnly=x),
                Flag("openers","Use job openers",()=>v.Openers??def.Openers??false,x=>v.Openers=x),
                Flag("potions","Use damage potions in supported openers",()=>v.OpenerPotions??def.OpenerPotions??false,x=>v.OpenerPotions=x)]
        };
        return new("Damage buttons · save changes",rows);
    }
    private static LayoutPreferences draft = null!;
    private static string scope = "Global";
    private static string? selectedOverride;
    private static bool includeOverrides = true;
    private static bool editingPvP;
    private static bool previewPvP;
    private static bool loadedPvP;
    private static string? selectedPvPOverride;
    private static uint previewJob = 23;
    private static string search = "";
    private static int kind;
    private static List<ActionChoice>? choices;
    private static int loadedKind = -1;
    private static bool highQuality;

    private static string saved = "";
    private static int selectedGroup;
    private static string groupScope = "Global";
    private static bool groupPvP;
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
        string current = JsonConvert.SerializeObject(setup.Settings.Layouts);
        if (JsonConvert.SerializeObject(draft) == current) { saved = current; return; }
        ImGui.TextUnformatted("Unsaved layout or rotation changes");
        if (ImGui.Button("Save changes"))
            setup.Run(() => setup.SaveLayouts(Copy(draft)));
        ImGui.SameLine();
        if (ImGui.Button("Discard changes"))
        {
            draft = Copy(setup.Settings.Layouts);
            saved = current;
        }
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
        editingPvP = page is AccordUi.Page.PvPGlobal or AccordUi.Page.PvPOverrides;
        if (page == AccordUi.Page.PvPGlobal) { PvPGlobal(setup); return; }
        if (page == AccordUi.Page.PvPOverrides)
        {
            (selectedOverride, selectedPvPOverride) = (selectedPvPOverride, selectedOverride);
            Overrides(setup, true);
            (selectedOverride, selectedPvPOverride) = (selectedPvPOverride, selectedOverride);
            return;
        }
        if (page == AccordUi.Page.Groups) { Groups(setup, draft!); return; }
        if (page == AccordUi.Page.Preview) { DrawPreview(setup); return; }
        if (page == AccordUi.Page.Overrides) { Overrides(setup); return; }
        scope = "Global";
        DrawSettings(setup, page, null, draft.Global);
    }

    private static bool InheritedAutomation(SetupController setup, VariantId id, bool pvp, string selected)
    {
        bool isRole = selected.StartsWith("Role: ");
        uint job = isRole ? 0 : setup.Pack.Data.Jobs.First(j => j.Job == selected).JobId;
        var roleKey = isRole ? Enum.Parse<JobRole>(selected[6..]) : LayoutPreferences.Role(job);
        bool value = setup.Settings.Enabled(id);
        if ((!isRole || pvp) && draft.Roles.TryGetValue(roleKey, out var normalRole) && normalRole.Automation.TryGetValue(id, out var roleValue)) value = roleValue;
        if (!pvp) return value;
        if (!isRole && draft.Classes.TryGetValue(job, out var normalClass) && normalClass.Automation.TryGetValue(id, out var classValue)) value = classValue;
        if (draft.PvP.Global.Automation.TryGetValue(id, out var global)) value = global;
        if (!isRole && draft.PvP.Roles.TryGetValue(roleKey, out var role) && role.Automation.TryGetValue(id, out var byRole)) value = byRole;
        return value;
    }

    private static void PvPGlobal(SetupController setup)
    {
        scope = "Global";
        ImGui.TextWrapped("PvP settings apply above your normal settings. Role and class exceptions are added on the PvP overrides page. Uncheck Apply to inherit.");
        int buttons = draft.PvP.RotationButtons == PvPRotationButtons.One ? 0 : 1;
        if (ImGui.Combo("Rotation buttons", ref buttons, new[] { "One", "Two (ST / AoE)" }, 2))
            draft.PvP.RotationButtons = buttons == 0 ? PvPRotationButtons.One : PvPRotationButtons.Two;
        ImGui.TextWrapped("One uses the full supported rotation. Ninja selects ST or AoE by enemy count and keeps that choice during mudras.");
        if (ImGui.Button("Use compact PvP layout"))
        {
            draft.PvP.Global.Mapping = PvPConfiguredLayout.CompactMapping();
            draft.PvP.RotationButtons = PvPRotationButtons.One;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Sets the draft mapping to one attack button, retained limit breaks and fixed Recuperate, Guard, Purify and Sprint positions. Job-specific tactical controls remain accessible. Review and apply the draft to update bars.");
        if (!ImGui.BeginTabBar("PvP Global settings")) return;
        if (ImGui.BeginTabItem("Automation"))
        {
            foreach (var definition in VariantDefinitions.All)
            {
                bool? local = draft.PvP.Global.Automation.TryGetValue(definition.Id, out var value) ? value : null;
                Bool(definition.Label, local ?? setup.Settings.Enabled(definition.Id), local, true,
                    v => { if (v.HasValue) draft.PvP.Global.Automation[definition.Id] = v.Value; else draft.PvP.Global.Automation.Remove(definition.Id); });
            }
            ImGui.EndTabItem();
        }
        foreach (var section in new[] { ("Mapping", AccordUi.Page.Hotbars), ("Role actions", AccordUi.Page.RoleActions), ("Exclusions", AccordUi.Page.Exclusions) })
            if (ImGui.BeginTabItem(section.Item1))
            { DrawSettings(setup, section.Item2, draft.PvP.Global, draft.Global); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }

    private static void DrawSettings(SetupController setup, AccordUi.Page page, LayoutOverride? over, LayoutValues parent)
    {
        var effective = over?.Over(parent) ?? parent;
        if (page == AccordUi.Page.Hotbars)
        {
        Bool("Keep manual controls when automated", effective.KeepManual, over?.KeepManual, over is not null,
            v => { if (over is null) draft.Global.KeepManual = v!.Value; else over.KeepManual = v; });
        if (!editingPvP)
        {
            Bool("Join PvE damage controls", effective.JoinedDamage, over?.JoinedDamage, over is not null,
                v => { if (over is null) draft.Global.JoinedDamage = v!.Value; else over.JoinedDamage = v; });
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Use one adaptive damage button for standard jobs. It uses your AoE target threshold and preserves separate ST/AoE exclusions. Limited jobs retain their dedicated controls.");
            Bool("Split class mechanic controls", effective.SplitMechanics, over?.SplitMechanics, over is not null,
                v => { if (over is null) draft.Global.SplitMechanics = v!.Value; else over.SplitMechanics = v; });
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Separate Bard songs, Dancer dances and Astrologian draw/play controls. Other existing mechanic controls already remain separate.");
        }

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
            ImGui.TextWrapped("Choose an action, then check the buttons that should skip it.");
            Picker("Add action", a =>
            {
                if (!exclusions.Any(e => e.Action.Id == a.Id && e.Action.Type == a.Type))
                    exclusions.Add(new() { Action = a });
            }, actionsOnly: true);
            for (int i = 0; i < exclusions.Count; i++)
            {
                var exclusion = exclusions[i];
                ImGui.PushID(i);
                if (ImGui.TreeNode($"{exclusion.Action.Name} ({exclusion.ExcludedFrom.Count} excluded)###action"))
                {
                    foreach (var route in routes)
                    {
                        bool excluded = exclusion.ExcludedFrom.Contains(route);
                        if (ImGui.Checkbox(Label(route), ref excluded))
                        {
                            if (excluded) exclusion.ExcludedFrom.Add(route);
                            else exclusion.ExcludedFrom.Remove(route);
                        }
                    }
                    if (ImGui.SmallButton("Remove exclusion rule")) exclusions.RemoveAt(i--);
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }
            ImGui.EndDisabled();
        }
    }

    internal static AetherglassUI.Topic ManagedBars(SetupController setup)=>new("Managed bars · layout draft",
        Enumerable.Range(1,10).Select(bar=>{
            bool shared=setup.SharedRegularBar(bar);
            return AccordControls.Flag("bar"+bar,"Bar "+bar,()=>bar<=3||draft.AdditionalBars.Contains(bar),v=>{
                if(v)draft.AdditionalBars.Add(bar);else draft.AdditionalBars.Remove(bar);
            },shared?"Shared bar · unshare before applying.":"") with {
                DisabledReason=()=>bar<=3?"Required":shared&&!draft.AdditionalBars.Contains(bar)?"Shared in game":null
            };
        }).ToArray());

    private static void Overrides(SetupController setup, bool pvp = false)
    {
        var roles = pvp ? draft.PvP.Roles : draft.Roles;
        var classes = pvp ? draft.PvP.Classes : draft.Classes;
        if (selectedOverride is null)
        {
            ImGui.TextWrapped("Global applies to every class. Add an override only where you want a role or class to behave differently.");
            if (ImGui.Button("Add override")) ImGui.OpenPopup("Add override");
            if (ImGui.BeginPopup("Add override"))
            {
                foreach (var role in Enum.GetValues<JobRole>().Where(r => r != JobRole.Noncombat && !roles.ContainsKey(r)))
                    if (ImGui.Selectable("Role: " + Label(role))) { roles[role] = new(); selectedOverride = "Role: " + role; }
                foreach (var job in setup.Pack.Data.Jobs.Where(j => j.JobId >= 19 && !classes.ContainsKey(j.JobId)))
                    if (ImGui.Selectable(job.Job)) { classes[job.JobId] = new(); selectedOverride = job.Job; }
                ImGui.EndPopup();
            }
            if (roles.Count + classes.Count == 0) ImGui.TextDisabled("No overrides. All classes use Global.");
            foreach (var role in roles.Keys.ToArray()) Row("Role: " + role, "Role: " + Label(role), () => roles.Remove(role));
            foreach (var job in classes.Keys.ToArray()) Row(setup.Pack.Find(job)!.Job, setup.Pack.Find(job)!.Job, () => classes.Remove(job));
            return;
        }
        if (ImGui.Button("Back to overrides")) { selectedOverride = null; return; }
        scope = selectedOverride;
        ImGui.TextUnformatted("Editing " + Label(scope));
        LayoutValues parent = pvp ? draft.PvP.Global.Over(draft.Global) : draft.Global;
        LayoutOverride? over;
        if (scope.StartsWith("Role: "))
        {
            var key = Enum.Parse<JobRole>(scope[6..]);
            if (pvp && draft.Roles.TryGetValue(key, out var normalRole)) parent = draft.PvP.Global.Over(normalRole.Over(draft.Global));
            roles.TryGetValue(key, out over);
        }
        else
        {
            var job = setup.Pack.Data.Jobs.First(j => j.Job == scope);
            if (pvp) parent = draft.PvP.Global.Over(draft.Resolve(job.JobId));
            if (roles.TryGetValue(LayoutPreferences.Role(job.JobId), out var role)) parent = role.Over(parent);
            classes.TryGetValue(job.JobId, out over);
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
            if (ImGui.BeginTabItem("Automation"))
            {
                ImGui.TextWrapped("Apply replaces the inherited option. Server-status buttons change the highest applied layer for the active class and mode.");
                foreach (var definition in VariantDefinitions.All)
                {
                    bool? local = over.Automation.TryGetValue(definition.Id, out var value) ? value : null;
                    Bool(definition.Label, local ?? InheritedAutomation(setup, definition.Id, pvp, scope), local, true,
                        v => { if (v.HasValue) over.Automation[definition.Id] = v.Value; else over.Automation.Remove(definition.Id); });
                }
                ImGui.EndTabItem();
            }
            if (!pvp && ImGui.BeginTabItem("Rotation behaviour"))
            { DrawRotation(over.Rotation, parent.Rotation, true); ImGui.EndTabItem(); }
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
        ImGui.Checkbox("PvP", ref previewPvP);
        if (previewPvP && setup.PvP.Pack.Find(previewJob) is null) previewJob = 23;
        if (ImGui.BeginCombo("Class", setup.Pack.Find(previewJob)!.Job))
        {
            foreach (var job in setup.Pack.Data.Jobs.Where(j => j.JobId >= 19 && (!previewPvP || setup.PvP.Pack.Find(j.JobId) != null)))
                if (ImGui.Selectable(job.Job, previewJob == job.JobId)) previewJob = job.JobId;
            ImGui.EndCombo();
        }
        ImGui.Checkbox("Include explicitly added overrides", ref includeOverrides);
        var preferences = Copy(draft);
        if (!includeOverrides) { preferences.Roles.Clear(); preferences.Classes.Clear(); preferences.PvP.Roles.Clear(); preferences.PvP.Classes.Clear(); }
        string source = "Global";
        if (preferences.Roles.TryGetValue(LayoutPreferences.Role(previewJob), out var role) && role.Mapping is not null) source = "Role: " + Label(LayoutPreferences.Role(previewJob));
        if (preferences.Classes.TryGetValue(previewJob, out var cls) && cls.Mapping is not null) source = setup.Pack.Find(previewJob)!.Job;
        if (previewPvP)
        {
            if (preferences.PvP.Global.Mapping is not null) source = "PvP Global";
            if (preferences.PvP.Roles.TryGetValue(LayoutPreferences.Role(previewJob), out var pvpRole) && pvpRole.Mapping is not null) source = "PvP role: " + Label(LayoutPreferences.Role(previewJob));
            if (preferences.PvP.Classes.TryGetValue(previewJob, out var pvpClass) && pvpClass.Mapping is not null) source = "PvP " + setup.Pack.Find(previewJob)!.Job;
        }
        ImGui.TextUnformatted("Mapping source: " + source);
        var previewSettings = ConfiguredLayout.Clone(setup.Settings);
        previewSettings.Layouts = preferences;
        foreach (var definition in VariantDefinitions.All)
            ImGui.TextDisabled(definition.Label + ": " + (previewSettings.Enabled(definition.Id, previewJob, previewPvP) ? "On" : "Off") +
                " — " + previewSettings.AutomationTarget(definition.Id, previewJob, previewPvP));
        string key = previewPvP + ":" + previewJob + JsonConvert.SerializeObject(preferences) + string.Join(",", preferences.ManagedBars.Where(setup.SharedRegularBar));
        if (key != previewKey) { preview = previewPvP ? setup.InspectPvPLayout(previewJob, preferences) : setup.InspectLayout(previewJob, preferences); previewKey = key; }
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
            if (ImGui.Button("Restore default intent mapping")) { var defaults = editingPvP ? PvPConfiguredLayout.DefaultMapping() : setup.DefaultMapping(); mapping.Areas = defaults.Areas; mapping.IntentVersion = defaults.IntentVersion; }
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

        void DrawEntries(MappingArea area) => AccordControls.MappingTable(stacked =>
        {
            for (int i = 0; i < area.Entries.Count; i++)
            {
                var entry = area.Entries[i]; ImGui.PushID(i);
                bool keep = keepAll || entry.KeepManual;
                if (AccordControls.MappingRow(stacked, $"{i + 1}. {EntryName(setup, entry, preferences)}", keepAll, ref keep))
                    entry.KeepManual = keep;
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
        });
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
        ImGui.Checkbox("Edit PvP contents", ref groupPvP);
        editingPvP = groupPvP;
        if (groupPvP)
        {
            bool applied = group.PvP is not null;
            if (ImGui.Checkbox("Apply PvP contents", ref applied))
                group.PvP = applied ? new PersonalGroup { Contents = Copy(group.Contents) } : null;
            if (group.PvP is null) { ImGui.TextWrapped("Inheriting normal contents. Apply PvP contents to choose different actions for PvP."); return; }
            group = group.PvP;
        }
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
            ImGui.TextWrapped(actions[i].Name);
            if (ImGui.GetContentRegionAvail().X >= 520 * ImGuiHelpers.GlobalScale) ImGui.SameLine();
            if (i > 0)
            {
                if (ImGui.SmallButton("Up")) (actions[i-1], actions[i]) = (actions[i], actions[i-1]);
                ImGui.SameLine();
            }
            if (ImGui.SmallButton("Remove")) actions.RemoveAt(i--);
            ImGui.PopID();
        }
        Picker("Add", actions.Add, actionsOnly: roleOnly, roleOnly: roleOnly);
        ImGui.PopID();
    }

    private static void Picker(string label, Action<ActionChoice> selected, bool actionsOnly = false, bool roleOnly = false)
    {
        if (!ImGui.BeginCombo(label, "Choose action or item")) return;
        if (ImGui.IsWindowAppearing()) search = "";
        if (!actionsOnly) ImGui.Combo("Type", ref kind, new[] { "Action", "Item", "General action" }, 3);
        int activeKind = actionsOnly ? 0 : kind;
        if (choices is null || loadedKind != activeKind || loadedPvP != editingPvP)
        {
            loadedKind = activeKind; loadedPvP = editingPvP;
            choices = activeKind switch
            {
                1 => Svc.Data.GetExcelSheet<Sheets.Item>().Where(r => !r.Name.IsEmpty && r.ItemAction.RowId != 0).Select(r => new ActionChoice { Name = r.Name.ToString(), Type = (byte)Native.HotbarSlotType.Item, Id = r.RowId }).ToList(),
                2 => Svc.Data.GetExcelSheet<Sheets.GeneralAction>().Where(r => !r.Name.IsEmpty).Select(r => new ActionChoice { Name = r.Name.ToString(), Type = (byte)Native.HotbarSlotType.GeneralAction, Id = r.RowId }).ToList(),
                _ => Svc.Data.GetExcelSheet<Sheets.Action>().Where(r => r.IsPlayerAction && r.IsPvP == editingPvP && !r.Name.IsEmpty).Select(r => new ActionChoice { Name = r.Name.ToString(), Id = r.RowId }).ToList(),
            };
        }
        ImGui.InputText("Search", ref search, 100);
        if (activeKind == 1) ImGui.Checkbox("High-quality item", ref highQuality);
        int shown = 0;
        foreach (var action in choices.Where(a => a.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                     (!roleOnly || (Svc.Data.GetExcelSheet<Sheets.Action>().GetRow(a.Id).IsRoleAction && FitsScope(a)))).Take(101))
        {
            if (++shown > 100) { ImGui.TextDisabled("Refine your search to see more results."); break; }
            if (ImGui.Selectable(action.Name + "##" + action.Id))
            {
                var choice = Copy(action);
                if (activeKind == 1 && highQuality && Svc.Data.GetExcelSheet<Sheets.Item>().GetRow(action.Id).CanBeHq)
                { choice.Id += 1000000; choice.Name += " (HQ)"; }
                selected(choice); ImGui.CloseCurrentPopup();
            }
        }
        if (shown == 0) ImGui.TextDisabled("No matching actions or items.");
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
