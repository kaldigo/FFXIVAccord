"""Exercise production movement reconciliation and override helpers with traced IPCs."""
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class MovementTraceTests(unittest.TestCase):
    def test_deduplication_autoduty_handoff_and_write_evidence(self):
        source = (ROOT / 'Accord.Plugin/BossModMovement.cs').read_text(encoding='utf-8-sig')
        start = source.index('            string? preset = role ==')
        reconcile = source[start:source.index('            Status = (role, autoDuty', start)]
        fields = source[source.index('    private readonly FieldOverride minimum'):source.index('    private long nextUpdate;')]
        helpers = source[source.index('    private sealed class FieldOverride('):source.rfind('}')]
        start = source.index('    private string? GetPreset()')
        get_preset = source[start:source.index('    public void Dispose()', start)]
        code = r'''
using System; using System.Linq; using System.Collections.Generic; using System.Globalization; using System.Text.Json;
enum JobRole { Noncombat,Melee,Ranged }
enum ConditionFlag { InCombat }
static class Svc { internal static Conditions Condition=new(); }
class Conditions { public bool this[ConditionFlag f]=>true; }
class Actor { internal ulong TargetObjectId=42; }
class Preferences { internal bool UseRoleDistances=true,FollowWrathPositionals=true; internal Distance? Distances(JobRole role)=>UseRoleDistances?new(0,2.6f):null; }
record Distance(float Minimum,float Maximum);
class NameGate { internal bool HasFunction=true;internal string? Name;internal string? InvokeFunc()=>Name; }
class PosGate { internal bool HasFunction=true;internal byte Direction=1;internal int Calls;internal byte InvokeFunc(ulong id){Calls++;return Direction;} }
class ReadGate { internal Func<List<string>,bool,List<string>> InvokeFunc=null!; }
class SetGate { internal Func<List<string>,List<string>> InvokeFunc=null!; }
class AddGate { internal Func<string,string,string,string,bool> InvokeFunc=null!; }
class ClearGate { internal Func<string,string,string,bool> InvokeFunc=null!; }
namespace AccordDiagnostics {
 static class DiagnosticLog { internal static void Event(string kind,Func<object?> data){} }
 static class IntegrationTrace {
  internal static List<(string Name,bool Write,object? Input,object? Result)> Calls=new();
  internal static T Call<T>(string endpoint,Func<T> invoke,bool write=false,object? input=null){T result=invoke();Calls.Add((endpoint,write,input,result));return result;}
 }
}
class BossModMovement {
 internal readonly Dictionary<string,string> Values=new(){["AIConfig.MinDistance"]="1",["AIConfig.MaxDistanceToTarget"]="20",["AutorotationConfig.FollowRSRDesiredPositional"]="True"};
 readonly ReadGate read=new();readonly SetGate setTransient=new();readonly AddGate setStrategy=new();readonly ClearGate clearStrategy=new();
 internal readonly NameGate activePreset=new(),aiPreset=new();internal readonly PosGate wrathPositional=new();
 readonly Preferences preferences=new();
 (string? Preset,JobRole Role,bool Rsr,bool AutoDuty,bool Distances,bool Positionals)? lastContext;
FIELDS
 internal BossModMovement(){
  read.InvokeFunc=(a,save)=>[Values[a[0]+"."+a[1]]];
  setTransient.InvokeFunc=a=>{Values[a[0]+"."+a[1]]=a[2];return [];};
  setStrategy.InvokeFunc=(name,module,track,value)=>true;clearStrategy.InvokeFunc=(name,module,track)=>true;
 }
GETPRESET
 internal void Tick(string? name,bool usesRsrPositionals=false){activePreset.Name=name;var role=JobRole.Melee;var player=new Actor();
RECONCILE
 }
HELPERS
}
class Program {
 static void Check(bool b,string why){if(!b)throw new Exception(why);}
 static int Writes()=>AccordDiagnostics.IntegrationTrace.Calls.Count(c=>c.Write);
 static void Main(){
  var movement=new BossModMovement();movement.Tick(null);
  Check(Writes()==2,"Role distance writes changed");int count=Writes();movement.Tick(null);Check(Writes()==count,"Unchanged field values wrote repeatedly");
  movement.Tick("MyPreset");Check(movement.Values["AIConfig.MinDistance"]=="1" && movement.Values["AIConfig.MaxDistanceToTarget"]=="20","Prior user distances not restored");
  Check(movement.Values["AutorotationConfig.FollowRSRDesiredPositional"]=="False","Wrath positional ownership not applied");
  count=Writes();movement.Tick("MyPreset");Check(Writes()==count,"Unchanged preset values wrote repeatedly");
  movement.wrathPositional.Direction=2;movement.Tick("MyPreset");
  var last=AccordDiagnostics.IntegrationTrace.Calls.Last(c=>c.Write);var detail=JsonSerializer.SerializeToElement(last.Input);
  Check(detail.GetProperty("Previous").GetString()=="Rear" && detail.GetProperty("Requested").GetString()=="Flank" && detail.GetProperty("Preset").GetString()=="MyPreset","Movement write evidence lost");
  movement.Tick("AutoDuty");Check(movement.Values["AutorotationConfig.FollowRSRDesiredPositional"]=="True","Positional field not restored at handoff");
  count=Writes();int predictions=movement.wrathPositional.Calls;
  movement.Tick("AutoDuty");movement.Tick("AutoDuty Passive");
  Check(Writes()==count && movement.wrathPositional.Calls==predictions,"AutoDuty preset ownership was changed or positional prediction requested");
  movement.Tick("MyPreset",true);
  Check(movement.Values["AutorotationConfig.FollowRSRDesiredPositional"]=="True","RSR positional ownership changed");
  Check(AccordDiagnostics.IntegrationTrace.Calls.Any(c=>c.Name.StartsWith("BossMod.Configuration.SetTransient/") && c.Write),"Field write timing absent");
  Console.WriteLine("Movement trace boundaries passed");
 }
}
'''.replace('FIELDS', fields).replace('GETPRESET', get_preset).replace('RECONCILE', reconcile).replace('HELPERS', helpers)
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory)
            (p / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup></Project>', encoding='utf-8')
            (p / 'Program.cs').write_text(code, encoding='utf-8')
            result = subprocess.run(['dotnet', 'run', '--project', str(p / 'Test.csproj'), '-p:UseSharedCompilation=false'], capture_output=True, text=True, timeout=120)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('Movement trace boundaries passed', result.stdout)
