using Accord;
using HotbarTools;
using Newtonsoft.Json.Linq;
using WrathFork.Contract;

internal static class PvPTests
{
    internal static int Run(JObject settings,JObject names,HashSet<int> ids,Action<JObject,JObject> validate)
    {
        int count=0;
        void Check(bool value,string message){count++;if(!value)throw new Exception("PvP: "+message);}
        var pack=new PvPPack();
        var bardDefault = PvPConfiguredLayout.Plan(pack.Find(23)!, new PvPSettings
            { Variants = Enum.GetValues<VariantId>().ToDictionary(v => v, _ => true) },
            new JObject { ["Settings"] = new JObject { ["CustomIntValuesV6"] = new JObject { ["EmergencyHealThreshold"] = 50 } } },
            _ => false, _ => true);
        Check(bardDefault.Valid, "Bard corrected default conflicts");
        Check(bardDefault.Controls.Where(c => c.Position.Bar == 0).OrderBy(c => c.Position.Slot)
            .Take(4).Select(c => c.Action.Id).SequenceEqual(new uint[] { 1000015, 29401, 29055, 29711 }),
            "Bard default must begin full rotation, LB, Elixir, Recuperate with automation on");
        Check(bardDefault.Controls.Single(c => c.Position.Bar == 1 && c.Position.Slot == 0).Action.Id == 43259,
            "Bard role action must start bar 2");
        Check(new AccordSettings().PvP.ActiveJobs.Count==0,"new selection is not empty");
        var allMaps=Defaults.Maps().Where(m=>m.Profile!="Noncombat").Concat(pack.Jobs.SelectMany(PvPLayout.Extras)).ToList();
        Defaults.Validate(allMaps);
        var baseMaps=Defaults.Maps().Where(m=>m.Profile!="Noncombat").ToList();
        foreach(var job in pack.Jobs)
        {
            Check(PvPLayout.AvailableExtras(job,baseMaps,p=>p.Bar>=11).Length==0,
                job.Job+" allocated overflow on a shared controller page");
            var available=PvPLayout.AvailableExtras(job,baseMaps,p=>p.Bar>=10 && p.Bar!=12);
            Check(available.Length==Math.Max(0,job.Controls.Length-12),job.Job+" lost overflow with a free page");
            Check(available.All(m=>m.Cross.Bar==12),job.Job+" failed to skip shared cross page 2");
            Defaults.Validate(baseMaps.Concat(available));
            var existing=baseMaps.Concat(available).ToList();
            Check(PvPLayout.AvailableExtras(job,existing,_=>false).Select(m=>m.Cross).SequenceEqual(available.Select(m=>m.Cross)),
                job.Job+" moved existing controller pairs when another page became available");
            Check(PvPLayout.AvailableExtras(job,existing,_=>true).Select(m=>m.Cross).SequenceEqual(available.Select(m=>m.Cross)),
                job.Job+" rewrote existing pairs after sharing changed");
        }
        var personal=(JObject)settings.DeepClone();
        personal["CustomIntValuesV6"]!["DNCWaltzThreshold"]=57;
        personal["CustomIntValuesV6"]!["MCHPvP_FMFOption"]=2;
        personal["CustomBoolValuesV6"]!["RDMPvP_Forte_SubOption"]=false;
        personal["CustomBoolValuesV6"]!["BLMPvP_Burst_SubOption"]=false;
        personal["CustomIntValuesV6"]!["ASTPvP_Burst_PlayCardOption"]=3;
        personal["AutoActions"]=new JObject{[names.Value<int>("DNCPvP_BurstMode").ToString()]=true};
        var snapshot=new JObject{["Settings"]=personal,["PresetIds"]=names};
        foreach(var job in pack.Jobs)
        for(int mask=0;mask<16;mask++)
        {
            var choices=new PvPSettings();
            foreach(var id in Enum.GetValues<VariantId>())choices.Variants[id]=(mask&(1<<(int)id))!=0;
            var patch=pack.Patch([job],choices,snapshot);
            var updated=SettingsPatch.Apply(personal,patch,ids);
            validate(personal,updated);
            Check(JToken.DeepEquals(personal["AutoActions"],updated["AutoActions"]),job.Job+" changes hands-free AutoActions");
            Check(JToken.DeepEquals(personal["CustomActionSettings"],updated["CustomActionSettings"]),"global custom buttons changed");
            Check(updated["CustomIntValuesV6"]!.Value<int>("DNCWaltzThreshold")==57,"personal HP threshold changed");
            Check(updated["CustomIntValuesV6"]!.Value<int>("MCHPvP_FMFOption")==2,"personal timing changed");
            Check(!updated["CustomBoolValuesV6"]!.Value<bool>("RDMPvP_Forte_SubOption"),"saved false Vice of Thorns was overwritten");
            Check(!updated["CustomBoolValuesV6"]!.Value<bool>("BLMPvP_Burst_SubOption"),"saved false defensive Burst was overwritten");
            Check(updated["CustomIntValuesV6"]!.Value<int>("ASTPvP_Burst_PlayCardOption")==3,"saved card policy was overwritten");
            Check(updated["ForkFeatures"]![job.JobId.ToString()]!.Value<bool>("PvPCommonHealing")==choices.Enabled(VariantId.Support),"common healing owner incorrect");
            Check(updated["ForkFeatures"]![job.JobId.ToString()]!.Value<bool>("PvPCommonGuard")==choices.Enabled(VariantId.Protection),"common Guard owner incorrect");
            Check(updated["ForkFeatures"]![job.JobId.ToString()]!.Value<bool>("PvPCommonPurify")==choices.Enabled(VariantId.Protection),"common Purify owner incorrect");
            Check(patch["Enable"]!.Values<int>().All(id=>names.Properties().Single(p=>p.Value.Value<int>()==id).Name.Contains("PvP")),"non-PvP preset touched");
            foreach(var name in job.Always)Check(updated["EnabledActionsV6"]!.Values<int>().Contains(names.Value<int>(name)),job.Job+" missing continuation "+name);
            foreach(var (owner,presets) in job.Variants)
                foreach(var preset in presets)
                    Check(updated["EnabledActionsV6"]!.Values<int>().Contains(names.Value<int>(preset))==choices.Enabled(owner),job.Job+" incorrect delegation for "+preset);
            foreach(var name in job.Manual)Check(!updated["EnabledActionsV6"]!.Values<int>().Contains(names.Value<int>(name)),job.Job+" delegates manual intent "+name);
            foreach(var (feature,variant) in job.Features)
            {
                Check(FeatureScope.Supports(job.JobId,Enum.Parse<Feature>(feature)),job.Job+" wrong feature scope");
                Check(updated["ForkFeatures"]![job.JobId.ToString()]!.Value<bool>(feature)==choices.Enabled(variant),feature+" does not follow its owner");
            }
            var after=new JObject{["Settings"]=updated,["PresetIds"]=names};
            foreach(var mode in Enum.GetValues<PvPRotationButtons>())
            {
                choices.Overrides.RotationButtons=mode;
                foreach(bool allOff in new[]{false,true})
                {
                    var plan=PvPConfiguredLayout.Plan(job,choices,after,_=>false,_=>true,allOff);
                    Check(plan.Valid,job.Job+" configured PvP layout conflict: "+string.Join(";",plan.Issues.Select(i=>i.Message)));
                    Check(plan.Controls.Any(c=>c.Action.Id==(mode==PvPRotationButtons.One?1000015u:1000000u)),job.Job+" lost rotation control");
                    Check(plan.Controls.Any(c=>c.Action.Id==1000001)==(mode==PvPRotationButtons.Two),job.Job+" wrong rotation button count");
                    Check(plan.Controls.All(c=>c.Position.Bar<2),job.Job+" unexpectedly added default bar-3 actions");
                    Check(plan.Controls.Select(c=>c.Position).Distinct().Count()==plan.Controls.Count,job.Job+" duplicate configured slot");
                }
            }
            choices.Overrides.RotationButtons=PvPRotationButtons.Two;
            var values=job.Controls.Select(c=>PvPPack.Resolve(c,choices,after)).ToArray();
            // Reproduce a keyboard user whose overflow controller pages are all shared.
            var keyboardPairs=PvPLayout.Pairs(job,baseMaps);
            var keyboardRoutes=PvPLayout.IntentRoutes(job,keyboardPairs,values,p=>p.Bar==0 && p.Slot>=8);
            var registered=keyboardRoutes.Where(r=>baseMaps.Any(m=>m.Id==r.Key)).ToDictionary(r=>r.Key,r=>r.Value);
            LayoutRoutes.Effective(baseMaps.Where(m=>m.Applies(job.JobId)),registered);
            Check(keyboardPairs.Length==values.Length && keyboardRoutes.Values.Distinct().Count()==values.Length,
                job.Job+" lost keyboard controls when controller overflow was unavailable");
            foreach(var (pair,index) in keyboardPairs.Select((p,i)=>(p,i)))
                Check(keyboardRoutes[pair.Id].Bar==job.Controls[index].KeyboardBar,job.Job+" keyboard-only overflow crossed bars");
            foreach(bool sharedSprint in new[]{false,true})
            {
                var layoutValues=values.ToArray();
                if(sharedSprint)
                    for(int i=0;i<layoutValues.Length;i++)if(job.Controls[i].Binding.Id==29057)layoutValues[i]=default;
                foreach(bool protectedGap in new[]{false,true})
                {
                    bool Protected(Position p)=>p.Bar==0 && p.Slot>=8 || protectedGap && p==new Position(1,11);
                    int countOnSecond=layoutValues.Where((v,i)=>v!=default && job.Controls[i].KeyboardBar==1).Count();
                    if(protectedGap && countOnSecond>11)continue; // Explicit capacity failure is checked separately.
                    var compact=PvPLayout.IntentRoutes(job,keyboardPairs,layoutValues,Protected);
                    foreach(int bar in new[]{0,1})
                    {
                        var active=keyboardPairs.Select((m,i)=>(m,i)).Where(x=>layoutValues[x.i]!=default && job.Controls[x.i].KeyboardBar==bar).ToArray();
                        var actual=active.Select(x=>compact[x.m.Id]).OrderBy(p=>p.Slot).ToArray();
                        var expected=Enumerable.Range(0,bar==0?8:12).Select(slot=>new Position(bar,slot)).Where(p=>!Protected(p)).Take(active.Length).ToArray();
                        Check(actual.SequenceEqual(expected),job.Job+" has an internal keyboard gap or crossed bars");
                        Check(active.All(x=>compact.ContainsKey(x.m.Id)),job.Job+" lost access when Sprint was shared");
                    }
                }
            }
            Check(values[0]!=default,job.Job+" lost primary selector");
            foreach(var (control,index) in job.Controls.Select((c,i)=>(c,i)))
                if(control.Binding!=default && (control.Variant==null || !choices.Enabled(control.Variant.Value)))Check(values[index]!=default,job.Job+" lost "+control.Name);
            var maps=job.Controls.Select((_,i)=>allMaps.Single(m=>m.Applies(job.JobId) && m.Regular==PvPLayout.Positions[i])).ToArray();
            var routes=PvPLayout.IntentRoutes(job,maps,values,p=>p.Bar==0 && p.Slot>=8);
            var effective=LayoutRoutes.Effective(allMaps.Where(m=>m.Applies(job.JobId)),routes);
            Check(routes.Values.Distinct().Count()==maps.Length,job.Job+" overlapping destinations");
            foreach(var (map,i) in maps.Select((m,i)=>(m,i)))
            {
                Check(routes[map.Id].Bar==job.Controls[i].KeyboardBar,"compaction crossed bars");
                Check(effective.Single(m=>m.Id==map.Id).Cross==map.Cross,"controller moved");
                if(job.Controls[i].Binding.Id is 1_000_000 or 1_000_001)Check(routes[map.Id]==new Position(0,(int)(job.Controls[i].Binding.Id-1_000_000)),job.Job+" moved primary control");
            }
            foreach(var common in new uint[]{29055,29711,29056,29054,29057,43259})Check(values.Contains(new(1,common)),job.Job+" lost common/role "+common);
            var custom=(JObject)updated.DeepClone();custom["CustomActionSettings"]!["SingleTargetDPS"]=true;
            Check(PvPPack.Resolve(job.Controls[0],choices,new JObject{["Settings"]=custom,["PresetIds"]=names})==new SlotValue(1,1_000_000),job.Job+" ignored custom damage button");
        }
        var all=Enum.GetValues<VariantId>().ToDictionary(v=>v,v=>true);
        var brd=pack.Find(23)!;
        var bardChoices=new PvPSettings { Variants=all };
        var bardPatch=pack.Patch([brd],bardChoices,snapshot);
        Check(bardPatch["Enable"]!.Values<int>().Contains(names.Value<int>("BRDPvP_SilentNocturne")),"all-on Bard still disables Nocturne");
        var bardAfter=new JObject{["Settings"]=SettingsPatch.Apply(personal,bardPatch,ids),["PresetIds"]=names};
        Check(!PvPConfiguredLayout.Plan(brd,bardChoices,bardAfter,_=>false,_=>true).Controls.Any(c=>c.Action.Id==29395),"automated manual Nocturne was not optional");
        bardChoices.Overrides.Global.Mapping!.Areas[0].Entries.Single(e=>e.Intent==ControlIntent.Other).KeepManual=true;
        Check(PvPConfiguredLayout.Plan(brd,bardChoices,bardAfter,_=>false,_=>true).Controls.Any(c=>c.Action.Id==29395),"group retention failed");
        Check(JToken.DeepEquals(bardPatch,pack.Patch([brd],bardChoices,snapshot)),"retaining a manual button changed automation");
        bardChoices.Overrides.Global.Exclusions=[new(){Action=new(){Id=29395,Name="Silent Nocturne"},ExcludedFrom=[ControlIntent.SingleTarget]}];
        var bardRoutes=PvPConfiguredLayout.Routes(brd,bardChoices);
        Check(bardRoutes["1000000"]!["Excluded"]!.Values<uint>().Contains(29395u) && bardRoutes["1000001"] is null,"exclusion scope crossed into AoE");
        Check(bardRoutes[WrathCombo.Engine.PvPAreaChoice.RouteInput(1000015,false).ToString()]!["Excluded"]!.Values<uint>().Contains(29395u),"one-button ST route bypasses exclusions");
        Check(bardRoutes[WrathCombo.Engine.PvPAreaChoice.RouteInput(1000015,true).ToString()] is null,"one-button area path inherited ST-only exclusions");
        var excludedState=SettingsPatch.Apply(personal,pack.Patch([brd],bardChoices,snapshot),ids);
        bardChoices.Overrides.Global.Exclusions=[];
        var clearedState=SettingsPatch.Apply(excludedState,pack.Patch([brd],bardChoices,
            new JObject{["Settings"]=excludedState,["PresetIds"]=names}),ids);
        Check(((JObject)clearedState["PvPActionRoutes"]!["23"]!).Count==0,"deleted PvP exclusion survived recursive merge");
        Check(JToken.DeepEquals(personal["ActionRoutes"],clearedState["ActionRoutes"]),"PvP exclusions changed PvE routes");
        var dnc=pack.Find(38)!;
        var zero=(JObject)personal.DeepClone();zero["CustomIntValuesV6"]!["DNCWaltzThreshold"]=0;
        var safe=new JObject{["Settings"]=zero,["PresetIds"]=names};
        Check(PvPPack.Resolve(dnc.Controls.Single(c=>c.Name=="Curing Waltz"),new PvPSettings{Variants=all},safe)!=default,"zero threshold lost manual Waltz");
        var blm=pack.Find(25)!;
        foreach(var (binding,key) in new[]{(1_000_010u,"BLMPvP_Xenoglossy_TargetHP"),(1_000_011u,"BLMPvP_ElementalWeave_PlayerHP")})
        {
            var blocked=(JObject)personal.DeepClone();blocked["CustomIntValuesV6"]![key]=0;
            Check(PvPPack.Resolve(blm.Controls.Single(c=>c.Binding.Id==binding),new PvPSettings{Variants=all},
                new JObject{["Settings"]=blocked,["PresetIds"]=names})!=default,"BLM lost manual access with disabled "+key);
        }
        Check(pack.Find(39)!.Controls.Single(c=>c.Binding.Id==1_000_014).Variant==null,"lost deliberate Fate Sealed release");
        return count;
    }
}
