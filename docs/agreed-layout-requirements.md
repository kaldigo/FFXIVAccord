# Agreed layout requirements — acceptance ledger

This is the controlling checklist reconstructed from the user's requests. Previous completion claims in the plan/audit do not establish acceptance. Status must distinguish implementation, automated evidence, and native acceptance.

## Defaults and mapping
- Preserve the original actions and bars. Do not use the Bard friend's customization as the default; do not populate bar 3 or add role actions by default.
- User clarification, 2026-10-28: use ONE global rule and normalize conflicting historical orders rather than create automatic exceptions. Healer movement may follow the common movement rule.
- Global is editable configuration, applies to every combat class, and offers every intent (including resurrection/healing/mechanics). Available does not mean placed.
- Normal editing: choose a bar, left/right edge, and ordered intents or reusable groups. Both edges fill inward; first group is nearest its edge. Latest clarification: exact positions are unnecessary for ordinary editing. The editor offers edges; existing custom assignments are preserved for compatibility.
- Close gaps for actions inapplicable to the class, not actions locked by current/synced level. Layouts always represent the max-level toolkit; never hard-code a numeric level cap. Keep zero-count items. Deduplicate direct actions by priority; automation selectors are exempt.
- Whole-mapping replacement only. Global → explicit role → explicit class overrides. Apply per setting; unchecked settings inherit future parent changes. No generated overrides, no reset-to-inherited button.
- Separate Global, override list/editor (one at a time), and read-only Preview. Class selection belongs in Preview, not Global.

## Content/settings
- Global keep-manual forces all mapping group checks on. Otherwise per-group retention applies; those checks are part of Mapping and do not get separate Apply controls.
- Split songs/dances/cards under existing class-mechanics automation, not a new automation toggle.
- Role-specific important/on-bar actions. Additional actions remain opt-in.
- Exclusions: select action, untick allowed intent routes; enforce in the Wrath fork, not just visually. Preserve deliberate manual access.
- Reusable extras groups on their own page, with inherited role/class child contents; mappings refer to groups, not repeated potion/roulette entries.
- Bars 1–3 required; optional unshared bars selectable. Warn if required/managed bars become shared. Managed bars are fully owned; no unmanaged slots.
- Validate resolved layouts with all FOUR automation toggles off. Respect existing overrides; consolidate whole-role warnings and remove resolved warnings.
- Controller layouts excluded from the new editor; retain existing controller/PvP/gatherer paths and static mode.
- Automation changes must refresh immediately when the game permits, retry during loading; no death/scene change required.

## Acceptance gates for this correction
- [x] Defaults: original action/bar comparison for all combat classes; no unsolicited actions or bar-3 assignments.
- [x] UI source: ordinary edge editing needs no first/last slot range, area naming, or fixed-slot avoidance setup; only existing custom assignments expose old coordinates.
- [x] Source/model: full Global intent choice, explicit overrides, reusable groups, retention and Preview remain intact.
- [x] Regression checks: both edges/order, collisions/capacity, fixed positions, migration preserves customized maps, all four automation combinations and split mechanics.
- [x] Paired build and packaging pass.
- [ ] Native visual/interaction acceptance at actual display scale. Automated builds cannot check this box.
- [ ] Native automation refresh and Bard example exercised in game. Simulated tests cannot check this box.

Publishing a test build is not native acceptance. Report remaining gaps explicitly.

## Correction review evidence

The default now uses bar-edge groups on bars 1 and 2, with no automatic role-action or bar-3 assignments. Tests compare its controls against the original resolved setup, including each original control's bar. The approved order normalization remains; no automatic role/class overrides are created.

The editor now offers bar tabs and ordered left/right group lists. The ordinary editor has no slot-range fields, area-name fields, fixed-slot avoidance switches, or direct action/item picker. Extras are created as reusable personal groups. Existing custom assignments are visible for moving their groups onto edges; they are not silently discarded.

The original setup has distinct primary and auxiliary controls. Additional burst, healing protection and primary class mechanics preserve those original bar assignments while remaining explicit Global intents. Existing customized v2 mappings retain their released intent membership, as well as their saved mapping, until the user restores the new default. An untouched shipped v2 default is recognized and repaired automatically; original settings are backed up by the existing migration path.

Known capacity case: AST's optional split-card layout with every automation category off exceeds bar 2. It reports AST/bar 2 instead of silently adding bar-3 controls. The normal unsplit default fits all 21 combat classes. This requires moving a group when opting into split cards, globally or in an explicit override.

Native visual acceptance and the delayed-update reproduction remain open. No claim of a bug-free or fully accepted in-game experience follows from compilation or simulated tests.

Local verification: paired client/engine build passed; final focused run passed 247,336 assertions; all three packaging checks passed. Package vulnerability-feed lookups emitted network warnings. Native acceptance boxes remain unchecked.

Final review also found and fixed a tank knockback/important-role duplicate. The direct Arm’s Length control remains deduplicated even when it occupies the primary protection group. A dedicated regression assertion covers that case.

## Class automation overrides — requested 2026-10-29

Each class override can apply any of the four automation toggles independently, including an explicit off value. Unchecked toggles inherit Global. Role automation overrides are not introduced by this request. Global automation remains editable on its own page.

A server-status click (or the matching automation command) updates the applied toggle override for the current active class; otherwise it updates Global. It never creates an override merely because a class is selected. Effective values must be used by status display, engine configuration, keyboard/controller visibility and the existing shared PvP automation choices. All-off capacity validation remains all-off regardless of class choices.

Unsaved unrelated layout edits must not undo subsequent server-status changes. Persist overrides, preserve explicit false, and retain them when a class is temporarily disabled. Native status interaction and class switching still require in-game verification.

Class automation verification: paired build passed with 247,372 assertions and three packaging checks. Added checks cover all four override values, explicit false serialization, inactive/inheriting click routing, cross-class isolation, dirty-editor rebasing, effective engine flags, controller/PvE visibility, PvP visibility and all-off validation. Server-status clicks and class switching have not been exercised in the running game.

## Max-level layout regression — reported 2026-10-29

Reaper's burst trigger disappeared during a level-synced dungeon because the live eligibility predicate checked the current character level. Preview already bypassed that check. Live and preview placement now share class/catalog eligibility and do not consult current/synced or stored class levels. The game decides whether a placed action is executable. There is no hard-coded level cap. Automation and explicit mapping choices still control visibility as configured.

Native verification remains open: enter a level-synced dungeon on Reaper with manual burst enabled and verify the burst trigger stays on the bar before, during and after the sync.
