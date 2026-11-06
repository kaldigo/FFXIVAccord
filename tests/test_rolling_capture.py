from pathlib import Path
import subprocess
import tempfile
import unittest

class RollingCaptureTests(unittest.TestCase):
    def test_retains_recent_inputs_and_start_after_rotation(self):
        source = (Path(__file__).resolve().parents[1] / "Accord.Plugin/Diagnostics/DiagnosticLog.cs").read_text()
        source = source.replace("ChunkBytes = 4L * 1024 * 1024", "ChunkBytes = 4096").replace("RetainedParts = 8", "RetainedParts = 4")
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp)
            (path/"Test.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><PackageReference Include="Newtonsoft.Json" Version="13.0.3"/></ItemGroup></Project>')
            (path/"DiagnosticLog.cs").write_text(source)
            (path/"DutyLogRetention.cs").write_text((Path(__file__).resolve().parents[1]/"Accord.Plugin/DutyLogRetention.cs").read_text())
            (path/"Program.cs").write_text(r'''using AccordDiagnostics;
using Newtonsoft.Json.Linq;
string old=Guid.NewGuid().ToString("N"), keep=Guid.NewGuid().ToString("N");
foreach(var group in new[]{old,keep}) {var folder=Directory.CreateDirectory(Path.Combine(args[0],group)); File.WriteAllText(Path.Combine(folder.FullName,"managed-duty"),group);}
string export=Path.Combine(args[0],"saved.zip"); File.WriteAllText(export,"keep");
Accord.DutyLogRetention.Trim(args[0],keep);
if(Directory.Exists(Path.Combine(args[0],old)) || !Directory.Exists(Path.Combine(args[0],keep)) || !File.Exists(export)) throw new Exception("Duty retention boundary failed");
string legacy=Guid.NewGuid().ToString("N");
var legacyDir=Directory.CreateDirectory(Path.Combine(args[0],legacy,"WrathCombo"));
File.WriteAllText(Path.Combine(legacyDir.FullName,"summary.json"),new JObject { ["Id"]=legacy,["Component"]="WrathCombo" }.ToString());
var personal=Path.Combine(legacyDir.FullName,"notes.txt");File.WriteAllText(personal,"keep");
Accord.DutyLogRetention.TrimLegacy(args[0]);
if(!File.Exists(personal)) throw new Exception("Legacy cleanup removed a personal file");
File.Delete(personal);Accord.DutyLogRetention.TrimLegacy(args[0]);
if(legacyDir.Exists && Directory.Exists(legacyDir.FullName)) throw new Exception("Closed legacy capture survived retention");
string id=Guid.NewGuid().ToString("N");
DiagnosticLog.Start(args[0], id, "Engine", true);
int payloads=0;
for(int i=0;i<50000;i++) DiagnosticLog.Event("Provider.Evaluate",()=>++payloads);
if(payloads>5) throw new Exception("Background checks still serialized individually");
for(int i=0;i<1500;i++) DiagnosticLog.Event("Native.Send",()=>new { Index=i, Padding=Guid.NewGuid().ToString()+Guid.NewGuid().ToString() });
using(DiagnosticLog.Begin("Native.Press",()=>new {Input=123}))
    DiagnosticLog.Event("Provider.Evaluate",()=>new {InManualPress=true});
DiagnosticLog.Stop("done");
if(!SpinWait.SpinUntil(()=>!JObject.FromObject(DiagnosticLog.Status()).Value<bool>("Flushing"),10000)) throw new Exception("Writer stalled");
var dir=Path.Combine(args[0],id,"Engine");
var parts=Directory.GetFiles(dir,"events-*.jsonl.gz").Order().ToArray();
var lines=parts.SelectMany(f=>{using var stream=File.OpenRead(f);using var gzip=new System.IO.Compression.GZipStream(stream,System.IO.Compression.CompressionMode.Decompress);using var reader=new StreamReader(gzip);return reader.ReadToEnd().Split((char)10,StringSplitOptions.RemoveEmptyEntries);}).Select(JObject.Parse).ToArray();
var summary=JObject.Parse(File.ReadAllText(Path.Combine(dir,"summary.json")));
if(parts.Length!=4 || summary.Value<int>("ExpiredParts")==0 || summary.Value<int>("Dropped")!=0 ||
    summary.Value<int>("SampledOut")<49995 || summary["Counts"]!.Value<int>("Provider.Evaluate")!=50001 ||
    lines.First().Value<string>("Kind")!="Session.Start" || lines.Last().Value<string>("Kind")!="Session.Stop" ||
    !lines.Any(x=>x["Data"] is JObject o && o.Value<bool?>("InManualPress")==true)) throw new Exception("Retention or manual press coverage failed");
''')
            result = subprocess.run(["dotnet","run","--project",str(path/"Test.csproj"),"--",tmp],capture_output=True,text=True,timeout=120)
            self.assertEqual(result.returncode,0,result.stdout+result.stderr)
