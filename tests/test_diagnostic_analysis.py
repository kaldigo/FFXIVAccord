import importlib.util
import json
from pathlib import Path
import unittest
spec = importlib.util.spec_from_file_location('diagnostics', Path(__file__).resolve().parents[1] / 'tools/analyze_diagnostics.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

class DiagnosticAnalysisTests(unittest.TestCase):
    def test_missing_coverage_unreached_feature_and_unconfirmed_send(self):
        events = [('Session.Start', {}), ('World', {'Job':22,'PlayerEntityId':7}),
                  ('State', {'Experimental':{'Drg':True,'Sage':True}}),
                  ('Native.Send', {'Action':10,'SourceSequence':2}),
                  ('Server.Effect', {'Actor':8,'Action':10,'SourceSequence':2}),
                  ('User.Marker', {'Note':'Nothing happened'})]
        lines=[json.dumps(dict(Component='WrathCombo',Kind=k,Data=d,Sequence=i+1,MonotonicMs=i*10000)) for i,(k,d) in enumerate(events)]
        result=module.analyze([('WrathCombo/events-0001.jsonl',lines)])
        self.assertEqual(result['EnabledJobFeaturesNeverReached'], ['Drg'])
        self.assertEqual(len(result['SendsWithoutObservedConfirmation']), 0) # still within the unfinished tail
        lines.append(json.dumps(dict(Component='WrathCombo',Kind='State',Data={},Sequence=7,MonotonicMs=100000)))
        result=module.analyze([('WrathCombo/events-0001.jsonl',lines)])
        self.assertEqual(len(result['SendsWithoutObservedConfirmation']),1) # another actor cannot confirm ours
        self.assertTrue(any('coverage missing' in warning for warning in result['Warnings']))
        self.assertEqual(result['Markers'][0]['Note'],'Nothing happened')

    def test_matching_effect_and_explicit_loss(self):
        events=[('Session.Start',{}),('World',{'PlayerEntityId':7}),('Native.Send',{'Action':10,'SourceSequence':2}),
                ('Server.Effect',{'Actor':7,'Action':10,'SourceSequence':2}),('State',{})]
        lines=[json.dumps(dict(Component='WrathCombo',Kind=k,Data=d,Sequence=i+1,MonotonicMs=i*10000)) for i,(k,d) in enumerate(events)]
        result=module.analyze([('events.jsonl',lines),('summary.json',[json.dumps(dict(Component='WrathCombo',Dropped=3,Error='overflow'))])])
        self.assertEqual(result['SendsWithoutObservedConfirmation'],[])
        self.assertTrue(any('incomplete capture' in warning for warning in result['Warnings']))
