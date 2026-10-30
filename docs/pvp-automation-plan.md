# PvP automation and override plan

Written 2026-10-29 before implementation. **Status: local implementation and automated verification completed; published as Accord 0.2.15.11 with Wrath Combo 1.0.4.35; native UI/combat acceptance remains pending.** Source decisions and limits are recorded in [rotation review](pvp-rotation-review.md).

This plan implements the user's direction following the Bard comparison. It supersedes previous PvP claims that Accord already provided maximum automation and the earlier requirement to always show separate ST/AoE buttons in PvP. It does not change the PvE two-button rule or authorize copying the other player's personal configuration.

## 1. Acceptance contract

1. With all four automation categories on, the rotation offers the full compatible rotation Wrath supports for that job. Accord must not silently disable supported actions just because they also move, silence, stun, defend or spend a limit break.
2. Full automation means selection through the player's rotation presses. It does not enable hands-free auto-rotation, bypass readiness/level/target checks, or press abilities without player input.
3. Preserve Wrath's sequencing, combo continuation, resource/charge reserves, HP thresholds, targeting conditions and compatible alternative choices. "All on" does not mean enable conflicting presets simultaneously or overwrite every detailed option.
4. Manual buttons remain independently available when requested. Keeping a manual button must not disable automatic selection. Explicit exclusions are the mechanism for demanding manual-only use.
5. Auto burst, protection, support and class mechanics still delegate identifiable behavior when individually enabled. Routine damage and already-started combo/phase follow-through must not be accidentally stranded when initiation is manual. Define ownership per action in the audit; do not infer it solely from an action's name or its utility side effect.
6. Actions Wrath genuinely cannot automate remain available manually. Every such limitation needs source evidence; it is not sufficient that Accord currently labels it Manual.
7. User-provided settings are evidence for comparison only. Do not install, publish or use the personal file as a universal preset.

## 2. Initial evidence, not a completed behavior audit

Bard's old file enabled every one of the eight current Bard PvP preset IDs. Accord forces Silent Nocturne off, gates Harmonic Arrow/Eagle Eye Shot on Burst and Apex Arrow on Class mechanics, and retains Blast Arrow/Encore continuation. With Protection on, self-cleanse is retained; Support adds ally cleanse. Her charge/HP preferences are examples of personal policies to preserve, not new defaults for everyone; their values are not copied into this public implementation plan.

The following inventory comes from `pvp-presets.json`, `CustomComboPreset.cs`, and the fork's PvP routing. **Every row still requires branch-level review and behavioral checks.** "None" means no fixed Manual list; it does not establish parity with Wrath.

| Job | Current forced-manual automation to review | Additional required review |
| --- | --- | --- |
| PLD | Intervene (including melee option), Shield Smite | Confiteor/Phalanx follow-through; Imperator, Sheltron, Holy Spirit and Rampart ownership |
| WAR | Blota | Bloodwhetting and Primal Rend initiation flags; Onslaught/Orogeny; Inner Chaos/Primal Scream continuation |
| DRK | Plunge, melee Plunge option | Shadowbringer, TBN, Salted Earth, Impalement, Rampart; Salt and Darkness/Scorn continuation |
| GNB | None | Gnashing initiation versus continuation; standalone Gnashing preset compatibility; Rough Divide, Fated Circle, Blasting Zone and Corundum |
| WHM | Afflatus Purgation, Miracle of Nature, Seraph-strike replacement option | Distinguish the two Seraph Strike presets rather than enabling conflicts; damage healing, Cure III/Aquaveil, targeting and LB follow-up |
| SCH | Deployment Tactics | Damage/Adlo/Expedient interaction, retargeting, Biolysis and Chain Stratagem; do not lose supported spreading behavior |
| AST | Epicycle | Draw/play and offensive/healing Double Cast; Gravity and Macrocosmos initiation; current forced Double Cast booleans and card-choice integer |
| SGE | Kardia retarget preset | Kardia recipient/reminder behavior, Eukrasia/Toxikon continuation, Pneuma support and Phlegma |
| MNK | Wind's Reply, Thunderclap, Meteodrive | Rising Phoenix/Fire's Reply sequence and Riddle of Earth; movement/CC/LB must not be blanket exclusions |
| DRG | None | Geirskogul/Nastrond, High Jump, Wyrmwind, Horrid Roar; Chaotic Spring execute versus sustain and Burst Protection options currently outside the profile |
| NIN | ST and AoE Seiton Tenchu | Separate ST/AoE roots, mudra selection/continuation, Bunshin/Dokumori/Meisui, ST/AoE mudra-pooling options |
| SAM | Soten, Mineuchi, Zantetsuken | Meikyo/Ogi continuation, Chiten; retain saved Soten charges/range and Mineuchi/Smite HP conditions |
| RPR | None | Death Warrant/Plentiful Harvest initiation, Immortal Sacrifice pooling, Enshroud/Communio and early release; Arcane Crest protection |
| VPR | Slither | Bloodcoil/Uncoiled Fury, Rattling Coil sub-options, Backlash behavior and separate Snake Scales replacement; fork-added defensive initiation |
| BRD | Silent Nocturne | Complete damage sequence, Apex upkeep, Harmonic reserves/execute logic, Eagle threshold, Blast/Encore follow-up and self/ally cleanse |
| MCH | Marksman's Spite | Analysis/Drill/Bioblaster/Air Anchor/Chainsaw sequence, alternative Analysis/Drill policies, Full Metal Field/Wildfire and LB |
| DNC | En Avant | Honing Dance/channel/release, Starfall, Fan Dance, Curing Waltz and partner selection; distinguish added ally-heal behavior from previous self-HP checks |
| BLM | Lethargy | Offensive/defensive Burst, Elemental Weave and Xenoglossy; currently forced sub-options, Aetherial Manipulation replacement and elemental continuation |
| SMN | None | Crimson Cyclone/Strike, Deathflare/Brand continuation, Mountain Buster/Slipstream/Necrotize and Radiant Aegis |
| RDM | Corps-a-corps, Displacement, Resolution | Embolden/melee/Forte initiation, movement conditions and dash replacement; current forced Vice of Thorns option overrides an explicit false preference in the supplied example |
| PCT | None | Motifs/muses/palette, Mog/Star Prism follow-through, Tempera; Burst Control and one-button motif alternatives outside the current profile |

For each row, produce an evidence record containing:
- Supported damage, defensive, support, movement, CC, role-action and LB selection paths; parent presets and incompatible alternatives.
- Current Accord differences versus that supported configuration, including fork gates, preset changes, forced booleans/integers, recipient selection and button visibility.
- Proposed always-available rotation behavior, category ownership, user-controlled policies, manual controls and explicit unsupported cases.
- Concrete source locations, intended change and a behavioral test. A populated matrix is not proof of rotation equivalence.

## 3. PvP settings as an explicit upper layer

Resolve settings in this order, with each later **applied** value replacing the earlier value:

`Normal Global → normal role → normal class → PvP Global → PvP role → PvP class`

- Resolve the normal configuration first, then overlay the PvP configuration. A PvP-global value deliberately takes precedence over a normal class override: PvP is the upper layer requested by the user.
- Support only fields valid in that context. Normal automation currently has Global/class choices; this plan does not silently invent normal role automation.
- PvP contents come from the PvP action catalog. Inheriting an intent or group never copies a PvE action ID onto a PvP bar. Inapplicable intents contribute no actions. Extras need mode eligibility checks and an actionable warning if a configured control cannot exist in PvP.
- Reuse Apply semantics: unchecked means live inheritance; false is an explicit value; Mapping is one complete setting. Do not copy all resolved values into newly added overrides. No automatically generated class/role override lists.
- The PvP layer covers automation, whole intent mapping, manual-button retention, role-action selections, exclusions and applicable group-content choices. Keep-manual works as already agreed: effective force-on switch, otherwise per-group retention stored within Mapping.
- PvP role/class override entries are explicitly added, listed separately from normal overrides and edited one at a time. Preview selects mode/class and identifies where effective values come from. Editing or previewing must not create an override.
- During PvP, a server-status toggle updates the highest applicable layer with Apply enabled for that toggle: PvP class, PvP role, PvP Global, then the existing normal class/Global behavior. Outside PvP, ignore all PvP layers. Show the target layer in the tooltip. Preserve unsaved unrelated editor changes when a status toggle changes a saved value.
- Mode/class changes must recompute the effective engine and layout state together, with existing loading retries, pending-state reporting and rollback. No PvP-only choice may leak into PvE.

## 4. Global PvP rotation-button option

Add a clear global PvP setting: **Rotation buttons: One / Two**.

- One: display a single rotation control. Two: display the existing ST/AoE pair.
- Default to One. On upgrade, migrate the exact previously shipped PvP Global mapping to the corrected default and One; preserve custom mappings, unchecked Mapping Apply, explicit role/class mappings and their existing button choice. Record the migration once so later user choices survive restarts.
- Do not alter PvE's button count, automation categories, exclusions, personal thresholds, or manual-control retention when switching this option.
- For jobs sharing one upstream PvP rotation, One routes to that complete rotation; Two must retain its existing documented routing, without claiming separate behavior where the roots are equivalent.
- **Ninja is a release gate:** it has genuinely separate ST/AoE paths. User decision on 2026-10-29: automatically choose by nearby enemy count. Route through the existing ST/AoE policies and keep selection stable during a mudra sequence. Implement and test this before claiming parity; hiding AoE is insufficient.
- Default bar 1 starts with full rotation, Limit Break, Elixir and Recuperate, then remaining applicable controls. Bar 2 starts with the selected role action, then utility. Retain LB and recovery manual buttons by default even when automated, using the existing per-group retention settings. Keep all groups editable.
- Reflow ordered groups without unmanaged gaps, unexpected bar-3 additions or silent overflow. Validate both button-count modes and all-off capacity after overrides; report the affected class/role.
- Preserve approved controller behavior unless a separately documented change is necessary for this setting; keyboard simplification does not authorize a controller redesign.

## 5. Implementation order

1. **Audit first:** finish the 21 evidence records, identify supported alternatives and any blockers (especially Ninja), and specify each category's behavior. Compare against the configured upstream-compatible paths, not merely Accord's existing profile lists.
2. **Settings/resolution:** implement the explicit PvP overlay, sparse serialization, mode-aware status updates and migration/backup behavior. Test precedence and isolation before connecting it to live writes.
3. **Rotation parity:** replace blanket Manual exclusions with reviewed delegation, honor explicit user exclusions, correct fork gates and forced policy changes. Add only integrations needed to expose existing supported behavior; do not rewrite combat priorities indiscriminately.
4. **Manual controls and layout:** separate retention from delegation, reuse edge/group editing for PvP intents, add One/Two, preserve direct-action targeting and deduplication, and make warnings reflect the resolved layer.
5. **Verification:** exercise all jobs, each category independently, all-on, all-off, One/Two, partial overrides, serialization/restart, switching classes/modes, loading retries and failure recovery.
6. **Review against this document:** record implementation, automated evidence and native acceptance separately. Update the ledger before publishing a test build; do not mark work complete while a required job or routing decision remains unresolved.

## 6. Required regression and acceptance evidence

- All-on parity for every supported job: no Wrath-supported compatible rotation function lost through an Accord denylist or fork feature gate. Tests must assert actual policy/routing outcomes, not simply that profiles fit on bars.
- Bard: automatic Silent Nocturne retained when not explicitly excluded; Apex/Blast, Harmonic and Eagle respect their supported sequencing and policies; optional manual buttons coexist. Guard against restoring a less complete rotation behind a Full rotation label.
- Categories off: manual initiation still allows correct follow-through; unrelated categories are not accidentally disabled. Explicit per-intent exclusions remain enforced.
- No unintended rewriting of charge reserves, HP thresholds, timing, mode restrictions or mutually exclusive alternatives. In particular, test imported false sub-options rather than unconditionally forcing them true.
- Native targeting/readiness preserved for movement, CC, LB and allied actions. Manual requests use their intended recipient; keeping a manual button is not permission to alter automatic target selection.
- PvP precedence tests include normal class versus PvP Global, PvP role versus PvP class, explicit false, unchecked Apply, removal of an override, status writes, dirty-editor saves and return to PvE.
- Both rotation-button modes preserve supported selection behavior and manual access. Ninja's distinct paths require specific evidence. Validate all-off worst-case layout with actual overrides and report honest capacity conflicts.
- Existing live-update, max-level placement, shared-bar ownership, logout fallback, controller, gatherer and PvE behavior must remain covered.
- Build and package both plugins when engine changes are required; pin and validate their compatible revisions. Do not publish the supplied personal configuration or game data.
- Native acceptance remains separate: user testing in PvP, with the mode identified, must check actual actions, timing, targeting, channels, control layout and mode transitions. Unit tests cannot establish combat quality.

## 7. Progress ledger

- [x] User direction written before code changes.
- [x] Initial current-profile/preset inventory covers all 21 jobs.
- [x] Branch-level source review completed for all 21 jobs, plus shared emergency actions. Native scenarios remain below.
- [x] Ninja one-button policy implemented and its count/sequence state tested; controller ST/AoE remain explicit.
- [x] PvP overlay implemented; precedence, explicit false, status destination, dirty-editor rebasing and serialization tested.
- [x] Reviewed rotation preset/feature changes implemented; automated policy and schema checks pass. This is not native rotation-quality acceptance.
- [x] Optional manual controls and One/Two keyboard mapping implemented; both modes and all-manual capacity checked across all jobs.
- [x] Migration/default preservation, mode isolation, status routing and existing regression tests pass.
- [x] Both local Release builds and isolated archive checks pass. Public release versions/feeds have not been changed.
- [x] Final source requirements comparison completed. Additional fixes found during comparison are recorded below.
- [ ] Native PvP acceptance completed.
- [x] Publish the paired test release and verify the shared feed (2026-10-29): Accord 0.2.15.11 / Wrath Combo 1.0.4.35.
- [ ] Verify installation and native gameplay in the user’s game.

## 8. Final local verification and remaining acceptance

- 257,459 assertions pass across the existing 512 PvE and 336 PvP job/category combinations, with the new PvP layout-mode, inheritance, exclusion, retention and Ninja policy checks. Three packaging tests pass. Both actual build directories were packaged and checked in an isolated staging directory, without rewriting public feeds.
- Final comparison additionally caught missing shared emergency-action delegation; unchecked PvP Global settings being reapplied on deserialization; ST/AoE exclusions being combined for the one-button route; and legacy cleanup touching unselected regular bars. Each was corrected and covered by focused checks or the existing transaction suite.
- PvP Global starts with an editable, applied two-bar intent mapping and selected-role-action group to preserve the existing mode's two-bar footprint. Unchecking Apply inherits the normal mapping/role selection. No role/class overrides are autogenerated. Additional group contents can have a PvP child with role/class conditions; unavailable extras produce actionable warnings instead of putting PvE IDs on PvP bars.
- Existing controller positions and explicit ST/AoE routing are retained. Shared bars and unselected regular bars are preserved. Live PvP changes validate all-manual capacity before changing engine behavior, and use the existing combined rollback flow.
- No supplied personal configuration or live game settings were modified. No release was published in this implementation pass.
- Native acceptance still requires a test build in the game: compare Bard all-on against the old supported rotation; check Support/Protection and retained manual controls; test an exclusion; change a class/role PvP override via the status toggle and return to PvE; test Ninja one/two enemies and mudra continuation; and inspect the UI/preview and layout reflow. Identify the PvP mode used. These checks cannot be marked complete by builds or unit tests.

## 9. Publication verification — 2026-10-29

Both release workflows passed: engine run 40930393111 and client run 40931028161. Shared feed refresh 40931505616 succeeded; the central pluginmaster.json reports WrathCombo 1.0.4.35 and Accord 0.2.15.11. Native acceptance remains open. Earlier statements about no publication describe the local implementation pass before this requested release.

## 10. Corrected defaults (2026-10-30)

User correction supersedes the old two-button compatibility default. Implement One by default and the friend-inspired ordering above. Only an exact match of the previous Global mapping migrates; a changed group, priority, retention flag, area or explicit inheritance is preserved. Role/class mappings, automation, exclusions and personal groups are untouched. A one-time marker preserves subsequent Two choices.

- [x] Implementation and regression cases written.
- [x] Client/engine builds passed; 257,470 assertions passed including all-job/all-toggle layouts, Bard first-four placement, custom mapping preservation and restart behavior. Three packaging tests and an actual staged archive check passed.
- [ ] Publish client update and verify central feed. Wrath behavior is unchanged.
- [ ] Native in-game acceptance (user testing).
