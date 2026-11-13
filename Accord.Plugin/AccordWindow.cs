using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ECommons.GameHelpers;
using Glasswork.Backend;
using Page = Accord.AccordUi.Page;

namespace Accord;

internal sealed class AccordWindow : Window
{
    private readonly AccordShell shell = new();
    private readonly SetupController setup;
    internal AccordWindow(SetupController setup) : base("Accord###Accord", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar)
    {
        this.setup = setup;
        Size = new Vector2(980, 680);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(360, 320), MaximumSize = new(5000, 5000) };
    }
    public override void Draw()
    {
        AccordShell.DrawBackground();
        if (Plugin.Instance.MigrationPending) { AccordChrome.Header("Import", "", () => IsOpen = false); Plugin.Instance.DrawMigration(); return; }
        LayoutEditor.BeginSession(setup);
        string job = Player.Available ? Player.Job.ToString() : "Offline";
        shell.Draw(job, Plugin.Instance.Automation.HeaderEngine, DrawTask, () =>
        {
            LayoutEditor.Footer(setup);
            if (!string.IsNullOrWhiteSpace(setup.Status))
            {
                ImGui.TextDisabled(setup.Status.Split('\n')[0]);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(setup.Status);
            }
        }, Plugin.Instance.Automation.DrawDock, () => IsOpen = false);
    }
    private void Layout(Page page) => LayoutEditor.Draw(setup, page);
    private void DrawTask(TaskPage task)
    {
        switch (task)
        {
            case TaskPage.Home:
                Plugin.Instance.Automation.Draw();
                AccordControls.Section("Action permissions", () =>
                {
                    foreach (var definition in VariantDefinitions.All)
                    {
                        bool enabled = setup.VariantEnabled(definition.Id);
                        uint jobId = Player.Available ? setup.Pack.Find((uint)Player.Job)?.JobId ?? 0 : 0;
                        if (AccordControls.Toggle(definition.Label, ref enabled, setup.Settings.AutomationTarget(definition.Id, jobId, SetupController.IsPvP)))
                            setup.Run(() => setup.SetVariant(definition.Id, enabled));
                    }
                });
                break;
            case TaskPage.Permissions: AccordUi.DrawPermissions(setup); break;
            case TaskPage.Enemies:
                Gw.Heading("Wrath preferences · saved automatically");
                WrathSettingsPanel.DrawTargeting();
                Gw.Heading("Rotation button rules · save changes");
                LayoutEditor.DrawGlobalRotation(LayoutEditor.RotationSection.Targeting); break;
            case TaskPage.Healing:
                Gw.Heading("Damage buttons · save changes");
                LayoutEditor.DrawGlobalRotation(LayoutEditor.RotationSection.Healing);
                Gw.Heading("Wrath preferences · saved automatically"); WrathSettingsPanel.DrawHealing(); break;
            case TaskPage.Opener: LayoutEditor.DrawGlobalRotation(LayoutEditor.RotationSection.Openers); break;
            case TaskPage.Reserved:
                Gw.Heading("Wrath rotation buttons"); Layout(Page.Exclusions);
                Gw.Heading("RSR automatic actions"); RsrPermissionsPanel.Draw(); break;
            case TaskPage.JobTuning: WrathSettingsPanel.DrawJobs(); break;
            case TaskPage.Rsr: RsrSettingsPanel.Draw(); break;
            case TaskPage.Jobs: AccordUi.DrawClassSelection(setup); break;
            case TaskPage.Managed: AccordUi.DrawGeneral(setup); Layout(Page.General); break;
            case TaskPage.Mapping: Layout(Page.Hotbars); break;
            case TaskPage.RoleActions: Layout(Page.RoleActions); break;
            case TaskPage.Groups: Layout(Page.Groups); break;
            case TaskPage.Overrides: Layout(Page.Overrides); break;
            case TaskPage.PvP: Layout(Page.PvPGlobal); WrathSettingsPanel.DrawPvP(); break;
            case TaskPage.PvPOverrides: Layout(Page.PvPOverrides); break;
            case TaskPage.Preview: Layout(Page.Preview); break;
            case TaskPage.DutyLog: DiagnosticCapturePanel.Draw(); break;
            case TaskPage.Trace: RotationTestingPanel.Draw(); break;
            case TaskPage.Recovery: AccordUi.DrawRecovery(setup); break;
        }
    }
}
