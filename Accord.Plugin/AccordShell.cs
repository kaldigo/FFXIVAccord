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
