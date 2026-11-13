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
    internal enum Page { General, Hotbars, RoleActions, Exclusions, Groups, Overrides, PvPGlobal, PvPOverrides, Preview }
    internal static AetherglassUI.Topic[] Managed(SetupController setup)=>[
        new("Hotbar updates · saved automatically",
            AccordControls.Flag("live","Update hotbars automatically",()=>setup.Settings.LiveMode,v=>setup.Run(()=>setup.SetLive(v))),
            AccordControls.Flag("cleanup","Clear other controller and noncombat actions",()=>setup.Settings.ClearOtherActions,v=>setup.Run(()=>setup.SetCleanup(v)),"Shared bars are preserved."),
            new AetherglassUI.Setting("apply","Set selected class hotbars",()=>{if(ImGui.Button("Apply layout"))setup.Run(setup.ApplyHotbars);},Visible:()=>!setup.Settings.LiveMode)),
        LayoutEditor.ManagedBars(setup)
    ];
    internal static AetherglassUI.Topic[] Recovery(SetupController setup)
    {
        AetherglassUI.Setting Command(string id,string title,string button,Action execute,string detail="")=>
            new(id,title,()=>{if(ImGui.Button(button))setup.Run(execute);},Detail:detail);
        var root=Svc.PluginInterface.GetPluginConfigDirectory();
        var imports=new List<AetherglassUI.Setting>{
            Command("report","Troubleshooting report","Save report",setup.ExportDiagnostics,"Save settings and status locally.")
        };
        // Discover old imports only when the import row is visible.
        imports.Add(new("imports","Import settings from the combined plugin",()=>{}, "migration backup"){
            ExpandedContent=()=>{
                foreach(var backup in Directory.EnumerateFiles(root,"combined-prototype-*.json").OrderByDescending(File.GetLastWriteTimeUtc)){
                    if(!ImGui.Button("Review "+Path.GetFileName(backup)))continue;
                    Plugin.Instance.MigrationSource=backup;
                    Plugin.Instance.MigrationPending=true;
                    setup.Run(setup.PreviewMigration);
                }
            }
        });
        return [new("Manual logout hotbars",
            AccordControls.Flag("logout","Save manual hotbars before logout",()=>setup.Settings.ManualLogoutFallback,
                v=>setup.Run(()=>setup.SetManualFallback(v)),"Save bars 1–3 for selected combat classes and restore the previous Accord layout on login."),
            Command("prepare","Prepare manual hotbars now","Prepare",setup.PrepareManualHotbars),
            Command("restore","Restore Accord hotbars","Restore",setup.RestoreAccordHotbars),
            new("status","Manual hotbar status",()=>ImGui.TextWrapped(setup.ManualStatus))),
            new("Recovery",
                Command("pending","Keep recovery record and review again","Review",setup.AbandonPendingOperation) with {Visible=()=>setup.HasPendingOperation},
                Command("reset","Reset selected classes to Accord defaults","Reset classes",setup.ResetSelectedSettings,"Backs up previous Wrath settings and keeps variants and hotbars."),
                Command("backup","Restore previous hotbar layout","Restore backup",setup.Restore,"Restores the last hotbar backup and turns live mode off.") with {
                    Visible=()=>!string.IsNullOrWhiteSpace(SetupController.IsPvP?setup.PvP.Settings.LastHotbarBackup:setup.Settings.LastHotbarBackup)},
                new("status","Setup status",()=>ImGui.TextWrapped(setup.Status))),
            new("Troubleshooting and import",imports.ToArray())];
    }

    internal static void DrawClassSelection(SetupController setup)
    {
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
        if (parts.Length == 0) { Plugin.Instance.Open(); return; }
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
