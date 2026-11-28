using System;
using System.Linq;
using System.Collections.Generic;
using AetherglassUI;
using WrathFork.Contract;

namespace Accord;

internal static partial class LayoutEditor
{
    private static readonly SplitWorkspace overrideStudio = new() { PrimaryMinimum = 360, SecondaryMinimum = 280, CompactLayout = CompactPaneLayout.Tabs };
    private static string overrideQuery = "";

    private static void Overrides(SetupController setup, bool pvp = false)
    {
        var roles = pvp ? draft.PvP.Roles : draft.Roles;
        var classes = pvp ? draft.PvP.Classes : draft.Classes;
        if (selectedOverride is null)
        {
            PageControls.Section("override-list", pvp ? "PvP role & job overrides" : "Role & job overrides", () =>
            {
                Controls.Search("##scopes", "Filter roles and jobs...", ref overrideQuery);
                foreach (var role in Enum.GetValues<JobRole>().Where(r => r != JobRole.Noncombat))
                    ScopeRow("Role: " + role, "Role: " + Label(role), roles.ContainsKey(role), () => roles[role] = new());
                foreach (var job in setup.Pack.Data.Jobs.Where(j => j.JobId >= 19))
                    ScopeRow(job.Job, job.Job, classes.ContainsKey(job.JobId), () => classes[job.JobId] = new());
            }, "Global settings apply until you add an override. Changes share the hotbar draft and Save / Discard controls.");
            return;
        }

        scope = selectedOverride;
        LayoutValues parent = pvp ? draft.PvP.Global.Over(draft.Global) : draft.Global;
        LayoutOverride? over;
        Action remove;
        string source = pvp ? "PvE settings and PvP global" : "Global";
        if (scope.StartsWith("Role: "))
        {
            var key = Enum.Parse<JobRole>(scope[6..]);
            if (pvp && draft.Roles.TryGetValue(key, out var normalRole)) parent = draft.PvP.Global.Over(normalRole.Over(draft.Global));
            roles.TryGetValue(key, out over);
            remove = () => roles.Remove(key);
        }
        else
        {
            var job = setup.Pack.Data.Jobs.First(j => j.Job == scope);
            if (pvp) parent = draft.PvP.Global.Over(draft.Resolve(job.JobId));
            if (roles.TryGetValue(LayoutPreferences.Role(job.JobId), out var role))
            {
                parent = role.Over(parent);
                source += " → " + Label(LayoutPreferences.Role(job.JobId)) + (pvp ? " PvP role" : " role");
            }
            classes.TryGetValue(job.JobId, out over);
            remove = () => classes.Remove(job.JobId);
        }
        if (over is null) { selectedOverride = null; return; }
        if (Controls.ActionButton("All role & job overrides")) { selectedOverride = null; return; }
        PageControls.Scope("override-scope", Label(scope) + (pvp ? " · PvP" : " · PvE"), source,
            reset: () => { remove(); selectedOverride = null; });
        // Reset can remove the scope while drawing this frame. Never edit that detached object.
        if (selectedOverride is null) return;
        OverrideSections(setup, over, parent, pvp);

        void ScopeRow(string key, string label, bool exists, Action add)
        {
            if (!label.Contains(overrideQuery, StringComparison.OrdinalIgnoreCase)) return;
            Controls.Row(key, label, exists ? "Custom values in the shared draft" : "Uses inherited values", () =>
            {
                if (Controls.ActionButton(exists ? "Edit override" : "Add override")) { if (!exists) add(); selectedOverride = key; }
            });
        }
    }

    private static void OverrideSections(SetupController setup, LayoutOverride over, LayoutValues parent, bool pvp)
    {
        var effective = over.Over(parent);

        PageControls.Section("button-options", "Button layout", () =>
        {
            OverrideFlag("keep", "Keep manual controls when automated", parent.KeepManual, over.KeepManual, v => over.KeepManual = v);
            if (!pvp)
            {
                OverrideFlag("join", "Join PvE damage controls", parent.JoinedDamage, over.JoinedDamage, v => over.JoinedDamage = v);
                OverrideFlag("split", "Split class mechanic controls", parent.SplitMechanics, over.SplitMechanics, v => over.SplitMechanics = v);
            }
        }, "Inherited values follow their source. On and Off create an explicit override.");

        PageControls.Section("mapping", "Assignments", () =>
        {
            OverrideCollection("mapping-override", "Use a different mapping", over.Mapping is not null,
                v => over.Mapping = v ? Copy(parent.Mapping) : null);
            if (over.Mapping is null) { ImGui.TextWrapped("Using inherited assignments. Enable a different mapping to edit a copy."); return; }
            var mapping = over.Mapping;
            overrideStudio.Height = 420;
            overrideStudio.Draw("override-assignments", "Assignments", () => DrawAssignments(setup, mapping, effective.KeepManual),
                "Assignment details", () => DrawAssignmentDetails(setup, mapping));
        }, "A mapping override replaces the complete mapping. Edit order, destinations and individual assignments here.");

        PageControls.Section("role-actions", "Role actions", () =>
        {
            OverrideCollection("roles-override", "Use different role actions", over.RoleActions is not null,
                v => over.RoleActions = v ? Copy(parent.RoleActions) : null);
            if (over.RoleActions is null) { ImGui.TextWrapped("Using inherited important and other role-action groups."); return; }
            OverrideActions("Important role actions", over.RoleActions.Important, true);
            OverrideActions("Other role actions", over.RoleActions.Other, true);
        });

        PageControls.Section("exclusions", "Rotation button exclusions", () =>
        {
            OverrideCollection("exclusions-override", "Use different exclusions", over.Exclusions is not null,
                v => over.Exclusions = v ? Copy(parent.Exclusions) : null);
            if (over.Exclusions is null) { ImGui.TextWrapped("Using inherited exclusion rules."); return; }
            DrawExclusionRules(over.Exclusions);
        });

        PageControls.Section("automation", "Automation permissions", () =>
        {
            foreach (var definition in VariantDefinitions.All)
                OverrideFlag(definition.Id.ToString(), definition.Label, (scope == "Global" ? setup.Settings.Enabled(definition.Id) : InheritedAutomation(setup, definition.Id, pvp, scope)),
                    over.Automation.TryGetValue(definition.Id, out var value) ? value : null,
                    v => { if (v.HasValue) over.Automation[definition.Id] = v.Value; else over.Automation.Remove(definition.Id); });
        }, "Permission changes also affect which manual controls the hotbar layout retains.");

        if (!pvp) PageControls.Section("rotation", "Rotation behavior", () => OverrideRotation(over.Rotation, parent.Rotation));

    }

    private static void DrawExclusionRules(List<ActionExclusion> exclusions)
    {
            Picker("Add excluded action", a =>
            {
                if (!exclusions.Any(e => e.Action.Id == a.Id && e.Action.Type == a.Type)) exclusions.Add(new() { Action = a });
            }, actionsOnly: true);
            foreach (var exclusion in exclusions.ToArray())
                PageControls.Section("action-" + exclusion.Action.Type + "-" + exclusion.Action.Id, exclusion.Action.Name, () =>
                {
                    foreach (var route in Enum.GetValues<ControlIntent>().Where(i => i is not (ControlIntent.ImportantRole or ControlIntent.OtherRole or ControlIntent.Exclusions)))
                        Controls.Row(route.ToString(), Label(route), "Skip this action on this button.", () =>
                        {
                            bool old = exclusion.ExcludedFrom.Contains(route), next = Controls.Switch("skip", old);
                            if (next) exclusion.ExcludedFrom.Add(route); else exclusion.ExcludedFrom.Remove(route);
                        }, FieldLayout.Switch);
                    if (Controls.ActionButton("Remove exclusion rule")) exclusions.Remove(exclusion);
                });
    }

    private static void OverrideCollection(string id, string label, bool value, Action<bool> set) =>
        Controls.Row(id, label, value ? "Custom · disable to inherit again" : "Inherited · enable to edit a copy", () =>
        {
            bool next = Controls.Switch("custom", value);
            if (next != value) set(next);
        }, FieldLayout.Switch);

    private static void OverrideFlag(string id, string label, bool inherited, bool? value, Action<bool?> set) =>
        Controls.Row(id, label, "Inherited value: " + (inherited ? "On" : "Off"), () =>
        {
            string old = value.HasValue ? value.Value ? "On" : "Off" : "Inherit";
            string next = Controls.Choice("##value", old, ["Inherit", "On", "Off"]);
            if (next != old) set(next == "Inherit" ? null : next == "On");
        });

    private static void OverrideActions(string title, List<ActionChoice> actions, bool roleOnly)
    {
        ImGui.PushID(title);
        try
        {
            ImGui.TextWrapped(title);
            foreach (var action in actions.ToArray())
            {
                if (roleOnly && !FitsScope(action)) continue;
                Controls.Row(action.Type + "-" + action.Id, action.Name, "", () =>
                {
                    int index = actions.IndexOf(action), next = OrderedEditor.ReorderButtons(index, actions.Count);
                    if (next != index) { actions.RemoveAt(index); actions.Insert(next, action); }
                    ImGui.SameLine();
                    if (Controls.ActionButton("Remove")) actions.Remove(action);
                });
            }
            Picker("Add action", a => { if (!actions.Any(x => x.Type == a.Type && x.Id == a.Id)) actions.Add(a); }, roleOnly: roleOnly, actionsOnly: roleOnly);
        }
        finally { ImGui.PopID(); }
    }

    private static void OverrideRotation(RotationPreferences local, RotationPreferences parent)
    {
        parent = parent.Over(RotationPreferences.Defaults());
        OverrideFlag("heal", "Weave healing into healer damage buttons", parent.IncludeHealing == true, local.IncludeHealing, v => local.IncludeHealing = v);
        OverrideFlag("healing-gcd", "Allow healing GCDs in healer damage buttons", parent.HealingGcds == true, local.HealingGcds, v => local.HealingGcds = v);
        string[] targets = ["Manual", "Enemies fighting your party", "Any eligible enemy"];
        Controls.Row("targeting", "Enemy targeting", "Inherited value: " + targets[(int)(parent.Targeting ?? 0)], () =>
        {
            string old = local.Targeting.HasValue ? targets[(int)local.Targeting.Value] : "Inherit";
            string next = Controls.Choice("##value", old, ["Inherit", ..targets]);
            if (next != old) local.Targeting = next == "Inherit" ? null : (EnemySelection)Array.IndexOf(targets, next);
        });
        Number("range", "Target selection range", local.TargetRange, parent.TargetRange ?? 30, 30, v => local.TargetRange = v);
        Number("aoe", "Switch to AoE at (enemies)", local.AoeTargets, parent.AoeTargets ?? 3, 10, v => local.AoeTargets = v);
        OverrideFlag("untargeted", "Untargeted AoE in activities", parent.ActivityUntargetedAoe == true, local.ActivityUntargetedAoe, v => local.ActivityUntargetedAoe = v);
        OverrideFlag("combat", "Act only during combat", parent.CombatOnly == true, local.CombatOnly, v => local.CombatOnly = v);
        OverrideFlag("openers", "Use job openers", parent.Openers == true, local.Openers, v => local.Openers = v);
        OverrideFlag("potions", "Use damage potions in supported openers", parent.OpenerPotions == true, local.OpenerPotions, v => local.OpenerPotions = v);

        void Number(string id, string label, int? value, int inherited, int max, Action<int?> set)
        {
            OverrideCollection(id + "-custom", "Override " + label.ToLowerInvariant(), value.HasValue, v => set(v ? inherited : null));
            if (value.HasValue) Controls.Row(id, label, "Inherited value: " + inherited, () =>
            {
                int edited = value.Value;
                Controls.Integer(ref edited, 1, max);
                if (edited != value.Value) set(edited);
            }, FieldLayout.Number);
        }
    }
}
