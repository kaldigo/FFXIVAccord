"""Run the actual capture writer, including concurrent producers and I/O failure."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
class DiagnosticWriterTests(unittest.TestCase):
    def test_durable_ordered_capture_failure_and_restart(self):
        source=(ROOT/'Accord.Plugin/Diagnostics/DiagnosticLog.cs').read_text()
        harness=r"""
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using AccordDiagnostics;
class Program {
    static void Check(bool ok, string why) { if(!ok) throw new Exception(why); }
    static void Drain() { Check(SpinWait.SpinUntil(()=>!JObject.FromObject(DiagnosticLog.Status()).Value<bool>("Flushing"),10000),"Writer did not drain"); }
    static void Main(string[] args) {
        string root=args[0], id=Guid.NewGuid().ToString("N");
        int calls=0;
        DiagnosticLog.Event("Disabled",()=>{ calls++; return null; }); Check(calls==0,"Disabled capture evaluated data");
        try { DiagnosticLog.Start(root,"../escape","Test"); throw new Exception("Accepted invalid session"); } catch(ArgumentException) {}
        DiagnosticLog.Start(root,id,"Test");
        Parallel.For(0,1000,i=>DiagnosticLog.Event("Parallel",()=>new { Number=i }));
        using(DiagnosticLog.Begin("Press",()=>new { Action=1 })) DiagnosticLog.Event("Result",()=>new { Accepted=true });
        DiagnosticLog.Stop("Test complete"); Drain();
        string directory=Path.Combine(root,id,"Test");
        var summary=JObject.Parse(File.ReadAllText(Path.Combine(directory,"summary.json")));
        var records=Directory.GetFiles(directory,"events-*.jsonl").Order().SelectMany(File.ReadLines).Select(JObject.Parse).ToArray();
        Check(records.Length==summary.Value<long>("Written"),"Written count mismatch");
        Check(records.Zip(records.Skip(1),(a,b)=>a.Value<long>("Sequence")<b.Value<long>("Sequence")).All(x=>x),"Writer reordered events");
        Check(summary.Value<long>("Sequence")==summary.Value<long>("Written")+summary.Value<long>("Dropped"),"Missing events not accounted for");
        Check(records.Single(x=>x.Value<string>("Kind")=="Press").Value<long>("Scope")==records.Single(x=>x.Value<string>("Kind")=="Result").Value<long>("Scope"),"Lost correlation scope");
        string failed=Guid.NewGuid().ToString("N"), failureDirectory=Path.Combine(root,failed,"Test");
        Directory.CreateDirectory(failureDirectory); File.WriteAllText(Path.Combine(failureDirectory,"events-0001.jsonl"),"existing");
        DiagnosticLog.Start(root,failed,"Test");
        Check(SpinWait.SpinUntil(()=>!DiagnosticLog.Active,10000),"Writer failure did not stop capture");
        DiagnosticLog.Stop("After I/O failure"); Drain();
        Check(JObject.FromObject(DiagnosticLog.Status()).Value<string>("Error")!.Contains("Writer:"),"Writer error not visible");
        Check(File.ReadAllText(Path.Combine(failureDirectory,"events-0001.jsonl"))=="existing","Existing log overwritten");
        DiagnosticLog.Start(root,Guid.NewGuid().ToString("N"),"Test");
        DiagnosticLog.Event("Throws",()=>throw new InvalidOperationException("bad snapshot"));
        Check(DiagnosticLog.Active,"Snapshot failure changed gameplay/capture lifetime");
        DiagnosticLog.Stop("Done"); Drain();
        Check(JObject.FromObject(DiagnosticLog.Status()).Value<long>("Dropped")==1,"Serialization failure not accounted for");
    }
}
"""
        with tempfile.TemporaryDirectory() as temporary:
            path=Path(temporary)
            (path/'Writer.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="Newtonsoft.Json" Version="13.0.3" /></ItemGroup></Project>')
            (path/'DiagnosticLog.cs').write_text(source)
            (path/'Program.cs').write_text(harness)
            result=subprocess.run(['dotnet','run','--project',str(path/'Writer.csproj'),'-c','Release','--',str(path/'logs')],capture_output=True,text=True,timeout=120)
            self.assertEqual(result.returncode,0,result.stdout+result.stderr)
