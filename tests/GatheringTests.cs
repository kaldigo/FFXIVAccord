using Accord;
using HotbarTools;
using ECommons.DalamudServices;
using ECommons.GameHelpers;

internal static class GatheringTests
{
    internal static int Run()
    {
        int checks=0;
        void Check(bool value,string message) { checks++; if(!value)throw new Exception("Gathering/cleanup: "+message); }
        var root=Path.Combine(Path.GetTempPath(),"accord-gather-tests-"+Guid.NewGuid().ToString("N"));
        var previousRoot=Svc.PluginInterface.Root;
        Svc.PluginInterface.Root=root;
        var pack=new SetupPack();
        var hotbars=new HotbarSetup();
        var catalog=new ActionCatalog(null!,null!);
        var sentinel=new SlotValue(9,12345); // Macros/items/general actions have no cleanup exemption.
        var positions=Enumerable.Range(0,18).SelectMany(b=>Enumerable.Range(0,b<10?12:16).Select(s=>new Position(b,s))).ToArray();
        try
        {
            foreach(var job in pack.Data.Jobs.Where(j=>PveLayoutPlan.IsGatherer(j.JobId)))
            for(int mask=0;mask<16;mask++)
            foreach(int sharing in new[]{0,1,2})
            {
                Player.Job=job.JobId;NativeHotbars.Mode=false;NativeHotbars.Slots.Clear();NativeHotbars.Shared.Clear();
                if(sharing==1)NativeHotbars.Shared.UnionWith([0,3,10,12,17]);
                if(sharing==2)NativeHotbars.Shared.UnionWith(Enumerable.Range(10,8));
                foreach(var p in positions) NativeHotbars.Put(job.JobId,p,sentinel);
                NativeHotbars.Put(job.JobId,new(1,11),new(10,4)); // Sprint used to survive anywhere.
                var settings=new AccordSettings();
                foreach(var id in Enum.GetValues<VariantId>())settings.Variants[id]=(mask&(1<<(int)id))!=0;
                var resolved=pack.Resolve(job,settings);
                Check(resolved.Slots.Length== (job.JobId==18 ? 22 : 14),"toolkit was truncated by a variant");
                bool Protected(Position p)=>NativeHotbars.Shared.Contains(p.Bar);
                var plan=PveLayoutPlan.Create(job.JobId,resolved,Defaults.Maps(),Protected,true);
                Check(plan.Select(e=>e.Regular.Bar).Distinct().Count()<=2,"compact layout spilled into a third regular bar");
                Check(plan.Select(e=>e.Regular).Distinct().Count()==plan.Length,"duplicate regular destination");
                Check(plan.Where(e=>e.Cross.HasValue).Select(e=>e.Cross).Distinct().Count()==plan.Count(e=>e.Cross.HasValue),"duplicate controller destination");
                var expected=new Dictionary<Position,SlotValue>();
                foreach(var e in plan)
                {
                    var value=catalog.ResolveSlot(job.Job,e.Slot!.Action);
                    expected.Add(e.Regular,value);
                    if(e.Cross is {} cross)expected.Add(cross,value);
                }
                var before=positions.ToDictionary(p=>p,p=>NativeHotbars.Read(job.JobId,p));
                var backup=hotbars.Apply([(job.JobId,resolved)],true);
                foreach(var p in positions)
                    Check(NativeHotbars.Read(job.JobId,p)==(Protected(p)?before[p]:expected.GetValueOrDefault(p)),"wrong contents at "+p);
                Check(!hotbars.NeedsRefresh(job.JobId,resolved,true),"layout refresh never settles");
                hotbars.Restore(backup);
                Check(positions.All(p=>NativeHotbars.Read(job.JobId,p)==before[p]),"restore lost an original assignment");
            }
            // All regular bars shared: reject the operation before any writes.
            Player.Job=16;NativeHotbars.Shared.Clear();NativeHotbars.Shared.UnionWith(Enumerable.Range(0,10));
            int writes=NativeHotbars.Writes;bool rejected=false;
            try {hotbars.Apply([(16,pack.Find(16)!)],true);}catch(InvalidOperationException){rejected=true;}
            Check(rejected && NativeHotbars.Writes==writes,"insufficient non-shared bars were not rejected atomically");

            // Combat and PvP also clear every former reservation on non-shared bars.
            Player.Job=19;NativeHotbars.Shared.Clear();NativeHotbars.Shared.UnionWith([4,9,17]);
            foreach(bool pvp in new[]{false,true})
            {
                NativeHotbars.Mode=pvp;NativeHotbars.Slots.Clear();
                foreach(var p in positions)NativeHotbars.Put(19,p,sentinel,pvp);
                NativeHotbars.Put(19,new(0,11),new(10,4),pvp);
                if(pvp)
                {
                    HotbarLayoutStore.ObservePvP(19);
                    var profile=new PvPPack().Find(19)!;
                    var snapshot=Newtonsoft.Json.Linq.JObject.Parse("{Settings:{CustomActionSettings:{}},PresetIds:{}}");
                    var choices=new PvPSettings{ClearOtherActions=true};
                    var pvpHotbars=new PvPHotbars();
                    pvpHotbars.Apply([profile],choices,snapshot);
                    Check(pvpHotbars.Apply([profile],choices,snapshot,true)==null,"PvP cleanup does not settle");
                }
                else hotbars.Apply([(19,pack.Resolve(pack.Find(19)!,new()))],true);
                foreach(var p in positions)
                    Check(NativeHotbars.Shared.Contains(p.Bar) ? NativeHotbars.Read(19,p,pvp:pvp)==sentinel : NativeHotbars.Read(19,p,pvp:pvp)!=sentinel,
                        "former slot reservation or shared bar handled incorrectly: "+p);
                Check(!ModeActions.IsSprint(NativeHotbars.Read(19,new(0,11),pvp:pvp)),"reserved Sprint escaped cleanup");
            }
        }
        finally
        {
            Player.Job=19;NativeHotbars.Mode=false;NativeHotbars.Shared.Clear();NativeHotbars.Slots.Clear();
            Svc.PluginInterface.Root=previousRoot;HotbarLayoutStore.Reset();
            var full=Path.GetFullPath(root);
            if(!full.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("accord-gather-tests-"))throw new Exception("Invalid test cleanup path");
            if(Directory.Exists(full))Directory.Delete(full,true);
        }
        return checks;
    }
}
