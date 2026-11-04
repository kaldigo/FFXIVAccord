# Rotation redesign: first implementation delivery

4 November 2026. The user authorised the first implementation stage on separate Accord and Wrath branches. This supersedes the planning documents' earlier “implementation not authorised” notes for this stage only.

Both repositories use `codex/rotation-stage0`, in separate worktrees. Accord starts from `8eef51a29cee077edb52135125e8e54666e4debb`; Wrath starts from `761b51ff9fec11ee9f7c71dcd3ef6279dcb31bae`. Stable checkouts, identities, release workflows, feeds and version numbers are unchanged. No plugin has been installed or published. The separate empty FFXIVAccordTesting scaffold is not the implementation destination. Future installation should use Dalamud testing versions of Accord and WrathCombo; testing feed/version allocation is a separate release task before installation, not a second identity.

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
