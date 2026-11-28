using System;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Accord;

internal static class RotationTestingPanel
{
    private static JObject? report;
    private static string status = "Capture is off by default. Requires the matching Wrath testing branch.";
    private static bool pending;
    private static JObject? clientBaseline;
    private static JToken? defaultProfile;
    private static int page;
    internal static AetherglassUI.Topic[] Rows()
    {
        AetherglassUI.Setting Command(string id,string label,string button)=>new(id,label,()=>{
            if(ImGui.Button(button))Request(id);
        }){DisabledReason=()=>pending?"Waiting for Wrath.":null};
        return [new("Wrath decision trace",
            Command("start","Start 60 second capture","Start") with {Detail="Starting a capture replaces the previous trace."},
            Command("stop","Stop capture","Stop"),Command("read","Refresh capture report","Refresh"),
            new("status","Capture status",()=>ImGui.TextWrapped(status)),
            new("copy","Copy complete capture report",()=>{if(ImGui.Button("Copy report"))ImGui.SetClipboardText(report!.ToString(Formatting.Indented));},Visible:()=>report!=null),
            new AetherglassUI.Setting("events","Captured decisions and action sends",()=>ImGui.TextUnformatted($"{(report?["Entries"] as JArray)?.Count??0} events"),Visible:()=>report!=null){
                ExpandedContent=()=>{
                    ImGui.TextWrapped($"Last refresh: {(report!.Value<bool>("Active")?"capturing":report.Value<string>("StopReason"))}. Dropped: {report["Dropped"]}. Repeated selections omitted: {report["RepeatedSelections"]}.");
                    var entries=report["Entries"] as JArray;
                    if(entries==null)return;
                    int last=Math.Max(0,(entries.Count-1)/32);
                    page=Math.Clamp(page,0,last);
                    if(page>0 && ImGui.SmallButton("Previous"))page--;
                    if(page<last && ImGui.SmallButton("Next"))page++;
                    for(int i=page*32;i<Math.Min(entries.Count,(page+1)*32);i++){
                        var entry=entries[i];
                        ImGui.TextWrapped($"{entry["ElapsedMs"]} ms | {entry["Kind"]} | {entry["Input"]} → {entry["Output"]} | recipient {entry["Recipient"]}");
                    }
                }})];
    }
    private static void Request(string command)
    {
        pending = true;
        _ = Svc.Framework.RunOnTick(() =>
        {
            try
            {
                var response = JObject.Parse(Svc.PluginInterface.GetIpcSubscriber<string, string>("WrathCombo.Testing.Trace.v1").InvokeFunc(command));
                if (response.Value<bool>("Success") != true) throw new InvalidOperationException(response.Value<string>("Error"));
                response["AccordBuild"] = typeof(RotationTestingPanel).Assembly.ManifestModule.ModuleVersionId.ToString();
                if (command == "start")
                {
                    var setup = SetupController.Instance!;
                    clientBaseline = JObject.FromObject(setup.Settings);
                    defaultProfile = SetupController.IsPvP
                        ? JToken.Parse(JsonConvert.SerializeObject(setup.PvP.Pack.Find((uint)Player.Job)))
                        : JToken.Parse(System.Text.Json.JsonSerializer.Serialize(setup.Pack.Find((uint)Player.Job)));
                }
                response["AccordSettingsAtStart"] = clientBaseline?.DeepClone();
                response["DefaultProfileAtStart"] = defaultProfile?.DeepClone();
                response["AccordSettingsAtRead"] = JObject.FromObject(SetupController.Instance!.Settings);
                if(command=="start")page=0;
                report = response;
                status = "Capture refreshed at " + DateTime.Now.ToLongTimeString();
            }
            catch (Exception ex) { status = "Capture unavailable: " + ex.Message; }
            finally { pending = false; }
        });
    }
}
