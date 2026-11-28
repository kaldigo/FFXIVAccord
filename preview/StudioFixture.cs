using System;
using System.Linq;
using System.Text.RegularExpressions;
using AetherglassUI;
using Newtonsoft.Json;

namespace Accord;

// Offline services only. The studio draw code itself is linked from Accord.Plugin.
public enum VariantId { Burst, Protection, Support, ClassMechanics }
internal static class PvPConfiguredLayout
{
    internal static IntentMapping DefaultMapping()=>new();
    internal static IntentMapping PreviousDefaultMapping()=>new();
}
internal sealed class AccordSettings { internal LayoutPreferences Layouts=new(){Global=new(){Mapping=DefaultAccordData.CreateDefaults()}}; }
internal sealed class SetupController
{
    internal AccordSettings Settings=new();
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
