using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ECommons.GameHelpers;
using Glasswork.Backend;
using Glasswork.Dalamud;
using Page = Accord.AccordUi.Page;

namespace Accord;

internal sealed class AccordWindow : Window
{
    private readonly AccordShell shell = new();
    private readonly SetupController setup;
    private readonly GlassworkHost glass;
    internal AccordWindow(SetupController setup, GlassworkHost glass) : base("Accord###Accord", ImGuiWindowFlags.NoBackground)
    {
        this.setup = setup;
        this.glass = glass;
        Size = new Vector2(980, 680);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new(360, 320), MaximumSize = new(5000, 5000) };
    }
    public override void Draw()
    {
        glass.DrawWindowBackground(16, .5f, false);
        if (Plugin.Instance.MigrationPending) { Plugin.Instance.DrawMigration(); return; }
        LayoutEditor.BeginSession(setup);
        string job = Player.Available ? Player.Job.ToString() : "Offline";
        shell.Draw(job, DrawTask, () =>
        {
            LayoutEditor.Footer(setup);
            if (!string.IsNullOrWhiteSpace(setup.Status))
            {
                ImGui.TextDisabled(setup.Status.Split('\n')[0]);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(setup.Status);
            }
        });
    }
    private void Layout(Page page) => LayoutEditor.Draw(setup, page);
    private void DrawTask(TaskPage task)
    {
        switch (task)
        {
            case TaskPage.Home: Plugin.Instance.Automation.Draw(); break;
            case TaskPage.Permissions: AccordUi.DrawPermissions(setup); break;
            case TaskPage.Enemies:
                WrathSettingsPanel.DrawTargeting();
                Gw.Heading("Rotation button rules");
                LayoutEditor.DrawGlobalRotation(LayoutEditor.RotationSection.Targeting); break;
            case TaskPage.Healing:
                LayoutEditor.DrawGlobalRotation(LayoutEditor.RotationSection.Healing);
                Gw.Heading("Ally selection and overrides"); WrathSettingsPanel.DrawHealing(); break;
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
