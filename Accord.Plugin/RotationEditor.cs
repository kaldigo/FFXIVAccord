using System;
using WrathFork.Contract;

namespace Accord;

internal static partial class LayoutEditor
{
    internal enum RotationSection { All, Healing, Targeting, Openers }
    internal static void DrawRotation(RotationPreferences local, RotationPreferences parent, bool over, RotationSection section = RotationSection.All)
    {
        parent = parent.Over(RotationPreferences.Defaults());
        if (section is RotationSection.All or RotationSection.Healing)
        {
            Switch("Weave healing into healer damage buttons", local.IncludeHealing, parent.IncludeHealing, v => local.IncludeHealing = v);
            Switch("Also allow healing GCDs in healer damage buttons", local.HealingGcds, parent.HealingGcds, v => local.HealingGcds = v);
            ImGui.TextWrapped("Enable Auto support to use these on Wrath’s damage buttons. Allowing healing GCDs lets a heal take the place of a damage spell.");
        }
        if (section is RotationSection.All or RotationSection.Targeting)
        {
            Choice("Enemy targeting", local.Targeting is { } t ? (int)t : null, parent.Targeting is { } pt ? (int)pt : null,
                ["Manual", "Enemies fighting your party", "Any eligible enemy"], v => local.Targeting = v.HasValue ? (EnemySelection)v.Value : null);
            Number("Target selection range", local.TargetRange, parent.TargetRange, 30, 1, 30, v => local.TargetRange = v);
            Number("Switch to AoE at (enemies)", local.AoeTargets, parent.AoeTargets, 3, 1, 10, v => local.AoeTargets = v);
            Switch("Untargeted AoE in activities", local.ActivityUntargetedAoe, parent.ActivityUntargetedAoe, v => local.ActivityUntargetedAoe = v);
            ImGui.TextWrapped("Allows suitable self-centred AoE against even one nearby enemy without selecting it, in duties and instanced combat activities only. Never enables this in the open world or PvP. Does not acquire a target; targeting and combat restrictions still apply.");
        }
        if (section is RotationSection.All or RotationSection.Openers)
        {
            Switch("Act only during combat", local.CombatOnly, parent.CombatOnly, v => local.CombatOnly = v);
            Switch("Use job openers", local.Openers, parent.Openers, v => local.Openers = v);
            Switch("Use damage potions in supported openers", local.OpenerPotions, parent.OpenerPotions, v => local.OpenerPotions = v);
            ImGui.TextWrapped("Openers retain Wrath's level, content and readiness requirements. Potions are only used where the selected opener supports them.");
        }
        void Switch(string label, bool? value, bool? inherited, Action<bool?> set)
        {
            if (over)
            {
                Choice(label, value.HasValue ? value.Value ? 1 : 0 : null, inherited == true ? 1 : 0,
                    ["Off", "On"], v => set(v.HasValue ? v == 1 : null));
                return;
            }
            bool enabled = value ?? inherited ?? false;
            if (AccordControls.Toggle(label, ref enabled)) set(enabled);
        }
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
            string prior=labels[Math.Clamp(selected,0,labels.Length-1)];
            string next=AetherglassUI.Controls.Choice(label,prior,labels);
            if(next!=prior)set(Array.IndexOf(labels,next));
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
            int edited=current;
            AetherglassUI.Controls.Row(label,label,"",()=>AetherglassUI.Controls.Integer(ref edited,min,max));
            if(edited!=current)set(edited);
            ImGui.EndDisabled();
            ImGui.PopID();
        }
    }
}
