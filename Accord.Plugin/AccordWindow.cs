using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ECommons.GameHelpers;
using AetherglassUI;
using System.Linq;
using System.Collections.Generic;
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
    private readonly WindowShell importShell=new();
    public override void Draw()
    {
        if (Plugin.Instance.MigrationPending){
            importShell.Close=()=>IsOpen=false;
            importShell.Draw("Accord","Import",[new AetherglassUI.Destination("import","Personal settings import","I",Plugin.Instance.DrawMigration)],AccordChrome.Mark);
            return;
        }
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
        }, Plugin.Instance.Automation.DrawDock, () => IsOpen = false, Topics);
    }
    private void Layout(Page page) => LayoutEditor.Draw(setup, page);
    private Topic Permissions(bool global)
    {
        var job=Player.Available?setup.Pack.Find((uint)Player.Job):null;
        var pvp=job!=null && SetupController.IsPvP?setup.PvP.Pack.Find(job.JobId):null;
        return new(global?"Global permissions":"Effective permissions",VariantDefinitions.All.Select(d=>{
            string detail=global?(job==null?d.Description:pvp==null?JobIntents.Describe(job.JobId,d.Id):pvp.Descriptions.GetValueOrDefault(d.Id,d.Description)):
                setup.Settings.AutomationTarget(d.Id,job?.JobId??0,SetupController.IsPvP);
            return AccordControls.Flag(d.Id.ToString(),d.Label,()=>global?setup.Settings.Enabled(d.Id):setup.VariantEnabled(d.Id),
                v=>setup.Run(()=>setup.SetVariant(d.Id,v,global:global)),detail);
        }).ToArray());
    }
    private static Topic Editor(string title,string label,Action draw,string keywords="")=>
        new(title,new Setting("editor",label,()=>{},keywords){ExpandedContent=draw});
    private Topic[]? Topics(TaskPage task)=>task switch {
        TaskPage.Home=>[Plugin.Instance.Automation.PlaySettings(),Permissions(false)],
        TaskPage.Permissions=>[Permissions(true)],
        TaskPage.Enemies=>[..WrathSettingsPanel.Rows(task),LayoutEditor.RotationRows(LayoutEditor.RotationSection.Targeting)],
        TaskPage.Healing=>[LayoutEditor.RotationRows(LayoutEditor.RotationSection.Healing),..WrathSettingsPanel.Rows(task)],
        TaskPage.Opener=>[LayoutEditor.RotationRows(LayoutEditor.RotationSection.Openers)],
        TaskPage.JobTuning=>WrathSettingsPanel.Rows(task),
        TaskPage.Managed=>AccordUi.Managed(setup),
        TaskPage.Trace=>RotationTestingPanel.Rows(),
        TaskPage.Recovery=>AccordUi.Recovery(setup),
        TaskPage.DutyLog=>DiagnosticCapturePanel.Rows(),
        TaskPage.Rsr=>RsrSettingsPanel.Rows(),
        TaskPage.Reserved=>[Editor("Wrath · layout draft","Rotation button exclusions",()=>Layout(Page.Exclusions)),
            Editor("RSR · current job and combat mode","Manual action permissions",RsrPermissionsPanel.Draw)],
        TaskPage.PvP=>[Editor("Layout draft","PvP hotbar setup",()=>Layout(Page.PvPGlobal)),..WrathSettingsPanel.Rows(task)],
        _=>null
    };
    private void DrawTask(TaskPage task)
    {
        switch(task){
            case TaskPage.Jobs: AccordUi.DrawClassSelection(setup); break;
            case TaskPage.Mapping: Layout(Page.Hotbars); break;
            case TaskPage.RoleActions: Layout(Page.RoleActions); break;
            case TaskPage.Groups: Layout(Page.Groups); break;
            case TaskPage.Overrides: Layout(Page.Overrides); break;
            case TaskPage.PvPOverrides: Layout(Page.PvPOverrides); break;
            case TaskPage.Preview: Layout(Page.Preview); break;
        }
    }
}
