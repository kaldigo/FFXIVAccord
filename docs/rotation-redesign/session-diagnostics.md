# Testing installation and session diagnostics

Installation repository: `https://raw.githubusercontent.com/kaldigo/DalamudPlugins/main/pluginmaster.json`.

The feed follows current stable feeds and overlays opt-in testing downloads for the unchanged Accord, WrathCombo and RotationSolver identities. Its default-branch refresh workflow updates only the testing feed hourly. A stable release that catches or exceeds a test supersedes that candidate. Stable plugin sources and release feeds are unchanged.

## Capture while playing

Open `/accord`, choose **Rotation testing**, and enable **Temporary detailed logging**. Confirm that all three components say **recording**. Missing/old plugins are explicitly shown as unavailable. Logging observes the saved gameplay configuration; it does not enable automation. Accord automatically enables, applies and verifies its default rotation improvements. Saved overrides are under Global automation, and changes made during combat wait until actions and combat finish. The verification status and Settings.WrathSelectionVerified records identify the configuration actually applied.

Use **Mark this moment** to describe an expected action or unexpected result. The session continues across job and territory changes; it does not use the old 60-second capture. Stop logging, wait for files to finish writing, then **Export diagnostic ZIP**. The UI shows the saved path and can copy the session folder. Nothing uploads automatically.

Logs are under `pluginConfigs/AccordDiagnostics/<session-id>/`, with one component directory each. Background writers split files at 32 MiB and stop at 2 GiB per component. Existing sessions are never deleted or overwritten. A bounded 8,192-event queue records loss counts and sequence gaps. Writer failure, component absence and lost events are visible. Plugin unload or a lost controller heartbeat (15 seconds) stops recording. Settings may include local paths/labels. Actor IDs/statuses and hotbar IDs are captured; the world sampler collects no character names, chat or macro text. Marker text is retained verbatim.

## Coverage

| Question | Recorded evidence |
|---|---|
| Correct builds/settings? | Versions/module IDs, session ID, initial and changed settings, active testing options, ownership commands/responses |
| Button exists and is pressed? | Live normal/cross-hotbar slots/shared flags, native input action/type/target/mode, provider output and final result |
| Why no trigger? | Provider entries/results, active-feature checks, permissions, readiness/native status, runner gates, RSR candidates/rejections and basic/cooldown predicate locations |
| Correct target? | Selection snapshots, mouseover/party mouseover/focus/soft/selected targets, nearby actors/positions/radii/statuses, bound recipient and ground point |
| Queued, sent, confirmed? | Native outcomes/queues, outgoing action/source sequence, incoming effect source/global sequences and recipients; submission is distinct from confirmation |
| Preparation followed through? | Immutable action/recipient proposals, planned GCD, preparation, pending/committed state, validation, confirmations, cancellations and job decisions |
| Forecasts working? | Wrath BossMod endpoint availability/errors/values; RSR stored module, timeline, damage, special-mode and cast-cancel guidance |
| Resources/status/movement? | Half-second world snapshots, job gauges, cooldown groups, casts, animation lock, conditions, HP/MP/statuses; exact decision/readiness data when evaluated |
| Runner and handoff working? | Controller health, handoff requests/responses, runner ticks/gates, selected actions and native execution, external-action bypass |
| Capture complete? | Ordered component sequence, UTC/monotonic time, nested request scopes/durations, backlog/loss/errors, checkpoints, stop reason and final counters |

Instrumentation observes existing calls and stored state; it never invokes a second rotation resolver. RSR's existing action tracer feeds the shared capture independently of its separate disk-tracer setting. A false predicate is logged separately from a rejected action: false `NeedsCasting` is not itself a failure.

## Review the session

Run `python tools/analyze_diagnostics.py <session-folder-or-zip> --output <report.json>`. The tool reads ZIPs without extracting them. It reports missing components/builds, sequence gaps, incomplete captures, markers, active-feature evaluations, frequent rejection reasons and sends without observed matching player confirmations. It excludes the final 30 seconds from missing-confirmation checks.

An enabled feature never reached on a job actually played is a review lead. A feature that never triggered may have had no eligible opportunity. Interrupted casts and incomplete coverage can also explain an unmatched send. These are not automatic bug verdicts: snapshots, exact source locations for the recorded build, action sequences and markers provide the evidence for review. Logging cannot by itself prove optimal rotation quality.
