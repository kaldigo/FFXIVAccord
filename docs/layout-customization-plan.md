# Intent layout customization acceptance ledger

Agreed and reviewed 2026-10-28. Checked scope items mean implemented and locally reviewed, not verified in a running game. Native acceptance below remains unchecked. The original implementation was published, but its class-template model did not satisfy the intended Global mapping. The correction and its verification are recorded in [global-mapping-audit.md](global-mapping-audit.md).

## Scope

- [x] Use one editable default intent mapping (superseding the old position exceptions); global → role → class inheritance, explicit Apply per setting; store applied overrides only.
- [x] Whole-mapping override; exact slots and edge-inward areas; first listed item nearest edge.
- [x] Global keep-manual switch forces effective group checks; off uses mapping-entry choices, preserving underlying values.
- [x] Important/other/omitted role actions configurable per role; role editors filter to appropriate actions.
- [x] Reusable personal groups, conditional role/class children replace contents, class precedence; item slots persist at zero quantity; HQ items supported.
- [x] Priority deduplicates direct actions (editor defaults: 30 explicit, 20 important role, 10 other); entry-order ties; selectors exempt; close gaps.
- [x] Action-by-intent exclusions enforced by engine, with native upgrade matching and a final output guard. Missing deliberate access to an excluded action is a layout error. Columns describe available class intent routes, not a promise that every route naturally uses every action.
- [x] Split mechanics provide direct controls for songs, dances and cards under class-mechanics automation. Other jobs retain their reviewed mechanics controls.
- [x] Evaluate visibility per control's automation owner, including actions added through personal groups, role lists or individual placements.
- [x] Required bars 1–3; optional additional non-shared bars; warn/block on managed shared bars; recheck at write time.
- [x] Fully own managed regular bars; leave deselected bars intact; invalid references block affected class.
- [x] Resolve all four automation categories OFF for capacity/conflict validation, after inheritance and deduplication.
- [x] Warnings identify classes/bars, consolidate complete roles, disappear when overrides resolve them; valid classes may apply independently.
- [x] Failed combined updates restore previous engine settings and use the existing slot rollback. Recovery failures report attention required. Loading changes remain pending and retry visibly.
- [x] New layout feature leaves controller placement unchanged; static mode retained.
- [x] Regression fix: remove the combat write block, invalidate/request immediate refresh on toggles, retain loading guards and retries. Native reproduction still pending below.
- [x] Fetch/merge upstream fork changes; preserve default upstream selection when no exclusions/direct input applies.
- [x] Build client + engine, run focused and existing checks, review final diff against every requirement.

## Acceptance examples

Existing controls remain available through one common intent mapping. Historical placement exceptions are normalized. Bard example: bar 1 left ST/AoE/exclusions/Burst; bar 2 left split songs and right roulette; bar 3 left important role actions/cleanse, right movement/protection/support/potions. Auto support/protection on, only desired groups keep manual controls. Rain of Death excluded from selected intent routes. Peloton retained. Global settings should populate other jobs without individual layouts.

## Local evidence

- Paired repository build succeeds: 246,148 assertions, including 512 PvE and 336 PvP combinations with engine-schema verification. Added tests cover inheritance/serialization, edge placement, priority/visibility, Bard's layout, all four automation toggles, item persistence, managed/shared bars, unchanged controllers, simulated combat writes and failure recovery. The final exclusion-table UI change also compiles successfully.
- One shared mapping also passes all 16 toggle combinations for each of the 21 combat jobs, without individual mapping overrides.
- Installed game data resolves all 332 existing PvE assignments and the new split/direct controls. Umbral Draw is a native replacement, so the AST split uses Astral Draw plus Play I/II/III and Minor Arcana.
- Packaging unit tests pass for both repositories (three each). These tests do not publish or install anything.
- Upstream through `d9c63e468` merged cleanly: four commits covering Ninja, BossMod integration and custom-action PvP saving. The merged engine builds and passes paired schema checks.
- Final review found and addressed distinct-input identities for manual controls, preserving those identities after removing mappings/restarting, automated-action ownership in personal groups, pending-change retries, and exact settings restoration instead of dictionary merging during recovery.

## Native acceptance still required

- [ ] Reproduce the original delayed-update case, then toggle all four automation categories in combat and out of combat. Confirm immediate layout changes without death, zone change or reopening settings. Repeat during loading to verify deferred application.
- [ ] Configure the Bard example, enable support/protection, retain only the desired bar-3 manual groups, and exercise the actual buttons. Rain of Death must remain manually usable and never be selected by its excluded damage route.
- [ ] Exercise song, dance and card direct inputs while the corresponding automated selector remains present. Confirm native replacements, targets, cooldown display, keybinds and saved bars work normally.
- [ ] Restart the game/plugin with direct inputs saved; remove a group or deselect an optional bar and restart again. Its retained buttons and restored backups must still work.
- [ ] Check zero-count/HQ potions, roulettes, low-level action availability, shared-bar changes, class overrides resolving warnings, and controller/PvP regressions in the running client.
- [ ] Review the settings UI at the user's normal display scale. No visual/in-game acceptance is inferred from compilation or simulated native calls.

The exclusion guard guarantees a prohibited action is rejected. Native testing must also confirm rotation continuity: an uncommon selection path that skips readiness helpers may fall back to Cease rather than select its next eligible action.

## Using the settings

Open Global mapping to edit the common layout for every combat class. All intents are available, including resurrection, healing and class mechanics; unsupported intents contribute no controls. An exact slot is an area with the same first and last slot. Global role actions and Global exclusions edit the corresponding shared settings.

Mapping overrides is a separate page. Add only the roles/classes that need differences, then edit one record at a time. Apply selects which settings replace their inherited values; Mapping is one complete setting. Preview has the class selector and can display either Global alone or the result including explicit overrides. Preview never creates or edits overrides.

Create reusable personal groups once, optionally give them role/class contents, then select those groups in mappings. The first entry in a right-edge area occupies its highest numbered slot. Higher numeric priority wins duplicate direct actions; automated selector buttons remain separate.

Keep manual buttons globally forces effective retention for all entries. With it off, choose retention inside the mapping per entry. These entry choices are preserved while the global setting is on. Save settings validates the full all-off layout before applying it; preview and warnings identify unresolved classes/areas.

## Superseded UI follow-up (0.2.15.5)

The prior UI introduced editable class templates inside Global and a shared scope selector. Although its regression tests passed, that model was wrong for the agreed design. Those claims are superseded by the common-mapping correction in [global-mapping-audit.md](global-mapping-audit.md). Native visual acceptance remains required.
