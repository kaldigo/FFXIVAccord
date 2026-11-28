"""Access and persistence boundaries for the layout-only migration."""
import hashlib
import json
from pathlib import Path
import re
import unittest

ROOT=Path(__file__).resolve().parents[1]

class UiMigrationTests(unittest.TestCase):
    def test_original_destinations_remain_registered(self):
        inventory=json.loads((ROOT/'docs/ui-migration-inventory.json').read_text())
        registry=(ROOT/'Accord.Plugin/AccordNavigation.cs').read_text()
        self.assertEqual(20,len(inventory['destinations']))
        registered=re.findall(r'new\(TaskPage\.(\w+),Desk\.',registry)
        for page in inventory['destinations']:
            self.assertEqual(1,registered.count(page['id']),page['id'])

    def test_settings_handlers_and_storage_are_unchanged(self):
        inventory=json.loads((ROOT/'docs/ui-migration-inventory.json').read_text())
        for file,expected in inventory['unchangedSources'].items():
            with self.subTest(file=file):
                actual=hashlib.sha256((ROOT/file).read_bytes().replace(b'\r\n',b'\n')).hexdigest()
                self.assertEqual(expected,actual,'Review any scope, setting or controller changes explicitly')
        for file,review in inventory.get('reviewedSources',{}).items():
            self.assertTrue(review['reason'])
            self.assertEqual(review['sha256'],hashlib.sha256((ROOT/file).read_bytes().replace(b'\r\n',b'\n')).hexdigest(),file)

    def test_override_editor_uses_hotbar_sections_and_shared_draft(self):
        editor=(ROOT/'Accord.Plugin/LayoutEditor.Overrides.cs').read_text()
        for operation in ['OverrideSections','OverrideFlag','DrawAssignments(setup, mapping','DrawAssignmentDetails(setup, mapping)',
                          'over.Mapping = v ? Copy(parent.Mapping) : null','over.RoleActions = v ? Copy(parent.RoleActions) : null',
                          'over.Exclusions = v ? Copy(parent.Exclusions) : null','over.Automation.Remove','OverrideRotation(over.Rotation']:
            self.assertIn(operation,editor)
        self.assertNotIn('BeginTabBar',editor)
        self.assertNotIn('ImGui.Checkbox',editor)
        registry=(ROOT/'Accord.Plugin/AccordNavigation.cs').read_text()
        self.assertNotIn('CombatOverrides',registry)
        collections=(ROOT/'Accord.Plugin/LayoutEditor.Collections.cs').read_text()
        self.assertIn('OverrideSections(setup, draft.PvP.Global',collections)
        self.assertIn('group.Classes.Remove',collections)
        self.assertIn('group.Roles.Remove',collections)

    def test_all_old_editors_have_an_entry_point(self):
        window=(ROOT/'Accord.Plugin/AccordWindow.cs').read_text()
        for page in ['RoleActions','Groups','Overrides','PvPOverrides','Preview','Jobs']:
            self.assertIn('case TaskPage.'+page+':',window)
        for topic in ['Permissions','Enemies','Healing','Opener','JobTuning','Managed','Trace','Recovery','DutyLog','Rsr','Reserved','PvP']:
            self.assertIn('TaskPage.'+topic+'=>',window)
        self.assertIn('Plugin.Instance.MigrationPending',window)
        self.assertIn('Plugin.Instance.DrawMigration',window)

    def test_studio_keeps_assignment_operations_and_existing_save(self):
        studio=(ROOT/'Accord.Plugin/LayoutEditor.Studio.cs').read_text()
        for operation in ['Restore default intent mapping','ChooseSource(','OrderedEditor.ReorderButtons',
                          'Move group','Duplicate priority','Save as personal group','Remove group','Remove assignment area']:
            self.assertIn(operation,studio)
        self.assertIn('setup.SaveLayouts(Copy(draft))',studio)
        self.assertIn('draft=Copy(setup.Settings.Layouts)',studio)
        self.assertIn('DrawPreview(setup)',studio)

    def test_global_search_includes_real_setting_metadata(self):
        shell=(ROOT/'Accord.Plugin/AccordShell.Navigation.cs').read_text()
        self.assertIn('foreach(var row in section.Rows)',shell)
        self.assertIn('row.Keywords',shell)
        self.assertIn('row.DisabledReason',shell)
        self.assertIn('row.ExpandedContent()',shell)
        self.assertIn('RevealSetting',shell)

if __name__=='__main__': unittest.main()
