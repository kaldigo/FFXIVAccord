# Accord V2 layout migration

Baseline: testing commit `72b5a1debbf903a108c026b698fa8b23ec3f5127`.
The migration changes presentation and navigation. Existing settings objects,
scope inheritance, engine preference handlers and setup controllers are retained.

## Access inventory

| Previous destination | New home |
| --- | --- |
| Play controls | Play |
| Automation permissions | Combat / Permissions |
| Enemy targeting | Combat / Targeting |
| Healing and allies | Combat / Healing & allies |
| Pulls and openers | Combat / Openers & potions |
| Reserve actions for manual use | Combat / Manual actions |
| Job improvements | Combat / Job preferences |
| RSR automation rules | Combat / RSR rules |
| Choose managed jobs | Setup / Managed jobs |
| Choose managed bars | Setup / Managed bars |
| Place rotation buttons | Hotbars / Layout |
| Place role actions | Hotbars / Role actions |
| Personal action groups | Hotbars / Personal groups |
| Role and job overrides | Hotbars / Role & job overrides; also linked from Combat |
| PvP setup | Hotbars / PvP setup |
| PvP overrides | Hotbars / PvP overrides |
| Preview resolved hotbars | Hotbars / Preview & apply |
| Latest duty log | Help & recovery / Duty recording |
| Inspect rotation decisions | Help & recovery / Diagnostics |
| Hotbar recovery | Help & recovery / Restore hotbars |

BossMod distances, positionals and movement status move from Targeting to
Setup / Movement integration. Engine selection and Follow AutoDuty remain
operational controls on Play. There are no new header selectors or implicit
job/scope changes.

The original complex editors retain their existing handlers and all sections:
role actions, personal groups, HQ/item picking, exclusions, PvP one/two-button
choice, compact PvP restore, role/job Apply flags and rotation overrides.
The studio retains every mapping area, including legacy ranges and additional
bars. Assignment selection exposes order, moving across bar edges, duplicate
priority, removing an entry and converting a direct action to a personal group.
The selected-class apply operation remains at Setup / Managed bars, including
its existing static-mode visibility. Preview links to that destination.

## Persistence and search

The shared change bar invokes the existing `SaveLayouts(Copy(draft))` path.
Discard reloads the saved layouts. It does not introduce a second transaction
or separate page-local save. Existing live/static application rules remain in
the setup controller. Save failures continue through `setup.Run` and its
visible status, leaving the draft available for retry.

Search indexes the actual Topic/Setting metadata, including legacy page terms.
Disabled reasons, visibility and expanded editor callbacks remain attached to
the original setting. Search and navigation do not invoke mutation callbacks.
Opening an individual result scrolls to its row. Custom-editor results open the
canonical editor rather than attempting to infer fields inside draw callbacks.

## Regression checks

`ui-migration-inventory.json` records all 20 old destinations and normalized
hashes for 14 unchanged settings, controller and planner sources.
`UiNavigationTests` exercises the compiled destination registry and old search
terms. `test_ui_migration.py` audits reachability, original handlers, storage
boundaries, expanded controls and assignment operations. Existing planner,
inheritance, variant, migration and transaction tests run against the pinned
engine source and compiled engine schema.

The native preview links the production navigation, chrome and studio draw
code. Its setup service and resolved-preview backend are explicitly offline
fixtures; its mapping factory contains the shipped 30 default assignments.
These renders verify the layout and density, not live game behavior.
In-game acceptance must still check real font scale, unavailable engines,
existing configurations, scopes, search return, pending edits, imports and
actual hotbar application before a release.

## Shared dependency

The build action pins AetherglassUI 0.2.0 at the published immutable commit
`9cd6071415e471690d5af68883805374ed3b0685`. Local builds can use the matching
sibling source; release builds check out that exact source and package the
resulting DLL. The shared library retains the established theme and includes
standalone navigation, search, layout and draft regression checks.
