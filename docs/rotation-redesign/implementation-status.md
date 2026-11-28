# Rotation redesign: implementation ledger

## Pre-game code review � 5 November 2026

The follow-up [integration review](pre-game-code-review.md) found and corrected source defects in strict target delivery, automatic healer proposal execution, ground prevention tracking, DRK/BLU target context and RSR ownership cleanup. Earlier build completion did not establish these behaviors were correct. The review adds focused regression coverage before native acceptance; no stable source or feed changes are included.


4 November 2026. The user explicitly confirmed **the entire roadmap**, including all jobs, PvP and RSR automation handoff. This supersedes earlier first-stage-only authorization and planning-only notes. Implementation uses branches named `testing` in the original repositories; stable Accord remains untouched. Authorization to implement is not evidence of native gameplay acceptance.

## Current checkpoint (supersedes the initial delivery notes below)

The current file-by-file/job-by-job disposition and explicit native acceptance record are in [implementation-map.md](implementation-map.md). This supersedes the older open-code lists below as those items are finished and verified.

Latest paired Release validation: **474,419 assertions**, 512 PvE and 336 PvP configurations. Python regressions: Wrath 30, client 11, RSR 11, central feed/publisher 18. RSR Release and settings/manual-action checks pass. Native acceptance and matched performance/rotation captures remain unrun; there was no live game process available. The testing providers remain opt-in and session-only, with the new PvP scheduling option separate from target selection and MCH Analysis.

The 5 November targeting/PvP audit adds a scoped preliminary enemy recipient, strict final action/recipient consistency, common automatic-runner acquisition, reviewed self-area coverage with press-time revalidation, native donut geometry and RSR's cone-angle convention. Joined PvE coverage uses the same policy. Provider-published healing/preparation/release decisions cannot be moved by a later generic target pass. Resurrection recipient eligibility now excludes another ally's observed raise before selection, including RDM/SMN and accepted continuations.

PvP adds confirmed Analysis/tool binding, separately opt-in RDM planned-GCD Embolden and SMN summon-phase/Cyclone policy, plus own accepted Wildfire/Warrant/Honing release controls. Smite's reviewed Guard bypass no longer disappears behind the broad ordinary-damage guard in the migrated melee paths. Native manual release controls remain available. AST's retained healing recipient is invalidated by context changes. The capture baseline now includes the experimental settings and encounter snapshot at start.

### Continuing implementation, 5 November

The entire roadmap remains the scope. The following additions are implemented locally on `testing`; they are not a declaration of complete roadmap or native acceptance. Latest paired Release build: **474,406 assertions pass**, including compact PvP layouts for all 21 jobs and 16 automation combinations per job. No stable source, feed, installation or public release was changed.

- WHM/SCH/AST simple healing adapters now use the shared recipient/preparation boundary. Added retained ground points for Asylum, Bell, Sacred Soil and Earthly Star; failed placement does not fall back to the player's feet. Observed party mitigation can defer another forecast mitigation when it lasts through the hit. Simple forecast actions share event accounting. Advanced WHM retains content-specific healing, binds Plenary to an eligible heal and avoids Thin Air for free lily spells; ST Neutral Sect prepares Aspected Benefic on the chosen ally. Cleanse inclusion and preventive healing are separately configurable.
- RDM retains a manually requested raise through Swiftcast/Vercure preparation and checks combined Vercure/raise mana. BLM reviews actual adjusted cast time against fresh cast/uptime limits and uses only permitted instant alternatives. PCT motif entry uses cast/downtime budgets. BossMod exposes independent, expiring `Hints.MaxCastTime` and `Timeline.NextDowntimeEndIn` inputs.
- SAM preserves accepted Iaijutsu repeats across ST/AoE changes and three-Sen spending through buff-loss recovery; equivalent Meikyo choices can prefer the available positional. No automatic Hagakure conversion was added. MNK's PB adapter derives the next form from accepted chakra types and Nadi, including manual reordering and synced forms. NIN previews restore the original mudra intention; only a matching accepted action commits the proposed intention across ST/AoE providers.
- DNC finishes an observed dance independently of initiation toggles, protects eligible expiring continuations and checks new dance duration against known downtime. GNB computes one GCD plan before abilities for simple/advanced ST/AoE, prioritizing accepted chains and usable expiring procs. MCH retains Reassemble's selected tool/recipient and checks whether five heat shots fit known uptime before Hypercharge.
- BLU binds Bristle to equipped/permitted Song of Torment and validates Final Sting before its explicit preparation control. BST's testing finisher is tied to pet capability, confirmed actions, permissions and a bounded lifetime; no capture, pet sacrifice or horn preference is enabled by this adapter.
- PvP SCH/AST recovery uses eligible ally selection before thresholds, while Double Benefic retains its accepted recipient. Friendly Deployment cannot be overwritten by the hostile-target pass. The optional compact layout keeps native survival controls fixed and both SMN LB choices. It is a draft preset, not an automatic remapping.
- Ally/enemy override sources can be reordered separately. Explicit recipients remain strict. Visible enemy/ally target switching has separate permissions; automatic ST/AoE delivery uses the resolved recipient for subsequent checks. New targeting options remain session-only during this test stage.

Still requiring code work/audit: broad preliminary enemy eligibility and action-specific target consistency (the common final-delivery pass alone is insufficient), remaining PvP per-action Guard/permission and release adapters, complete capture/provenance/acceptance matrix, packaging checks of the final paired commits, and public testing activation if/when an installable candidate is released. Native queue/hook/geometry acceptance, matched rotation captures and performance measurements remain separate and cannot be inferred from builds. Retained job rules are not claimed to be translated RSR rotations.

All new policy code is locally authored. Behavioral references remain the pinned comparison sources; no RSR rotation source was copied into the BSD Wrath repository. Job decisions explicitly retain Wrath's useful existing rotation priorities, resources, movement reserves and manual controls.

### Continuing implementation, 4 November

Additional local implementation after the checkpoint below (still not entire-roadmap completion):

- Shared healer prepull on the explicit focus recipient; dead-recipient resurrection, confirmed Swiftcast/Thin Air follow-through, cohealer cast avoidance, configurable inclusion/hard-cast permission, and eligible-recipient cleansing. Sage's continuation now distinguishes resurrection from Eukrasia/Zoe.
- PvP MCH Analysis follows the selected eligible tool. Shared opt-in PvP final targeting checks Guard per action, keeps reviewed bypass exceptions separate from invulnerability/Hardened Scales, and revalidates at press time. Native manual controls are not routed through this new target selector. Legacy job preliminary eligibility still needs migration.
- BRD snapshot refresh considers existing DoTs, expiring observed buffs, the actual GCD delay and known downtime. Existing song/repertoire priorities remain. VPR applies duration/combo protection to both Reawaken paths; RPR checks phase duration/known downtime and basic-combo expiry before entry. These are entry changes, not replacements of accepted phase progression.
- SMN binds Swiftcast to its enabled, available Ruby/Slipstream spell and recipient. Confirmed preparation survives previews, cancels on invalid phase/recipient or another consuming spell, and reserves a manually initiated resurrection. Native target validation is shared with the preparation boundary.
- AutoDuty session following uses verified public status APIs and the handoff coordinator. Start/end/disconnect and manual override are covered; upstream route pause retains its navigation flag and is explicitly not detectable through those endpoints. See automation-handoff.md.
- A separate central `testing` worktree contains a fresh-stable testing-feed composer, explicit three-plugin registrations, immutable candidate verification, atomic output and a testing-only refresh workflow. Candidate registrations remain empty; no public feed or release has been changed.

Latest completed paired checks before the SMN adapter: 472,318 assertions, both Release builds pass. Native acceptance remains separate from code completion. Remaining stages below remain open; session toggles and broad compatibility counts do not establish all-job migration or rotation quality.

The work remains in progress against the **entire** roadmap. A successful build is not completion. Latest paired validation: 472,217 assertions; RSR Release build and settings/manual-permission checks also pass. Stable checkouts and feeds have not been changed.

Implemented since the earlier checkpoint:

- Shared opt-in WHM/SCH/AST advanced healing selection and damage-button healing inclusion, retained preparation recipients, and Recitation → Adloquium → Deployment progression from confirmed effects. Existing priority thresholds and Scholar resource restrictions are retained. Simple providers, complete prepull/ground coverage and cohealer mitigation spacing still require work.
- Bounded, recipient-specific pending-health effects with deduplication, acknowledgement and expiry; actual raidwide cast timing and distinct tankbuster marker identity. Unknown timing stays unknown.
- Opt-in MCH Reassemble eligibility uses the selected GCD branch with preparation disabled; excludes Full Metal Field, Bioblaster, Flamethrower and heat attacks. Does not execute a second rotation. PLD burst entry uses planned GCD/proc state and actual accepted buff continuation. WAR checks buff/known uptime duration; DRK adds an event-specific shield reserve without lowering configured reserves. These focused changes do not yet complete the offensive scheduling stage.
- Opt-in GNB uses actual No Mercy status for burst membership, including its dedicated buff control. Remaining planned-GCD/phase scheduling work is not represented as complete by this change.
- Action-specific enemy selection can be enabled across PvE damage providers, with separate mouseover/focus permissions, acquisition versus switching, and press-time recipient validation. Automatic selection still excludes unengaged collateral. This is a delivery/coverage extension; it does not yet replace every job's preliminary target eligibility checks.
- RSR has explicit per-job/per-PvE/PvP manual-only action lists, checked at selection and execution. They apply to coordinated automation and do not intercept native manual buttons. Accord can edit these lists and verified RSR preferences independently of Wrath. RSR AutoHeal is labelled as automatic threshold calculation, not healing permission.

All new policy code is locally authored. Behavioral references are the pinned RSR next-GCD preparation, PLD buff coordination, and action-specific targeting structure. No RSR rotation source has been copied into the BSD-licensed Wrath project. Deterministic captures can replay the MCH, PLD and WAR policy inputs as well as the earlier DRG/preparation/target inputs.

Joined PvE controls now use a separate custom action (`1000016`), with Global → role → job inheritance and keyboard/controller layouts. One enabled provider is selected using the area action's coverage and the existing AoE threshold; the chosen path retains its own exclusions. Unavailable area actions fall back to ST. Accepted Ninja sequences retain their mode; icon previews cannot commit it. Limited jobs keep their dedicated controls. The paired suite passes 472,288 assertions including these layout and selection cases. Client packaging tests pass (11); engine Python regressions pass (30). Native action-cache and queue acceptance remains required.

Still open: finish healer adapters and targeting preferences, complete Stage 3/4 job/limited-job adapters and replay coverage, PvP migrations, activity integration and testing publication. The older table below is the previous checkpoint, not evidence these open items are finished.

The paired Release build passes with 472,127 assertions, including the new ownership and external-queue tests. The DRG/SGE pilot now includes pure decision snapshots, fixed recipients, confirmed preparation progression, actual-hit Life Surge selection, opt-in healing inclusion, action-specific target geometry, queue-recipient protection, and read-only BossMod forecasts. Captures include replayable policy inputs and outputs; `--replay-capture <report.json>` compares them without game execution. Experimental selection options are session-only and disabled by default. Opting into coordinated automation persists only a stopped-on-reload guard. These changes are not yet validated in game.

RSR now also has an isolated `testing` worktree at base `156de237f5e4231d93558c8e6fba6ee337e7b902`. Its runtime identity remains unchanged. No plugin has been installed or published.

| Workstream | Current evidence | Remaining work |
|---|---|---|
| Baseline and replay | Bounded capture, setting/context snapshots, deterministic policy replay, paired build/tests | Matched native baseline captures and performance measurements |
| DRG/SGE pilot | Opt-in runtime adapters and pure policy tests implemented | Native prepull/queue/target acceptance, review all simple/advanced paths |
| Targeting | Ally overrides, native eligibility, per-action geometry, retained recipients in pilot | Other jobs, complete preference surface, ground targeting |
| BossMod | Nullable, expiring independent forecasts; observed event windows; no invented buster victim | Broader healer integration, native module/context-change acceptance |
| Healer coordination | SGE pilot with preparation and prevention window budget | WHM/SCH/AST, pending effects, cohealer overlap, full shared orchestration |
| Other PvE jobs | Existing providers retained | Stages 3–4 adaptations, BLU/BST capability review |
| RSR handoff | Runtime gates, action-hook isolation, coordinator, testing controls, external IPC and deterministic failure tests implemented | Native hook/queue ordering acceptance, permission mapping, automatic-state integration for activity plugins |
| PvP | Prior comparison and existing compatibility suite | Planned selector changes, compact controls and manual escape routes |
| Release/testing channel | Isolated `testing` branches; Accord/Wrath candidate workflows and stable-preserving package tests | Central feed composition, public testing publication, RSR candidate packaging and installation acceptance; no stable feed changes |

### Handoff audit findings

Wrath's master flag is insufficient: IPC leases can override it, and stance/partner retries execute separately from its main runner. RSR can auto-enable itself and also changes targets/cancels casts outside its central action call. The ownership boundary must cover these paths. A failed/expired controller must leave both runners stopped until an explicit restart.

RSR `Service.GetAdjustedActionId` calls the native function hooked by Wrath. The integration must isolate RSR selection and execution from Wrath replacement/retarget hooks, while keeping ordinary manual Wrath button calls working. Blocking one automatic loop alone does not satisfy the handoff contract. Stop must clear plugin requests and recipient caches, then wait for already queued/casting native actions to settle before starting the next runner.

Implemented handoff details and remaining acceptance checks are in [automation-handoff.md](automation-handoff.md). The RSR Release build and existing settings conversion checks pass. Its shared authority policy is identical to Wrath's locally authored policy. No upstream RSR rotation code has been transplanted by this handoff work.

## Initial Stage 0 delivery record

The remainder records the original instrumentation checkpoint. Its descriptions of unimplemented Stage 1 work are historical; use the table above for current status.

Both repositories use `testing`, in separate worktrees. Accord starts from `8eef51a29cee077edb52135125e8e54666e4debb`; Wrath starts from `761b51ff9fec11ee9f7c71dcd3ef6279dcb31bae`. Stable checkouts, identities, release workflows, feeds and version numbers are unchanged. No plugin has been installed or published. The separate empty FFXIVAccordTesting scaffold is not the implementation destination. Future installation should use Dalamud testing versions of Accord and WrathCombo; testing feed/version allocation is a separate release task before installation, not a second identity.

## Implemented

Accord has a **Rotation testing** tools page. Start records a new session, replacing any prior capture. Stop and Refresh retrieve it; Copy capture report copies JSON locally. Capturing is off by default, ends after 60 seconds, and stops if character, job, level, territory or PvP context changes. Plugin unload stops it. Ordinary Accord features do not require this optional testing API.

The report contains the job/level/duty context, build module IDs, Wrath saved settings at start and read time, Accord settings at start and read time, the current job's default profile, and observations. The default profile maps buttons to functions and preset configuration using the existing setup data (including SetupPack's DNC normalization). Personal mapping/automation overrides are in AccordSettings. This is not a scrape of the player's actual hotbar slots or a snapshot of external IPC leases. Baseline sources remain `Accord.Plugin/presets.json`, `pvp-presets.json`, `SetupPack.cs`, `PvPPack.cs`, `ConfiguredLayout.cs` and `JobIntents.cs`.

Wrath observes each normal uncached replacement result exactly once after resolution. Identical successive results for the same input are omitted and counted. Changed results retain chronological order. The existing outgoing-action hook supplies action type and recipient ID; repeated sends are never coalesced. The latest 2,048 observations are retained, with explicit overflow counts. The buffer and deduplication state reset on every start.

**Selection is not a press, and Send is not server acceptance.** No recipient is invented for a suggestion. Cached icon returns and early bypasses before the resolver are not selection events. Automatic actions can appear as sends without a preceding selection. There is no automatic correlation between a suggestion and a send. Reports contain settings (potentially including local backup paths) and session object IDs; nothing is uploaded.

## Mutation audit and comparison boundary

| Existing path | Why a second speculative call is unsafe | Capture approach |
|---|---|---|
| `CustomCombo.TryInvoke` | Reads request state and executes job-specific stateful selection | Observe the final ordinary ActionReplacer result; do not invoke again |
| `ActionRetargeting.Register` / `TryGetTargetFor` | Selection registers target resolvers; delivery removes the registration before invoking it | Never resolve a target just for diagnostics; observe the outgoing recipient |
| `SGE.cs` and `SGE_Helper.cs` retarget calls | Kardia, raises, healing and DoTs can register different recipients | Preserve original call count and timing |
| `ActionWatching.SendActionDetour` | Advances action/opener state and clears target overrides | Add an observer only, retaining the original hook and cleanup |
| RSR `CanUse` / action target fields | Shared action objects hold mutable preview/target state | No parallel live RSR resolver for comparison |

This does **not** make the existing Wrath selector pure or fix existing preview side effects. Stage 1 must establish a pure decision snapshot and separately controlled commitment boundary. The new capture's read operation never invokes a selector, target resolver, opener or request consumer. Its standalone tests use immutable observations and a supplied clock/context, without loading a game or executing actions.

## Validation and remaining gate

The paired Release build and existing compatibility suite validate both branches together. Recorder scenarios cover disabled capture, unchanged previews, changed suggestions, repeated sends, immutable snapshots, recipient separation, ordering, context changes, reset, exact expiry, overflow and stopping. These checks verify instrumentation, not rotation quality.

In-game acceptance is still required: capture DRG ST/AoE with burst held/released and Life Surge; SGE Eukrasia/Zoe with single/party healing and target overrides; repeat under manual and automatic execution. Check identical behavior with capture off/on, recipient correctness, repeated report reads, logout/job change and plugin reload. Keep settings fixed during each run; endpoint snapshots cannot reveal a setting changed and restored mid-session. Disable external leases or separately record them. RSR comparison runs must be separate, matched runs. No performance or potency improvement is claimed from this instrumentation delivery.

Next is [Stage 1](wrath-improvement-plan.md): DRG/SGE decision architecture, RSR-derived targeting, and read-only BossMod forecasts. New policies and a side-by-side policy comparison remain unimplemented; this stage provides their baseline recorder and test entry point.

Build from the Accord worktree with `./tools/build.ps1 -EngineSourceRoot <Wrath-worktree>`. This builds both plugins and runs the paired contract/scenario suite without publishing or installing.

Local validation completed: both Release builds passed, and the paired suite passed 471,931 assertions (512 PvE job/variant combinations and 336 PvP combinations with engine-schema verification, including 11 new recorder assertions). NuGet vulnerability metadata was unavailable during restore; this produced warnings, not build or test failures. In-game acceptance remains pending.
