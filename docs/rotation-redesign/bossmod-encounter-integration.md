# BossMod encounter information for Wrath

Investigation and proposal, 4 November 2026. No implementation. Wrath baseline `761b51ff9fec11ee9f7c71dcd3ef6279dcb31bae`; RSR `156de237f5e4231d93558c8e6fba6ee337e7b902`. Provider source checked independently through GitHub: BossModReborn `97595f07cb2bd5e1f466e3e49a6ce567e24e8ed0`; original BossMod `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`. These checks establish source contracts, not which provider/version is installed in the game.

## Answer and recommendation

Yes. Our Wrath fork can subscribe directly to the same public BossModReborn encounter endpoints that RSR uses. RSR need not be installed or executing for Wrath to consume this information. Add a read-only encounter-information provider in Wrath and feed the new shared decision system. Accord exposes permissions; it does not duplicate encounter parsing or make BossMod execute the suggested action.

Distinguish two features: **encounter forecasts** say what is likely to happen; **cooldown plans** request particular actions during chosen windows. Adopt forecasts early. Make explicit cooldown-plan integration a separate later opt-in, because it introduces target instructions, reservations and execution ownership.

This does not give Wrath every internal detail known to a boss module or an automatically optimal strategy for every boss. Availability depends on the module, its annotations, active state and exposed API. BossModReborn and original BossMod do not currently expose equivalent contracts in the reviewed sources.

## What RSR actually consumes

Local sources: `RotationSolver/IPC/BMRTimeline_IPCSubscriber.cs`, `BMRInfo_IPCSubscriber.cs`, `Updaters/BossModUpdater.cs`, `StateUpdater.cs`, `CancelCastUpdater.cs`, `Basic/Actions/ActionBasicInfo.cs`, `ActionTargetInfo.cs`, `Basic/Rotations/CustomRotation_OtherInfo.cs`, `RebornRotations/Ranged/MCH_Reborn.cs`. Paths are relative to `RotationSolverReborn`.

| Information | Current RSR consumption | Wrath use worth adopting |
|---|---|---|
| Active module and name | Stores active state/name; timeline helper predicates require an active module. | Show whether encounter guidance is available and reset context on encounter changes. Name alone is not a unique event identity. |
| Next raidwide/tankbuster | Combines timeline, type-specific predicted damage and generic typed damage using earliest time. `StateUpdater` requests defense inside configurable windows (default 5s raidwide/3s buster, with a 0.6s lower bound). | Start prevention early enough for preparation/casting. Keep user permissions and choose one coherent prevention sequence rather than spending every eligible cooldown. |
| Knockback | Requests anti-knockback in a configured window. | Suggest permitted immunity with encounter context; do not assume every knockback should be prevented. |
| Downtime start/end | Available to job helpers; main MCH uses it to avoid short-lived Barrel/Wildfire commitments and optionally spend before downtime. Extra ChurinDNC also consumes downtime. | Avoid starting a phase that cannot complete, or spend resources when waiting loses value. Expand job by job; this is not already implemented across every RSR rotation. |
| Vulnerability start/end | Exposed through DataCenter/public rotation helpers. | Potential buff/spender alignment. Available data is not evidence that every default RSR job already uses it. |
| Damage type and special mode | Stores generic predicted damage type and special-mode timing/type. Action/autoattack paths check Pyretic. | Time bounded action suppression/selection when required. Use activation timing; do not suppress an entire earlier window merely because the next special mode is Pyretic. |
| Cast cancellation and movement | Reads mechanic/AI cancellation and movement flags; cast eligibility and cancellation use them. | Avoid starting an incompatible cast; optional cancellation of an active cast under separately approved policy. Reading a movement hint does not grant movement control. |
| Position/dash safety | `ActionTargetInfo` checks destination/path functions where enabled. DataCenter helpers return true on unavailable/failed calls. | Validate a permitted movement action; distinguish unavailable advice from a positively safe result. This remains guidance, not a guarantee of survival. |
| Explicit cooldown plan | `BMRPlanUpdater` polls upcoming planned actions and queues matching actions during active windows. | Separate future plan-aware suggestion/reservation feature, with preserved target and manual-only policy. See limitations below. |

## What Wrath already has

`Accord/WrathCombo/CustomCombo/Functions/Action.cs::GroupDamageIncoming` combines recognized raidwide casts and shared-damage VFX. `RaidwideCasting` uses cast shape/range and `BattleData` classification/ignore lists. `Functions/VFX.cs::HasIncomingTankBusterEffect` identifies markers on a specific actor. `AutoRotationController` already has raidwide/buster handling, and many job helpers call these detection functions.

Wrath therefore already has some pre-emptive support. Its limitation here is the absence of the reviewed BossMod forecast feed, not total absence of incoming-damage awareness. Keep cast/VFX evidence as fallback and for recipient/multihit information that a scalar timeline does not supply.

`Services/IPC_Subscriber/BossMod.cs` currently focuses on conflict detection: automatic queues, targeting, presets and custom queue behaviour. It is not the timeline subscriber. Add a separate read-only adapter instead of coupling encounter information to whether BossMod's rotation runner is enabled.

One existing implementation concern: `RaidwideCasting(maxTimeRemaining)` shares a single 100ms cached boolean across different requested windows. A later redesign should cache observations and apply each caller's timing window independently. This is a source-level consistency risk to test, not a reproduced encounter failure.

## Verified provider contract and limits

The [BossModReborn IPC provider](https://github.com/FFXIV-CombatReborn/BossmodReborn/blob/97595f07cb2bd5e1f466e3e49a6ce567e24e8ed0/BossMod/Framework/IPCProvider.cs) registers public `BossMod.*` endpoints, so another plugin can read them without taking ownership of BossMod automation.

- `Timeline.NextRaidwideIn`, `NextTankbusterIn`, `NextKnockbackIn`, `NextDowntimeIn/EndIn`, `NextVulnerableIn/EndIn` return seconds, or `float.MaxValue` for unknown.
- `Hints.NextDamageIn/Type`, `NextRaidwideDamageIn`, `NextTankbusterDamageIn` expose damage predictions. The type-specific functions search for the first matching prediction.
- `Hints.PredictedDamagePlayers` exposes a player-slot bitmask for the **first generic damage prediction**. It cannot safely be paired with an independently queried next-tankbuster time if that refers to another event. Resolve party slots against the provider's party model, not guessed UI ordering.
- Additional provider endpoints include `Hints.MaxCastTime`, `ShouldCleansePlayers`, `RecommendedPositional` and separate mechanic/other cast-cancel flags. They are opportunities beyond the inspected RSR timeline subscriber, not already-adopted RSR behaviour.
- `Hints.IsPositionSafe(Vector3)`, `IsDashSafe(Vector3, Vector3)`, `IsFixedDashSafe(float range, bool backwards)` and `IsBackdashSafe(Vector3 enemyPos, float range)` expose movement checks.

**Compatibility finding:** our RSR subscriber declares `Hints.IsFixedDashSafe` as `Func<Vector3, Vector3, bool>`, while the reviewed provider takes `float, bool`. The call contracts do not match. RSR catches failures and allows movement in its wrapper. This is a concrete source mismatch; whether a particular installed pairing exhibits it needs runtime validation. Wrath should use the verified provider signature or the explicit from/to dash endpoint, probe capabilities independently, and report unavailable checks without disabling unrelated forecasts.

The provider's [state-machine walker](https://github.com/FFXIV-CombatReborn/BossmodReborn/blob/97595f07cb2bd5e1f466e3e49a6ce567e24e8ed0/BossMod/BossModule/StateMachine.cs) follows a unique successor chain. An unresolved branch ends its look-ahead. Unknown means unknown, not “nothing dangerous is coming.” Seconds-to-event also lacks a stable event ID, severity, complete hit sequence and universal target list.

Concrete module example: [M1S Black Cat](https://github.com/FFXIV-CombatReborn/BossmodReborn/blob/97595f07cb2bd5e1f466e3e49a6ce567e24e8ed0/BossMod/Modules/Dawntrail/Savage/M01SBlackCat/M01SBlackCatStates.cs) annotates Bloody Scratch as raidwide and the two Biscuit Maker hits as tankbusters. Those annotations can support earlier preparation and successive-hit planning as each forecast becomes available. By contrast, [normal Valigarmanda](https://github.com/FFXIV-CombatReborn/BossmodReborn/blob/97595f07cb2bd5e1f466e3e49a6ce567e24e8ed0/BossMod/Modules/Dawntrail/Trial/T01Valigarmanda/T01ValigarmandaStates.cs) uses a trivial phase; its timeline file alone does not provide that same scripted future sequence. Component-generated hints may still supply current predictions. Do not advertise uniform advance notice for every supported encounter.

The reviewed [original BossMod IPC service](https://github.com/awgil/ffxiv_bossmod/blob/6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0/BossMod/Services/IpcService.cs) exposes configuration/rotation/preset facilities but not these timeline/hint/plan endpoints. Initial support should explicitly target compatible BossModReborn. Original BossMod support would need an upstream/additive IPC change or another supported adapter; do not silently depend on reflection into private boss-module state. Neither installed-provider capability nor update compatibility has been tested here.

## Cooldown plans are a separate feature

BossModReborn publishes `Plan.GetUpcomingActions(float lookAheadSeconds)` and `Plan.ActionsChanged`. Its [plan exporter](https://github.com/FFXIV-CombatReborn/BossmodReborn/blob/97595f07cb2bd5e1f466e3e49a6ce567e24e8ed0/BossMod/Autorotation/PlanExecution.cs) returns module/track/option, action type/ID, activation/window end, and target strategy/parameter for the current branch. It only exports tracks whose options map unambiguously to actions; it is not a serialization of every strategy in the plan. No active planner yields an empty list.

RSR's experimental importer is off by default. It polls on change or every five seconds, adjusts relative times by elapsed poll time, deduplicates active action IDs and submits matching actions to the command queue. In the inspected updater, `Target`, `TargetParam` and `ActionType` are not used when queuing. Its deduplication is by action ID rather than distinct plan entry. These are reasons to adapt the concept rather than copy the importer as a complete plan executor.

For Wrath, proposed levels are: forecast-aware suggestions; optionally reserve selected cooldowns for a plan; optionally include due planned actions in eligible button/automation decisions. A plan must never bypass manual-only exclusions or start background execution by itself. Preserve target strategy, window, encounter/branch context and distinct entry identity. Confirm actual use before marking an entry complete; expired or unsatisfied entries should report why they were missed. RSR-owned automation and BossMod's own rotation queues must not execute the same plan independently.

## Concrete custom work

1. Add an independently capability-checked subscriber for the verified read-only forecast endpoints. Read on the game/framework thread into a single snapshot, with provider identity/version, encounter generation, observation time and explicit unavailable fields. Reset on unload, zone change, wipe and module transition. Avoid an exception in one endpoint erasing unrelated valid data.
2. Normalise forecast timing using a monotonic observation clock. Reject nonfinite/sentinel/stale times and handle due/overdue events deliberately. Retain source and confidence; resolve cast/VFX versus timeline conflicts, rather than blindly combining incompatible evidence. Avoid counting one event twice.
3. Supply this snapshot to the shared selector. A full-health party can still justify shields; a tankbuster countdown without a known victim must not silently turn into a shield for an arbitrary tank. Use cast/VFX recipient evidence or an explicitly configured target fallback.
4. Add prevention coordination: choose the permitted preparation chain, ensure it can finish, account for existing/pending shields and healing, and reserve resources where justified. Do not OR a forecast into every old boolean and thereby fire several independent support branches.
5. Add specific offensive consumers first (MCH Wildfire/heat, then reviewed job phase-entry/reserve cases). Missing or uncertain forecasts revert to ordinary job logic; do not indefinitely hold burst awaiting an unknown vulnerability window.
6. Add movement/cast guidance separately from action execution. Honour manual mobility reservations. A forecast can veto an automatic dash or improve a suggestion without moving the player. Automatic cancellation of manual casts and preventive suppression on direct buttons need explicit settings.
7. Consider structured-event IPC later if we need stable event IDs, multi-hit schedules, severity or targets for more than the first prediction. This would require provider work; scalar polling cannot invent information it does not carry. Add explicit cooldown-plan integration only after these fundamentals.

Suggested settings: encounter guidance Off/On; preventive protection permission; downtime-aware spending; knockback assistance; movement-safety advice; automatic cast cancellation; and separate cooldown-plan mode Off/Suggest/Reserve-and-use-permissions. Keep advanced encounter overrides optional. These are planning proposals, not newly applied toggles.

## Verification and delivery gates

Add the forecast adapter to the first SGE/DRG architecture milestone, with the initial SGE prevention consumer. Complete healer coordination in the following stage and add MCH downtime decisions with its conversion. Explicit cooldown plans follow as a separate optional milestone, not a requirement to begin the redesign.

Validate missing provider; original BossMod without endpoints; partial/mismatched endpoint support; module without useful forecast; branched timeline; early/late transition; wipe/reset; multiple hits; unknown buster victim; party-slot remapping; a co-healer applying the effect first; manual-only cooldown; no available weave; provider unload; and new engine ownership while an event/plan is pending. Measure preparation completion before impact, redundant spending, incorrect recipients, unnecessary burst holds and false cast/movement vetoes.

In manual play, improved forecasts change what the next permitted press suggests; they do not guarantee the player presses in time. In automatic play, only the active runner executes. The read-only BossMod feed remains usable in either case.
