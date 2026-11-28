# Wrath suggestion redesign and RSR automation handoff

Implementation update: the user authorised the **entire roadmap** on `testing` branches. See [implementation status](implementation-status.md) for completed code and remaining gates. Earlier proposal/approval wording below is historical and does not limit that authorisation.

Planning proposal, 4 November 2026. No runtime implementation is authorised by this document. Evidence: [framework, healer and first job comparison](rotation-comparison.md) and [remaining jobs and final disposition](remaining-job-comparison.md).

## Recommendation

Completed follow-up reviews: [all-job PvP comparison and compact controls](pvp-comparison.md), and [direct BossMod encounter integration](bossmod-encounter-integration.md). PvP requires a distinct policy/validation track, not a blanket import of RSR defaults. Wrath can read compatible BossModReborn forecasts directly; explicit cooldown plans are a separate optional feature.

Targeting update: adopt RSR's targeting approach wholesale as the foundation, with configurable retargeting and party-list mouseover, world mouseover and focus overrides. See [targeting comparison and player options](targeting-and-options.md) for source findings, proposed settings and first-milestone gates. This is an agreed targeting direction; detailed defaults remain proposals.

Replace substantial parts of Wrath's suggestion architecture in stages. Retain the custom hotbar identity, manual controls, existing integrations and useful job knowledge. Introduce a shared decision pipeline with explicit planned GCD, eligible weave, recipient, preparation commitment, resource reserves and encounter context. Migrate job rules into it; do not indefinitely layer another set of exceptions around the existing selectors.

Keep RSR as an independent optional automatic runner. Wrath still resolves the buttons while its automatic execution is off. RSR does not consume Wrath suggestions. Actual manual actions affect both engines' observed world state. Accord coordinates runner ownership and settings, not per-action competition between two automatic engines.

This is custom work inside the Wrath fork, openly acknowledged. The benefit is preserving the interaction model that already fits Accord while changing the decision machinery that limits it. A full runtime dependency on RSR for manual suggestion generation is not recommended under the newly agreed handoff.

## Alternatives and decision

| Approach | Benefit | Cost/limitation | Recommendation |
|---|---|---|---|
| Patch individual helpers only | Quick local corrections | Leaves duplicate healer orchestration and inconsistent context; improvements remain difficult to share | Use only for isolated verified defects |
| New shared selector, incremental job migration | Large architectural improvement; keeps working controls and permits comparison/fallback | Requires adapters, state discipline and staged maintenance | Preferred |
| Replace all job rules with translated RSR rules | One conceptual source for priorities | Loses useful Wrath behaviour; RSR dependencies and mutable selection must be reworked; superiority not demonstrated across jobs | Reject as a blanket strategy; allow substantial per-job ports where evidence supports them |
| Run RSR's resolver inside Wrath for every button | Reuses original priorities directly | Reintroduces speculative-state, ST/AoE and intent-scope problems from earlier investigation | Not the preferred foundation |

The new selector should be capable of replacing a whole job provider. Scope is not restricted to small helper edits. The decision to replace a job's entire provider requires better measured behaviour or materially simpler ownership of its state, with compatibility tests passed.

## Behaviour contract

- Native direct controls retain native action meaning. Custom buttons retain their assignments and resolve a current action when pressed.
- Split ST/AoE and joined controls remain supported. Offensive preference does not fabricate enemy counts or remove valuable shared actions.
- Inclusion permits selection through the rotation controls; it does not by itself start background execution. Existing assistance behaviour must be mapped explicitly, not accidentally removed or expanded.
- Initiation permission and accepted continuation are separate. Withholding a buff/phase does not discard a phase the player actually started.
- Manual-only actions are excluded from suggestions that could execute through rotation presses or automation, across all normal/emergency branches. Native direct access remains available.
- Immediate presses attempt one action under normal game locks. Scheduled requests are explicit and bounded. Merely displaying a suggestion never queues an action or accepts a sequence.
- Manual-target and automatic-target preferences are explicit. Preserve the chosen recipient through preparation where appropriate; revalidate when pressing/executing.
- Only one automatic runner is allowed. RSR ownership disables every Wrath unattended execution source, including any separate assistance/activity paths, not merely its main damage loop. Wrath's button queries remain active.

These preserve agreed direction. Exact defaults for background assistance, raise continuation and new function settings still require a concrete behaviour specification; this plan does not pretend those choices have already been made.

## Proposed selection flow

1. **Observe:** construct one consistent view of current combat inputs: cooldowns, resources, buffs, combo/proc deadlines, targets, actual geometry, queued game action, cast/animation state and optional encounter forecasts. Keep encounter uncertainty explicit.
2. **Determine permissions:** requested control/scope, ST/AoE preference, manual-only exclusions, initiation settings, resource reservations and immediate/scheduled context.
3. **Respect commitments:** establish active sequences from confirmed actions and current game state. A proposed preview cannot create a commitment. Reconstruct observable state after reload/job change; expire or cancel stale internal requests.
4. **Select the intended GCD:** job-specific priority rules return the action and target with reasons, dependencies and deadlines. Healing/preparation can compete under the reviewed inclusion policy.
5. **Select the ability/preparation:** consider that GCD, the remaining weave opportunity and resources. Examples: Life Surge for the chosen finisher; Zoe for selected Pneuma; a spender before its generator. Handle preparation for another ability explicitly rather than pretending nextGCD covers it.
6. **Return an independent decision:** requested control, action/adjusted ID, target or position, planned GCD, reason, validity/readiness, preparation/continuation information and relevant state version. Do not return only a shared mutable action object.
7. **Revalidate on press:** refresh changed state and reselect where necessary; execute through the existing compatible action handoff. Only observed acceptance/effects advance persistent sequence state. Display can remain stable while unchanged, but freshness must take precedence over cosmetic stability.

Do not construct a new general mathematical damage optimiser as the first version. Use reviewed job priorities and bounded forecasts of the next few actions where they resolve a concrete choice. Longer resource/encounter horizons can be inputs without simulating every possible rotation.

## Where this fits in the existing fork

Current `WrathCombo/CustomCombo/CustomCombo.cs::TryInvoke` is a common boundary for icon/manual and auto invocations; it applies route context, explicit action requests and filtering around the job Invoke method. `Core/ActionReplacer.cs` and the existing custom action route machinery remain compatibility boundaries to inspect, not arbitrary rewrite targets.

Proposed new components under `WrathCombo/Engine` (names provisional): combat snapshot, encounter-event provider, decision/result types, job selector interface, preparation/sequence tracker and selection coordinator. Existing `RotationBehaviour`, `Options`, `ActionRoutes`, `IntentRouting`, `IntentHandoff` and `BurstPolicy` supply behaviour contracts to preserve or migrate explicitly.

Job-specific providers initially adapt `Combos/PvE/<JOB>` helpers. Separate pure eligibility from selecting/advancing actions before using a legacy helper speculatively. Calling the current Invoke twice and assuming it is read-only is not an acceptable implementation shortcut: request consumption, opener progression, target changes and private state all need auditing.

`AutoRotation/AutoRotationController.cs` currently contains healer preparation/event execution alongside ordinary rotation calls. Migrate selection from those direct-execution branches into the shared decision system when a job is converted. Retain a single chosen Wrath execution path. Do not leave old and new healer rules active together.

Existing `IntentHandoff` already waits for a confirmed action after observed input and bounds its lifetime. Preserve that principle while replacing generic time-window assumptions with job-specific accepted phases where useful.

## Improvement backlog tied to evidence

| Work | Evidence from RSR / current Wrath | Concrete deliverable | Priority |
|---|---|---|---|
| GCD/ability coordination | RSR nextGCD used by DRG, MCH, PLD and healers | One GCD decision consumed by weave/preparation selection; target and reason retained | P0 |
| Common healer orchestration | RSR common stages versus Wrath controller lists plus job helpers | One prevention/recovery/preparation decision path per converted healer | P0 |
| Preparation commitments | RSR Eukrasia aim and next-spell buffs; both engines have gaps | Explicit intended follow-up, recipient, readiness, expiry and cancellation | P0 |
| Incoming event context | RSR cast plus BossModReborn timeline inputs | Typed raidwide/buster/multihit/downtime hints with source, timing, freshness and fallback | P1 |
| Pending healing | RSR effect-window HP accounting | Prevent redundant healing while confirmed effects are pending; avoid double-counting once landed | P1 |
| Shared resource forecast | Both engines contain useful local forecasts | Job-specific reserves/deadlines accessible to all candidates | P1 |
| Target geometry and coverage | RSR per-action hit geometry and recipient targeting | Adopt the targeting foundation with overrides in stage 1; expand actual affected-target coverage across jobs | P0 foundation, P1 expansion |
| Death/downtime-sensitive spending | RSR MCH/SMN/RPR examples | Avoid starting commitments that cannot pay off; controlled dump/hold rules when confidence is sufficient | P1 |
| Sequence recovery | Existing Wrath NIN/MNK/SAM/VPR strengths and RSR stateful selection | Confirmed sequence progression with manual-deviation/reload recovery | P0 contract, per-job delivery |
| Explainable decisions | Needed to compare both approaches | Trace selected/rejected candidates and reasons without executing them | P0 |

A preparation deadline, an event-wide prevention budget and immutable previews are proposed improvements, not claims that RSR already solves them fully. Likewise, future movement-aware motif/summon ordering is custom policy requiring evidence.

## Concrete delivery stages and gates

### Stage 0 — behaviour baseline and trace harness

Deliver a configuration/action map for the existing Accord defaults; record exact job/level/presets and relevant engine versions. Capture observed inputs, actions and decisions for the scenarios below. Define the minimal decision format and prove which existing queries mutate state.

Build a deterministic scenario harness for the pure extracted policies, plus an opt-in non-executing comparison view. Do not call a live mutating RSR or legacy selector in parallel merely to obtain a comparator. RSR traces can be captured in a separate run under matched conditions; replayed policy tests and live runs answer different questions.

Gate: repeated previews do not consume requests, move targets or advance openers; expected behaviour and active settings are known. A differing action is explained rather than automatically labelled a regression.

### Stage 1 — vertical slice for the new suggestion architecture

Include the RSR-derived targeting foundation and initial read-only BossModReborn forecast adapter here. Prove SGE prevention with recipient evidence and missing-data fallback; do not defer targeting or encounter-input correctness until after job migration.

Use DRG for explicit next-GCD/Life Surge coordination and SGE for Eukrasia/Zoe preparation. These exercise both offence and multi-step healer decisions; GNB alone would not prove the architecture. Include actual ST/AoE controls, immediate requests, target retention, manual initiation and no-action cases.

Gate: original hotbar assignments work; no unsolicited execution; no wrong-recipient preparation; Life Surge and Zoe reach the intended eligible action; repeated/query-order-independent previews; support can pre-empt damage when its reviewed urgency requires it. If these fail, fix the architecture before migrating more jobs.

### Stage 2 — all healer coordination

Migrate SCH, AST and WHM after SGE. Add event identity/timing, pending-effect accounting, existing-shield/HoT awareness and recipient coverage. Replace duplicated controller/job selection responsibilities for converted jobs.

Required sequences: SCH Recitation → Adlo → Deployment with stable shielded recipient; AST Neutral Sect/Horoscope with the intended spell and appropriate stored-heal release; SGE Eukrasia/Zoe without wasting preparations on damage; WHM Bell/multihit and major-tool spacing. Retain explicit user ownership of emergency or reserved tools.

Gate: preparation completes before impact when feasible; misses cancel/replan cleanly; no automatic expenditure of every ready mitigation for one event; no redundant immediate heal because the first has not visibly landed. Test full-health incoming damage separately from post-hit recovery and multi-hit follow-ups.

### Stage 3 — coordinated offensive scheduling

Migrate GNB, MCH and PLD first: shared burst state, planned-GCD preparation and deadlines. Migrate DRK/WAR reserves and BRD song/DoT decisions next. Preserve old providers as selectable fallback until each converted job passes its cases.

Gate: no additional dropped procs/combos or avoidable resource loss in deterministic cases; manual holds recover; cooldown use and buff coverage improve or differences have a documented reason. Compare live results under matched settings and plausible timing variation. Do not set a promised DPS percentage before measurements exist.

### Stage 4 — stateful melee and caster providers

Migrate NIN/MNK/SAM/RPR/VPR/DNC phase handling, then BLM/RDM/SMN/PCT casting/preparation. Use the per-job disposition table. Large provider replacement is allowed where maintaining the old control flow is more complicated than expressing it in the new architecture; carry forward useful rules through explicit tests.

Gate: level-sync and interrupted/manual sequences pass, movement does not silently spend reserved mobility, and holding one initiator does not deadlock the remaining rotation. BLU/BST receive dedicated loadout/pet adapters after standard jobs; neither is a normal fixed-action rotation.

### Separate track — RSR automation handoff

PvP extension: audit RSR's match-start/death/end state changes against the same runner ownership. Its normal native-action interception excludes PvP. Preserve immediate controls and separately test action/queue contention; PvE interception assumptions cannot be reused unchanged.

This can be developed independently after its own coexistence audit; suggestion improvements need not wait for it. Keep original RSR identity. Begin with explicit switch at a stable state, then validate in-combat switching rather than promising seamless transfer of private engine state.

Deliver an Accord controller that stops the outgoing automatic executor, confirms it is stopped, cancels its scheduled requests as specified, and only then enables the other. Audit action hooks, interception, game queues, assistance/activity executors and AutoDuty/BossMod mode requests. Failed ownership transfer leaves automation off and reports the cause. Never silently restore a runner through a competing external command.

Wrath's hotbar resolution remains enabled during RSR automation. Verify that RSR-issued actions bypass inappropriate Wrath replacement/interception, and that manual button presses can coexist with RSR's observed state. No per-action arbitration or shared private rotation state is required by the design. Manual press/automatic timing contention still requires gameplay testing.

RSR settings and manual-only exclusions must be mapped explicitly. Existing settings IPC covers global scalar configuration, not every action/job setting; inspect native facilities and add narrowly scoped access if needed. Do not promise that the current bridge already transfers all permissions.

## Per-job acceptance cases

PvP has its own [21-job disposition and acceptance cases](pvp-comparison.md). Pilot MCH, AST or SCH, and NIN after the shared targeting/permission boundary is ready. BossMod forecast and optional cooldown-plan acceptance is specified [separately](bossmod-encounter-integration.md); this is principally PvE encounter work, not PvP opponent prediction.

| Job group | Required cases beyond ordinary continuous damage |
|---|---|
| PLD/GNB | Manually withheld buff; accepted magic/cartridge chain; expiring proc; level-sync entry; invulnerability exclusion; channel protection |
| WAR/DRK | Gauge/MP reserve during imminent buster; generator overcap; buff expiry; mitigation under burst; manual TBN/IR |
| WHM/SCH/AST/SGE | Full HP before known hit; short/long lead time; multihit; target moves/dies; co-healer supplies shield; missed preparation; pending heal; recovery after damage |
| MNK/NIN | Interrupted PB/mudra; manually started sequence; delayed state update; missing Nadi; level sync; unrelated requested action |
| SAM/RPR/VPR | ST/AoE change mid-sequence; generator imminent; buff loss/downtime; positional choice; ranged transition; missed follow-up |
| DRG | Life Surge intended hit changes; Stardiver lock; Wyrmwind generator; movement/target loss |
| BRD/DNC/MCH | Song/dance/proc expiry; no target; pooled resources; burst held; predicted downtime changes or disappears |
| BLM/RDM | Movement with limited instant resources; phase deadline; proc overwrite; manual raise reservation; no permitted damage candidate |
| SMN/PCT | Movement/cast deadline; short downtime; demi/motif readiness; manual preparation; expired summon/hammer support |
| BLU/BST | Different loadout/pet; unavailable required step; deliberate sacrifice/capture; resource/role mismatch |

Common metrics: wrong/invalid targets, illegal or excluded suggestions, dropped sequences/procs, resource waste, avoidable clipping, cooldown drift, buff coverage, heal/shield waste, preparation before impact and time spent unable to produce a useful action. Performance measurement includes per-frame/press cost and allocations, but optimisation follows correctness.

## Rollout and maintenance

Keep stable Accord independently updateable and keep the testing feed/package separate. Develop experimental Wrath providers in an isolated branch/build; testing a shared plugin identity must not overwrite stable behaviour without an explicit opt-in installation path. Final packaging for the Wrath test build is a design task before deployment, not permission to rename RSR.

Select old/new provider per job for testing; never alternate providers mid-sequence automatically. Preserve previous settings and a reliable fallback. Use traces to decide when a new provider becomes the test default, then separately review stable promotion. Migration of existing user settings remains deferred until the schema and behaviour are demonstrated.

Maintain a provenance ledger for adapted RSR rules: source revision/method, why adopted, changed assumptions and validation cases. Direct code reuse requires checking applicable repository licensing/attribution before copying; the current recommendation is architectural and behavioural adaptation. Do not create an automatic upstream merge pipeline that overwrites Accord's manual-control semantics.

## Decision now

Approve or revise the architecture and stage order before implementation. The evidence supports a substantial shared selector redesign and targeted large job-block replacements. It does not support wholesale replacement of every Wrath rotation with RSR code, nor a claim of already-proven performance parity. The first implementation milestone must prove both offensive coordination and healer preparation, then expand with the explicit gates above.
