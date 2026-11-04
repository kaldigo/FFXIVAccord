# Rotation redesign: implementation ledger

4 November 2026. The user explicitly confirmed **the entire roadmap**, including all jobs, PvP and RSR automation handoff. This supersedes earlier first-stage-only authorization and planning-only notes. Implementation uses branches named `testing` in the original repositories; stable Accord remains untouched. Authorization to implement is not evidence of native gameplay acceptance.

## Current checkpoint (supersedes the initial delivery notes below)

### Continuing implementation, 4 November

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
