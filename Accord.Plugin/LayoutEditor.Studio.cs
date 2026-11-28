using System;
using System.Linq;
using System.Numerics;
using AetherglassUI;
using Dalamud.Bindings.ImGui;
using HotbarTools;
using Newtonsoft.Json;

namespace Accord;

internal static partial class LayoutEditor
{
    private static readonly SplitWorkspace studio=new(){PrimaryMinimum=380,SecondaryMinimum=300,CompactLayout=CompactPaneLayout.Tabs};
    private static MappingEntry? selectedEntry;
    private static string assignmentQuery="";

    internal static ChangeSummary PendingChanges(SetupController setup,Action review)
    {
        bool dirty=JsonConvert.SerializeObject(draft)!=JsonConvert.SerializeObject(setup.Settings.Layouts);
        return new("Shared rules & layout draft","Unsaved changes",dirty,
            ()=>setup.Run(()=>setup.SaveLayouts(Copy(draft))),
            ()=>{draft=Copy(setup.Settings.Layouts);saved=JsonConvert.SerializeObject(setup.Settings.Layouts);selectedEntry=null;})
            {SaveLabel="Save shared draft",Review=review};
    }
    internal static void DrawStudio(SetupController setup,bool revealOptions=false)
    {
        editingPvP=false;scope="Global";
        PageControls.Scope("layout-scope","Global PvE layout","Global",reset:null);
        bool compact=ImGui.GetContentRegionAvail().X<500*WorkspaceControls.Scale;
        if(compact&&revealOptions)ImGui.SetNextItemOpen(true,ImGuiCond.Always);
        if(!compact||ImGui.CollapsingHeader("Button layout options"))PageControls.Section("layout-options","Button layout",()=>{
            Bool("Keep manual controls when automated",draft.Global.KeepManual,null,false,v=>draft.Global.KeepManual=v!.Value);
            Bool("Join PvE damage controls",draft.Global.JoinedDamage,null,false,v=>draft.Global.JoinedDamage=v!.Value);
            Bool("Split class mechanic controls",draft.Global.SplitMechanics,null,false,v=>draft.Global.SplitMechanics=v!.Value);
        },status:"Shared draft");
        studio.Height=Math.Max(180,ImGui.GetContentRegionAvail().Y/WorkspaceControls.Scale-8);
        studio.Draw("layout-studio","Assignments",()=>DrawAssignments(setup),"Preview & details",()=>{
            DrawAssignmentDetails(setup);
            PageControls.Section("resolved","Resolved hotbars",()=>DrawPreview(setup));
        });
    }
    private static void DrawAssignments(SetupController setup,IntentMapping? overrideMapping=null,bool? keepManual=null)
    {
        var mapping=overrideMapping??draft.Global.Mapping;
        bool keepAll=keepManual??draft.Global.KeepManual;
        Controls.Search("##assignments","Filter assignments...",ref assignmentQuery);
        if(ImGui.CollapsingHeader("Default layout")&&Controls.ActionButton("Restore default intent mapping"))
        {var defaults=editingPvP?PvPConfiguredLayout.DefaultMapping():setup.DefaultMapping();mapping.Areas=defaults.Areas;mapping.IntentVersion=defaults.IntentVersion;selectedEntry=null;}
        if(!string.IsNullOrWhiteSpace(setup.LayoutStatus))ImGui.TextWrapped(setup.LayoutStatus);
        foreach(int bar in draft.ManagedBars.Concat(mapping.Areas.Select(a=>a.Bar)).Distinct().Order())
        {
            ImGui.PushID(bar);
            try
            {
                foreach(var area in mapping.Areas.Where(a=>a.Bar==bar).ToArray())
                {
                    var entries=area.Entries.Where(e=>(EntryName(setup,e,draft)+" "+area.Name).Contains(assignmentQuery,StringComparison.OrdinalIgnoreCase)).ToArray();
                    if(entries.Length==0&&assignmentQuery.Length>0)continue;
                    PageControls.Section("area-"+mapping.Areas.IndexOf(area),area.Name,()=>{
                        ImGui.TextWrapped($"Bar {area.Bar} · {Destination(area)}");
                        if(area.AvoidFixedSlots)ImGui.TextDisabled("Skip fixed slots");
                        bool compact=ImGui.GetContentRegionAvail().X<470*WorkspaceControls.Scale;
                        if(ImGui.BeginTable("assignments",compact?2:3,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp))
                        {
                            try
                            {
                                ImGui.TableSetupColumn("Assignment");
                                ImGui.TableSetupColumn("Keep",ImGuiTableColumnFlags.WidthFixed,48*WorkspaceControls.Scale);
                                if(!compact)ImGui.TableSetupColumn("Priority",ImGuiTableColumnFlags.WidthFixed,65*WorkspaceControls.Scale);
                                ImGui.TableHeadersRow();
                                foreach(var entry in entries)
                                {
                                    ImGui.PushID(area.Entries.IndexOf(entry));
                                    try
                                    {
                                        ImGui.TableNextRow();ImGui.TableNextColumn();
                                        string name=EntryName(setup,entry,draft);
                                        if(ImGui.Selectable((area.Entries.IndexOf(entry)+1)+". "+name,ReferenceEquals(selectedEntry,entry)))selectedEntry=entry;
                                        if(ImGui.IsItemHovered())ImGui.SetTooltip(name+"\nSelect Preview & details for order, destination and options.");
                                        ImGui.TableNextColumn();ImGui.BeginDisabled(keepAll);
                                        try{bool before=keepAll||entry.KeepManual;bool after=Controls.Switch("keep",before);if(after!=before)entry.KeepManual=after;}finally{ImGui.EndDisabled();}
                                        if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip(keepAll?"Enabled by Keep manual controls when automated.":"Keep this group's manual controls when automated.");
                                        if(!compact){ImGui.TableNextColumn();ImGui.TextUnformatted(entry.Priority.ToString());}
                                    }
                                    finally{ImGui.PopID();}
                                }
                            }
                            finally{ImGui.EndTable();}
                        }
                        if(area.Entries.Count==0)ImGui.TextDisabled("No assignments.");
                        if((area.First!=1||area.Last!=12)&&Controls.ActionButton("Remove assignment area"))
                        {mapping.Areas.Remove(area);selectedEntry=null;}
                    },status:area.Entries.Count+" assignments");
                }
                foreach(bool right in new[]{false,true})
                {
                    ImGui.PushID(right?"right":"left");
                    try{if(ImGui.BeginCombo($"Bar {bar} · {(right?"Add at right edge":"Add at left edge")}","Choose an intent or personal group..."))
                    {try{ChooseSource(draft,e=>Edge(mapping,bar,right).Entries.Add(e));}finally{ImGui.EndCombo();}}}
                    finally{ImGui.PopID();}
                }
            }
            finally{ImGui.PopID();}
        }
    }
    private static void DrawAssignmentDetails(SetupController setup,IntentMapping? overrideMapping=null)
    {
        var mapping=overrideMapping??draft.Global.Mapping;
        var area=mapping.Areas.FirstOrDefault(a=>selectedEntry!=null&&a.Entries.Contains(selectedEntry));
        if(area==null){selectedEntry=mapping.Areas.SelectMany(a=>a.Entries).FirstOrDefault();area=mapping.Areas.FirstOrDefault(a=>selectedEntry!=null&&a.Entries.Contains(selectedEntry));}
        if(area==null||selectedEntry==null)return;
        var entry=selectedEntry;
        PageControls.Section("assignment-details",EntryName(setup,entry,draft),()=>{
            ImGui.TextWrapped($"Bar {area.Bar} · {Destination(area)}");
            int index=area.Entries.IndexOf(entry);
            int target=OrderedEditor.ReorderButtons(index,area.Entries.Count);
            if(target!=index){area.Entries.RemoveAt(index);area.Entries.Insert(target,entry);}
            if(ImGui.BeginCombo("Move group","Choose destination..."))
            {
                try{foreach(int bar in draft.ManagedBars)foreach(bool right in new[]{false,true})
                    if(ImGui.Selectable($"Bar {bar} · {(right?"Right edge":"Left edge")}"))
                    {var destination=Edge(mapping,bar,right);if(destination!=area){area.Entries.Remove(entry);destination.Entries.Add(entry);}}}
                finally{ImGui.EndCombo();}
            }
            int priority=entry.Priority;if(ImGui.InputInt("Duplicate priority",ref priority))entry.Priority=priority;
            ImGui.TextWrapped("Higher priority keeps this group's copy of a repeated action. Automation selectors remain separate.");
            if(entry.Action!=null&&Controls.ActionButton("Save as personal group"))
            {var group=new PersonalGroup{Name=entry.Action.Name,Contents=[entry.Action]};draft.Groups.Add(group);entry.Action=null;entry.GroupId=group.Id;}
            if(Controls.ActionButton("Remove group")){area.Entries.Remove(entry);selectedEntry=null;}
        },status:"Shared draft");
    }
}
