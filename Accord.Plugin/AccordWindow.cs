using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using RebornMaterial;
using Page = Accord.AccordUi.Page;

namespace Accord;

internal sealed class AccordWindow : M3ShellWindow
{
    private sealed record Topic(string Label, string Keywords, Action Draw);
    private readonly Dictionary<string, Topic[]> topics = new();
    private readonly Dictionary<string, int> selected = new();
    private readonly SetupController setup;

    internal AccordWindow(SetupController setup) : base("Accord###Accord", "accord", "Accord", "Accord", new(980, 680),
        new() { MinimumSize = new(420, 360), MaximumSize = new(5000, 5000) })
    {
        this.setup = setup;
        AddPage(new("import", "Import settings", FontAwesomeIcon.Download, "Setup", null,
            Plugin.Instance.DrawMigration, visible: () => Plugin.Instance.MigrationPending));

        Area("Combat", FontAwesomeIcon.Bolt, "Play",
            new("Automation", "engine status auto rotation autoduty burst protection mitigation support class mechanics", () => AccordUi.DrawAutomation(setup)),
            new("Targeting", "enemy visible acquire switch mouseover focus priority range aoe threshold", () => Rotation(LayoutEditor.RotationSection.Targeting, WrathSettingsPanel.DrawTargeting)),
            new("Healing", "healer weave gcd resurrection raise cleanse ally focus mouseover ground", () => Rotation(LayoutEditor.RotationSection.Healing, WrathSettingsPanel.DrawHealing)),
            new("Openers", "combat only damage potions", () => LayoutEditor.DrawGlobalRotation(LayoutEditor.RotationSection.Openers)));
        Area("Jobs", FontAwesomeIcon.Users, "Setup",
            new("Active jobs", "enable disable classes crafting gathering", () => AccordUi.DrawClassSelection(setup)),
            new("Role and job overrides", "inherit class rotation automation hotbars", () => Layout(Page.Overrides)),
            new("Job improvements", "wrath rotation forecasts mitigation savage bossmod", WrathSettingsPanel.DrawJobs));
        Area("Hotbars", FontAwesomeIcon.Th, "Setup",
            new("Managed bars", "live static shared controller automatic cleanup", () => { AccordUi.DrawGeneral(setup); Layout(Page.General); }),
            new("Button mapping", "slots position st aoe intent", () => Layout(Page.Hotbars)),
            new("Role actions", "important role skills", () => Layout(Page.RoleActions)),
            new("Manual actions", "wrath exclude reserve exclusions", () => Layout(Page.Exclusions)),
            new("Groups", "items potions roulette extras", () => Layout(Page.Groups)),
            new("Preview", "resolved inherited job layout", () => Layout(Page.Preview)));
        Area("PvP", FontAwesomeIcon.ShieldAlt, "Setup",
            new("Hotbars", "compact split single rotation buttons mapping automation", () => Layout(Page.PvPGlobal)),
            new("Overrides", "class role exceptions", () => Layout(Page.PvPOverrides)),
            new("Rotation", "machinist guard burst target eligibility", WrathSettingsPanel.DrawPvP));
        Area("RSR", FontAwesomeIcon.Cogs, "Engines",
            new("Automation settings", "heal thresholds defense stance timeline ground actions burst", RsrSettingsPanel.Draw),
            new("Manual actions", "reserve permissions exclude", RsrPermissionsPanel.Draw));
        Area("Troubleshooting", FontAwesomeIcon.Tools, "Tools",
            new("Duty logging", "advanced logging mark export zip", DiagnosticCapturePanel.Draw),
            new("Rotation trace", "60 second capture rejection action recipient testing", RotationTestingPanel.Draw),
            new("Recovery", "backup restore fallback manual logout migration diagnostics report reset", () => AccordUi.DrawRecovery(setup)));
    }

    private void Layout(Page page) => LayoutEditor.Draw(setup, page);

    private static void Rotation(LayoutEditor.RotationSection section, Action drawEngine)
    {
        M3Widgets.SectionLabel("Rotation buttons");
        LayoutEditor.DrawGlobalRotation(section);
        M3Widgets.Divider();
        M3Widgets.SectionLabel("Wrath preferences · saved automatically");
        drawEngine();
    }

    private void Area(string name, FontAwesomeIcon icon, string section, params Topic[] entries)
    {
        topics.Add(name, entries);
        selected.Add(name, 0);
        AddPage(new(name, name, icon, section, null, () =>
        {
            var flow = new M3Flow();
            for (int i = 0; i < entries.Length; i++)
            {
                string label = entries[i].Label;
                flow.Next(M3Widgets.ChipWidth(label));
                if (M3Widgets.Chip(label, label, selected[name] == i)) selected[name] = i;
            }
            M3Widgets.Divider();
            var topic = entries[selected[name]];
            ImGui.PushID(name + "/" + topic.Label);
            try { topic.Draw(); }
            finally { ImGui.PopID(); }
        }, visible: () => !Plugin.Instance.MigrationPending));
    }

    protected override bool SupportsSearch => !Plugin.Instance.MigrationPending;
    protected override string SearchHint => "Find a setting";
    protected override void DrawSearchResults(string text)
    {
        bool found = false;
        foreach (var (area, entries) in topics)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var topic = entries[i];
                if (!(area + " " + topic.Label + " " + topic.Keywords).Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                found = true;
                if (ImGui.Selectable(area + " / " + topic.Label))
                {
                    selected[area] = i;
                    ShowPage(area);
                    return;
                }
            }
        }
        if (!found) ImGui.TextDisabled("No matching settings.");
    }

    protected override void BeforeDrawShell() => LayoutEditor.BeginSession(setup);
    protected override void DrawPageHeader()
    {
        if (Plugin.Instance.MigrationPending) return;
        LayoutEditor.Footer(setup);
        if (!string.IsNullOrWhiteSpace(setup.Status))
        {
            ImGui.TextWrapped(setup.Status.Split('\n')[0]);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(setup.Status);
        }
    }
}
