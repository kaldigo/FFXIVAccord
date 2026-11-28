using System;
using System.Linq;
using AetherglassUI;

namespace Accord;

internal static partial class LayoutEditor
{
    private static void GlobalRoleActions()
    {
        PageControls.Section("global-role-actions", "Role-action groups", () =>
        {
            OverrideActions("Important role actions", draft.Global.RoleActions.Important, true);
            OverrideActions("Other role actions", draft.Global.RoleActions.Other, true);
        }, "Add these groups to the layout. Job eligibility filters the actions; higher-priority groups keep duplicate actions.");
    }

    private static void PvPGlobal(SetupController setup)
    {
        scope = "Global";
        PageControls.Scope("pvp-global", "Global PvP", "Normal PvE settings");
        PageControls.Section("pvp-buttons", "PvP damage buttons", () =>
        {
            Controls.Row("count", "Rotation buttons", "One uses the supported full rotation. Ninja selects ST or AoE by enemy count and retains the choice during mudras.", () =>
            {
                string old = draft.PvP.RotationButtons == PvPRotationButtons.One ? "One" : "Two (ST / AoE)";
                string next = Controls.Choice("##count", old, ["One", "Two (ST / AoE)"]);
                if (next != old) draft.PvP.RotationButtons = next == "One" ? PvPRotationButtons.One : PvPRotationButtons.Two;
            });
            if (Controls.ActionButton("Use compact PvP layout"))
            {
                draft.PvP.Global.Mapping = PvPConfiguredLayout.CompactMapping();
                draft.PvP.RotationButtons = PvPRotationButtons.One;
            }
            ImGui.TextWrapped("Compact keeps limit breaks, job tactical controls, Recuperate, Guard, Purify and Sprint. Review and save the shared draft to update bars.");
        });
        OverrideSections(setup, draft.PvP.Global, draft.Global, true);
    }

    private static void Groups(SetupController setup, LayoutPreferences preferences)
    {
        scope = "Global";
        PageControls.Section("personal-groups", "Personal groups", () =>
        {
            if (Controls.ActionButton("New group")) { preferences.Groups.Add(new()); selectedGroup = preferences.Groups.Count - 1; }
            if (preferences.Groups.Count == 0) { ImGui.TextWrapped("Create a group, choose its contents, then add it to the layout."); return; }
            selectedGroup = Math.Clamp(selectedGroup, 0, preferences.Groups.Count - 1);
            Controls.Row("selected", "Editing group", "", () =>
            {
                if (ImGui.BeginCombo("##group", preferences.Groups[selectedGroup].Name))
                {
                    try { for (int i = 0; i < preferences.Groups.Count; i++) if (ImGui.Selectable(preferences.Groups[i].Name + "##" + i, selectedGroup == i)) selectedGroup = i; }
                    finally { ImGui.EndCombo(); }
                }
            });
        });
        if (preferences.Groups.Count == 0) return;
        var original = preferences.Groups[selectedGroup];
        PageControls.Section("group-contents", "Contents", () =>
        {
            Controls.Row("name", "Group name", "", () => { string name = original.Name; if (ImGui.InputText("##name", ref name, 100)) original.Name = name; });
            Controls.Row("pvp", "Edit PvP contents", "Switch between normal and PvP group contents.", () => groupPvP = Controls.Switch("pvp", groupPvP), FieldLayout.Switch);
            editingPvP = groupPvP;
            var group = original;
            if (groupPvP)
            {
                OverrideCollection("pvp-contents", "Use different PvP contents", group.PvP is not null,
                    v => group.PvP = v ? new PersonalGroup { Contents = Copy(group.Contents) } : null);
                if (group.PvP is null) { ImGui.TextWrapped("PvP uses normal group contents."); return; }
                group = group.PvP;
            }
            Controls.Row("contents-scope", "Contents for", "Global contents are inherited until a role or job has its own list.", () =>
            {
                if (!ImGui.BeginCombo("##scope", Label(groupScope))) return;
                try
                {
                    if (ImGui.Selectable("Global")) groupScope = "Global";
                    foreach (var role in Enum.GetValues<JobRole>().Where(r => r != JobRole.Noncombat))
                        if (ImGui.Selectable("Role: " + Label(role))) groupScope = "Role: " + role;
                    foreach (var job in setup.Pack.Data.Jobs.Where(j => j.JobId >= 19))
                        if (ImGui.Selectable(job.Job)) groupScope = job.Job;
                }
                finally { ImGui.EndCombo(); }
            });
            scope = groupScope;
            var contents = group.Contents;
            if (groupScope.StartsWith("Role: "))
            {
                var role = Enum.Parse<JobRole>(groupScope[6..]);
                OverrideCollection("role-contents", "Use different contents for this role", group.Roles.ContainsKey(role),
                    v => { if (v) group.Roles[role] = Copy(group.Contents); else group.Roles.Remove(role); });
                if (!group.Roles.TryGetValue(role, out contents)) { ImGui.TextWrapped("Using global group contents."); return; }
            }
            else if (groupScope != "Global")
            {
                var job = setup.Pack.Data.Jobs.First(j => j.Job == groupScope);
                OverrideCollection("job-contents", "Use different contents for this job", group.Classes.ContainsKey(job.JobId),
                    v => { if (v) group.Classes[job.JobId] = Copy(group.Resolve(job.JobId).ToList()); else group.Classes.Remove(job.JobId); });
                if (!group.Classes.TryGetValue(job.JobId, out contents)) { ImGui.TextWrapped("Using inherited group contents."); return; }
            }
            OverrideActions("Actions and items", contents, false);
        });
        PageControls.Section("remove-group", "Remove group", () =>
        {
            ImGui.TextWrapped("Mappings that use this group will need a replacement. Save the shared draft to apply removal.");
            if (Controls.ActionButton("Delete group")) { preferences.Groups.RemoveAt(selectedGroup); selectedGroup = 0; }
        });
    }
}
