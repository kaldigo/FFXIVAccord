using System;
using System.Linq;
using WrathFork.Contract;

namespace Accord;

internal static partial class LayoutEditor
{
    private static void DrawRotation(RotationPreferences local, RotationPreferences parent, bool over)
    {
        ImGui.TextWrapped("PvE preferences. Existing Wrath settings remain in use until you choose an option. Wrath's master auto-rotation switch is unchanged.");
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
            if (over && !apply && inherited is null)
            {
                ImGui.TextDisabled(label + ": existing Wrath setting");
                ImGui.PopID();
                return;
            }
            var choices = over ? labels : new[] { "Use existing Wrath setting" }.Concat(labels).ToArray();
            int selected = over ? value ?? inherited ?? 0 : value.HasValue ? value.Value + 1 : 0;
            ImGui.BeginDisabled(over && !apply);
            if (ImGui.Combo(label, ref selected, choices, choices.Length)) set(over ? selected : selected == 0 ? null : selected - 1);
            ImGui.EndDisabled();
            ImGui.PopID();
        }
        void Number(string label, int? value, int? inherited, int fallback, int min, int max, Action<int?> set)
        {
            ImGui.PushID(label);
            bool apply = value.HasValue;
            if (ImGui.Checkbox(over ? "Apply" : "Set", ref apply)) { value = apply ? inherited ?? fallback : null; set(value); }
            ImGui.SameLine(); int current = value ?? inherited ?? fallback;
            if (!apply && inherited is null)
            {
                ImGui.TextDisabled(label + ": existing Wrath setting");
                ImGui.PopID();
                return;
            }
            ImGui.BeginDisabled(!apply);
            if (ImGui.SliderInt(label, ref current, min, max)) set(current);
            ImGui.EndDisabled();
            ImGui.PopID();
        }
    }
}
