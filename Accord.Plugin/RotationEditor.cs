using System;
using WrathFork.Contract;

namespace Accord;

internal static partial class LayoutEditor
{
    private static void DrawRotation(RotationPreferences local, RotationPreferences parent, bool over)
    {
        parent = parent.Over(RotationPreferences.Defaults());
        if (!over)
        {
            var resolved = local.Over(parent);
            local.Targeting = resolved.Targeting; local.TargetRange = resolved.TargetRange;
            local.AoeTargets = resolved.AoeTargets; local.ActivityUntargetedAoe = resolved.ActivityUntargetedAoe;
            local.CombatOnly = resolved.CombatOnly; local.Openers = resolved.Openers; local.OpenerPotions = resolved.OpenerPotions;
            local.IncludeHealing = resolved.IncludeHealing; local.HealingGcds = resolved.HealingGcds;
        }
        ImGui.TextWrapped("Accord owns these PvE preferences. Role and class overrides inherit from Accord's global settings.");
        Switch("Weave healing into healer damage buttons", local.IncludeHealing, parent.IncludeHealing, v => local.IncludeHealing = v);
        Switch("Also allow healing GCDs in healer damage buttons", local.HealingGcds, parent.HealingGcds, v => local.HealingGcds = v);
        ImGui.TextWrapped("Requires Auto support. Healing abilities use available weave windows; healing GCDs may replace damage only when separately enabled. Dedicated healing buttons remain available. These settings control Wrath's buttons; RSR's automatic runner has its own healing permissions.");
        Choice("Enemy targeting", local.Targeting is { } t ? (int)t : null, parent.Targeting is { } pt ? (int)pt : null,
            ["Manual", "Enemies fighting your party", "Any eligible enemy"], v => local.Targeting = v.HasValue ? (EnemySelection)v.Value : null);
        Number("Target selection range", local.TargetRange, parent.TargetRange, 30, 1, 30, v => local.TargetRange = v);
        Number("Switch to AoE at (enemies)", local.AoeTargets, parent.AoeTargets, 3, 1, 10, v => local.AoeTargets = v);
        Switch("Untargeted AoE in activities", local.ActivityUntargetedAoe, parent.ActivityUntargetedAoe, v => local.ActivityUntargetedAoe = v);
        ImGui.TextWrapped("Allows suitable self-centred AoE against even one nearby enemy without selecting it, in duties and instanced combat activities only. Never enables this in the open world or PvP. Does not acquire a target; targeting and combat restrictions still apply.");
        Switch("Act only during combat", local.CombatOnly, parent.CombatOnly, v => local.CombatOnly = v);
        Switch("Use job openers", local.Openers, parent.Openers, v => local.Openers = v);
        Switch("Use damage potions in supported openers", local.OpenerPotions, parent.OpenerPotions, v => local.OpenerPotions = v);
        ImGui.TextWrapped("Openers retain Wrath's level, content and readiness requirements. Potions are only used where the selected opener supports them.");

        void Switch(string label, bool? value, bool? inherited, Action<bool?> set) =>
            Choice(label, value.HasValue ? value.Value ? 1 : 0 : null, inherited.HasValue ? inherited.Value ? 1 : 0 : null,
                ["Off", "On"], v => set(v.HasValue ? v == 1 : null));
        void Choice(string label, int? value, int? inherited, string[] labels, Action<int?> set)
        {
            ImGui.PushID(label);
            bool apply = value.HasValue;
            if (over)
            {
                if (ImGui.Checkbox("Apply", ref apply)) { value = apply ? inherited ?? 0 : null; set(value); }
                ImGui.SameLine();
            }
            int selected = value ?? inherited ?? 0;
            ImGui.BeginDisabled(over && !apply);
            if (ImGui.Combo(label, ref selected, labels, labels.Length)) set(selected);
            ImGui.EndDisabled();
            ImGui.PopID();
        }
        void Number(string label, int? value, int? inherited, int fallback, int min, int max, Action<int?> set)
        {
            ImGui.PushID(label);
            bool apply = value.HasValue;
            if (over)
            {
                if (ImGui.Checkbox("Apply", ref apply)) { value = apply ? inherited ?? fallback : null; set(value); }
                ImGui.SameLine();
            }
            int current = value ?? inherited ?? fallback;
            ImGui.BeginDisabled(over && !apply);
            if (ImGui.SliderInt(label, ref current, min, max)) set(current);
            ImGui.EndDisabled();
            ImGui.PopID();
        }
    }
}
