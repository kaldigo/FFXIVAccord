# PvP comparison and compact controls

Source review and proposal, 4 November 2026. No runtime implementation or live-match benchmark. Covers the default selection paths for all 21 standard PvP jobs, common survival/targeting and Accord's current layout machinery. This is not a certification of every configuration, game action or mode. Baselines: Wrath `761b51ff9fec11ee9f7c71dcd3ef6279dcb31bae`, RSR `156de237f5e4231d93558c8e6fba6ee337e7b902`, Accord client `8eef51a29cee077edb52135125e8e54666e4debb`.

## Recommendation

Keep Wrath's custom controls, deliberate initiation, saved resource policies and useful PvP follow-through. Adopt RSR's action-specific targeting/eligibility foundation and its concrete next-GCD coordination. Rebuild the shared PvP permission, survival and sequence boundaries alongside the new selector. Do not replace every PvP job priority with RSR's default: several are simple ordered lists and spend tactical tools freely when eligible.

Offer a compact layout built around one damage button, optional burst initiation, direct survival controls, and only the distinct tactical controls a job needs. A smaller bar is not achieved by making Guard, Purify, Recuperate and a job shield compete for the same unpredictable button. Automating a function and retaining direct access must remain independent choices.

## Existing functionality we should reuse

`FFXIVAccord/Accord.Plugin/LayoutPreferences.cs` already defaults PvP to one rotation button. `PvPConfiguredLayout.Contents` removes the area control in that mode and maps the remaining control to `1000015`. `Accord/WrathCombo/Engine/PvPIntentRouting` selects the existing Ninja ST/AoE paths using `PvPAreaChoice`, retaining the choice during Three Mudra. Most other jobs use their single-target-labelled combined PvP rotation for either input. Therefore the raw `pvp-presets.json` ST/AoE pair is not evidence that every applied bar still has two buttons, and one-button mode is not new work.

The fork already has Offense/Defense/Support/Mechanic requests, scoped action exclusions, confirmed continuation, early-release controls for MCH/DNC/RPR, and allied-recipient handling. Reuse their contracts while replacing stateful preview/retarget internals as required. Existing settings allow manual retention of delegated actions; do not remove that ability to meet a slot target. Source profiles contain roughly a dozen or more nonempty candidates, but actual visible counts depend on delegation, retention, personal mapping and controller layout.

## Shared comparison

| Concern | Wrath source behaviour | RSR source behaviour | Adoption decision |
|---|---|---|---|
| Guard on an enemy | `PvPCommon.TargetImmuneToDamage` groups Guard/reductions and invulnerability; many jobs wrap offensive branches with it, but exceptions vary and fallback attacks can still be returned. | `ActionTargetInfo` checks Guard per action, permits `IgnoreGuard` actions, and considers whether Guard ends within cast time. Drill, Chain Stratagem and some role actions explicitly ignore Guard. | Adopt per-action handling, preserving deliberate Guard-breaking tools and adding consistent final eligibility. Do not equate every reduction with invulnerability. |
| Target choice | General automatic selectors plus job-specific checks; manual controls depend heavily on current target/retarget stack. | Shared candidate filtering and per-action geometry; optional low HP, nearest and PvP role priority. Role priority selects nearest matching role, then nearest fallback. | Adopt foundation, add stable selected/focus target override and controlled switching. A role filter is not a tactical assessment of who should die next. |
| Self survival | `Combos/PvP/ALL/ALL.cs` has configurable Recuperate/Guard/Purify and exclusions for special states, plus Guard mash protection. Recuperate explicitly checks MP cost and missing-HP-adjusted threshold. | Shared `TryPvPEmergency`: configurable status cleanse, low-HP Guard, Recuperate/Elixir eligibility. WHM/BRD supply separate cleanse handling. | Consolidate rather than run both. Preserve Wrath special-state handling and resource checks; use action-specific recipient validation. |
| Survival priority | Common replacements and individual job branches can compete. | GCD and ability paths can pause for Guard; optional experimental GCD hold below 50% effective HP uses an MP threshold of 2000. Several job emergency overrides execute before calling common emergency logic. | Do not copy a blanket low-HP GCD stop. Reserve a feasible heal, then allow other useful actions if it cannot execute. Review emergency priority centrally. |
| Preparation | Job-local status/timing checks and explicit fork initiation permissions. | MCH Analysis looks at intended tool GCD; SAM Soten and RDM Embolden also inspect the intended GCD. | Adopt intended-action coordination, with movement permission and recipient/sequence commitment. |
| Manual interception | Accord has its own explicit request/continuation machinery. | `Updaters/ActionQueueManager.UseActionDetour` explicitly excludes PvP from normal interception. | Immediate native PvP presses should remain direct. PvP scheduled interception would be custom work, not an existing RSR capability. |
| Limit breaks | Some jobs have automatic initiation checks (e.g. WHM/NIN/SAM/MCH/MNK); others chiefly continue manually initiated LB states. | Reviewed default job selectors contain many LB follow-ups but no comprehensive automatic LB-initiation policy; `LimitBreakPvP` alone is not evidence of one. | Keep deliberate LB access for every job. Auto-LB is a separate explicit per-job permission, not implied by Burst. |
| Modes/lifecycle | Existing common code recognises special states including mecha riding. | PvP start/death/end settings, Shatter target preferences, and RPR immediate-Communio option for Frontline. | Keep CC/Frontline/Rival Wings policies separate where needed. Do not claim that a combat selector understands objectives or safely operates every vehicle. |

RSR automatic eligibility does not guarantee every useful manual use remains available. For example, WHM Aquaveil is configured as a dispel needing cleanse statuses; a deliberate preventive shield must not inherit that automatic-use restriction. Similarly RSR's SCH Deployment is configured hostile with Biolysis required, whereas the Wrath fork also offers deliberate friendly shield spreading. Adopt the targeting pipeline without losing valid manual action semantics.

## All-job disposition

Source convention: W = `Accord/WrathCombo/Combos/PvP/<JOB>PVP.cs` (SMN uses `SMNPvP.cs`); R = `RotationSolverReborn/RotationSolver/RebornRotations/PVPRotations/<role>/<JOB>_Default.PVP.cs` (MCH uses `.PvP.cs`). Each row compares those selectors; RSR eligibility additionally comes from `Basic/Rotations/Basic/<JobName>Rotation.cs` and common `CanUse`/target logic. The earlier client-only review is in `FFXIVAccord/docs/pvp-rotation-review.md`.

| Job | Keep from Wrath | Adopt/evaluate from RSR | Compact-bar/manual consequence |
|---|---|---|---|
| PLD | Imperator/Confiteor and Phalanx continuation; deliberate Guardian and movement; Holy Spirit sustain/ranged fallback. | Explicit eligible follow-ups and optional Guardian recipient/HP selection. Do not turn automatic Guardian under Hallowed into an unrequested default. | One attack; optional burst; Sheltron, Guardian, Intervene, Shield Smite and LB remain retainable tactical controls. |
| WAR | Blota before/after Rend policy; separate Rend/Bloodwhetting initiation and follow-ups. | Common eligibility and clear transformed-action selection. RSR's ordered Blota/Onslaught spending is not superior tactical timing by itself. | Merge routine combo/continuations; retain pull, engage, Bloodwhetting and LB. |
| DRK | Shadowbringer HP reserve, Dark Arts exception, Scorn and Salt follow-through. | Explicit delirium follow-ups and low-HP Impalement priority; action-specific eligibility. | Remove duplicate self-defense/TBN presentation when one recipient-aware control covers it; keep Salted Earth initiation, Plunge and Eventide deliberate. |
| GNB | Rough Divide reserve/No Mercy setup, manual Gnashing continuation and configurable Corundum threshold. | Protect continuation opportunities using ready-state checks before advancing GCDs. | Combine ordinary damage chains; retain deliberate dash, recipient-aware Corundum and LB/channel release. RSR's hardcoded emergency Corundum branch still needs our common permissions. |
| WHM | Cure chain and configurable recipient/threshold; manual Miracle, gap close and LB. | Separate emergency cleanse/healing stages and actual heal recipient checks. | One healing control for allowed Cure follow-ups; keep Miracle/Seraph Strike available. Do not replace proactive Aquaveil with cleanse-only behaviour. |
| SCH | Expedient/Biolysis preparation, reserved Deployment charge, friendly Catalyze spread. | Guard-aware Chain Stratagem and shared healing selection; examine stronger expiry handling in Seraph states. | Keep friendly shield/spread and hostile spread distinguishable by explicit recipient/scope. Preserve manual Expedient, Seraph and LB; no ambiguous enemy/ally fallback. |
| AST | Saved card/Double Cast policy and confirmed Benefic recipient retention. | Expiry handling for Microcosmos/Lady, separate heal and offensive follow-ups. | Cards may join allowed damage/heal decisions; retain explicit Macrocosmos/Microcosmos timing, movement and LB. Healing follow-up stays on the accepted ally. |
| SGE | Manual Eukrasia continuation; Phlegma/Psyche; validated Kardia recipient. | Shared target eligibility and structured healing/support categories. Default RSR priority alone is not a new strategic improvement. | Eukrasia setup can join damage; Kardia becomes optional setup/override access. Keep Pneuma, Icarus and Mesotes retainable. |
| MNK | Combo/Fire's Reply follow-through, expiring Earth reply and tactical Thunderclap. | Earth's Reply has separate expiry, self-heal and nearby-enemy triggers. | Retain Riddle/Reply and LB, plus movement and desired Wind's Reply control. Avoid automatically releasing a stored defensive effect just because an enemy is nearby. |
| DRG | Nastrond health/expiry policy, distance-aware Wyrmwind, optional evasive burst setup. | Common action geometry and explicit movement categories. | One damage/burst sequence; retain Elusive/High Jump as desired and direct LB. Preserve distance policy rather than blindly adopting RSR's priority list. |
| NIN | Configurable ST/AoE mudra orders, pooling, defensive ninjutsu requests, Hidden behaviour and area-choice stability. | Per-action transformed-ID/sealed-status checks and common eligibility. | Existing adaptive button is useful; keep optional split mode and defensive ninjutsu, Shukuchi and LB. RSR defaults Goka's AoE count to one: its priority list is not proof of better ST/AoE choice. |
| SAM | Soten reserve, bind/range checks, Ogi/Meikyo continuations and explicit LB rules. | Soten tied to intended normal GCD; Mineuchi Kuzushi eligibility; shared transformed-action checks. | Keep Mineuchi, Chiten, movement and LB deliberate. Review Wrath's unusual `!HasBattleTarget()` Mineuchi condition as a focused defect candidate, not an accepted policy. |
| RPR | Warrant/Harvest pooling, manual Warrant release, Enshroud continuation and moving-Communio handling. | Warrant expiry and Communio stack/expiry versus immediate Frontline option. | Combine initiation/active release only when the displayed release has a unique commitment. Keep escape/return, Crest and LB. |
| VPR | Serpent's Tail follow-ups, Slither reserves, ranged fallback, Scales channel/release handling. | Explicit generation/legacy gating and no ordinary rotation while Hardened Scales. | Rattling Coil can be internal when permitted; retain Scales/release, movement and LB. Do not lose the fork's release path by importing only RSR's pause. |
| BRD | Harmonic charge/execute policy, Nocturne/Repertoire sequencing, separate self/ally cleanse. | Action-specific Guard bypass, shared cleanse eligibility and Frontliner's March-aware Apex ordering. | Fold routine Apex into damage where permitted; one recipient-aware Paean control replaces duplicate self-cleanse presentation. Keep silence, backstep and LB manual. |
| MCH | Alternative Analysis/tool policies, Wildfire/FMF timing, explicit detonation, direct Scattergun/turret and LB. | Strong candidate: bind Analysis to actual next tool GCD; avoid Blazing Shot while Analysis is reserved. | Tool cycling can join attack/burst. Preserve tactical Scattergun/turret and manual LB; burst can display Detonator while its own accepted Wildfire is active. |
| DNC | Honing channel and early release, dance/partner recipient handling, dash reserves, ally-aware Waltz. | Common channel guards and heal-stage separation. | Put Honing initiation/release on the same control; optional automatic partner setup; retain Waltz, En Avant, LB and manual partner override. |
| BLM | Movement-aware Fire/Ice handling, offensive/defensive element packages, configurable Xeno spending. | Explicit high/low self-HP Xeno policy and per-action area checks as alternatives to compare. | Retain an optional element/stance override, Lethargy, movement and LB. Fire versus Ice is not merely ST versus AoE. |
| SMN | Necrotize/Ruin follow-through, manual summon choice and safe self-shield path. | Avoid Necrotize during trance; separate deliberate gap close from near-target offensive Cyclone; shared target/ground eligibility. | Combine duplicate self-Aegis controls; keep both Bahamut and Phoenix choices accessible, plus tactical movement/area controls. Never hide one summon behind an arbitrary LB resolver. |
| RDM | Melee continuation, movement reserves, expiry-sensitive Prefulgence, defensive Forte. | Embolden coordinated with planned Resolution/Redoublement/Scorch. | One damage chain; optional burst; retain forward/back movement separately, Forte, tactical Resolution and LB. |
| PCT | Quick Sketch/movement motif rules, burst threshold, expiring Star Prism/Tempera, manual paint cycle. | Common action validation and explicit motif/muse stage selection. RSR's movement palette switching is already broadly represented in Wrath. | Fold routine paint/palette into allowed rotation; retain stance override, coat/release, Smudge and LB. |

## Proposed compact layout

Use consistent positions, with job-specific additions. Proposed common core:

1. Attack (adaptive; optional split alternative).
2. Burst initiation, only when the player wants a separate manual burst control.
3. Limit break (native or explicit chosen LB; preserve transformed follow-ups).
4. Recuperate, direct.
5. Guard, direct, with configurable mash protection.
6. Purify, direct.
7. Sprint, direct/shared as already supported.
8. Selected PvP role action, direct.

Keep Elixir accessible on a secondary utility position. Add the job's meaningful healing, protection, crowd-control and movement choices, rather than allocating every job an empty generic Support or Mechanic slot. This is typically one main bar plus a short secondary row; it is not a promise that every manual configuration fits twelve slots. Extra retained controls remain supported on keyboard and controller layouts.

Specific reductions beyond existing one-button mode:

- Combine duplicate self-defense/native presentations only after a single control preserves both self and ally uses through clear recipient overrides (DRK/GNB/BRD/SMN examples).
- Fold routine setup into the permitted rotation: MCH tool/Analysis cycle, BRD Apex, SGE Eukrasia, PCT motifs/palette, and VPR coil recovery. If excluded, provide its explicit control rather than leaving the function inaccessible.
- Put initiation and its own active early release on one control for Honing, Wildfire or Warrant where unambiguous. Never resolve a multi-purpose Burst button into a random active release; show the action and capture the committed sequence.
- Offer a secondary manual-access row for rarely changed partner/Kardia/stance choices. Automatic assignment must not prevent a deliberate override.
- Do not combine incompatible choices: Guard versus Purify versus Recuperate, forward versus backward movement, or SMN's two LB summons. Tactical CC must not disappear merely because damage can spend it.

Suggested defaults: one adaptive attack; manual LB, movement and tactical CC; optional automatic self-recovery/protection; optional healer assistance. Automatic target switching is separate from area selection. For the friend's style, turn off unwanted assistance while retaining immediate native controls. For the more automated style, delegate functions and hide redundant selectors while retaining selected emergencies. These are proposals, not applied settings.

Concrete draft examples, assuming the eight-control core above and a separate Elixir utility slot:

| Job | Additions to the core | Proposed visible total | What enabled the reduction |
|---|---|---|---|
| GNB | Rough Divide; recipient-aware Heart of Corundum | 10 main controls + Elixir | No second damage button or duplicate self-defense selector; Gnashing/Continuation remains in the damage path with initiation permission. Native Gnashing can still be retained. |
| MCH | Scattergun; Bishop Autoturret | 10 main controls + Elixir | Tool/Analysis cycle joins permitted damage; Burst displays the accepted Wildfire's detonation when appropriate. The latter requires new, unambiguous release handling. |
| WHM | Healing; Aquaveil; Miracle of Nature; Seraph Strike | 12 main controls + Elixir | Healing handles allowed Cure follow-ups; manual shield, crowd control and movement retain separate access. There is less safe compression here than on MCH. |

These counts are proposed assignments, not measured current-layout savings. Additional desired native controls increase them. The layout should not move the survival buttons when optional job controls are hidden.

## Custom work and acceptance

The shared new selector must keep initiation permissions, automatic eligibility and direct manual legality separate. Track target intent, accepted phase and expiry. A Guard-protected target may be ineligible for normal burst but eligible for a specific bypass; an automatic cleanse restriction must not block a manually requested shield. Do not add global immunity overrides just to make one exception work.

Prioritise a PvP pilot with MCH (Analysis and release), AST or SCH (recipient-dependent support), and NIN (split/adaptive sequence). Then convert all jobs using the disposition table. Guard/Purify/Recuperate and movement permissions are common gates before rollout. Existing RSR auto-on at match start must respect Accord runner ownership; a lifecycle event must not restart the inactive engine. Direct presses during RSR automation need collision/queue testing even though PvP interception is disabled.

Required cases: selected/focused enemy versus lower-HP distractor; Guard appearing during a cast; eligible Guard bypass; enemy death/obstruction; ally mouseover with enemy selected; no valid override; queue target changes; manually held burst; dash reserves; incapacitation and insufficient MP; Guard mashing; LB channel/landing/release; Honing and Scales protection; manual early release; Ninja enemy-count changes mid-mudra; role-action availability; match start/death/respawn/end; Frontline NPC targets and Rival Wings special states. Compare tactical-control loss and unintended movement alongside damage, healing and resource waste.

PvP engine selection remains independent of compact hotbars. No evidence here establishes RSR as universally better in live PvP, nor that its PvE interception or BossMod encounter facilities automatically solve PvP decisions.
