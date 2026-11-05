# Testing implementation map and acceptance record

5 November 2026. This records the implementation against the whole approved roadmap. Source completion, automated verification, and game acceptance are separate columns: none substitutes for another. Stable source and feeds are outside this implementation.

## Source and ownership

Behavioral reference: RotationSolverReborn `156de237f5e4231d93558c8e6fba6ee337e7b902`; Wrath comparison baseline `761b51ff9fec11ee9f7c71dcd3ef6279dcb31bae`. The detailed method-level comparisons remain in `rotation-comparison.md`, `remaining-job-comparison.md` and `pvp-comparison.md`. New Wrath policies are independently authored, not copied RSR source. References below are relative to those repositories.

Wrath retains custom hotbar actions, existing native/manual routes, presets, job priorities not explicitly replaced, resource holds, movement reservations, confirmed intent handoff, and its original plugin identity. Accord owns layouts, user preferences and runner coordination. RSR remains its own engine with its original identity and settings; its automatic actions do not pass through Wrath's hotbar resolver. Neither engine imports the other's private rotation state.

## Shared work

| Plan requirement | Implementation | Verification and remaining acceptance |
|---|---|---|
| Immutable proposals and accepted preparations | Wrath `Engine/Selection`, `SelectionRuntime`, action-effect confirmations | Pure proposal/press/acceptance/expiry tests; native packet/queue timing still requires the game |
| Explainable comparison | Bounded `TestingApi` capture and `CaptureReplay`; baseline settings, experimental settings, job/level, build identity and encounter snapshot recorded at capture start | Deterministic replay; matched legacy/new/RSR game captures remain required. No live second invocation of a mutating selector |
| Enemy targeting | Scoped evaluation recipient before job checks; per-action final validation; retained provider decisions; automatic runner uses the same target policy | Tests cover invalid overrides, coverage, collateral and refusal to redirect. Range/LOS and game geometry need native acceptance |
| Ally targeting | Eligible-recipient selection before HP thresholds; ordered party/world mouseover, selected and focus overrides; strict explicit targets | Pure targeting tests; actual party-list input and queue recipient checks pending |
| Target switching | Independent enemy/ally visible-target permissions | Selection/delivery do not require changing the visible target; native target-write verification pending |
| Ground healing | Retained and revalidated points for Asylum, Bell, Soil and Star | Build and policy coverage; terrain collision and placement must be checked in game |
| BossMod information | Independent nullable forecast readers, monotonic freshness and encounter generation; raidwide, buster, downtime, return, knockback and cast-budget inputs | Missing/stale/reset policy cases pass. Actual IPC compatibility and branched encounters pending |
| Preventive healing | Shared event budget, observed cast/marker identity, preparation deadlines, pending-health accounting and observed party mitigation coverage | Deterministic state tests; full-HP incoming hit, multihit and co-healer cases require captures |
| Joined rotation button | Separate custom action; Global → role → job preference; existing area thresholds; accepted NIN sequence retains mode | All-job layout/variant tests; native hotbar and queue checks pending |
| Optional healer damage inclusion | Separate recovery, healing-GCD, raise, hard-cast raise, cleanse and preventive permissions | Settings and selection policies; actual weave timing and saved user configurations pending |
| Manual-only actions | Existing Wrath route exclusions; separate RSR per-job/PvE/PvP exclusions at selection and execution | Contract tests; direct manual use during either runner needs native acceptance |
| Automatic runner ownership | Stop → verify → drain native queue/cast → start → verify; heartbeats and fail-closed ownership in both engines | Deterministic failures/reloads/queues; both loaded plugins and match lifecycle pending |
| Activity integration | Optional AutoDuty session following through the same owner coordinator | State-machine tests. Upstream navigation pause is not exposed by the inspected IPC; explicit Accord stop remains available |
| Testing distribution | Three candidate workflows and central immutable publisher/composer; stable fields retained and refreshed | Packaging/composer tests; public candidate activation and installation remain separate delivery steps |

## PvE provider disposition

References are RSR `RotationSolver/RebornRotations/<role>/<job>_*` and common `CustomRotation`/action targeting/preparation logic. The comparison documents identify the precise branches. A retained rule is a deliberate disposition, not a claim that it was rewritten.

| Job | Adapted or custom testing behavior | Retained behavior and native acceptance focus |
|---|---|---|
| PLD | Planned eligible GCD/proc state for burst entry; observed buff continuation | Magic/Confiteor priorities, reserves and channel controls; manually held buff and Passage cases |
| WAR | Inner Release duration and known-uptime budget | Gauge priorities and configured reserves; overcap/manual IR/short uptime |
| DRK | Event-specific TBN resource reserve | Existing burst and MP/gauge priorities; known victim versus unknown victim |
| GNB | One GCD plan before abilities for simple/advanced ST/AoE; accepted chain and expiring Sonic priority; observed No Mercy | Existing spending/permission helpers; cartridges, manual chains, level sync and late buff visibility |
| WHM | Shared simple/advanced healing, retained Thin Air/Plenary follow-up, Bell/ground placement and preventive budget | Content-specific healing and configured priorities; free lilies, multihit, co-healer coverage |
| SCH | Shared recovery/prevention; Recitation → Adlo → Deployment; pending effects and ground Soil | Aetherflow restrictions and heal priorities; shield overlap, recipient movement and lost preparation |
| AST | Shared recovery/prevention and prepared spell; ST Neutral Sect binds Aspected Benefic; ground Star | Card and stored-heal choices; explicit release, ally overrides and deadline misses |
| SGE | Eukrasia/Zoe intended spell and recipient; preventive shield and raise continuation | Gauge/ability priorities and manual ownership; movement, missing weave and Eukrasia transitions |
| DRG | Life Surge uses the actual planned hit and retained target; confirmed opener observation | Existing positional/jump/resource priorities; action replacement and lock timing |
| MNK | PB next form from observed accepted chakra and Nadi, including manual reordering | Existing form/proc priorities; sync and interrupted PB recovery |
| NIN | Preview rollback of mudra proposals; native confirmation commits the intended sequence across ST/AoE | Existing mudra priorities/reserves; delayed state, manual deviation and failed first sign |
| SAM | Accepted Kaeshi continuation across mode changes; three-Sen recovery and equivalent positional choice | Kenki policy; no automatic Hagakure conversion; buff-loss and sync cases |
| RPR | Phase duration/uptime and combo-expiry protection on entry | Accepted Enshroud progression and spending rules; manual hold and target loss |
| VPR | Reawaken duration/uptime and combo protection | Accepted generation/legacy progression; mode changes and interrupted follow-ups |
| BRD | Snapshot refresh uses actual GCD delay and expiring observed buffs | Song/repertoire priorities; short uptime, missing buffs and deliberate holds |
| DNC | Finish observed dance independently of initiation settings; proc deadlines; dance-entry duration | Existing step/proc/gauge priorities; no-target finish, manual dance and downtime changes |
| MCH | Reassemble planned-tool binding and recipient; five heat-shot uptime budget | Existing tool/resource scheduling; overwritten tool, manual consumption and target loss |
| BLM | Adjusted cast time versus fresh cast/uptime limits; only permitted instant alternatives | Existing elemental phase/provider logic; limited movement resources and unavailable candidates |
| RDM | Manual raise reservation through Swiftcast/Vercure, combined MP cost and recipient binding | Existing mana/proc/melee logic; lost recipient, delayed Dualcast and manual consumption |
| SMN | Purpose-bound Swiftcast for permitted Ruby/Slipstream; manual raise reservation | Existing summon/phase priorities; phase expiry, movement and missing follow-up |
| PCT | Motif entry versus actual cast/return/uptime budget | Existing paint/hammer/muse priorities; short downtime and unavailable instants |
| BLU | Equipped/permitted Bristle → Song; deliberate Final Sting prerequisite validation | Loadout-specific existing rotation and sacrifice control; changed loadout/role |
| BST | Pet-capability and confirmed-action finisher with permission checks and bounded lifetime | No automatic capture/sacrifice/horn preference; pet change, unavailable step and manual deviation |

These are focused replacements and shared foundations. They do not assert that every legacy job branch has become a new immutable candidate generator, nor that RSR's complete rotations have been transplanted.

## PvP disposition

All 21 standard jobs retain existing tactical permissions and use the shared opt-in target/eligibility boundary. The compact layout preserves native Recuperate, Guard, Purify, Sprint, role action and LB access, including both SMN summons. It is an explicit draft preset. Native manual controls remain available independently of automation.

| Jobs | Specific disposition beyond shared targeting and compact layouts |
|---|---|
| PLD, WAR, DRK, GNB | Keep existing chains, tactical pulls/dashes, shields and LB/channel controls. No automatic rescue/engage policy imported from RSR |
| WHM, SGE | Keep existing heal/Eukrasia/Kardia follow-through and direct tactical choices; apply common recipient validity without replacing priorities merely because RSR orders them differently |
| SCH, AST | Shared eligible-ally recovery; friendly shield spread remains friendly; Double Benefic retains the accepted ally; Chain Stratagem uses its own Guard eligibility |
| MNK, DRG, NIN, SAM, VPR | Keep accepted chains/channel behavior and movement reserves. Reviewed Guard exceptions are action-specific; Smite can be considered through Guard without permitting ordinary damage. SAM's contradictory Mineuchi target check is corrected only on the testing target path |
| RPR | Burst control releases its own confirmed Warrant; automatic expiry release uses the same retained commitment. Native release remains an escape route |
| BRD | Retain Nocturne/Repertoire and Harmonic policy plus recipient-aware cleanse; no arbitrary replacement of tactical silence/backstep |
| MCH | Analysis selects and commits an eligible tool/recipient; ordinary filler cannot consume that preparation first. Burst displays its own accepted Wildfire release |
| DNC | Honing initiation and confirmed early release share the mechanic control; preserve partner/Waltz, dash and native release access |
| BLM, PCT | Keep explicit element/paint/stance and movement choices; no conflation of Fire/Ice with ST/AoE and no imported tactical auto-spending |
| SMN | Suppress Necrotize during native summon phase; routine Cyclone only at melee range, while deliberate native movement remains available; retain both summons |
| RDM | Compute eligible next GCD once; coordinate automatic Embolden with Resolution/Redoublement/Scorch; retain direct burst intent and separate movement controls |

RSR PvP behavioral sources for the last two changes are `PVPRotations/Magical/SMN_Default.PVP.cs:AttackAbility` and `RDM_Default.PVP.cs:AttackAbility`. Release ownership is custom work. Installed game sheets were inspected read-only: Detonator `41470` and Fate Sealed `41457` are self-targeted; the accepted hostile effect's recipient is still validated separately. Honing Ovation `29470` is self-targeted. Healing and damage circles cannot safely be distinguished by `AttackType` alone (Holy and Medica share it), so damage coverage uses reviewed spell identities rather than a blanket self-circle rule.

## Acceptance still requiring the game

No live game process was available during the 5 November validation. Therefore the following results remain **not run**, rather than inferred from a build:

- Both plugins loaded: native replacement hooks, checked RSR execution, queue contention and direct manual presses during each runner.
- All job cases in `wrath-improvement-plan.md`, including level sync, manually held initiators, interruption/reload, movement reserves and target loss.
- Mouseover/party-list/focus overrides, queued recipient changes, LOS, actual cone/line/circle coverage and ground placement on slopes/different floors.
- Healer full-health incoming hit, multihit, co-healer overlap, effects in flight, preparation completion and missed windows.
- PvP match transitions, Guard appearance mid-cast, bypass versus true invulnerability, Honing/Scales/LB channels, early release and separate SMN summons.
- Matched legacy/new/RSR traces and measured frame/press cost. No DPS gain or performance parity is claimed.

Optional cooldown-plan execution, new structured BossMod event IPC and stable promotion are separate proposed milestones in the approved documents; this implementation does not invent them from scalar forecasts. A native failure must reopen the relevant implementation item. The test release must not be called gameplay-accepted until these records exist.
