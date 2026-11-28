using System;
using System.Linq;
using System.Text.RegularExpressions;
using AetherglassUI;
using Newtonsoft.Json;

namespace Accord;

// Offline services only. The studio draw code itself is linked from Accord.Plugin.
public enum VariantId { Burst, Protection, Support, ClassMechanics, RoleMechanics }
internal static class VariantDefinitions
{
    internal sealed record Definition(VariantId Id,string Label);
    internal static readonly Definition[] All=Enum.GetValues<VariantId>().Select(id=>new Definition(id,"Auto "+id)).ToArray();
}
internal sealed class AccordSettings {
    internal LayoutPreferences Layouts=new(){Global=new(){Mapping=DefaultAccordData.CreateDefaults()}};
    internal bool Enabled(VariantId id)=>false;
}
internal sealed class PreviewPack
{
    internal sealed record JobEntry(uint JobId,string Job);
    internal JobEntry[] Jobs=[new(19,"PLD"),new(21,"WAR"),new(32,"DRK"),new(37,"GNB"),new(24,"WHM"),new(28,"SCH"),new(33,"AST"),new(40,"SGE"),new(20,"MNK"),new(22,"DRG"),new(30,"NIN"),new(34,"SAM"),new(39,"RPR"),new(41,"VPR"),new(23,"BRD"),new(31,"MCH"),new(38,"DNC"),new(25,"BLM"),new(27,"SMN"),new(35,"RDM"),new(42,"PCT"),new(36,"BLU")];
    internal PreviewPack Data=>this;
    internal JobEntry? Find(uint id)=>Jobs.FirstOrDefault(j=>j.JobId==id);
}
internal sealed class SetupController
{
    internal AccordSettings Settings=new();
    internal PreviewPack Pack=new();
    internal string LayoutStatus=>"";
    internal void Run(Action action)=>action();
    internal void SaveLayouts(LayoutPreferences value)=>Settings.Layouts=value;
    internal IntentMapping DefaultMapping()=>DefaultAccordData.CreateDefaults();
}
internal static partial class LayoutEditor
{
    private static LayoutPreferences draft=new(){Global=new(){Mapping=DefaultAccordData.CreateDefaults()}};
    private static string saved="",scope="Global";
    private static bool editingPvP;
    private static int selectedGroup;
    private static string groupScope="Global";
    private static bool groupPvP;
    private static string? selectedOverride;
    private static string Label(object value)=>Regex.Replace(value.ToString()!,"([a-z])([A-Z])","$1 $2");
    private static bool FitsScope(ActionChoice action)=>true;
    private static void Picker(string label,Action<ActionChoice> selected,bool actionsOnly=false,bool roleOnly=false)
    {ImGui.TextDisabled(label+" · action catalogue is available in game");}
    private static bool InheritedAutomation(SetupController setup,VariantId id,bool pvp,string selected)=>false;
    private static bool overrideInitialized;
    internal static void DrawCollectionPreview(SetupController setup,string page)
    {
        if(page=="roles"){scope="Global";GlobalRoleActions();}
        else if(page=="pvp"){editingPvP=true;PvPGlobal(setup);}
        else {
            if(draft.Groups.Count==0)draft.Groups.Add(new(){Name="Recovery",Contents=[new(){Name="Second Wind",Id=7541},new(){Name="Bloodbath",Id=7542}]});
            Groups(setup,draft);
        }
    }
    internal static void DrawOverridePreview(SetupController setup,bool pvp=false,bool list=false)
    {
        if(!overrideInitialized){
            draft.Roles[JobRole.Tank]=new(){KeepManual=true};
            var custom=new LayoutOverride{Mapping=Copy(draft.Global.Mapping),JoinedDamage=false};
            custom.Automation[VariantId.RoleMechanics]=false;
            custom.Rotation.AoeTargets=2;
            if(pvp)draft.PvP.Classes[21]=custom;else draft.Classes[21]=custom;
            selectedOverride=list?null:"WAR";
            overrideInitialized=true;
        }
        editingPvP=pvp;
        Overrides(setup,pvp);
    }
    private static T Copy<T>(T value)=>JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value))!;
    private static string EntryName(SetupController setup,MappingEntry entry,LayoutPreferences preferences)=>
        entry.Intent is {} intent?Regex.Replace(intent.ToString(),"([a-z])([A-Z])","$1 $2"):
            entry.Action?.Name??preferences.Groups.FirstOrDefault(g=>g.Id==entry.GroupId)?.Name??"Missing group";
    private static string Destination(MappingArea area)=>$"slots {area.First}-{area.Last} · {(area.FromRight?"right edge":"left edge")}";
    private static MappingArea Edge(IntentMapping mapping,int bar,bool right)
    {
        var area=mapping.Areas.FirstOrDefault(a=>a.Bar==bar&&a.First==1&&a.Last==12&&a.FromRight==right);
        if(area!=null)return area;
        area=new(){Bar=bar,Name=right?"Right edge":"Left edge",FromRight=right,AvoidFixedSlots=true};mapping.Areas.Add(area);return area;
    }
    private static void Bool(string label,bool effective,bool? local,bool overridden,Action<bool?> set)
    {if(ImGui.Checkbox(label,ref effective))set(effective);}
    private static void ChooseSource(LayoutPreferences preferences,Action<MappingEntry> selected)
    {
        foreach(var intent in Enum.GetValues<ControlIntent>())if(ImGui.Selectable(intent.ToString()))selected(new(){Intent=intent});
        foreach(var group in preferences.Groups)if(ImGui.Selectable(group.Name))selected(new(){GroupId=group.Id});
    }
    private static void DrawPreview(SetupController setup)
    {
        ImGui.TextWrapped("Offline preview uses all 30 shipped assignment entries.");
        ImGui.TextWrapped("In game, this pane uses Accord's existing PvE/PvP resolver with class selection, inheritance, shared-bar checks and warnings.");
        ImGui.TextDisabled(scope+" · "+(editingPvP?"PvP":"PvE")+" · no game state is read or written.");
    }
}
