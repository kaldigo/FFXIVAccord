using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using ECommons.GameHelpers;

namespace Accord;

internal static class AccordUi
{
    internal enum Page { General, Automation, Classes, Hotbars, RoleActions, Exclusions, Groups, Recovery }
    private static Page page;
    private static string search = "";
    private static readonly string[] titles = ["General", "Automation", "Active classes", "Hotbar mapping", "Role actions", "Action exclusions", "Personal groups", "Recovery & tools"];
    private static readonly string[] keywords = ["live mode bars shared", "burst protection support mechanics", "jobs enable", "layout slots position manual defaults override", "important role", "exclude manual wrath", "potions roulette items extras", "backup restore logout diagnostics"];

    internal static void Draw()
    {
        var setup = SetupController.Instance;
        if (setup is null) return;
        LayoutEditor.BeginSession(setup);
        float scale = ImGuiHelpers.GlobalScale;
        float footer = 90 * scale;
        float width = Math.Clamp(ImGui.GetContentRegionAvail().X * .22f, 150 * scale, 205 * scale);
        if (ImGui.BeginChild("Navigation", new Vector2(width, -footer), true))
        {
            ImGui.TextUnformatted("ACCORD");
            ImGui.TextDisabled(SetupController.IsPvP ? "PvP" : setup.Live ? "Live hotbars" : "Static hotbars");
            ImGui.Spacing(); ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##find", "Find a setting...", ref search, 80);
            foreach (var item in Enum.GetValues<Page>())
            {
                int index = (int)item;
                if (!(titles[index] + " " + keywords[index]).Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
                if (index is 0 or 3 or 7) { ImGui.Spacing(); ImGui.TextDisabled(index == 0 ? "SETUP" : index == 3 ? "LAYOUT" : "TOOLS"); }
                if (ImGui.Selectable(titles[index], page == item)) page = item;
            }
        }
        ImGui.EndChild(); ImGui.SameLine();
        if (ImGui.BeginChild("SettingsPage", new Vector2(0, -footer), false))
        {
            ImGui.TextUnformatted(titles[(int)page]); ImGui.Separator(); ImGui.Spacing();
            ImGui.PushItemWidth(Math.Min(300 * scale, ImGui.GetContentRegionAvail().X));
            switch (page)
            {
                case Page.General: DrawGeneral(setup); LayoutEditor.Draw(setup, page); break;
                case Page.Automation: DrawAutomation(setup); break;
                case Page.Classes: DrawClassSelection(setup); break;
                case Page.Recovery: DrawRecovery(setup); break;
                default: LayoutEditor.Draw(setup, page); break;
            }
            ImGui.PopItemWidth();
        }
        ImGui.EndChild();
        ImGui.Separator();
        LayoutEditor.Footer(setup);
        ImGui.TextWrapped(setup.Status.Split('\n')[0]);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(setup.Status);
    }

    private static void DrawAutomation(SetupController setup)
    {
        ImGui.TextWrapped("Choose what your rotation buttons handle automatically. Changes apply immediately.");
        var job = Player.Available ? setup.Pack.Find((uint)Player.Job) : null;
        if (job is null) ImGui.TextWrapped("Switch to a supported class to see what each variant controls for that class.");
        foreach (var definition in VariantDefinitions.All)
        {
            bool enabled = setup.Settings.Enabled(definition.Id);
            if (ImGui.Checkbox(definition.Label, ref enabled)) setup.Run(() => setup.SetVariant(definition.Id, enabled));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(definition.Description);
            if (job is not null)
            {
                ImGui.Indent();
                var pvp = SetupController.IsPvP ? setup.PvP.Pack.Find(job.JobId) : null;
                ImGui.TextWrapped($"{job.Job}: {(pvp is null ? JobIntents.Describe(job.JobId, definition.Id) : pvp.Descriptions.GetValueOrDefault(definition.Id, "No additional automation for this variant."))}");
                ImGui.Unindent();
            }
        }
    }

    private static void DrawGeneral(SetupController setup)
    {
        bool live = setup.Settings.LiveMode;
        if (ImGui.Checkbox("Update hotbars automatically", ref live)) setup.Run(() => setup.SetLive(live));
        ImGui.TextWrapped(live ? "Layout and automation changes apply immediately, including in combat."
            : "Apply your layout manually using the button below.");
        bool cleanup = setup.Settings.ClearOtherActions;
        if (ImGui.Checkbox("Clear other controller and noncombat actions", ref cleanup)) setup.Run(() => setup.SetCleanup(cleanup));
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Combat regular bars are always fully managed according to the bar selection. This option retains the existing cleanup behaviour for controller, noncombat and PvP layouts. Shared bars are preserved.");
        if (!setup.Settings.LiveMode && ImGui.Button("Set selected class hotbars")) setup.Run(setup.ApplyHotbars);
        ImGui.Spacing(); ImGui.Separator(); ImGui.TextUnformatted("Managed bars");
    }

    private static void DrawRecovery(SetupController setup)
    {
        bool manualLogout = setup.Settings.ManualLogoutFallback;
        if (ImGui.Checkbox("Save manual hotbars before logout", ref manualLogout)) setup.Run(() => setup.SetManualFallback(manualLogout));
        ImGui.TextWrapped("Saves three-bar manual layouts for selected combat classes before normal logout. Restores your exact previous Accord layouts on login when compatible Wrath is available. Uses regular bars 1–3 and controller pages 1–3; other bars stay unchanged.");
        if (ImGui.Button("Prepare manual hotbars now")) setup.Run(setup.PrepareManualHotbars);
        ImGui.SameLine();
        if (ImGui.Button("Restore Accord hotbars")) setup.Run(setup.RestoreAccordHotbars);
        ImGui.TextWrapped(setup.ManualStatus);
        ImGui.Separator();
        ImGui.TextWrapped(setup.Status);
        if (setup.HasPendingOperation && ImGui.Button("Keep recovery record and review again")) setup.Run(setup.AbandonPendingOperation);
            ImGui.TextWrapped("Reapply Accord's default Wrath settings for selected classes. Backs up the previous settings and keeps your variants and hotbars.");
            if (ImGui.Button("Reset selected classes to Accord defaults")) setup.Run(setup.ResetSelectedSettings);
            if (!string.IsNullOrWhiteSpace(SetupController.IsPvP ? setup.PvP.Settings.LastHotbarBackup : setup.Settings.LastHotbarBackup))
            {
                ImGui.Separator();
                ImGui.TextWrapped("Restore the saved hotbar layout from before the last Accord hotbar operation, including live updates. Turns live mode off; Wrath settings and variants stay as they are. This is not necessarily your layout from before first using Accord.");
                if (ImGui.Button("Restore previous hotbar layout")) setup.Run(setup.Restore);
            }
            ImGui.Separator();
            ImGui.TextWrapped("Save Accord and Wrath settings and status to a local file for troubleshooting. Nothing is uploaded.");
            if (ImGui.Button("Save troubleshooting report")) setup.Run(setup.ExportDiagnostics);
            foreach (var backup in Directory.EnumerateFiles(Svc.PluginInterface.GetPluginConfigDirectory(), "combined-prototype-*.json").OrderByDescending(File.GetLastWriteTimeUtc))
                if (ImGui.Button("Review settings from old combined plugin: " + Path.GetFileName(backup)))
                {
                    Plugin.Instance.MigrationSource = backup;
                    Plugin.Instance.MigrationPending = true;
                    setup.Run(setup.PreviewMigration);
                }
    }

    private static void DrawClassSelection(SetupController setup)
    {
        ImGui.TextUnformatted("Active classes");
        ImGui.TextWrapped("Click a class to enable or disable Accord. Base classes follow their job.");
        if (ImGui.Button("Select all")) setup.Run(() => setup.SetActiveJobs(setup.Pack.Data.Jobs.Select(j => j.JobId)));
        ImGui.SameLine();
        if (ImGui.Button("Select none")) setup.Run(() => setup.SetActiveJobs(Array.Empty<uint>()));

        float scale = ImGuiHelpers.GlobalScale;
        int columns = Math.Clamp((int)(ImGui.GetContentRegionAvail().X / (72 * scale)), 2, 8);
        if (ImGui.BeginTable("AccordClasses", columns, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var candidate in setup.Pack.Data.Jobs)
            {
                ImGui.TableNextColumn();
                bool active = setup.Pack.IsActive(candidate.JobId, setup.Settings);
                var icon = ClassIcons.Get(candidate.JobId, active);
                ImGui.PushStyleColor(ImGuiCol.Button, active
                    ? ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]
                    : new Vector4(0.15f, 0.15f, 0.15f, 0.4f));
                ImGui.PushID((int)candidate.JobId);
                bool clicked = icon is not null
                    ? ImGui.ImageButton(icon.Handle, new Vector2(40 * scale))
                    : ImGui.Button($"{candidate.Job}##class-{candidate.JobId}", new Vector2(48 * scale));
                ImGui.PopID();
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{candidate.Job}: {(active ? "selected — click to disable Accord" : "not selected — click to enable Accord")}\nDeselecting retains existing settings and hotbars.");
                if (clicked) setup.Run(() =>
                {
                    var selected = setup.Pack.SelectedJobs(setup.Settings).Select(j => j.JobId).ToHashSet();
                    if (active) selected.Remove(candidate.JobId); else selected.Add(candidate.JobId);
                    setup.SetActiveJobs(selected);
                });
                ImGui.TextUnformatted(candidate.Job);
            }
            ImGui.EndTable();
        }
        ImGui.TextUnformatted($"Accord active for {setup.Pack.SelectedJobs(setup.Settings).Length} classes.");
    }

    internal static void Command(string args)
    {
        var setup = SetupController.Instance!;
        var parts = args.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) { Plugin.Instance.IsOpen = true; return; }
        if (Plugin.Instance.MigrationPending) throw new InvalidOperationException("Open /accord to complete separate setup first.");
        switch (parts[0])
        {
            case "selected": setup.ApplyHotbars(); return;
            case "live": setup.SetLive(Value(setup.Live)); return;
            case "restore": setup.Restore(); return;
            case "manual": setup.PrepareManualHotbars(); return;
            case "normal": setup.RestoreAccordHotbars(); return;
        }
        var id = parts[0] switch
        {
            "burst" => VariantId.Burst, "protection" => VariantId.Protection,
            "support" => VariantId.Support, "mechanics" => VariantId.ClassMechanics,
            _ => throw new ArgumentException("Use /accord manual, normal, selected, restore, live, burst, protection, support or mechanics."),
        };
        setup.SetVariant(id, Value(setup.VariantEnabled(id)));
        bool Value(bool previous) => parts.ElementAtOrDefault(1) switch
        {
            "on" => true, "off" => false, "toggle" => !previous,
            _ => throw new ArgumentException("Specify on, off or toggle."),
        };
    }
}
